using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services.Export;

/// <summary>Répartition des gains du bulletin vers les colonnes de la feuille de paie CNSS.</summary>
public sealed class FeuillePaieCnssColonnes
{
    public decimal SalaireBase { get; set; }
    public decimal IndemniteVieChere { get; set; }
    public decimal Primes { get; set; }
    public decimal Gratifications { get; set; }
    public decimal AllocationsConges { get; set; }
    public decimal AvantagesNature { get; set; }
    public decimal Commissions { get; set; }
    public decimal AutresIndemnites { get; set; }
}

public static class FeuillePaieCnssColonnesMapper
{
    public static FeuillePaieCnssColonnes Repartir(BulletinPaie bulletin, Contrat? contrat)
    {
        var cols = new FeuillePaieCnssColonnes();
        var details = bulletin.Details?
            .Where(d => d.Gain > 0)
            .ToList() ?? new List<BulletinDetail>();

        decimal salaireBaseLigne = 0;
        foreach (var d in details)
        {
            var lib = d.Libelle.ToLowerInvariant();
            if (EstSalaireBase(lib))
            {
                salaireBaseLigne += d.Gain;
                continue;
            }

            if (Contient(lib, "vie chère", "vie chere", "cherté", "cherte"))
                cols.IndemniteVieChere += d.Gain;
            else if (Contient(lib, "gratification"))
                cols.Gratifications += d.Gain;
            else if (Contient(lib, "congé", "conge") || (Contient(lib, "allocation") && Contient(lib, "cong")))
                cols.AllocationsConges += d.Gain;
            else if (Contient(lib, "avantage") && Contient(lib, "nature") || lib.Contains("avantage en nature"))
                cols.AvantagesNature += d.Gain;
            else if (Contient(lib, "commission"))
                cols.Commissions += d.Gain;
            else if (Contient(lib, "prime"))
                cols.Primes += d.Gain;
            else if (!EstLigneInformation(lib))
                cols.AutresIndemnites += d.Gain;
        }

        // Uniquement les montants du bulletin (pas le salaire contractuel si absence de prestation).
        cols.SalaireBase = salaireBaseLigne;

        return cols;
    }

    private static bool EstSalaireBase(string lib) =>
        (Contient(lib, "salaire") && Contient(lib, "base"))
        || lib.Contains("salaire de base")
        || lib.Contains("salaire base");

    private static bool EstLigneInformation(string lib) =>
        Contient(lib, "heure", "période", "periode", "absence info", "info");

    private static bool Contient(string lib, params string[] mots) =>
        mots.Any(m => lib.Contains(m, StringComparison.OrdinalIgnoreCase));
}
