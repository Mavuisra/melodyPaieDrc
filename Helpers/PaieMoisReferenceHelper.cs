using System;
using System.Collections.Generic;
using System.Linq;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Helpers;

/// <summary>
/// Salaire de référence (ex. août validé) reporté sur les mois suivants.
/// Formule : Net = NetRéf + (quinzaine+prêt+sanction)Réf − (quinzaine+prêt+sanction+retenues)Mois.
/// Les ajustements du mois de référence restent inclus dans le net validé.
/// </summary>
public static class PaieMoisReferenceHelper
{
    public readonly record struct VariablesMois(
        decimal Quinzaine,
        decimal Pret,
        decimal Sanction,
        decimal Retenue);

    public static VariablesMois ExtraireVariables(IEnumerable<BulletinDetail> details)
    {
        decimal q = 0, p = 0, s = 0, r = 0;
        foreach (var d in details)
        {
            var ret = d.Retenue;
            if (ret <= 0) continue;
            switch (Classer(d.Libelle))
            {
                case "quinzaine": q += ret; break;
                case "pret": p += ret; break;
                case "sanction": s += ret; break;
                case "retenue": r += ret; break;
            }
        }
        return new VariablesMois(q, p, s, r);
    }

    public static decimal CalculerNet(
        decimal netReference,
        VariablesMois varsReference,
        VariablesMois varsMois)
    {
        var salairePlein = netReference + varsReference.Quinzaine + varsReference.Pret + varsReference.Sanction;
        var net = salairePlein - varsMois.Quinzaine - varsMois.Pret - varsMois.Sanction - varsMois.Retenue;
        return net < 0 ? 0 : decimal.Round(net, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Remplace gains/impôts du bulletin courant par ceux de la référence,
    /// applique les variables du mois, recalcule le net. INPP = 0.
    /// </summary>
    public static void AppliquerSurBulletin(
        BulletinPaie bulletin,
        BulletinPaie reference,
        VariablesMois varsMois)
    {
        ArgumentNullException.ThrowIfNull(bulletin);
        ArgumentNullException.ThrowIfNull(reference);

        var varsRef = ExtraireVariables(reference.Details ?? Enumerable.Empty<BulletinDetail>());
        var net = CalculerNet(reference.NetAPayer, varsRef, varsMois);

        bulletin.TotalGainImposable = reference.TotalGainImposable;
        bulletin.TotalGainNonImposable = reference.TotalGainNonImposable;
        bulletin.BaseIpr = reference.BaseIpr;
        bulletin.MontantIprBrut = reference.MontantIprBrut;
        bulletin.ReductionFamille = reference.ReductionFamille;
        bulletin.MontantIprNet = reference.MontantIprNet;
        bulletin.CotisationCnssOuvrier = reference.CotisationCnssOuvrier;
        bulletin.CotisationInpp = 0m;
        bulletin.NetAPayer = net;
        if (reference.NetAPayerDeviseLocale == reference.NetAPayer)
            bulletin.NetAPayerDeviseLocale = net;

        var details = new List<BulletinDetail>();
        foreach (var d in reference.Details ?? Enumerable.Empty<BulletinDetail>())
        {
            var cat = Classer(d.Libelle);
            if (cat is "quinzaine" or "pret" or "sanction" or "retenue")
                continue;
            if (string.Equals(d.Libelle, "INPP", StringComparison.OrdinalIgnoreCase))
                continue;
            details.Add(new BulletinDetail
            {
                Libelle = d.Libelle,
                BaseCalcul = d.BaseCalcul,
                Taux = d.Taux,
                Gain = d.Gain,
                Retenue = d.Retenue
            });
        }

        void AddRetenue(string libelle, decimal montant)
        {
            if (montant <= 0) return;
            details.Add(new BulletinDetail
            {
                Libelle = libelle,
                BaseCalcul = 0,
                Taux = 0,
                Gain = 0,
                Retenue = decimal.Round(montant, 2, MidpointRounding.AwayFromZero)
            });
        }

        AddRetenue("Acomptes salaire", varsMois.Quinzaine);
        AddRetenue("Prêts / avances", varsMois.Pret);
        AddRetenue("Sanctions / retards", varsMois.Sanction);
        AddRetenue("Ajustements retenues", varsMois.Retenue);

        bulletin.Details = details;
    }

    private static string? Classer(string? libelle)
    {
        if (string.IsNullOrWhiteSpace(libelle)) return null;
        var L = libelle.ToUpperInvariant();
        if (L == "IPR" || L.StartsWith("IPR ", StringComparison.Ordinal) || L.Contains("CNSS", StringComparison.Ordinal) || L == "INPP")
            return "tax";
        if (L.Contains("ACOMPTE", StringComparison.Ordinal) || L.Contains("QUINZ", StringComparison.Ordinal))
            return "quinzaine";
        if (L.Contains("PRET", StringComparison.Ordinal) || L.Contains("PRÊT", StringComparison.Ordinal) || L.Contains("AVANCE", StringComparison.Ordinal))
            return "pret";
        if (L.Contains("SANCTION", StringComparison.Ordinal) || L.Contains("RETARD", StringComparison.Ordinal))
            return "sanction";
        if (L.Contains("AJUSTEMENT", StringComparison.Ordinal) || L.Contains("AUTRES RETENUE", StringComparison.Ordinal))
            return "retenue";
        if (L.Contains("TRANSPORT ABS", StringComparison.Ordinal))
            return "transport_abs";
        return null;
    }
}
