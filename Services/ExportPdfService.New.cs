using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MelodyPaieRDC.Services;

/// <summary>Ligne pour export PDF du suivi journalier (pointage).</summary>
public sealed record SuiviJournalierPdfLigne(
    string DateAffichage,
    string JourSemaine,
    int JourCode,
    string ModeCalcul,
    decimal HeuresPrestees,
    string TypeJour);

/// <summary>Bloc employé pour export PDF multi-employés.</summary>
public sealed record SuiviJournalierPdfEmployeBloc(
    string Matricule,
    string NomComplet,
    string? Departement,
    IReadOnlyList<SuiviJournalierPdfLigne> Lignes);

/// <summary>Ligne PDF synthèse du jour : une ligne par employé avec les moments.</summary>
public sealed record PresencePdfLigne(
    string Jour,
    string Matricule,
    string NomComplet,
    string Departement,
    string Entree,
    string DebutPause,
    string FinPause,
    string Sortie,
    string Autres,
    string Statut);

public class ExportPdfService
{
    private const string DefaultPrimary = "#1E3A5F";
    private const string DefaultSecondary = "#00A6B8";
    private const string BorderColor = "#DCE3EC";
    private const string HeaderOnPrimary = "#FFFFFF";
    private const string Muted = "#64748B";

    private sealed record BrandingInfo(
        string? RaisonSociale,
        string? Adresse,
        string? Telephone,
        string? Email,
        string? SiteWeb,
        string? Nif,
        string? IdNat,
        string? Nrc,
        string? NumCnssEnt,
        string? NumeroAffiliationCnss,
        string? LogoPath,
        string PrimaryHex,
        string SecondaryHex);

    public void ExporterBulletin(BulletinPaie bulletin, string cheminFichier)
    {
        ArgumentNullException.ThrowIfNull(bulletin);
        var branding = LoadBranding();
        var document = BuildBulletinDocument(bulletin, branding);

        try
        {
            document.GeneratePdf(cheminFichier);
        }
        catch (Exception ex) when (IsLayoutConstraintException(ex))
        {
            TryGenerateDebugLayoutPdf(document, cheminFichier);
            BuildFallbackBulletinDocument(bulletin, branding).GeneratePdf(cheminFichier);
        }
    }

    public void ExporterLivrePaiePdf(IEnumerable<BulletinPaie> bulletins, int mois, int annee, string cheminFichier)
    {
        var liste = bulletins.OrderBy(x => x.Employe?.Matricule).ToList();
        var branding = LoadBranding();

        var totalBrut = liste.Sum(x => x.TotalGainImposable + x.TotalGainNonImposable);
        var totalIpr = liste.Sum(x => x.MontantIprNet);
        var totalCnss = liste.Sum(x => x.CotisationCnssOuvrier);
        var totalNet = liste.Sum(x => x.NetAPayer);

        var document = BuildLivreDocument(liste, branding, mois, annee, totalBrut, totalIpr, totalCnss, totalNet);

        try
        {
            document.GeneratePdf(cheminFichier);
        }
        catch (Exception ex) when (IsLayoutConstraintException(ex))
        {
            TryGenerateDebugLayoutPdf(document, cheminFichier);
            BuildFallbackLivreDocument(liste, mois, annee, totalBrut, totalIpr, totalCnss, totalNet).GeneratePdf(cheminFichier);
        }
    }

    /// <summary>Export du pointage journalier (grille mois / employé).</summary>
    public void ExporterSuiviJournalierPdf(
        string matricule,
        string nomCompletEmploye,
        string? departement,
        int mois,
        int annee,
        IReadOnlyList<SuiviJournalierPdfLigne> lignes,
        string cheminFichier)
    {
        ArgumentException.ThrowIfNullOrEmpty(cheminFichier);
        var branding = LoadBranding();
        var totalHeures = lignes.Sum(l => l.HeuresPrestees);
        var document = BuildSuiviJournalierDocument(branding, matricule, nomCompletEmploye, departement, mois, annee, lignes, totalHeures);

        try
        {
            document.GeneratePdf(cheminFichier);
        }
        catch (Exception ex) when (IsLayoutConstraintException(ex))
        {
            TryGenerateDebugLayoutPdf(document, cheminFichier);
            BuildFallbackSuiviJournalierDocumentBranded(branding, matricule, nomCompletEmploye, mois, annee, totalHeures).GeneratePdf(cheminFichier);
        }
    }

    /// <summary>Export récapitulatif + une page détail par employé (données issues de la base pour la période).</summary>
    public void ExporterSuiviJournalierPdfTousEmployes(
        IReadOnlyList<SuiviJournalierPdfEmployeBloc> employes,
        int mois,
        int annee,
        string cheminFichier)
    {
        ArgumentException.ThrowIfNullOrEmpty(cheminFichier);
        if (employes == null || employes.Count == 0)
            throw new ArgumentException("Aucun employé à exporter.", nameof(employes));

        var branding = LoadBranding();
        var document = BuildSuiviJournalierDocumentTousEmployes(branding, employes, mois, annee);

        try
        {
            document.GeneratePdf(cheminFichier);
        }
        catch (Exception ex) when (IsLayoutConstraintException(ex))
        {
            TryGenerateDebugLayoutPdf(document, cheminFichier);
            var total = employes.Sum(e => e.Lignes.Sum(l => l.HeuresPrestees));
            BuildFallbackSuiviJournalierTousDocumentBranded(branding, mois, annee, employes.Count, total).GeneratePdf(cheminFichier);
        }
    }

    /// <summary>Export PDF journalier des pointés du jour en synthèse moments par employé.</summary>
    public void ExporterPointesAujourdhuiSynthesePdf(
        IReadOnlyList<PresencePdfLigne> lignes,
        int mois,
        int annee,
        string cheminFichier)
    {
        ArgumentException.ThrowIfNullOrEmpty(cheminFichier);
        if (lignes == null || lignes.Count == 0)
            throw new ArgumentException("Aucune ligne de présence à exporter.", nameof(lignes));

        var branding = LoadBranding();
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8.5f));

                var jour = lignes[0].Jour;
                ComposeHeaderBand(page.Header(), branding, "POINTAGE JOURNALIER — POINTES DU JOUR",
                    $"Date {jour} — {lignes.Count} employe(s)");

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Synthese des moments par employe : Entree, Debut pause, Fin pause et Sortie.")
                        .FontSize(7.5f).FontColor(Muted);

                    col.Item().Border(1).BorderColor(BorderColor).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(68);
                            c.RelativeColumn(1.9f);
                            c.RelativeColumn(1.3f);
                            c.ConstantColumn(72);
                            c.ConstantColumn(72);
                            c.ConstantColumn(72);
                            c.ConstantColumn(72);
                            c.RelativeColumn(1.4f);
                        });

                        t.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Mat.", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Employe", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Departement", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Entree", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Debut pause", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Fin pause", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Sortie", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Autres", branding.PrimaryHex);
                        });

                        var i = 0;
                        foreach (var l in lignes.OrderBy(x => x.NomComplet, StringComparer.OrdinalIgnoreCase))
                        {
                            var bg = i++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                            DataCell(t.Cell(), Clip(l.Matricule, 18), bg);
                            DataCell(t.Cell(), Clip(l.NomComplet, 56), bg);
                            DataCell(t.Cell(), Clip(l.Departement, 34), bg);
                            DataCell(t.Cell(), Clip(l.Entree, 16), bg);
                            DataCell(t.Cell(), Clip(l.DebutPause, 16), bg);
                            DataCell(t.Cell(), Clip(l.FinPause, 16), bg);
                            DataCell(t.Cell(), Clip(l.Sortie, 16), bg);
                            DataCell(t.Cell(), Clip(l.Autres, 24), bg);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Melody Paie RDC - Page ").FontSize(8).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    t.Span(" / ").FontSize(8).FontColor(Muted);
                    t.TotalPages().FontSize(8).FontColor(Muted);
                });
            });
        });

        try
        {
            document.GeneratePdf(cheminFichier);
        }
        catch (Exception ex) when (IsLayoutConstraintException(ex))
        {
            TryGenerateDebugLayoutPdf(document, cheminFichier);
            BuildFallbackSuiviJournalierTousDocumentBranded(branding, mois, annee, lignes.Count, 0m).GeneratePdf(cheminFichier);
        }
    }

    private static IDocument BuildSuiviJournalierDocument(
        BrandingInfo b,
        string matricule,
        string nomCompletEmploye,
        string? departement,
        int mois,
        int annee,
        IReadOnlyList<SuiviJournalierPdfLigne> lignes,
        decimal totalHeures)
    {
        var bloc = new SuiviJournalierPdfEmployeBloc(matricule, nomCompletEmploye, departement, lignes);
        var sousTitre = $"Periode {mois:D2}/{annee} — {Clip(matricule, 24)} {Clip(nomCompletEmploye, 70)}";
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8.5f));

                page.Header().Element(header =>
                {
                    ComposeHeaderBand(header, b, "POINTAGE JOURNALIER", sousTitre);
                });

                page.Content().Column(col =>
                {
                    ComposeSuiviJournalierEmployeSection(
                        col,
                        b,
                        bloc,
                        mois,
                        annee,
                        totalHeures,
                        "Document genere a partir de la grille affichee — enregistrez le mois dans l'application pour conserver les donnees en base.");
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Melody Paie RDC - Page ").FontSize(8).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    t.Span(" / ").FontSize(8).FontColor(Muted);
                    t.TotalPages().FontSize(8).FontColor(Muted);
                });
            });
        });
    }

    private static IDocument BuildSuiviJournalierDocumentTousEmployes(
        BrandingInfo b,
        IReadOnlyList<SuiviJournalierPdfEmployeBloc> employes,
        int mois,
        int annee)
    {
        var liste = employes.OrderBy(e => e.Matricule, StringComparer.OrdinalIgnoreCase).ToList();
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8.5f));
                var sousTitreRecap = $"Periode {mois:D2}/{annee} — {liste.Count} employe(s)";
                page.Header().Element(header =>
                {
                    ComposeHeaderBand(header, b, "POINTAGE JOURNALIER — RECAPITULATIF", sousTitreRecap);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text("Totaux d'heures par employe (donnees en base pour la periode selectionnee).")
                        .FontSize(8).FontColor(Muted);
                    col.Item().Border(1).BorderColor(BorderColor).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(28);
                            c.ConstantColumn(72);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.ConstantColumn(72);
                        });

                        t.Header(h =>
                        {
                            HeaderCell(h.Cell(), "N", b.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Mat.", b.PrimaryHex);
                            HeaderCell(h.Cell(), "Employe", b.PrimaryHex);
                            HeaderCell(h.Cell(), "Departement", b.PrimaryHex);
                            HeaderCell(h.Cell(), "Total h.", b.PrimaryHex, true);
                        });

                        var n = 1;
                        var grandTotal = 0m;
                        foreach (var e in liste)
                        {
                            var tot = e.Lignes.Sum(l => l.HeuresPrestees);
                            grandTotal += tot;
                            var bg = n % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
                            DataCell(t.Cell(), n.ToString(CultureInfo.InvariantCulture), bg, true);
                            DataCell(t.Cell(), Clip(e.Matricule, 20), bg);
                            DataCell(t.Cell(), Clip(e.NomComplet, 60), bg);
                            DataCell(t.Cell(), Clip(e.Departement, 40), bg);
                            DataCell(t.Cell(), tot.ToString("N2", CultureInfo.InvariantCulture), bg, true);
                            n++;
                        }

                        t.Cell().ColumnSpan(4).Background("#EEF2F7").Padding(6).Text("TOTAL GENERAL").Bold().FontColor(b.PrimaryHex);
                        t.Cell().Background("#EEF2F7").Padding(6).AlignRight().Text(grandTotal.ToString("N2", CultureInfo.InvariantCulture)).Bold().FontColor(b.PrimaryHex);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Melody Paie RDC - Page ").FontSize(8).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    t.Span(" / ").FontSize(8).FontColor(Muted);
                    t.TotalPages().FontSize(8).FontColor(Muted);
                });
            });

            foreach (var emp in liste)
            {
                var totalHeures = emp.Lignes.Sum(l => l.HeuresPrestees);
                var sousTitre = $"Periode {mois:D2}/{annee} — {Clip(emp.Matricule, 24)} {Clip(emp.NomComplet, 70)}";
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8.5f));
                    page.Header().Element(header =>
                    {
                        ComposeHeaderBand(header, b, "POINTAGE JOURNALIER — DETAIL", sousTitre);
                    });

                    page.Content().Column(col =>
                    {
                        ComposeSuiviJournalierEmployeSection(
                            col,
                            b,
                            emp,
                            mois,
                            annee,
                            totalHeures,
                            "Detail calcule depuis la base (meme logique que la grille a l'ecran).");
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Melody Paie RDC - Page ").FontSize(8).FontColor(Muted);
                        t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                        t.Span(" / ").FontSize(8).FontColor(Muted);
                        t.TotalPages().FontSize(8).FontColor(Muted);
                    });
                });
            }
        });
    }

    private static void ComposeSuiviJournalierEmployeSection(
        ColumnDescriptor col,
        BrandingInfo b,
        SuiviJournalierPdfEmployeBloc emp,
        int mois,
        int annee,
        decimal totalHeures,
        string footerNote)
    {
        col.Spacing(8);
        col.Item().Border(1).BorderColor(BorderColor).Padding(8).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn();
            });

            AddInfoCell(t, "Employe", Clip(emp.NomComplet, 90));
            AddInfoCell(t, "Matricule", Clip(emp.Matricule, 40));
            AddInfoCell(t, "Departement", Clip(emp.Departement, 80));
            AddInfoCell(t, "Periode", $"{mois:D2}/{annee}");
        });

        col.Item().Text("Regles de service (pointage) : horaires, pause et tolerance selon les parametres de l'entreprise.")
            .FontSize(7.5f).FontColor(Muted).Italic();

        col.Item().Border(1).BorderColor(BorderColor).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.ConstantColumn(72);
                c.RelativeColumn(1.4f);
                c.ConstantColumn(36);
                c.RelativeColumn(1f);
                c.ConstantColumn(52);
                c.RelativeColumn(1.6f);
            });

            t.Header(h =>
            {
                HeaderCell(h.Cell(), "Date", b.PrimaryHex);
                HeaderCell(h.Cell(), "Jour", b.PrimaryHex);
                HeaderCell(h.Cell(), "1/0", b.PrimaryHex, true);
                HeaderCell(h.Cell(), "Mode", b.PrimaryHex);
                HeaderCell(h.Cell(), "Heures", b.PrimaryHex, true);
                HeaderCell(h.Cell(), "Type de jour", b.PrimaryHex);
            });

            var i = 0;
            foreach (var ligne in emp.Lignes)
            {
                var bg = i++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                DataCell(t.Cell(), Clip(ligne.DateAffichage, 14), bg);
                DataCell(t.Cell(), Clip(ligne.JourSemaine, 18), bg);
                DataCell(t.Cell(), ligne.JourCode.ToString(CultureInfo.InvariantCulture), bg, true);
                DataCell(t.Cell(), Clip(ligne.ModeCalcul, 14), bg);
                DataCell(t.Cell(), ligne.HeuresPrestees.ToString("N2", CultureInfo.InvariantCulture), bg, true);
                DataCell(t.Cell(), Clip(ligne.TypeJour, 36), bg);
            }

            t.Cell().ColumnSpan(4).Background("#EEF2F7").Padding(6).Text("TOTAL HEURES (mois)").Bold().FontColor(b.PrimaryHex);
            t.Cell().Background("#EEF2F7").Padding(6).AlignRight().Text(totalHeures.ToString("N2", CultureInfo.InvariantCulture)).Bold().FontColor(b.PrimaryHex);
            t.Cell().Background("#EEF2F7").Padding(6).Text("").FontColor(b.PrimaryHex);
        });

        col.Item().Text(footerNote)
            .FontSize(7.5f).FontColor(Muted);
    }

    private static IDocument BuildFallbackSuiviJournalierTousDocument(int mois, int annee, int nbEmployes, decimal totalHeures)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9));
                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Pointage journalier — tous employes (mode securise)").FontSize(13).Bold();
                    col.Item().Text($"Periode : {mois:D2}/{annee}");
                    col.Item().Text($"Employes : {nbEmployes} — Total heures : {totalHeures:N2}").SemiBold();
                });
            });
        });
    }

    private static IDocument BuildFallbackSuiviJournalierTousDocumentBranded(
        BrandingInfo b,
        int mois,
        int annee,
        int nbEmployes,
        decimal totalHeures)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9));
                page.Header().Element(header =>
                {
                    ComposeHeaderBand(header, b, "POINTAGE JOURNALIER — MODE SECURISE", $"Periode {mois:D2}/{annee}");
                });
                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"Employes : {nbEmployes}");
                    col.Item().Text($"Total heures : {totalHeures:N2}").SemiBold();
                });
            });
        });
    }

    private static IDocument BuildFallbackSuiviJournalierDocument(
        string matricule,
        string nomCompletEmploye,
        int mois,
        int annee,
        decimal totalHeures)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9));
                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Pointage journalier (mode securise)").FontSize(13).Bold();
                    col.Item().Text($"Employe : {Clip(matricule, 20)} — {Clip(nomCompletEmploye, 100)}");
                    col.Item().Text($"Periode : {mois:D2}/{annee}");
                    col.Item().Text($"Total heures : {totalHeures:N2}").SemiBold();
                });
            });
        });
    }

    private static IDocument BuildFallbackSuiviJournalierDocumentBranded(
        BrandingInfo b,
        string matricule,
        string nomCompletEmploye,
        int mois,
        int annee,
        decimal totalHeures)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9));
                page.Header().Element(header =>
                {
                    ComposeHeaderBand(
                        header,
                        b,
                        "POINTAGE JOURNALIER (MODE SECURISE)",
                        $"Periode {mois:D2}/{annee}");
                });
                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"Employe : {Clip(matricule, 120)} — {Clip(nomCompletEmploye, 200)}");
                    col.Item().Text($"Total heures : {totalHeures:N2}").SemiBold();
                });
            });
        });
    }

    private static IDocument BuildBulletinDocument(BulletinPaie bulletin, BrandingInfo b)
    {
        var details = bulletin.Details?.ToList() ?? new List<BulletinDetail>();
        var detailsUtiles = details
            .Where(d =>
                Math.Abs(d.Gain) > 0.0001m ||
                Math.Abs(d.Retenue) > 0.0001m ||
                (!string.IsNullOrWhiteSpace(d.Libelle) && d.Libelle.Contains("Absence", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var employeeName = Clip($"{bulletin.Employe?.Nom} {bulletin.Employe?.Postnom} {bulletin.Employe?.Prenom}".Trim(), 90);
        var totalBrut = bulletin.TotalGainImposable + bulletin.TotalGainNonImposable;
        var retenuesDiverses = details.Sum(d => d.Retenue);
        var retenuesLegales = bulletin.MontantIprNet + bulletin.CotisationCnssOuvrier + bulletin.CotisationInpp;
        var retenuesTotales = retenuesLegales + retenuesDiverses;
        var totalGains = bulletin.TotalGainImposable + bulletin.TotalGainNonImposable;
        var netRecompose = decimal.Round(totalGains - retenuesTotales, 2, MidpointRounding.AwayFromZero);
        var (salaireMensuelUsd, salaireMensuelCdf) = ResolveSalaireMensuelDepuisContrat(bulletin);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(22);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9));

                page.Header().Element(header =>
                {
                    ComposeHeaderBand(
                        header,
                        b,
                        "BULLETIN DE PAIE",
                        $"Periode {bulletin.PeriodePaie?.Mois:D2}/{bulletin.PeriodePaie?.Annee}");
                });

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Border(1).BorderColor(BorderColor).Padding(8).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });

                        AddInfoCell(t, "Employe", employeeName);
                        AddInfoCell(t, "Matricule", bulletin.Employe?.Matricule ?? "—");
                        AddInfoCell(t, "Departement", Clip(bulletin.Employe?.Departement?.NomDepartement, 50));
                        AddInfoCell(t, "Date emission", bulletin.DateGeneration.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                        AddInfoCell(t, "Numero bulletin", bulletin.NumeroBulletin ?? "—");
                        AddInfoCell(t, "CNSS salarie", bulletin.Employe?.NumCnss ?? "—");
                    });

                    // Salaire mensuel affiché en haut, plus discret (demande utilisateur).
                    col.Item().Border(1).BorderColor("#DCE8FF").Background("#F5F9FF").Padding(8).Row(r =>
                    {
                        r.RelativeItem().Text("Salaire mensuel").SemiBold().FontSize(9).FontColor("#1E3A5F");
                        r.ConstantItem(170).AlignRight().Text($"{FormatMoney(salaireMensuelUsd)} USD")
                            .SemiBold().FontSize(12).FontColor("#0D47A1");
                        r.ConstantItem(190).AlignRight().Text($"{FormatMoney(salaireMensuelCdf)} CDF")
                            .SemiBold().FontSize(12).FontColor("#0B8043");
                    });

                    col.Item().Text("Elements de paie").FontSize(10).SemiBold().FontColor(b.PrimaryHex);
                    col.Item().Border(1).BorderColor(BorderColor).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.8f);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.2f);
                        });

                        t.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Rubrique", b.PrimaryHex);
                            HeaderCell(h.Cell(), "Quantite", b.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Montant", b.PrimaryHex, true);
                        });

                        var i = 0;
                        foreach (var d in detailsUtiles)
                        {
                            var bg = i++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                            DataCell(t.Cell(), Clip(d.Libelle, 80), bg);

                            var quantite = d.BaseCalcul > 0 && d.Taux > 0
                                ? $"{d.BaseCalcul:N2} x {d.Taux:N2}"
                                : d.Taux > 0
                                    ? $"{d.Taux:N2}"
                                    : "—";
                            DataCell(t.Cell(), quantite, bg, true);

                            var montant = d.Gain > 0
                                ? $"+ {FormatMoney(d.Gain)}"
                                : $"- {FormatMoney(d.Retenue)}";
                            DataCell(t.Cell(), montant, bg, true);
                        }
                    });

                    col.Item().Border(1).BorderColor(BorderColor).Padding(8).Column(s =>
                    {
                        s.Spacing(4);
                        SummaryLine(s, "Total gains imposables", bulletin.TotalGainImposable);
                        SummaryLine(s, "Total gains non imposables", bulletin.TotalGainNonImposable);
                        SummaryLine(s, "Total brut", totalBrut);
                        SummaryLine(s, "IPR net", bulletin.MontantIprNet);
                        SummaryLine(s, "CNSS ouvrier", bulletin.CotisationCnssOuvrier);
                        SummaryLine(s, "INPP", bulletin.CotisationInpp);
                        SummaryLine(s, "Retenues diverses", retenuesDiverses);
                        SummaryLine(s, "Total retenues (legales + diverses)", retenuesTotales);

                        // Net à payer très remarquable en fin de bulletin.
                        s.Item().PaddingTop(10).Background("#0D47A1").Padding(12).Column(net =>
                        {
                            var memeDevise = Math.Abs(bulletin.NetAPayer - bulletin.NetAPayerDeviseLocale) < 0.01m;
                            var suffix = memeDevise ? " CDF" : " USD";
                            net.Item().AlignCenter().Text("NET A PAYER").Bold().FontSize(12).FontColor("#E3F2FD");
                            net.Item().AlignCenter().Text($"{FormatMoney(bulletin.NetAPayer)}{suffix}")
                                .Bold().FontSize(28).FontColor("#FFFFFF");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Melody Paie RDC - Page ").FontSize(8).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    t.Span(" / ").FontSize(8).FontColor(Muted);
                    t.TotalPages().FontSize(8).FontColor(Muted);
                });
            });
        });
    }

    private static IDocument BuildLivreDocument(
        List<BulletinPaie> liste,
        BrandingInfo b,
        int mois,
        int annee,
        decimal totalBrut,
        decimal totalIpr,
        decimal totalCnss,
        decimal totalNet)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8.5f));

                page.Header().Element(header =>
                {
                    ComposeHeaderBand(
                        header,
                        b,
                        "LIVRE DE PAIE",
                        $"Periode {mois:D2}/{annee} - Effectif {liste.Count}");
                });

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Border(1).BorderColor(BorderColor).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(24);
                            c.RelativeColumn(1.0f);
                            c.RelativeColumn(2.2f);
                            c.RelativeColumn(1.3f);
                            c.RelativeColumn(1.0f);
                            c.RelativeColumn(0.8f);
                            c.RelativeColumn(0.8f);
                            c.RelativeColumn(1.0f);
                        });

                        t.Header(h =>
                        {
                            HeaderCell(h.Cell(), "N", b.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Mat.", b.PrimaryHex);
                            HeaderCell(h.Cell(), "Employe", b.PrimaryHex);
                            HeaderCell(h.Cell(), "Departement", b.PrimaryHex);
                            HeaderCell(h.Cell(), "Brut", b.PrimaryHex, true);
                            HeaderCell(h.Cell(), "IPR", b.PrimaryHex, true);
                            HeaderCell(h.Cell(), "CNSS", b.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Net", b.PrimaryHex, true);
                        });

                        var n = 1;
                        foreach (var bulletin in liste)
                        {
                            var bg = n % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
                            var brut = bulletin.TotalGainImposable + bulletin.TotalGainNonImposable;
                            var nom = Clip($"{bulletin.Employe?.Nom} {bulletin.Employe?.Postnom} {bulletin.Employe?.Prenom}".Trim(), 60);
                            var dep = Clip(bulletin.Employe?.Departement?.NomDepartement, 30);

                            DataCell(t.Cell(), n.ToString(CultureInfo.InvariantCulture), bg, true);
                            DataCell(t.Cell(), Clip(bulletin.Employe?.Matricule, 20), bg);
                            DataCell(t.Cell(), nom, bg);
                            DataCell(t.Cell(), dep, bg);
                            DataCell(t.Cell(), FormatMoney(brut), bg, true);
                            DataCell(t.Cell(), FormatMoney(bulletin.MontantIprNet), bg, true);
                            DataCell(t.Cell(), FormatMoney(bulletin.CotisationCnssOuvrier), bg, true);
                            DataCell(t.Cell(), FormatMoney(bulletin.NetAPayer), bg, true);
                            n++;
                        }

                        t.Cell().ColumnSpan(4).Background("#EEF2F7").Padding(6).Text("TOTAUX").Bold().FontColor(b.PrimaryHex);
                        t.Cell().Background("#EEF2F7").Padding(6).AlignRight().Text(FormatMoney(totalBrut)).Bold().FontColor(b.PrimaryHex);
                        t.Cell().Background("#EEF2F7").Padding(6).AlignRight().Text(FormatMoney(totalIpr)).Bold().FontColor(b.PrimaryHex);
                        t.Cell().Background("#EEF2F7").Padding(6).AlignRight().Text(FormatMoney(totalCnss)).Bold().FontColor(b.PrimaryHex);
                        t.Cell().Background("#EEF2F7").Padding(6).AlignRight().Text(FormatMoney(totalNet)).Bold().FontColor(b.PrimaryHex);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Document interne - Melody Paie RDC - Page ").FontSize(8).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    t.Span(" / ").FontSize(8).FontColor(Muted);
                    t.TotalPages().FontSize(8).FontColor(Muted);
                });
            });
        });
    }

    private static IDocument BuildFallbackBulletinDocument(BulletinPaie bulletin, BrandingInfo branding)
    {
        var (salaireMensuelUsd, salaireMensuelCdf) = ResolveSalaireMensuelDepuisContrat(bulletin);
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9));

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text(Clip(branding.RaisonSociale, 120)).FontSize(13).Bold();
                    col.Item().Text("Bulletin de paie (mode securise)").SemiBold();
                    col.Item().Text($"Employe : {Clip($"{bulletin.Employe?.Nom} {bulletin.Employe?.Postnom} {bulletin.Employe?.Prenom}".Trim(), 120)}");
                    col.Item().Text($"Periode : {bulletin.PeriodePaie?.Mois:D2}/{bulletin.PeriodePaie?.Annee}");
                    col.Item().Text($"Net a payer : {FormatMoney(bulletin.NetAPayer)} USD").Bold();
                    col.Item().PaddingTop(8).Text($"SALAIRE MENSUEL : {FormatMoney(salaireMensuelUsd)} USD / {FormatMoney(salaireMensuelCdf)} CDF")
                        .Bold().FontSize(11);
                });
            });
        });
    }

    private static (decimal Usd, decimal Cdf) ResolveSalaireMensuelDepuisContrat(BulletinPaie bulletin)
    {
        if (bulletin.EmployeId <= 0)
            return (0m, 0m);

        var annee = bulletin.PeriodePaie?.Annee ?? bulletin.DateGeneration.Year;
        var mois = bulletin.PeriodePaie?.Mois ?? bulletin.DateGeneration.Month;
        var debutPeriode = new DateTime(annee, mois, 1);
        var finPeriode = debutPeriode.AddMonths(1).AddDays(-1);

        using var db = new PaieDbContext();
        var contrat = db.Contrats
            .Where(c => c.EmployeId == bulletin.EmployeId
                        && c.DateDebut <= finPeriode
                        && (c.DateFin == null || c.DateFin >= debutPeriode))
            .OrderByDescending(c => c.DateDebut)
            .FirstOrDefault();

        if (contrat == null)
            return (0m, 0m);

        var taux = ParametresApplicationHelper.GetTauxCdfParUsd(db);
        if (taux <= 0m) taux = 1m;

        var devise = (contrat.DeviseBase?.ToString() ?? "USD").Trim().ToUpperInvariant();
        if (string.Equals(devise, "CDF", StringComparison.Ordinal))
        {
            var cdf = decimal.Round(contrat.SalaireBase, 2, MidpointRounding.AwayFromZero);
            var usd = decimal.Round(cdf / taux, 2, MidpointRounding.AwayFromZero);
            return (usd, cdf);
        }
        else
        {
            var usd = decimal.Round(contrat.SalaireBase, 2, MidpointRounding.AwayFromZero);
            var cdf = decimal.Round(usd * taux, 2, MidpointRounding.AwayFromZero);
            return (usd, cdf);
        }
    }

    private static IDocument BuildFallbackLivreDocument(
        List<BulletinPaie> liste,
        int mois,
        int annee,
        decimal totalBrut,
        decimal totalIpr,
        decimal totalCnss,
        decimal totalNet)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9));
                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Livre de paie (mode securise)").FontSize(13).Bold();
                    col.Item().Text($"Periode : {mois:D2}/{annee}");
                    col.Item().Text($"Effectif : {liste.Count}");
                    col.Item().Text($"Totaux - Brut {FormatMoney(totalBrut)} | IPR {FormatMoney(totalIpr)} | CNSS {FormatMoney(totalCnss)} | Net {FormatMoney(totalNet)}")
                        .SemiBold();
                });
            });
        });
    }

    private static void ComposeHeaderBand(IContainer container, BrandingInfo b, string title, string subtitle)
    {
        container.Column(col =>
        {
            col.Item().Background(b.PrimaryHex).Padding(10).Row(row =>
            {
                if (!string.IsNullOrWhiteSpace(b.LogoPath))
                {
                    try
                    {
                        row.ConstantItem(52).Height(36).Image(b.LogoPath).FitArea();
                    }
                    catch
                    {
                    }
                }

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(Clip(b.RaisonSociale ?? "Entreprise", 120)).FontSize(13).Bold().FontColor(HeaderOnPrimary);
                    if (!string.IsNullOrWhiteSpace(b.Adresse))
                        c.Item().Text(Clip(b.Adresse, 170)).FontSize(8).SemiBold().FontColor("#C7D5E8");

                    var contacts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(b.Telephone)) contacts.Add($"Tel. {b.Telephone}");
                    if (!string.IsNullOrWhiteSpace(b.Email)) contacts.Add(b.Email);
                    if (!string.IsNullOrWhiteSpace(b.SiteWeb)) contacts.Add(b.SiteWeb);
                    if (contacts.Count > 0)
                        c.Item().Text(Clip(string.Join(" - ", contacts), 170)).FontSize(7).SemiBold().FontColor("#B6C7DF");

                    var ids = new List<string>();
                    if (!string.IsNullOrWhiteSpace(b.Nif)) ids.Add($"NIF {b.Nif}");
                    if (!string.IsNullOrWhiteSpace(b.IdNat)) ids.Add($"ID.NAT {b.IdNat}");
                    if (!string.IsNullOrWhiteSpace(b.Nrc)) ids.Add($"RCCM {b.Nrc}");
                    if (!string.IsNullOrWhiteSpace(b.NumCnssEnt)) ids.Add($"CNSS {b.NumCnssEnt}");
                    if (!string.IsNullOrWhiteSpace(b.NumeroAffiliationCnss)) ids.Add($"Aff CNSS {b.NumeroAffiliationCnss}");
                    if (ids.Count > 0)
                        c.Item().Text(Clip(string.Join(" - ", ids), 200)).FontSize(6.5f).SemiBold().FontColor("#ADC2DB");
                });
            });

            col.Item().PaddingTop(6).Background("#F1F5F9").Border(1).BorderColor(BorderColor).Padding(8).Row(row =>
            {
                row.RelativeItem().Text(title).Bold().FontSize(12).FontColor(b.PrimaryHex);
                row.ConstantItem(260).AlignRight().Text(subtitle).SemiBold().FontSize(10).FontColor(b.SecondaryHex);
            });
        });
    }

    private static void AddInfoCell(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(5).BorderBottom(0.5f).BorderColor(BorderColor).Text($"{label} : {Clip(value, 110)}").FontSize(8.5f);
    }

    private static void HeaderCell(IContainer cell, string text, string backgroundHex, bool right = false)
    {
        if (right)
        {
            cell.Background(backgroundHex)
                .Padding(6)
                .BorderBottom(1)
                .BorderColor(backgroundHex)
                .AlignRight()
                .Text(text)
                .SemiBold()
                .FontColor(HeaderOnPrimary)
                .FontSize(8);
        }
        else
        {
            cell.Background(backgroundHex)
                .Padding(6)
                .BorderBottom(1)
                .BorderColor(backgroundHex)
                .Text(text)
                .SemiBold()
                .FontColor(HeaderOnPrimary)
                .FontSize(8);
        }
    }

    private static void DataCell(IContainer cell, string text, string bgHex, bool right = false)
    {
        if (right)
        {
            cell.Background(bgHex)
                .Padding(5)
                .BorderBottom(0.5f)
                .BorderColor(BorderColor)
                .AlignRight()
                .Text(text)
                .FontSize(8);
        }
        else
        {
            cell.Background(bgHex)
                .Padding(5)
                .BorderBottom(0.5f)
                .BorderColor(BorderColor)
                .Text(text)
                .FontSize(8);
        }
    }

    private static void SummaryLine(ColumnDescriptor column, string label, decimal value)
    {
        column.Item().Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(8.5f);
            r.ConstantItem(170).AlignRight().Text(FormatMoney(value)).FontSize(8.5f).SemiBold();
        });
    }

    private static string Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";

        var clean = value.Trim().Replace("\r", " ").Replace("\n", " ");
        return clean;
    }

    private static string FormatMoney(decimal value) => $"{value:N2}";

    private static bool IsLayoutConstraintException(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) &&
                current.Message.Contains("conflicting size constraints", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void TryGenerateDebugLayoutPdf(IDocument document, string outputPath)
    {
        var debugPath = BuildSiblingPdfPath(outputPath, "_debug_layout");
        var previous = QuestPDF.Settings.EnableDebugging;
        QuestPDF.Settings.EnableDebugging = true;
        try
        {
            document.GeneratePdf(debugPath);
        }
        catch
        {
        }
        finally
        {
            QuestPDF.Settings.EnableDebugging = previous;
        }
    }

    private static string BuildSiblingPdfPath(string originalPath, string suffix)
    {
        var directory = Path.GetDirectoryName(originalPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(originalPath);
        var ext = Path.GetExtension(originalPath);
        return Path.Combine(directory, $"{name}{suffix}{ext}");
    }

    private static BrandingInfo LoadBranding()
    {
        string? raison = null, adr = null, tel = null, email = null, site = null, nif = null, idNat = null, nrc = null, cnssEnt = null, affCnss = null, logo = null;
        var primary = DefaultPrimary;
        var secondary = DefaultSecondary;

        var profil = EntrepriseBrandingService.ChargerProfilCourant();
        if (!string.IsNullOrWhiteSpace(profil.RaisonSociale))
        {
            raison = profil.RaisonSociale;
            primary = EntrepriseBrandingService.NormaliserCouleurHex(profil.CouleurPrincipale, DefaultPrimary);
            secondary = EntrepriseBrandingService.NormaliserCouleurHex(profil.CouleurSecondaire, DefaultSecondary);
            logo = profil.CheminLogo;

            using var db = new PaieDbContext();
            var id = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(db);
            var ent = id > 0
                ? db.Entreprises.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(e => e.Id == id)
                : null;
            if (ent != null)
            {
                adr = ent.Adresse;
                tel = ent.Telephone;
                email = ent.Email;
                site = ent.SiteWeb;
                nif = ent.Nif;
                idNat = ent.IdNat;
                nrc = ent.Nrc;
                cnssEnt = ent.NumCnss;
                affCnss = ent.NumeroAffiliationCnss;
            }
        }

        return new BrandingInfo(raison, adr, tel, email, site, nif, idNat, nrc, cnssEnt, affCnss, logo, primary, secondary);
    }

    private static string NormalizeHex(string raw, string fallback)
    {
        var value = raw.Trim();
        if (value.Length == 0)
            return fallback;
        return value.StartsWith("#", StringComparison.Ordinal) ? value : "#" + value;
    }
}
