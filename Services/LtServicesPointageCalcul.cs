using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MelodyPaieRDC.Services;

/// <summary>Ligne pour affichage brut vs heure retenue pour le calcul LT.</summary>
public sealed record PointageAffichageLtDto(string HeureBruteHhMm, string HeurePourCalculLtHhMm, string MomentCle, string MomentLibelle);

/// <summary>
/// Règles horaires LTservices (référence client) :
/// <list type="bullet">
/// <item>Lun–Ven : journée 7h30–16h00 ; samedi 7h30–12h30.</item>
/// <item>Pause déjeuner : 12h00–13h00 (deux équipes de 30 minutes) — non comptée comme temps de travail.</item>
/// <item>Entrée le matin : le travail commence à 7h30 ; tolérance de pointage 10 minutes (jusqu’à 7h40 sans retard).</item>
/// <item>Au-delà de 7h40 : arrivée considérée en retard (début du travail = heure réelle de pointage).</item>
/// <item>Avant 7h30 : le temps est compté à partir de 7h30 (pas d’heures sup avant l’ouverture).</item>
/// <item>Pointages attendus en journée complète : entrée, début pause, fin pause, sortie.</item>
/// </list>
/// </summary>
public static class LtServicesPointageCalcul
{
    /// <summary>Journée complète lun.–ven. (hors pause déjeuner).</summary>
    public const decimal HeuresNormalesJourSemaine = 8m;

    /// <summary>Durée standard d’un samedi ouvré (7h30–12h30).</summary>
    public const decimal HeuresNormalesSamedi = 5m;

    public static readonly TimeSpan HeureDebutTravail = new(7, 30, 0);
    public static readonly TimeSpan HeureLimiteTolerance = new(7, 40, 0);
    public static readonly TimeSpan HeureFinSemaine = new(16, 0, 0);
    public static readonly TimeSpan HeureFinSamedi = new(12, 30, 0);

    /// <summary>Durée standard de la pause méridienne (12h00–13h00).</summary>
    public static readonly TimeSpan DureePauseStandard = TimeSpan.FromHours(1);

    public static decimal CalculerHeuresPrestees(IReadOnlyList<DateTime> pointagesJour, DateTime jour, LtServicesRegles? regles = null)
    {
        var r = regles ?? LtServicesRegles.Defaut;
        if (pointagesJour == null || pointagesJour.Count == 0)
            return 0m;

        var sorted = pointagesJour.OrderBy(t => t).ToList();
        var dow = jour.DayOfWeek;

        if (dow == DayOfWeek.Sunday)
            return 0m;

        if (dow == DayOfWeek.Saturday)
            return CalculerSamedi(sorted, r);

        // Lun–Ven : journée type = 4 pointages ; au-delà, seuls les 4 premiers horaires sont pris en compte.
        if (sorted.Count >= 4)
        {
            var quatre = sorted.Take(4).ToList();
            return CalculerJourneeSemaineQuatrePointages(quatre, r);
        }

        if (sorted.Count == 3)
            return CalculerJourneeSemaineTroisPointages(sorted, r);

        if (sorted.Count == 2)
            return CalculerJourneeSemaineDeuxPointages(sorted, r);

        return 0m;
    }

    private static decimal CalculerSamedi(List<DateTime> sorted, LtServicesRegles r)
    {
        if (sorted.Count < 2)
            return 0m;

        var t1 = sorted[0].TimeOfDay;
        var t2 = sorted[^1].TimeOfDay;
        var debut = AjusterEntreeMatin(t1, r);
        var fin = t2 <= r.HeureFinSamedi ? t2 : r.HeureFinSamedi;
        if (fin <= debut)
            return 0m;
        var h = (decimal)(fin - debut).TotalHours;
        if (h > r.HeuresNormalesSamedi)
            h = r.HeuresNormalesSamedi;
        return decimal.Round(h, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculerJourneeSemaineQuatrePointages(List<DateTime> sorted, LtServicesRegles r)
    {
        var t1 = sorted[0].TimeOfDay;
        var t2 = sorted[1].TimeOfDay;
        var t3 = sorted[2].TimeOfDay;
        var t4 = sorted[3].TimeOfDay;
        var t4Eff = AjusterSortieFinSemaine(t4, r);

        var debutMatin = AjusterEntreeMatin(t1, r);
        if (t2 <= debutMatin)
            return 0m;
        var matin = (decimal)(t2 - debutMatin).TotalHours;

        if (t4Eff <= t3)
            return decimal.Round(matin, 2, MidpointRounding.AwayFromZero);

        var apresMidi = (decimal)(t4Eff - t3).TotalHours;
        return decimal.Round(matin + apresMidi, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculerJourneeSemaineTroisPointages(List<DateTime> sorted, LtServicesRegles r)
    {
        var t1 = sorted[0].TimeOfDay;
        var t3 = AjusterSortieFinSemaine(sorted[2].TimeOfDay, r);
        var debut = AjusterEntreeMatin(t1, r);
        if (t3 <= debut)
            return 0m;
        var brut = (decimal)(t3 - debut).TotalHours;
        if (brut > 5.5m && t3 > r.HeureDebutPause && debut < r.HeureFinPause)
            brut -= (decimal)r.DureePauseStandard.TotalHours;
        return decimal.Round(Math.Max(0m, brut), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculerJourneeSemaineDeuxPointages(List<DateTime> sorted, LtServicesRegles r)
    {
        var t1 = sorted[0].TimeOfDay;
        var t2 = AjusterSortieFinSemaine(sorted[^1].TimeOfDay, r);
        var debut = AjusterEntreeMatin(t1, r);
        if (t2 <= debut)
            return 0m;
        var brut = (decimal)(t2 - debut).TotalHours;
        if (brut > 5.5m && t2 > r.HeureDebutPause && debut < r.HeureFinPause)
            brut -= (decimal)r.DureePauseStandard.TotalHours;
        return decimal.Round(Math.Max(0m, brut), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Entrée effective du matin : avant 7h30 → 7h30 ; 7h30–7h40 → 7h30 ; après 7h40 → heure réelle (retard).
    /// </summary>
    public static TimeSpan AjusterEntreeMatin(TimeSpan heurePointage, LtServicesRegles? regles = null)
    {
        var r = regles ?? LtServicesRegles.Defaut;
        if (heurePointage < r.HeureDebutTravail)
            return r.HeureDebutTravail;
        if (heurePointage <= r.HeureLimiteTolerance)
            return r.HeureDebutTravail;
        return heurePointage;
    }

    /// <summary>
    /// Sortie en semaine : au-delà de l’heure de fin officielle (ex. 16h00), le temps compté est plafonné à cette heure.
    /// </summary>
    public static TimeSpan AjusterSortieFinSemaine(TimeSpan heurePointage, LtServicesRegles? regles = null)
    {
        var r = regles ?? LtServicesRegles.Defaut;
        return heurePointage > r.HeureFinSemaine ? r.HeureFinSemaine : heurePointage;
    }

    /// <summary>
    /// Détail brut vs heure retenue pour le calcul LT (lun–ven : entrée / pauses / sortie / supplémentaires).
    /// </summary>
    public static IReadOnlyList<PointageAffichageLtDto> DecrirePointagesPourAffichage(
        IReadOnlyList<DateTime> pointagesJour,
        DateTime jour,
        LtServicesRegles? regles = null)
    {
        var r = regles ?? LtServicesRegles.Defaut;
        var sorted = pointagesJour.OrderBy(t => t).ToList();
        var cul = CultureInfo.CurrentCulture;
        string H(DateTime d) => d.ToString("HH:mm", cul);

        static DateTime Combine(DateTime day, TimeSpan tod) => day.Date + tod;

        var dow = jour.DayOfWeek;
        var list = new List<PointageAffichageLtDto>(sorted.Count);

        if (sorted.Count == 0)
            return list;

        if (dow == DayOfWeek.Sunday)
        {
            foreach (var dt in sorted)
                list.Add(new PointageAffichageLtDto(H(dt), H(dt), "Dimanche", "Pointage (jour non ouvré)"));
            return list;
        }

        if (dow == DayOfWeek.Saturday)
        {
            for (var i = 0; i < sorted.Count; i++)
            {
                var dt = sorted[i];
                var tod = dt.TimeOfDay;
                TimeSpan eff;
                string cle;
                string lib;
                if (i == 0)
                {
                    eff = AjusterEntreeMatin(tod, r);
                    cle = "Entree";
                    lib = "Entrée";
                }
                else if (i == sorted.Count - 1)
                {
                    eff = tod <= r.HeureFinSamedi ? tod : r.HeureFinSamedi;
                    cle = "Sortie";
                    lib = "Sortie";
                }
                else
                {
                    eff = tod;
                    cle = "Extra";
                    lib = "Autre lecture";
                }

                list.Add(new PointageAffichageLtDto(H(dt), H(Combine(jour, eff)), cle, lib));
            }

            return list;
        }

        // Lun–Ven
        for (var i = 0; i < sorted.Count; i++)
        {
            var dt = sorted[i];
            var tod = dt.TimeOfDay;
            TimeSpan eff;
            string cle;
            string lib;
            switch (i)
            {
                case 0:
                    eff = AjusterEntreeMatin(tod, r);
                    cle = "Entree";
                    lib = "Entrée";
                    break;
                case 1:
                    eff = tod;
                    cle = "PauseDebut";
                    lib = "Début pause";
                    break;
                case 2:
                    eff = tod;
                    cle = "PauseFin";
                    lib = "Fin pause";
                    break;
                case 3:
                    eff = AjusterSortieFinSemaine(tod, r);
                    cle = "Sortie";
                    lib = "Sortie";
                    break;
                default:
                    eff = tod;
                    cle = "Extra";
                    lib = "Supplémentaire";
                    break;
            }

            list.Add(new PointageAffichageLtDto(H(dt), H(Combine(jour, eff)), cle, lib));
        }

        return list;
    }
}
