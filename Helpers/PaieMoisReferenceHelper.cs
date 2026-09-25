using System;
using System.Collections.Generic;
using System.Linq;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Helpers;

/// <summary>
/// Salaire de référence (août) pour les mois suivants :
/// - copie gains / primes / IPR / CNSS + <b>retenue salaire permanente</b> d'août ;
/// - <b>ne clone jamais</b> quinzaine, prêt, sanction/retard d'août ;
/// - applique uniquement les variables du mois courant.
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

    /// <summary>
    /// Net = NetRéf + (Q+P+S)Réf − (Q+P+S+retenues_add)Mois.
    /// On remet les dettes d'août pour repartir du salaire plein, puis on applique celles du mois.
    /// </summary>
    public static decimal CalculerNet(
        decimal netReference,
        VariablesMois varsReference,
        VariablesMois varsMois,
        decimal retenuesAdditionnellesMois = 0m)
    {
        var salairePlein = netReference
            + varsReference.Quinzaine
            + varsReference.Pret
            + varsReference.Sanction;
        var net = salairePlein
            - varsMois.Quinzaine
            - varsMois.Pret
            - varsMois.Sanction
            - retenuesAdditionnellesMois;
        return net < 0 ? 0 : decimal.Round(net, 2, MidpointRounding.AwayFromZero);
    }

    public static void AppliquerSurBulletin(
        BulletinPaie bulletin,
        BulletinPaie reference,
        VariablesMois varsMoisSaisies,
        bool aucuneSaisieVariablesMois)
    {
        ArgumentNullException.ThrowIfNull(bulletin);
        ArgumentNullException.ThrowIfNull(reference);

        var varsRef = ExtraireVariables(reference.Details ?? Enumerable.Empty<BulletinDetail>());
        // Jamais cloner Q/P/S d'août : si pas de saisie mois → 0 (modifs du mois restent vides / à saisir)
        var varsMois = aucuneSaisieVariablesMois
            ? new VariablesMois(0, 0, 0, 0)
            : new VariablesMois(
                varsMoisSaisies.Quinzaine,
                varsMoisSaisies.Pret,
                varsMoisSaisies.Sanction,
                0m);
        var retenuesAdd = aucuneSaisieVariablesMois ? 0m : varsMoisSaisies.Retenue;
        var net = CalculerNet(reference.NetAPayer, varsRef, varsMois, retenuesAdd);

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
            // Ne jamais reporter quinzaine / prêt / sanction d'août
            if (cat is "quinzaine" or "pret" or "sanction")
                continue;
            if (string.Equals(d.Libelle, "INPP", StringComparison.OrdinalIgnoreCase))
                continue;
            // Retenue salaire (ajustements) d'août : permanente
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
        if (retenuesAdd > 0)
            AddRetenue("Ajustements retenues (mois)", retenuesAdd);

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
        if (L.Contains("AJUSTEMENT", StringComparison.Ordinal) || L.Contains("AUTRES RETENUE", StringComparison.Ordinal)
            || L.Contains("RETENUE SALAIRE", StringComparison.Ordinal) || L.Contains("RETENU SALAIRE", StringComparison.Ordinal))
            return "retenue";
        if (L.Contains("TRANSPORT ABS", StringComparison.Ordinal))
            return "transport_abs";
        return null;
    }
}
