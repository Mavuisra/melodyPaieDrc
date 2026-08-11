using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Aligne le calcul de paie sur le suivi journalier réel : recalcul LT depuis les horodatages enregistrés
/// et jours équivalents pondérés (8 h lun.–ven., 5 h sam. selon le calendrier), pas seulement Σh ÷ 8.
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

    /// <summary>Contexte calendrier pour une période (ouverture samedi, jours fériés/repos).</summary>
    public static CalendrierPaieContext ChargerCalendrierPaie(PaieDbContext db, DateTime dateDebut, DateTime dateFin)
    {
        dateDebut = dateDebut.Date;
        dateFin = dateFin.Date;

        var calendrier = db.JoursTravailCalendrier
            .AsNoTracking()
            .Where(j => j.DateJour >= dateDebut && j.DateJour <= dateFin)
            .ToDictionary(j => j.DateJour.Date);

        var semaineSixJours = calendrier.Any(kvp =>
            kvp.Key.DayOfWeek == DayOfWeek.Saturday &&
            string.Equals(kvp.Value.TypeJour, "Ouvre", StringComparison.OrdinalIgnoreCase));

        return new CalendrierPaieContext(calendrier, semaineSixJours);
    }

    /// <summary>
    /// Totaux heures + jours équivalents pondérés pour un employé sur une période (même règles que la paie).
    /// </summary>
    public static SuiviJournalierPresenceTotaux CalculerTotauxPresenceEmploye(
        PaieDbContext db,
        int employeId,
        PeriodePaie periode)
    {
        var (politique, debut, fin) = PeriodePaieHelper.ResoudrePeriode(db, periode);
        return CalculerTotauxPresenceEmploye(db, employeId, debut, fin, politique);
    }

    /// <summary>
    /// Totaux heures + jours équivalents pondérés pour un employé sur une période (même règles que la paie).
    /// </summary>
    public static SuiviJournalierPresenceTotaux CalculerTotauxPresenceEmploye(
        PaieDbContext db,
        int employeId,
        DateTime dateDebut,
        DateTime dateFin,
        PolitiquePaieContext? politique = null)
    {
        dateDebut = dateDebut.Date;
        dateFin = dateFin.Date;
        if (dateFin < dateDebut)
            return SuiviJournalierPresenceTotaux.Vide;

        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(db);
        var calendrierCtx = ChargerCalendrierPaie(db, dateDebut, dateFin);

        var suivis = db.SuivisJournaliers
            .AsNoTracking()
            .Where(s => s.EmployeId == employeId && s.Date >= dateDebut && s.Date <= dateFin)
            .ToList();

        List<SuiviJournalier> suivisComptables;
        var semaineSixJours = calendrierCtx.SemaineSixJours;
        if (politique != null)
        {
            semaineSixJours = calendrierCtx.SemaineSixJours || politique.ForcerSamediOuvre;
            if (politique.CompleterJoursSansSaisie || politique.ForcerSamediOuvre)
            {
                suivis = SuiviJournalierGrilleHelper.FusionnerMoisCompletPourCalculPaie(
                    employeId,
                    dateDebut,
                    dateFin,
                    suivis,
                    semaineSixJours,
                    calendrierCtx.Calendrier,
                    politique.CompleterJoursSansSaisie,
                    politique.ForcerSamediOuvre);
            }
        }

        suivisComptables = suivis
            .Where(s => string.Equals(s.TypeJour, SuiviJournalier.TypeNormal, StringComparison.OrdinalIgnoreCase))
            .Where(s => RecalculerHeuresEffectives(s, reglesLt) > 0m)
            .ToList();

        var totalHeures = suivisComptables.Sum(s => RecalculerHeuresEffectives(s, reglesLt));
        var joursEquiv = CalculerJoursEquivalentsPaie(
            suivisComptables,
            semaineSixJours,
            calendrierCtx.Calendrier,
            reglesLt);

        return new SuiviJournalierPresenceTotaux
        {
            TotalHeures = decimal.Round(totalHeures, 2, MidpointRounding.AwayFromZero),
            JoursEquivalents = decimal.Round(joursEquiv, 2, MidpointRounding.AwayFromZero)
        };
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
            var denom = hNom > 0 ? hNom : LtServicesRegles.Defaut.HeuresNormalesJourSemaine;
            sum += h / denom;
        }

        return decimal.Round(sum, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>Même logique que le suivi journalier / LTservices pour une journée « pleine » théorique.</summary>
    public static decimal DeterminerHeuresNominalesJour(
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

    /// <summary>Heures nominales d'un jour via le calendrier en base (maladie, congé, etc.).</summary>
    public static decimal DeterminerHeuresNominalesJourDepuisDb(PaieDbContext db, DateTime date)
    {
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(db);
        var debutMois = new DateTime(date.Year, date.Month, 1);
        var finMois = debutMois.AddMonths(1).AddDays(-1);
        var ctx = ChargerCalendrierPaie(db, debutMois, finMois);
        return DeterminerHeuresNominalesJour(date, ctx.SemaineSixJours, ctx.Calendrier, reglesLt);
    }

    /// <summary>Heures sup. d'une journée (pointages LT ou saisie manuelle).</summary>
    public static decimal CalculerHeuresSupplementairesJour(
        SuiviJournalier suivi,
        decimal heuresNominalesJour,
        LtServicesRegles? reglesLt = null)
    {
        if (heuresNominalesJour <= 0m)
            return 0m;
        if (!string.Equals(suivi.TypeJour, SuiviJournalier.TypeNormal, StringComparison.OrdinalIgnoreCase))
            return 0m;

        if (!string.IsNullOrEmpty(suivi.PointagesJson) && !suivi.HeuresManuelles)
        {
            var pointages = PointagesJournalierSerializer.Deserialiser(suivi.PointagesJson, suivi.Date);
            return LtServicesPointageCalcul.CalculerHeuresSupplementaires(
                pointages, suivi.Date, heuresNominalesJour, reglesLt);
        }

        var exces = suivi.HeuresPrestees - heuresNominalesJour;
        return exces > 0m
            ? decimal.Round(exces, 2, MidpointRounding.AwayFromZero)
            : 0m;
    }
}

public sealed class CalendrierPaieContext
{
    public IReadOnlyDictionary<DateTime, JourTravailCalendrier> Calendrier { get; }
    public bool SemaineSixJours { get; }

    public CalendrierPaieContext(IReadOnlyDictionary<DateTime, JourTravailCalendrier> calendrier, bool semaineSixJours)
    {
        Calendrier = calendrier;
        SemaineSixJours = semaineSixJours;
    }
}

public sealed class SuiviJournalierPresenceTotaux
{
    public static SuiviJournalierPresenceTotaux Vide { get; } = new();

    public decimal TotalHeures { get; init; }
    public decimal JoursEquivalents { get; init; }
}
