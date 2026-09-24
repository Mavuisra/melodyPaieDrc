using System;
using System.Collections.Generic;
using System.Linq;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Helpers;

/// <summary>
/// Salaire de référence (août validé) reporté sur les mois suivants.
/// - Gains, primes, impôts CNSS/IPR, transport absences et <b>retenue salaire</b> (ajustements)
///   du mois de référence sont toujours conservés.
/// - Quinzaine / prêt / sanction : ceux du mois courant s'ils sont saisis, sinon ceux de la référence.
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
    /// Net = NetRéf + (Q+P+S)Réf − (Q+P+S)Mois − retenues_additionnelles_mois.
    /// La retenue salaire du mois de référence reste « cuite » dans NetRéf (jamais retirée).
    /// </summary>
    public static decimal CalculerNet(
        decimal netReference,
        VariablesMois varsReference,
        VariablesMois varsMoisEffectives,
        decimal retenuesAdditionnellesMois = 0m)
    {
        var salairePlein = netReference
            + varsReference.Quinzaine
            + varsReference.Pret
            + varsReference.Sanction;
        var net = salairePlein
            - varsMoisEffectives.Quinzaine
            - varsMoisEffectives.Pret
            - varsMoisEffectives.Sanction
            - retenuesAdditionnellesMois;
        return net < 0 ? 0 : decimal.Round(net, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Si aucune variable du mois n'est saisie → clone complet (même net, même retenue salaire).
    /// Sinon → overlay quinzaine/prêt/sanction, retenue salaire de référence toujours affichée.
    /// </summary>
    public static void AppliquerSurBulletin(
        BulletinPaie bulletin,
        BulletinPaie reference,
        VariablesMois varsMoisSaisies,
        bool aucuneSaisieVariablesMois)
    {
        ArgumentNullException.ThrowIfNull(bulletin);
        ArgumentNullException.ThrowIfNull(reference);

        var varsRef = ExtraireVariables(reference.Details ?? Enumerable.Empty<BulletinDetail>());
        var varsEffectives = aucuneSaisieVariablesMois
            ? varsRef
            : new VariablesMois(
                varsMoisSaisies.Quinzaine,
                varsMoisSaisies.Pret,
                varsMoisSaisies.Sanction,
                0m);

        // Retenue salaire d'août = permanente. AutresRetenues du mois = additionnelle seulement.
        var retenuesAdd = aucuneSaisieVariablesMois ? 0m : varsMoisSaisies.Retenue;
        var net = CalculerNet(reference.NetAPayer, varsRef, varsEffectives, retenuesAdd);

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
            // Remplacés ci-dessous (sauf retenue salaire permanente)
            if (cat is "quinzaine" or "pret" or "sanction")
                continue;
            if (string.Equals(d.Libelle, "INPP", StringComparison.OrdinalIgnoreCase))
                continue;
            // "retenue" (ajustements / retenue salaire) : TOUJOURS garder celle de la référence
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

        AddRetenue("Acomptes salaire", varsEffectives.Quinzaine);
        AddRetenue("Prêts / avances", varsEffectives.Pret);
        AddRetenue("Sanctions / retards", varsEffectives.Sanction);
        // Retenue salaire mois courant en plus de celle d'août (si saisie)
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
