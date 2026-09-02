namespace MelodyPaieRDC.Helpers;

/// <summary>
/// Indemnités dont le montant bulletin = montant d'affectation (sans gross-up « salaire en net »).
/// </summary>
public static class IndemniteAffectationFixeHelper
{
    public static bool ConserverMontantAffectationSurBulletin(string? libelle)
    {
        if (string.IsNullOrWhiteSpace(libelle))
            return false;

        var l = libelle.ToUpperInvariant();
        return l.Contains("KM", StringComparison.Ordinal)
               || l.Contains("KILOM", StringComparison.Ordinal)
               || l.Contains("LOGEMENT", StringComparison.Ordinal);
    }
}
