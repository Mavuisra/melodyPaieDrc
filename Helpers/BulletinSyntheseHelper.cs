using System;
using System.Linq;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Helpers;

/// <summary>Synthèse lisible du bulletin : montant total, déductions et solde à payer.</summary>
public sealed record BulletinSynthesePaie(
    decimal MontantTotal,
    decimal Quinzaine,
    decimal Pret,
    decimal RetenueSociale,
    decimal Impot,
    decimal Sanctions,
    decimal AutresRetenues,
    decimal TotalDeductions,
    decimal Solde)
{
    public string FormuleSolde =>
        $"Solde = {MontantTotal:N2} − ({Quinzaine:N2} + {Pret:N2} + {RetenueSociale:N2} + {Impot:N2}" +
        (Sanctions > 0 ? $" + {Sanctions:N2}" : "") +
        (AutresRetenues > 0 ? $" + {AutresRetenues:N2}" : "") +
        $") = {Solde:N2}";
}

/// <summary>Extrait les montants clés d'un bulletin pour l'affichage et l'impression.</summary>
public static class BulletinSyntheseHelper
{
    public static BulletinSynthesePaie Construire(BulletinPaie bulletin)
    {
        ArgumentNullException.ThrowIfNull(bulletin);

        var montantTotal = bulletin.TotalGainImposable + bulletin.TotalGainNonImposable;
        var impot = bulletin.MontantIprNet;
        var retenueSociale = bulletin.CotisationCnssOuvrier;
        var quinzaine = RetenueParLibelles(bulletin, "acompte", "quinzaine");
        var pret = RetenueParLibelles(bulletin, "prêt", "pret", "avance");
        var sanctions = RetenueParLibelles(bulletin, "sanction", "retard");
        var autresRetenues = RetenueParLibelles(bulletin, "ajustement") + RetenuesPrimesEtDiverses(bulletin);

        var totalDeductions = quinzaine + pret + retenueSociale + impot + sanctions + autresRetenues;
        var solde = bulletin.NetAPayer;

        return new BulletinSynthesePaie(
            montantTotal,
            quinzaine,
            pret,
            retenueSociale,
            impot,
            sanctions,
            autresRetenues,
            totalDeductions,
            solde);
    }

    private static decimal RetenuesPrimesEtDiverses(BulletinPaie bulletin)
    {
        var details = bulletin.Details;
        if (details == null || details.Count == 0)
            return 0m;

        var dejaComptes = new[]
        {
            "acompte", "quinzaine", "prêt", "pret", "avance", "sanction", "retard",
            "ajustement", "ipr", "impôt", "impot", "cnss", "inpp", "cotisation"
        };

        return details
            .Where(d => d.Retenue > 0.0001m && !EstLibelleExclu(d.Libelle, dejaComptes))
            .Sum(d => d.Retenue);
    }

    private static bool EstLibelleExclu(string? libelle, string[] motsExclus)
    {
        if (string.IsNullOrWhiteSpace(libelle))
            return true;

        var lower = libelle.ToLowerInvariant();
        return motsExclus.Any(m => lower.Contains(m, StringComparison.Ordinal));
    }

    private static decimal RetenueParLibelles(BulletinPaie bulletin, params string[] mots)
    {
        var details = bulletin.Details;
        if (details == null || details.Count == 0)
            return 0m;

        return details
            .Where(d => d.Retenue > 0.0001m && mots.Any(m =>
                d.Libelle.Contains(m, StringComparison.OrdinalIgnoreCase)))
            .Sum(d => d.Retenue);
    }
}
