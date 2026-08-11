using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>Résolution unifiée de la base CNSS à partir d'un bulletin généré.</summary>
public static class BulletinCnssBaseResolver
{
    /// <summary>
    /// Base CNSS : priorité à la ligne de détail CNSS ouvrier, sinon total des gains du bulletin.
    /// </summary>
    public static decimal ObtenirBaseCnss(BulletinPaie bulletin)
    {
        var details = bulletin.Details;
        if (details != null)
        {
            var ligne = details.FirstOrDefault(d =>
                !string.IsNullOrWhiteSpace(d.Libelle) &&
                d.Libelle.Contains("CNSS", StringComparison.OrdinalIgnoreCase) &&
                d.Libelle.Contains("ouvr", StringComparison.OrdinalIgnoreCase) &&
                d.BaseCalcul > 0m);

            if (ligne != null)
                return ligne.BaseCalcul;
        }

        return bulletin.TotalGainImposable + bulletin.TotalGainNonImposable;
    }
}
