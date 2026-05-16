namespace MelodyPaieRDC.Helpers;

/// <summary>
/// Outils pour les taux de change (CDF / USD etc.).
/// </summary>
public static class TauxChangeHelper
{
    public static decimal CdfVersUsd(decimal montantCdf, decimal tauxCdfUsd)
    {
        return montantCdf / tauxCdfUsd;
    }

    public static decimal UsdVersCdf(decimal montantUsd, decimal tauxCdfUsd)
    {
        return montantUsd * tauxCdfUsd;
    }
}
