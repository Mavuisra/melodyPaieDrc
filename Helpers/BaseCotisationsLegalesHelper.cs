namespace MelodyPaieRDC.Helpers;

/// <summary>
/// Base CNSS / IPR / INPP : salaire de base (proratisé) + prime d'ancienneté uniquement.
/// Transport, KM, logement et autres indemnités en sont exclus.
/// </summary>
public static class BaseCotisationsLegalesHelper
{
    public const decimal TauxCnssOuvrier = 5m;
    public const decimal TauxIpr = 10m;

    public static bool EstPrimeAnciennete(string? libelle)
        => !string.IsNullOrWhiteSpace(libelle)
           && libelle.Contains("anciennet", StringComparison.OrdinalIgnoreCase);

    public static decimal CalculerBase(decimal salaireBrut, IEnumerable<(string Libelle, decimal Montant)> gainsImposables)
    {
        var anciennete = gainsImposables
            .Where(g => EstPrimeAnciennete(g.Libelle))
            .Sum(g => g.Montant);
        return Round(Math.Max(0m, salaireBrut + anciennete));
    }

    public static decimal CalculerCnss(decimal baseLegale)
        => baseLegale <= 0 ? 0m : Round(baseLegale * TauxCnssOuvrier / 100m);

    public static decimal CalculerIpr(decimal baseLegale)
        => baseLegale <= 0 ? 0m : Round(baseLegale * TauxIpr / 100m);

    public static decimal CalculerInpp(decimal baseLegale, decimal tauxInpp)
        => baseLegale <= 0 || tauxInpp <= 0 ? 0m : Round(baseLegale * tauxInpp / 100m);

    private static decimal Round(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
