using System.Globalization;
using System.Text;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services.Export;

/// <summary>Résout les jetons SourceDonnee configurables vers des valeurs texte.</summary>
public static class ExportDonneesPaieResolver
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static string Resoudre(ExportDonneesPaieContext ctx, string sourceDonnee, ColonneExportConfig? colonne = null)
    {
        if (string.IsNullOrWhiteSpace(sourceDonnee))
            return colonne?.ValeurParDefaut ?? "";

        var parts = sourceDonnee.Split('.', 2, StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return colonne?.ValeurParDefaut ?? "";

        var valeur = parts[0] switch
        {
            "Stat" => ResoudreStat(ctx, parts[1]),
            "Employe" => ResoudreEmploye(ctx, parts[1]),
            "Entreprise" => ResoudreEntreprise(ctx, parts[1]),
            "Contrat" => ResoudreContrat(ctx, parts[1]),
            "Bulletin" => ResoudreBulletin(ctx, parts[1]),
            "Saisie" => ResoudreSaisie(ctx, parts[1]),
            "Periode" => ResoudrePeriode(ctx, parts[1]),
            "Detail" => ResoudreDetail(ctx, parts[1]),
            "Virement" => ResoudreVirement(ctx, parts[1]),
            _ => ""
        };

        if (string.IsNullOrEmpty(valeur) && colonne?.ValeurParDefaut != null)
            return colonne.ValeurParDefaut;

        return Formater(valeur, colonne);
    }

    public static string ResoudreEntete(Entreprise? entreprise, PeriodePaie? periode, LigneEnteteExport ligne)
    {
        if (!string.IsNullOrWhiteSpace(ligne.ValeurFixe))
            return ligne.ValeurFixe;

        var ctx = new ExportDonneesPaieContext
        {
            Bulletin = new BulletinPaie { PeriodePaie = periode },
            Entreprise = entreprise,
            NumeroOrdre = 0
        };
        return Resoudre(ctx, ligne.SourceDonnee);
    }

    private static string ResoudreStat(ExportDonneesPaieContext ctx, string champ) =>
        champ switch
        {
            "NumeroOrdre" => ctx.NumeroOrdre.ToString(Fr),
            "HeuresTravail" => FormaterNombre(
                ctx.HeuresTravailPeriode > 0
                    ? ctx.HeuresTravailPeriode
                    : ObtenirJoursPrestes(ctx) * 8m,
                "N2"),
            _ => ""
        };

    private static string ResoudreEmploye(ExportDonneesPaieContext ctx, string champ)
    {
        var e = ctx.Employe;
        if (e is null) return "";
        return champ switch
        {
            "Matricule" => e.Matricule,
            "Nom" => e.Nom,
            "Postnom" => e.Postnom ?? "",
            "Prenom" => e.Prenom ?? "",
            "NomComplet" => NomComplet(e),
            "NomCompletMajuscules" => NomComplet(e).ToUpperInvariant(),
            "NumCnss" => e.NumCnss ?? "",
            "Nif" => ObtenirChampDynamique(e.Id, "NIF") ?? "",
            "Fonction" => ObtenirChampDynamique(e.Id, "FONCTION") ?? ctx.Contrat?.CategorieProfessionnelle?.Libelle ?? "",
            "CodeBanque" => e.CodeBanque ?? "",
            "LibelleBanque" => e.LibelleBanque ?? "",
            "AgenceBancaire" => e.AgenceBancaire ?? "",
            "NumeroCompteBancaire" => e.NumeroCompteBancaire ?? "",
            "TitulaireCompte" => !string.IsNullOrWhiteSpace(e.TitulaireCompteBancaire)
                ? e.TitulaireCompteBancaire
                : NomComplet(e),
            "DeviseCompte" => string.IsNullOrWhiteSpace(e.DeviseCompteBancaire) ? "USD" : e.DeviseCompteBancaire,
            "NbEnfants" => ctx.NbEnfants.ToString(Fr),
            "Departement" => e.Departement?.NomDepartement ?? "",
            "TypeTravailleurCnss" => ResoudreTypeTravailleurCnss(ctx),
            "CommuneAffectation" => ResoudreCommuneAffectation(ctx),
            _ => ""
        };
    }

    private static string ResoudreTypeTravailleurCnss(ExportDonneesPaieContext ctx)
    {
        var e = ctx.Employe;
        if (e is null) return "1";
        if (e.TypeTravailleurCnss is 1 or 2)
            return e.TypeTravailleurCnss.ToString(Fr);
        var typeContrat = ctx.Contrat?.TypeContrat ?? "";
        if (typeContrat.Contains("assimil", StringComparison.OrdinalIgnoreCase))
            return "2";
        return "1";
    }

    private static string ResoudreCommuneAffectation(ExportDonneesPaieContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(ctx.CommuneAffectation))
            return ctx.CommuneAffectation.Trim();
        var e = ctx.Employe;
        if (!string.IsNullOrWhiteSpace(e?.CommuneAffectation))
            return e.CommuneAffectation.Trim();
        return e?.Departement?.Etablissement?.NomSite
               ?? e?.Departement?.NomDepartement
               ?? "";
    }

    private static string? ObtenirChampDynamique(int employeId, string code)
    {
        // Résolution optionnelle via champs dynamiques — sans bloquer l'export si absent.
        return null;
    }

    private static string ResoudreEntreprise(ExportDonneesPaieContext ctx, string champ)
    {
        var ent = ctx.Entreprise;
        if (ent is null) return "";
        return champ switch
        {
            "RaisonSociale" => ent.RaisonSociale,
            "Nif" => ent.Nif ?? "",
            "NumCnss" => ent.NumCnss ?? "",
            "NumeroAffiliationCnss" => ent.NumeroAffiliationCnss ?? "",
            "NumInpp" => ent.NumInpp ?? "",
            _ => ""
        };
    }

    private static string ResoudreContrat(ExportDonneesPaieContext ctx, string champ)
    {
        var c = ctx.Contrat;
        if (c is null) return "";
        return champ switch
        {
            "SalaireBase" => FormaterNombre(c.SalaireBase, "N2"),
            "CategorieLibelle" => c.CategorieProfessionnelle?.Libelle ?? "",
            "TypeContrat" => c.TypeContrat,
            _ => ""
        };
    }

    private static string ResoudreBulletin(ExportDonneesPaieContext ctx, string champ)
    {
        var b = ctx.Bulletin;
        var brut = b.TotalGainImposable + b.TotalGainNonImposable;
        var ded = b.MontantIprNet + b.CotisationCnssOuvrier + b.CotisationInpp;
        return champ switch
        {
            "SalaireBrut" => FormaterNombre(brut, "N2"),
            "BaseIpr" => FormaterNombre(b.BaseIpr, "N2"),
            "IprBrut" => FormaterNombre(b.MontantIprBrut, "N2"),
            "IprNet" => FormaterNombre(b.MontantIprNet, "N2"),
            "ReductionFamille" => FormaterNombre(b.ReductionFamille, "N2"),
            "CnssOuvrier" => FormaterNombre(b.CotisationCnssOuvrier, "N2"),
            "CnssPatronal" => FormaterNombre(ctx.Cotisations?.CnssPatronal ?? 0, "N2"),
            "BaseCnss" => FormaterNombre(ctx.Cotisations?.BaseCnss ?? brut, "N2"),
            "BrutImposableCnss" => FormaterNombre(ctx.Cotisations?.BaseCnss ?? brut, "N2"),
            "MontantCotiseCnss" => FormaterNombre(b.CotisationCnssOuvrier, "N2"),
            "Inpp" => FormaterNombre(b.CotisationInpp, "N2"),
            "NetAPayer" => FormaterNombre(b.NetAPayer, "N2"),
            "NetAPayerCdf" => FormaterNombre(b.NetAPayerDeviseLocale, "N2"),
            "TotalDeductions" => FormaterNombre(ded, "N2"),
            "NumeroBulletin" => b.NumeroBulletin ?? "",
            _ => ""
        };
    }

    private static string ResoudreSaisie(ExportDonneesPaieContext ctx, string champ)
    {
        var s = ctx.Saisie;
        if (s is null) return champ == "JoursPrestes" ? "26" : "";
        return champ switch
        {
            "JoursPrestes" => s.JoursPrestes > 0 ? s.JoursPrestes.ToString(Fr) : "26",
            "AutresGainsImposables" => FormaterNombre(s.AutresGainsImposables, "N2"),
            "AutresGainsNonImposables" => FormaterNombre(s.AutresGainsNonImposables, "N2"),
            _ => ""
        };
    }

    private static string ResoudrePeriode(ExportDonneesPaieContext ctx, string champ)
    {
        var p = ctx.Periode;
        if (p is null) return "";
        return champ switch
        {
            "Mois" => p.Mois.ToString("D2", Fr),
            "Annee" => p.Annee.ToString(Fr),
            "Libelle" => $"{p.Mois:D2}/{p.Annee}",
            "AaaaMm" => $"{p.Annee}{p.Mois:D2}",
            "CotiseeJjMmAaaa" => new DateTime(p.Annee, p.Mois, 1)
                .AddMonths(1)
                .AddDays(-1)
                .ToString("dd/MM/yyyy", Fr),
            _ => ""
        };
    }

    private static int ObtenirJoursPrestes(ExportDonneesPaieContext ctx)
    {
        var s = ctx.Saisie;
        if (s is { JoursPrestes: > 0 })
            return s.JoursPrestes;
        return 26;
    }

    private static string ResoudreDetail(ExportDonneesPaieContext ctx, string champ)
    {
        var details = ctx.Bulletin.Details?.ToList() ?? new List<BulletinDetail>();
        return champ switch
        {
            "MontantHeuresSup" => FormaterNombre(SommeDetail(details, "heure", "sup"), "N2"),
            "MontantFeries" => FormaterNombre(SommeDetail(details, "féri", "ferie", "dimanche", "samedi"), "N2"),
            "MontantPrimes" => FormaterNombre(SommeDetail(details, "prime"), "N2"),
            "MontantConges" => FormaterNombre(SommeDetail(details, "congé", "conge"), "N2"),
            "MontantMaladie" => FormaterNombre(SommeDetail(details, "maladie", "accident"), "N2"),
            "AllocationsFamiliales" => FormaterNombre(SommeDetail(details, "allocation", "famille"), "N2"),
            _ => ""
        };
    }

    private static string ResoudreVirement(ExportDonneesPaieContext ctx, string champ)
    {
        var p = ctx.Periode;
        var prefix = "PAIE";
        return champ switch
        {
            "Reference" => ctx.ReferenceVirement ?? $"{prefix}-{p?.Annee}{p?.Mois:D2}-{ctx.Employe?.Matricule}",
            "Libelle" => $"Salaire {p?.Mois:D2}/{p?.Annee} {ctx.Employe?.Matricule}",
            _ => ""
        };
    }

    private static decimal SommeDetail(IEnumerable<BulletinDetail> details, params string[] motsCle)
    {
        decimal total = 0;
        foreach (var d in details)
        {
            var lib = d.Libelle.ToLowerInvariant();
            if (!motsCle.Any(m => lib.Contains(m, StringComparison.OrdinalIgnoreCase)))
                continue;
            total += d.Gain > 0 ? d.Gain : 0;
        }
        return total;
    }

    private static string NomComplet(Employe e) =>
        $"{e.Nom} {e.Postnom} {e.Prenom}".Trim();

    private static string Formater(string valeur, ColonneExportConfig? colonne)
    {
        if (colonne?.LargeurFixe is int width && width > 0)
        {
            if (valeur.Length > width)
                valeur = valeur[..width];
            var alignRight = string.Equals(colonne.AlignementFixe, "Droite", StringComparison.OrdinalIgnoreCase);
            return alignRight ? valeur.PadLeft(width) : valeur.PadRight(width);
        }
        return valeur;
    }

    private static string FormaterNombre(decimal valeur, string format) =>
        valeur.ToString(format, Fr);

    public static string EchapperCsv(string valeur, string separateur)
    {
        if (valeur.Contains(separateur) || valeur.Contains('"') || valeur.Contains('\n'))
            return $"\"{valeur.Replace("\"", "\"\"")}\"";
        return valeur;
    }
}
