using System.Globalization;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
namespace MelodyPaieRDC.Services;

/// <summary>
/// Retards : tolérance par employé, calcul unique pour UI, PDF et bulletin.
/// </summary>
public static class RetardPaieHelper
{
    // Règle client : on ne commence à sanctionner qu'à partir du 4e retard sanctionnable.
    // (Retard sanctionnable = dépassement du seuil en minutes.)
    public const int NbRetardsAvantSanction = 3;

    public static LtServicesRegles ReglesPourEmploye(LtServicesRegles entreprise, Employe? employe)
    {
        if (employe == null || string.IsNullOrWhiteSpace(employe.HeureLimiteTolerance))
            return entreprise;

        if (!TryParseHeure(employe.HeureLimiteTolerance, out var tol))
            return entreprise;

        return new LtServicesRegles
        {
            ModePointage = entreprise.ModePointage,
            DeductionPauseAutomatique = entreprise.DeductionPauseAutomatique,
            HeureDebutTravail = entreprise.HeureDebutTravail,
            HeureLimiteTolerance = tol,
            HeureDebutPause = entreprise.HeureDebutPause,
            HeureFinPause = entreprise.HeureFinPause,
            HeureFinSemaine = entreprise.HeureFinSemaine,
            HeureFinSamedi = entreprise.HeureFinSamedi
        };
    }

    public static string LibelleHeureLimite(LtServicesRegles regles) =>
        regles.HeureLimiteTolerance.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    public static int CalculerMinutesRetard(DateTime entree, TimeSpan heureLimite)
    {
        var t = entree.TimeOfDay;
        if (t <= heureLimite)
            return 0;
        return (int)Math.Floor((t - heureLimite).TotalMinutes);
    }

    public static bool EstRetard(DateTime? entree, TimeSpan heureLimite) =>
        entree.HasValue && entree.Value.TimeOfDay > heureLimite;

    public static string FormaterDureeRetard(int minutes)
    {
        if (minutes <= 0) return "—";
        var h = minutes / 60;
        var m = minutes % 60;
        return h > 0 ? $"{h} h {m:D2} min" : $"{m} min";
    }

    /// <summary>Montant sanction pour un jour (0 si sous le seuil ou politique inactive).</summary>
    public static decimal CalculerSanctionJour(
        PolitiquePaieContext politique,
        int minutesRetard,
        decimal salaireJour,
        decimal tauxHoraire)
    {
        if (!politique.RetardSanctionActive || minutesRetard <= 0)
            return 0m;
        if (minutesRetard < politique.RetardSeuilMinutes)
            return 0m;

        return politique.RetardModeSanction switch
        {
            ParametrePolitiquePaie.RetardModeDemiJour =>
                decimal.Round(salaireJour / 2m, 2, MidpointRounding.AwayFromZero),
            ParametrePolitiquePaie.RetardModeHoraire =>
                decimal.Round(minutesRetard / 60m * tauxHoraire, 2, MidpointRounding.AwayFromZero),
            _ => 0m
        };
    }

    public static decimal CalculerSanctionsPeriode(
        PolitiquePaieContext politique,
        Employe employe,
        Contrat contrat,
        IEnumerable<SuiviJournalier> suivisPeriode,
        LtServicesRegles reglesEntreprise)
    {
        if (!politique.RetardSanctionActive)
            return 0m;

        var regles = ReglesPourEmploye(reglesEntreprise, employe);
        var joursRef = politique.JoursReferencePaie;
        if (joursRef <= 0) joursRef = SalaireReferenceHelper.JoursDefaut;
        var heuresJour = politique.HeuresParJour;
        if (heuresJour <= 0) heuresJour = SalaireReferenceHelper.HeuresDefaut;

        var salaireJour = contrat.SalaireBase / joursRef;
        var tauxHoraire = salaireJour / heuresJour;
        decimal total = 0m;
        var nbRetardsSanctionnables = 0;

        foreach (var sj in suivisPeriode.Where(s =>
                     string.Equals(s.TypeJour, SuiviJournalier.TypeNormal, StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(sj.PointagesJson))
                continue;

            var pointages = PointagesJournalierSerializer.Deserialiser(sj.PointagesJson, sj.Date);
            if (pointages.Count == 0)
                continue;

            var entree = pointages.Min();
            var minutes = CalculerMinutesRetard(entree, regles.HeureLimiteTolerance);
            if (minutes <= 0 || minutes < politique.RetardSeuilMinutes)
                continue;

            nbRetardsSanctionnables++;
            if (nbRetardsSanctionnables <= NbRetardsAvantSanction)
                continue;

            total += CalculerSanctionJour(politique, minutes, salaireJour, tauxHoraire);
        }

        return total;
    }

    public static bool TryParseHeure(string? texte, out TimeSpan heure)
    {
        heure = default;
        if (string.IsNullOrWhiteSpace(texte))
            return false;
        return TimeSpan.TryParse(texte.Trim(), CultureInfo.InvariantCulture, out heure)
               || TimeSpan.TryParse(texte.Trim(), out heure);
    }
}
