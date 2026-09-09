using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Grille « mois complet » : heures par défaut (calendrier + semaine 6 jours) et fusion avec les lignes
/// déjà en base pour que le calcul de paie corresponde à ce que l’écran affiche (y compris si le mois est partiellement renseigné).
/// </summary>
public static class SuiviJournalierGrilleHelper
{
    /// <summary>
    /// En mode pointages terminal, on n'invente jamais de présence :
    /// seuls les jours réellement pointés / saisis comptent.
    /// </summary>
    public static bool CompleterJoursEffectif(PolitiquePaieContext? politique)
    {
        if (politique == null || !politique.CompleterJoursSansSaisie)
            return false;

        return !string.Equals(
            politique.ModeCalculPresence,
            ParametrePolitiquePaie.ModePresencePointages,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Même logique que la grille de suivi journalier / export PDF pour un jour sans saisie.</summary>
    public static decimal DeterminerHeuresParDefaut(
        DateTime date,
        bool semaineSixJours,
        IReadOnlyDictionary<DateTime, JourTravailCalendrier> calendrier)
    {
        if (calendrier.TryGetValue(date.Date, out var jour))
        {
            if (string.Equals(jour.TypeJour, "Repos", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(jour.TypeJour, "Ferie", StringComparison.OrdinalIgnoreCase))
                return 0m;

            if (string.Equals(jour.TypeJour, "Ouvre", StringComparison.OrdinalIgnoreCase))
            {
                return date.DayOfWeek == DayOfWeek.Saturday
                    ? LtServicesPointageCalcul.HeuresNormalesSamedi
                    : LtServicesPointageCalcul.HeuresNormalesJourSemaine;
            }
        }

        if (date.DayOfWeek == DayOfWeek.Sunday)
            return 0m;

        if (date.DayOfWeek == DayOfWeek.Saturday)
            return semaineSixJours ? LtServicesPointageCalcul.HeuresNormalesSamedi : 0m;

        return LtServicesPointageCalcul.HeuresNormalesJourSemaine;
    }

    /// <summary>
    /// Un jour par date dans la période : donnée en base ou jour « Normal » avec heures par défaut.
    /// Les objets créés pour les dates manquantes ne sont pas suivis par EF (usage calcul uniquement).
    /// <paramref name="dateLimiteCompletion"/> : au-delà, aucune heure inventée (jours futurs).
    /// </summary>
    public static List<SuiviJournalier> FusionnerMoisCompletPourCalculPaie(
        int employeId,
        DateTime dateDebut,
        DateTime dateFin,
        IReadOnlyList<SuiviJournalier> enBase,
        bool semaineSixJours,
        IReadOnlyDictionary<DateTime, JourTravailCalendrier> calendrier,
        bool completerJoursSansSaisie = false,
        bool forcerSamediOuvre = false,
        DateTime? dateLimiteCompletion = null)
    {
        dateDebut = dateDebut.Date;
        dateFin = dateFin.Date;
        var limite = dateLimiteCompletion?.Date;
        var semaine6 = semaineSixJours || forcerSamediOuvre;

        var parDate = enBase
            .GroupBy(s => s.Date.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var liste = new List<SuiviJournalier>();
        for (var d = dateDebut; d <= dateFin; d = d.AddDays(1))
        {
            if (parDate.TryGetValue(d, out var s))
            {
                liste.Add(s);
                continue;
            }

            var peutCompleter = completerJoursSansSaisie && (limite == null || d <= limite);
            var heures = ResoudreHeuresJourSansSaisie(d, semaine6, calendrier, peutCompleter, forcerSamediOuvre);

            liste.Add(new SuiviJournalier
            {
                EmployeId = employeId,
                Date = d,
                HeuresPrestees = heures,
                TypeJour = SuiviJournalier.TypeNormal,
                PointagesJson = null,
                HeuresManuelles = false
            });
        }

        return liste;
    }

    private static decimal ResoudreHeuresJourSansSaisie(
        DateTime date,
        bool semaineSixJours,
        IReadOnlyDictionary<DateTime, JourTravailCalendrier> calendrier,
        bool completerJoursSansSaisie,
        bool forcerSamediOuvre)
    {
        if (completerJoursSansSaisie && date.DayOfWeek != DayOfWeek.Sunday)
            return DeterminerHeuresParDefaut(date, semaineSixJours, calendrier);

        if (forcerSamediOuvre && date.DayOfWeek == DayOfWeek.Saturday)
            return LtServicesPointageCalcul.HeuresNormalesSamedi;

        return 0m;
    }
}
