using System.Linq;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Aligne le calcul de paie sur le suivi journalier réel : recalcul LT depuis les horodatages enregistrés
/// et jours équivalents pondérés (8 h lun.–ven., 5 h sam. selon calendrier), pas seulement Σh ÷ 8.
/// </summary>
public static class SuiviJournalierCalculPaieHelper
{
    /// <summary>Heures effectives pour la paie : même logique que la grille (pointages auto → recalcul LT).</summary>
    public static decimal RecalculerHeuresEffectives(SuiviJournalier s, LtServicesRegles? reglesLt = null)
    {
        if (s.TypeJour == SuiviJournalier.TypeNormal &&
            !string.IsNullOrEmpty(s.PointagesJson) &&
            !s.HeuresManuelles)
            return PointagesJournalierSerializer.CalculerHeuresLt(s.PointagesJson, s.Date.Date, reglesLt);

        return s.HeuresPrestees;
    }

    /// <summary>
    /// Somme des ratios h / h nominal du jour (calendrier + semaine 6 jours) pour obtenir un « jour équivalent »
    /// comparable au prorata sur le mois calendaire.
    /// </summary>
    public static decimal CalculerJoursEquivalentsPaie(
        IReadOnlyList<SuiviJournalier> suivis,
        bool semaineSixJours,
        IReadOnlyDictionary<DateTime, JourTravailCalendrier> calendrier,
        LtServicesRegles? reglesLt = null)
    {
        decimal sum = 0m;
        foreach (var s in suivis)
        {
            var h = RecalculerHeuresEffectives(s, reglesLt);
            var hNom = DeterminerHeuresNominalesJour(s.Date, semaineSixJours, calendrier, reglesLt);
            var denom = hNom > 0 ? hNom : 8m;
            sum += h / denom;
        }

        return decimal.Round(sum, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>Même logique que le suivi journalier / LTservices pour une journée « pleine » théorique.</summary>
    private static decimal DeterminerHeuresNominalesJour(
        DateTime date,
        bool semaineSixJours,
        IReadOnlyDictionary<DateTime, JourTravailCalendrier> calendrier,
        LtServicesRegles? reglesLt = null)
    {
        var r = reglesLt ?? LtServicesRegles.Defaut;
        if (calendrier.TryGetValue(date.Date, out var jour))
        {
            if (string.Equals(jour.TypeJour, "Repos", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(jour.TypeJour, "Ferie", StringComparison.OrdinalIgnoreCase))
                return 0m;

            if (string.Equals(jour.TypeJour, "Ouvre", StringComparison.OrdinalIgnoreCase))
            {
                return date.DayOfWeek == DayOfWeek.Saturday
                    ? r.HeuresNormalesSamedi
                    : r.HeuresNormalesJourSemaine;
            }
        }

        if (date.DayOfWeek == DayOfWeek.Sunday)
            return 0m;

        if (date.DayOfWeek == DayOfWeek.Saturday)
            return semaineSixJours ? r.HeuresNormalesSamedi : 0m;

        return r.HeuresNormalesJourSemaine;
    }
}
