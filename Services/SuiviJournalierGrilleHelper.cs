using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Grille « mois complet » : heures par défaut (calendrier + semaine 6 jours) et fusion avec les lignes
/// déjà en base pour que le calcul de paie corresponde à ce que l’écran affiche (y compris si le mois est partiellement renseigné).
/// </summary>
public static class SuiviJournalierGrilleHelper
{
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
    /// </summary>
    public static List<SuiviJournalier> FusionnerMoisCompletPourCalculPaie(
        int employeId,
        DateTime dateDebut,
        DateTime dateFin,
        IReadOnlyList<SuiviJournalier> enBase,
        bool semaineSixJours,
        IReadOnlyDictionary<DateTime, JourTravailCalendrier> calendrier)
    {
        dateDebut = dateDebut.Date;
        dateFin = dateFin.Date;

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

            liste.Add(new SuiviJournalier
            {
                EmployeId = employeId,
                Date = d,
                // Pour le calcul de paie, un jour sans ligne en base ne doit pas être payé par défaut.
                // Il est considéré à 0h tant qu'aucun pointage/saisie explicite n'existe.
                HeuresPrestees = 0m,
                TypeJour = SuiviJournalier.TypeNormal,
                PointagesJson = null,
                HeuresManuelles = false
            });
        }

        return liste;
    }
}
