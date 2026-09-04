using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
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

/// <summary>Ligne récapitulatif heures travaillées (page Heures).</summary>
public sealed record HeuresTotauxEmployePdfLigne(
    string Matricule,
    string NomComplet,
    string? Departement,
    decimal TotalHeures,
    decimal TotalJoursEquivalent);

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

/// <summary>Ligne PDF rapport journalier des mouvements (arrivée / sortie).</summary>
public sealed record MouvementJourPdfLigne(
    string Jour,
    string Matricule,
    string NomComplet,
    string Departement,
    string Arrivee,
    string Sortie,
    string StatutRetard,
    string DureeRetard);

/// <summary>Ligne PDF rapport des retards du jour.</summary>
public sealed record RetardPdfLigne(
    string Jour,
    string Matricule,
    string NomComplet,
    string Departement,
    string HeureEntree,
    string DureeRetard,
    string TauxHoraire,
    string CoutRetard,
    string HeureLimite);

public class ExportPdfService
{
    private const string DefaultPrimary = "#047857";
    private const string DefaultSecondary = "#34D399";
    private const string BorderColor = "#DCE3EC";
    private const string HeaderOnPrimary = "#FFFFFF";
    private const string Muted = "#64748B";

    static ExportPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

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

    /// <summary>
    /// Exporte les bulletins en format A5, deux par feuille A4 (découpe au milieu).
    /// </summary>
    public void ExporterBulletinsFeuilleA4(IEnumerable<BulletinPaie> bulletins, string cheminFichier)
    {
        ArgumentNullException.ThrowIfNull(bulletins);
        var liste = bulletins
            .OrderBy(x => x.PeriodePaie?.Annee)
            .ThenBy(x => x.PeriodePaie?.Mois)
            .ThenBy(x => x.Employe?.Matricule)
            .ToList();
        if (liste.Count == 0)
            throw new InvalidOperationException("Aucun bulletin à exporter.");

        var branding = LoadBranding();
        var layouts = liste.Select(PrepareBulletinLayout).ToList();
        var document = BuildBulletinsDeuxParA4Document(layouts, branding);

        try
        {
            document.GeneratePdf(cheminFichier);
        }
        catch (Exception ex) when (IsLayoutConstraintException(ex))
        {
            TryGenerateDebugLayoutPdf(document, cheminFichier);
            throw new InvalidOperationException("Impossible de générer la feuille A4 (2 bulletins A5). Réduisez le nombre de lignes ou contactez le support.", ex);
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

    /// <summary>Export PDF du récapitulatif des heures travaillées par employé (période de paie).</summary>
    public void ExporterTotauxHeuresEmployesPdf(
        IReadOnlyList<HeuresTotauxEmployePdfLigne> lignes,
        int mois,
        int annee,
        decimal totalHeures,
        decimal totalJoursEquivalent,
        string cheminFichier)
    {
        ArgumentException.ThrowIfNullOrEmpty(cheminFichier);
        if (lignes == null || lignes.Count == 0)
            throw new ArgumentException("Aucun employé à exporter.", nameof(lignes));

        var branding = LoadBranding();
        var liste = lignes.OrderBy(l => l.Matricule, StringComparer.OrdinalIgnoreCase).ToList();
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9f));
                var sousTitre = $"Période {mois:D2}/{annee} — {liste.Count} employé(s)";
                page.Header().Element(h => ComposeHeaderBand(h, branding, "HEURES TRAVAILLÉES — RÉCAPITULATIF", sousTitre));

                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text("Totaux issus du suivi journalier (pointages LT / saisie) — même calcul que la page Heures.")
                        .FontSize(8).FontColor(Muted);
                    col.Item().Border(1).BorderColor(BorderColor).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(28);
                            c.ConstantColumn(72);
                            c.RelativeColumn(2.2f);
                            c.RelativeColumn(1.5f);
                            c.ConstantColumn(72);
                            c.ConstantColumn(72);
                        });

                        t.Header(h =>
                        {
                            HeaderCell(h.Cell(), "N°", branding.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Matricule", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Employé", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Département", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Total h.", branding.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Jours équiv.", branding.PrimaryHex, true);
                        });

                        var n = 1;
                        foreach (var l in liste)
                        {
                            var bg = n % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
                            DataCell(t.Cell(), n.ToString(CultureInfo.InvariantCulture), bg, true);
                            DataCell(t.Cell(), Clip(l.Matricule, 20), bg);
                            DataCell(t.Cell(), Clip(l.NomComplet, 60), bg);
                            DataCell(t.Cell(), Clip(l.Departement ?? "—", 40), bg);
                            DataCell(t.Cell(), l.TotalHeures.ToString("N2", CultureInfo.InvariantCulture), bg, true);
                            DataCell(t.Cell(), l.TotalJoursEquivalent.ToString("N2", CultureInfo.InvariantCulture), bg, true);
                            n++;
                        }

                        t.Cell().ColumnSpan(4).Background("#EEF2F7").Padding(6).Text("TOTAL GÉNÉRAL").Bold().FontColor(branding.PrimaryHex);
                        t.Cell().Background("#EEF2F7").Padding(6).AlignRight().Text(totalHeures.ToString("N2", CultureInfo.InvariantCulture)).Bold().FontColor(branding.PrimaryHex);
                        t.Cell().Background("#EEF2F7").Padding(6).AlignRight().Text(totalJoursEquivalent.ToString("N2", CultureInfo.InvariantCulture)).Bold().FontColor(branding.PrimaryHex);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Melody Paie RDC — ").FontSize(8).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    t.Span(" / ").FontSize(8).FontColor(Muted);
                    t.TotalPages().FontSize(8).FontColor(Muted);
                });
            });
        });

        document.GeneratePdf(cheminFichier);
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

    /// <summary>Export PDF rapport journalier des mouvements (arrivée / sortie).</summary>
    public void ExporterMouvementsJourPdf(
        IReadOnlyList<MouvementJourPdfLigne> lignes,
        DateTime jour,
        string? titreAgent,
        string heureLimiteTolerance,
        string cheminFichier)
    {
        ArgumentException.ThrowIfNullOrEmpty(cheminFichier);
        if (lignes == null || lignes.Count == 0)
            throw new ArgumentException("Aucune ligne de mouvement à exporter.", nameof(lignes));

        var branding = LoadBranding();
        var titre = string.IsNullOrWhiteSpace(titreAgent)
            ? "RAPPORT JOURNALIER DES MOUVEMENTS"
            : "MOUVEMENTS DU JOUR — AGENT";
        var sousTitre = string.IsNullOrWhiteSpace(titreAgent)
            ? $"Date {jour:dd/MM/yyyy} — {lignes.Count} agent(s) — limite {heureLimiteTolerance}"
            : $"{titreAgent} — {jour:dd/MM/yyyy} — limite {heureLimiteTolerance}";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9f));

                ComposeHeaderBand(page.Header(), branding, titre, sousTitre);

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Entrees et sorties enregistrees — retards calcules automatiquement apres l'heure limite.")
                        .FontSize(7.5f).FontColor(Muted);

                    col.Item().Border(1).BorderColor(BorderColor).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(68);
                            c.RelativeColumn(2f);
                            c.RelativeColumn(1.3f);
                            c.ConstantColumn(72);
                            c.ConstantColumn(72);
                            c.ConstantColumn(88);
                            c.ConstantColumn(72);
                        });

                        t.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Mat.", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Employe", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Departement", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Arrivee", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Sortie", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Statut", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Retard", branding.PrimaryHex);
                        });

                        var i = 0;
                        foreach (var l in lignes)
                        {
                            var bg = i++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                            DataCell(t.Cell(), Clip(l.Matricule, 18), bg);
                            DataCell(t.Cell(), Clip(l.NomComplet, 56), bg);
                            DataCell(t.Cell(), Clip(l.Departement, 34), bg);
                            DataCell(t.Cell(), Clip(l.Arrivee, 16), bg);
                            DataCell(t.Cell(), Clip(l.Sortie, 16), bg);
                            DataCell(t.Cell(), Clip(l.StatutRetard, 18), bg);
                            DataCell(t.Cell(), Clip(l.DureeRetard, 16), bg);
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
            BuildFallbackMouvementsJourDocument(branding, lignes, jour, titreAgent, heureLimiteTolerance)
                .GeneratePdf(cheminFichier);
        }
    }
    public void ExporterRetardsJourPdf(
        IReadOnlyList<RetardPdfLigne> lignes,
        DateTime jour,
        string heureLimiteTolerance,
        string totalUsd,
        string totalCdf,
        string cheminFichier)
    {
        ArgumentException.ThrowIfNullOrEmpty(cheminFichier);
        if (lignes == null || lignes.Count == 0)
            throw new ArgumentException("Aucun retard a exporter.", nameof(lignes));

        var branding = LoadBranding();
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9f));

                ComposeHeaderBand(page.Header(), branding, "GESTION DES RETARDS",
                    $"Date {jour:dd/MM/yyyy} — {lignes.Count} retard(s) — limite {heureLimiteTolerance}");

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Cout estime = duree du retard x taux horaire du contrat actif.")
                        .FontSize(7.5f).FontColor(Muted);

                    col.Item().Border(1).BorderColor(BorderColor).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(68);
                            c.RelativeColumn(2f);
                            c.RelativeColumn(1.2f);
                            c.ConstantColumn(72);
                            c.ConstantColumn(72);
                            c.ConstantColumn(88);
                            c.ConstantColumn(88);
                        });

                        t.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Mat.", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Employe", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Departement", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Entree", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Duree", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Taux/h", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Cout", branding.PrimaryHex);
                        });

                        var i = 0;
                        foreach (var l in lignes)
                        {
                            var bg = i++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                            DataCell(t.Cell(), Clip(l.Matricule, 18), bg);
                            DataCell(t.Cell(), Clip(l.NomComplet, 56), bg);
                            DataCell(t.Cell(), Clip(l.Departement, 30), bg);
                            DataCell(t.Cell(), Clip(l.HeureEntree, 16), bg);
                            DataCell(t.Cell(), Clip(l.DureeRetard, 16), bg);
                            DataCell(t.Cell(), Clip(l.TauxHoraire, 20), bg);
                            DataCell(t.Cell(), Clip(l.CoutRetard, 20), bg);
                        }
                    });

                    col.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Text($"Total estime USD : {totalUsd}").FontSize(9).SemiBold();
                        r.RelativeItem().AlignRight().Text($"Total estime CDF : {totalCdf}").FontSize(9).SemiBold();
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
            BuildFallbackRetardsJourDocument(branding, lignes, jour, heureLimiteTolerance, totalUsd, totalCdf)
                .GeneratePdf(cheminFichier);
        }
    }

    /// <summary>Export PDF d'un contrat de travail (fiche récapitulative).</summary>
    public void ExporterContratPdf(int contratId, string cheminFichier, PaieDbContext? dbExistant = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(cheminFichier);

        var possedeDb = dbExistant != null;
        var db = dbExistant ?? new PaieDbContext();
        try
        {
            var contrat = db.Contrats
                .Include(c => c.Employe)
                .ThenInclude(e => e!.Departement)
                .Include(c => c.CategorieProfessionnelle)
                .FirstOrDefault(c => c.Id == contratId)
                ?? throw new InvalidOperationException("Contrat introuvable.");

            var employe = contrat.Employe
                ?? throw new InvalidOperationException("Employé introuvable pour ce contrat.");

            var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseIdEmploye(db, employe.Id);
            var politique = new PolitiquePaieService(db).Charger(entrepriseId);
            contrat.JoursReferencePaie = politique.JoursReferencePaie;
            contrat.HeuresParJour = politique.HeuresParJour;

            var branding = LoadBranding();
            var nomComplet = Clip($"{employe.Nom} {employe.Postnom} {employe.Prenom}".Trim(), 120);
            var departement = Clip(employe.Departement?.NomDepartement ?? "—", 80);
            var categorie = Clip(contrat.CategorieProfessionnelle?.Libelle ?? "—", 80);
            var dateFin = contrat.DateFin.HasValue
                ? contrat.DateFin.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : "Indéterminée (CDI)";
            var preavisMontant = decimal.Round(contrat.SalaireBase * contrat.PreavisMoisBase, 2, MidpointRounding.AwayFromZero);
            var indemniteMontant = decimal.Round(contrat.SalaireBase * contrat.IndemniteLicenciementMoisBase, 2, MidpointRounding.AwayFromZero);
            var generation = DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9.5f));

                    ComposeHeaderBand(page.Header(), branding, "CONTRAT DE TRAVAIL", $"Type {contrat.TypeContrat}");

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().Text("Fiche récapitulative du contrat de travail")
                            .FontSize(10).SemiBold().FontColor(branding.PrimaryHex);

                        col.Item().Border(1).BorderColor(BorderColor).Padding(10).Column(info =>
                        {
                            info.Item().Text("Informations employé").SemiBold().FontColor(branding.PrimaryHex);
                            info.Item().PaddingTop(4).Text($"Matricule : {Clip(employe.Matricule, 40)}");
                            info.Item().Text($"Nom complet : {nomComplet}");
                            info.Item().Text($"Département : {departement}");
                            if (!string.IsNullOrWhiteSpace(employe.NumCnss))
                                info.Item().Text($"N° CNSS : {Clip(employe.NumCnss, 40)}");
                        });

                        col.Item().Border(1).BorderColor(BorderColor).Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.4f);
                                c.RelativeColumn();
                            });

                            t.Header(h =>
                            {
                                HeaderCell(h.Cell(), "Élément du contrat", branding.PrimaryHex);
                                HeaderCell(h.Cell(), "Valeur", branding.PrimaryHex);
                            });

                            void Ligne(string libelle, string valeur, int i)
                            {
                                var bg = i % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                                DataCell(t.Cell(), libelle, bg);
                                DataCell(t.Cell(), valeur, bg);
                            }

                            var i = 0;
                            Ligne("Type de contrat", contrat.TypeContrat, i++);
                            Ligne("Date de début", contrat.DateDebut.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), i++);
                            Ligne("Date de fin", dateFin, i++);
                            Ligne("Catégorie professionnelle", categorie, i++);
                            Ligne("Salaire de base mensuel", $"{FormatMoney(contrat.SalaireBase)} {contrat.DeviseBase}", i++);
                            Ligne($"Salaire journalier (/{contrat.JoursReferencePaie:0.##} j)", $"{FormatMoney(contrat.SalaireJour)} {contrat.DeviseBase}", i++);
                            Ligne($"Salaire horaire (/{contrat.HeuresParJour:0.##} h)", $"{FormatMoney(contrat.SalaireHeure)} {contrat.DeviseBase}", i++);
                            Ligne("Majoration heures supplémentaires", $"{contrat.TauxMajorationHeuresSup:N0} %", i++);
                            Ligne("Majoration travail de nuit", $"{contrat.TauxMajorationNuit:N0} %", i++);
                            Ligne("Majoration jours fériés", $"{contrat.TauxMajorationJourFerie:N0} %", i++);
                            Ligne("Préavis (base mois de salaire)", $"{contrat.PreavisMoisBase:N2} mois — {FormatMoney(preavisMontant)} {contrat.DeviseBase}", i++);
                            Ligne("Indemnité licenciement (base)", $"{contrat.IndemniteLicenciementMoisBase:N2} mois — {FormatMoney(indemniteMontant)} {contrat.DeviseBase}", i++);
                        });

                        col.Item().Text("Document généré à partir des données enregistrées dans Melody Paie RDC. " +
                                       "Les montants de préavis et d'indemnité sont indicatifs et doivent être validés conformément au Code du travail et aux accords applicables.")
                            .FontSize(8f).FontColor(Muted).Italic();
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span($"Généré le {generation} — Melody Paie RDC — Page ").FontSize(8).FontColor(Muted);
                        t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    });
                });
            });

            document.GeneratePdf(cheminFichier);
        }
        finally
        {
            if (!possedeDb)
                db.Dispose();
        }
    }

    /// <summary>Rapport personnalisé : situation mensuelle d'un agent.</summary>
    public void ExporterRapportAgentSituationPdf(
        SituationPaieAgentLigne ligne,
        int mois,
        int annee,
        string cheminFichier)
    {
        ArgumentException.ThrowIfNullOrEmpty(cheminFichier);
        ArgumentNullException.ThrowIfNull(ligne);

        var branding = LoadBranding();
        var periode = $"{mois:D2}/{annee}";
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(22);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9.5f));

                ComposeHeaderBand(page.Header(), branding, "SITUATION MENSUELLE AGENT", $"Periode {periode}");

                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Border(1).BorderColor(BorderColor).Padding(10).Column(info =>
                    {
                        info.Item().Text($"Matricule : {Clip(ligne.Matricule, 40)}").SemiBold();
                        info.Item().Text($"Nom : {Clip(ligne.NomComplet, 120)}");
                        info.Item().Text($"Departement : {Clip(ligne.Departement, 80)}");
                        info.Item().Text($"Heures prestees (periode) : {ligne.TotalHeuresLibelle}").FontColor(branding.PrimaryHex);
                        info.Item().Text(ligne.StatutBulletinLibelle).FontSize(8.5f).FontColor(Muted);
                    });

                    col.Item().Border(1).BorderColor(BorderColor).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn();
                        });

                        t.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Rubrique", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Montant (USD)", branding.PrimaryHex, true);
                        });

                        void Ligne(string libelle, decimal montant, int i)
                        {
                            var bg = i % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                            DataCell(t.Cell(), libelle, bg);
                            DataCell(t.Cell(), FormatMoney(montant), bg, true);
                        }

                        var i = 0;
                        Ligne("Salaire (brut periode)", ligne.Salaire, i++);
                        Ligne("Quinzaine / acomptes", ligne.Quinzaine, i++);
                        Ligne("Retenue (CNSS)", ligne.Retenue, i++);
                        Ligne("Impôt (IPR net)", ligne.Impot, i++);
                        Ligne("Retards / sanctions", ligne.Retards, i++);
                        Ligne("Prêts / avances", ligne.Prets, i++);
                        t.Cell().Background("#ECFDF5").Padding(6).Text("Solde à payer").Bold().FontColor(branding.PrimaryHex);
                        t.Cell().Background("#ECFDF5").Padding(6).AlignRight().Text(FormatMoney(ligne.SoldeAPayer)).Bold().FontColor(branding.PrimaryHex);
                    });

                    if (!ligne.AvecBulletin)
                    {
                        col.Item().Text("Bulletin non genere : lancez le calcul de paie (menu Calcul) pour completer les montants.")
                            .FontSize(8.5f).FontColor(Muted);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ").FontSize(8).FontColor(Muted);
                    t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                });
            });
        });

        document.GeneratePdf(cheminFichier);
    }

    /// <summary>Justificatif des octrois de quinzaine d'une période.</summary>
    public void ExporterOctroisQuinzainesPdf(
        IReadOnlyList<QuinzaineOctroi> octrois,
        int mois,
        int annee,
        string cheminFichier)
    {
        ArgumentException.ThrowIfNullOrEmpty(cheminFichier);
        if (octrois == null || octrois.Count == 0)
            throw new ArgumentException("Aucun octroi à exporter.", nameof(octrois));

        var branding = LoadBranding();
        var periode = $"{mois:D2}/{annee}";
        var total = octrois.Sum(o => o.Montant);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9));
                ComposeHeaderBand(page.Header(), branding, "OCTROIS DE QUINZAINE", $"Période {periode}");
                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"{octrois.Count} octroi(s) — total {total:N2}")
                        .FontSize(10).SemiBold();
                    col.Item().Border(1).BorderColor(BorderColor).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(80);
                            c.ConstantColumn(90);
                            c.RelativeColumn(2);
                            c.ConstantColumn(90);
                            c.RelativeColumn(1.5f);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Background(DefaultPrimary).Padding(4).Text("Date").FontColor("#FFFFFF").SemiBold();
                            h.Cell().Background(DefaultPrimary).Padding(4).Text("Matricule").FontColor("#FFFFFF").SemiBold();
                            h.Cell().Background(DefaultPrimary).Padding(4).Text("Employé").FontColor("#FFFFFF").SemiBold();
                            h.Cell().Background(DefaultPrimary).Padding(4).Text("Montant").FontColor("#FFFFFF").SemiBold();
                            h.Cell().Background(DefaultPrimary).Padding(4).Text("Signature").FontColor("#FFFFFF").SemiBold();
                        });
                        foreach (var o in octrois.OrderBy(x => x.DateOctroi))
                        {
                            var nom = o.Employe == null
                                ? "—"
                                : $"{o.Employe.Nom} {o.Employe.Postnom} {o.Employe.Prenom}".Trim();
                            t.Cell().Padding(4).MinHeight(28).Text(o.DateOctroi.ToString("dd/MM/yyyy"));
                            t.Cell().Padding(4).MinHeight(28).Text(o.Employe?.Matricule ?? "—");
                            t.Cell().Padding(4).MinHeight(28).Text(nom);
                            t.Cell().Padding(4).MinHeight(28).Text($"{o.Montant:N2}");
                            t.Cell().Padding(4).MinHeight(28).Text("");
                        }
                    });
                });
            });
        });
        document.GeneratePdf(cheminFichier);
    }

    /// <summary>Rapport périodique des quinzaines (acomptes sur salaire).</summary>
    public void ExporterRapportQuinzainesPdf(
        IReadOnlyList<SituationPaieAgentLigne> lignes,
        int mois,
        int annee,
        string cheminFichier)
    {
        ArgumentException.ThrowIfNullOrEmpty(cheminFichier);
        if (lignes == null || lignes.Count == 0)
            throw new ArgumentException("Aucune ligne a exporter.", nameof(lignes));

        var branding = LoadBranding();
        var periode = $"{mois:D2}/{annee}";
        var totalQuinz = lignes.Sum(l => l.Quinzaine);
        var totalSolde = lignes.Sum(l => l.SoldeAPayer);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8.5f));

                ComposeHeaderBand(page.Header(), branding, "RAPPORT DES QUINZAINES", $"Periode {periode}");

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Acomptes sur salaire verses en cours de mois (rubrique ACOMPTES_SALAIRE).")
                        .FontSize(8f).FontColor(Muted);

                    col.Item().Border(1).BorderColor(BorderColor).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(72);
                            c.RelativeColumn(2f);
                            c.RelativeColumn(1.2f);
                            c.ConstantColumn(88);
                            c.ConstantColumn(88);
                            c.ConstantColumn(88);
                        });

                        t.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Matricule", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Employe", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Departement", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Salaire brut", branding.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Quinzaine", branding.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Solde a payer", branding.PrimaryHex, true);
                        });

                        var idx = 0;
                        foreach (var l in lignes)
                        {
                            var bg = idx++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                            DataCell(t.Cell(), Clip(l.Matricule, 20), bg);
                            DataCell(t.Cell(), Clip(l.NomComplet, 48), bg);
                            DataCell(t.Cell(), Clip(l.Departement, 36), bg);
                            DataCell(t.Cell(), FormatMoney(l.Salaire), bg, true);
                            DataCell(t.Cell(), FormatMoney(l.Quinzaine), bg, true);
                            DataCell(t.Cell(), FormatMoney(l.SoldeAPayer), bg, true);
                        }

                        t.Cell().ColumnSpan(3).Background("#EEF2F7").Padding(6).Text("TOTAL").Bold();
                        t.Cell().Background("#EEF2F7").Padding(6).AlignRight().Text(FormatMoney(lignes.Sum(x => x.Salaire))).Bold();
                        t.Cell().Background("#EEF2F7").Padding(6).AlignRight().Text(FormatMoney(totalQuinz)).Bold().FontColor(branding.PrimaryHex);
                        t.Cell().Background("#EEF2F7").Padding(6).AlignRight().Text(FormatMoney(totalSolde)).Bold();
                    });
                });
            });
        });

        document.GeneratePdf(cheminFichier);
    }

    /// <summary>Rapport mensuel des salaires (situation complete par agent).</summary>
    public void ExporterRapportMensuelSalairesPdf(
        IReadOnlyList<SituationPaieAgentLigne> lignes,
        int mois,
        int annee,
        string cheminFichier)
    {
        ArgumentException.ThrowIfNullOrEmpty(cheminFichier);
        if (lignes == null || lignes.Count == 0)
            throw new ArgumentException("Aucune ligne a exporter.", nameof(lignes));

        var branding = LoadBranding();
        var periode = $"{mois:D2}/{annee}";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(14);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(7.5f));

                ComposeHeaderBand(page.Header(), branding, "RAPPORT MENSUEL DES SALAIRES", $"Periode {periode}");

                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Border(1).BorderColor(BorderColor).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(58);
                            c.RelativeColumn(1.6f);
                            c.ConstantColumn(52);
                            c.ConstantColumn(58);
                            c.ConstantColumn(52);
                            c.ConstantColumn(52);
                            c.ConstantColumn(48);
                            c.ConstantColumn(48);
                            c.ConstantColumn(48);
                            c.ConstantColumn(58);
                        });

                        t.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Mat.", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Employe", branding.PrimaryHex);
                            HeaderCell(h.Cell(), "Heures", branding.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Salaire", branding.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Quinz.", branding.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Reten.", branding.PrimaryHex, true);
                            HeaderCell(h.Cell(), "IPR", branding.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Retards", branding.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Prets", branding.PrimaryHex, true);
                            HeaderCell(h.Cell(), "Solde", branding.PrimaryHex, true);
                        });

                        var idx = 0;
                        foreach (var l in lignes)
                        {
                            var bg = idx++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                            DataCell(t.Cell(), Clip(l.Matricule, 14), bg);
                            DataCell(t.Cell(), Clip(l.NomComplet, 36), bg);
                            DataCell(t.Cell(), l.TotalHeures.ToString("N1", CultureInfo.InvariantCulture), bg, true);
                            DataCell(t.Cell(), FormatMoney(l.Salaire), bg, true);
                            DataCell(t.Cell(), FormatMoney(l.Quinzaine), bg, true);
                            DataCell(t.Cell(), FormatMoney(l.Retenue), bg, true);
                            DataCell(t.Cell(), FormatMoney(l.Impot), bg, true);
                            DataCell(t.Cell(), FormatMoney(l.Retards), bg, true);
                            DataCell(t.Cell(), FormatMoney(l.Prets), bg, true);
                            DataCell(t.Cell(), FormatMoney(l.SoldeAPayer), bg, true);
                        }

                        t.Cell().ColumnSpan(2).Background("#EEF2F7").Padding(5).Text("TOTAL").Bold();
                        t.Cell().Background("#EEF2F7").Padding(5).AlignRight().Text(lignes.Sum(x => x.TotalHeures).ToString("N1", CultureInfo.InvariantCulture)).Bold();
                        t.Cell().Background("#EEF2F7").Padding(5).AlignRight().Text(FormatMoney(lignes.Sum(x => x.Salaire))).Bold();
                        t.Cell().Background("#EEF2F7").Padding(5).AlignRight().Text(FormatMoney(lignes.Sum(x => x.Quinzaine))).Bold();
                        t.Cell().Background("#EEF2F7").Padding(5).AlignRight().Text(FormatMoney(lignes.Sum(x => x.Retenue))).Bold();
                        t.Cell().Background("#EEF2F7").Padding(5).AlignRight().Text(FormatMoney(lignes.Sum(x => x.Impot))).Bold();
                        t.Cell().Background("#EEF2F7").Padding(5).AlignRight().Text(FormatMoney(lignes.Sum(x => x.Retards))).Bold();
                        t.Cell().Background("#EEF2F7").Padding(5).AlignRight().Text(FormatMoney(lignes.Sum(x => x.Prets))).Bold();
                        t.Cell().Background("#EEF2F7").Padding(5).AlignRight().Text(FormatMoney(lignes.Sum(x => x.SoldeAPayer))).Bold().FontColor(branding.PrimaryHex);
                    });

                    col.Item().Text($"{lignes.Count(l => l.AvecBulletin)} bulletin(s) sur {lignes.Count} employe(s).")
                        .FontSize(8f).FontColor(Muted);
                });
            });
        });

        document.GeneratePdf(cheminFichier);
    }

    private static IDocument BuildFallbackMouvementsJourDocument(
        BrandingInfo b,
        IReadOnlyList<MouvementJourPdfLigne> lignes,
        DateTime jour,
        string? titreAgent,
        string heureLimiteTolerance)
    {
        var titre = string.IsNullOrWhiteSpace(titreAgent) ? "Mouvements du jour" : $"Mouvements — {titreAgent}";
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(10));
                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Text(titre).Bold();
                    col.Item().Text($"{jour:dd/MM/yyyy} — limite {heureLimiteTolerance}");
                    foreach (var l in lignes)
                    {
                        col.Item().Text(
                            $"{l.Matricule} | {l.NomComplet} | arr. {l.Arrivee} | sort. {l.Sortie} | {l.StatutRetard}");
                    }
                });
            });
        });
    }

    private static IDocument BuildFallbackRetardsJourDocument(
        BrandingInfo b,
        IReadOnlyList<RetardPdfLigne> lignes,
        DateTime jour,
        string heureLimiteTolerance,
        string totalUsd,
        string totalCdf)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(10));
                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Text("Retards du jour").Bold();
                    col.Item().Text($"{jour:dd/MM/yyyy} — limite {heureLimiteTolerance}");
                    foreach (var l in lignes)
                    {
                        col.Item().Text(
                            $"{l.NomComplet} | {l.DureeRetard} | {l.TauxHoraire} | {l.CoutRetard}");
                    }
                    col.Item().Text($"Total USD : {totalUsd} | Total CDF : {totalCdf}").SemiBold();
                });
            });
        });
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

    private sealed record BulletinLayoutData(
        BulletinPaie Bulletin,
        List<BulletinDetail> DetailsUtiles,
        string EmployeeName,
        decimal TotalBrut,
        decimal RetenuesLegales,
        decimal RetenuesDiverses,
        decimal RetenuesTotales,
        decimal SalaireMensuelUsd,
        decimal SalaireMensuelCdf,
        BulletinSynthesePaie Synthese);

    private static BulletinLayoutData PrepareBulletinLayout(BulletinPaie bulletin)
    {
        var details = bulletin.Details?.ToList() ?? new List<BulletinDetail>();
        var detailsUtiles = details
            .Where(d =>
                Math.Abs(d.Gain) > 0.0001m ||
                Math.Abs(d.Retenue) > 0.0001m ||
                (!string.IsNullOrWhiteSpace(d.Libelle) && (
                    d.Libelle.Contains("Absence", StringComparison.OrdinalIgnoreCase) ||
                    d.Libelle.Contains("Suspension", StringComparison.OrdinalIgnoreCase) ||
                    d.Libelle.Contains("Heures sup", StringComparison.OrdinalIgnoreCase))))
            .ToList();
        var employeeName = Clip($"{bulletin.Employe?.Nom} {bulletin.Employe?.Postnom} {bulletin.Employe?.Prenom}".Trim(), 90);
        var totalBrut = bulletin.TotalGainImposable + bulletin.TotalGainNonImposable;
        var retenuesLegales = bulletin.MontantIprNet + bulletin.CotisationCnssOuvrier;
        var totalGains = bulletin.TotalGainImposable + bulletin.TotalGainNonImposable;
        var retenuesDiverses = decimal.Round(
            Math.Max(0m, totalGains - bulletin.NetAPayer - retenuesLegales),
            2,
            MidpointRounding.AwayFromZero);
        var retenuesTotales = retenuesLegales + retenuesDiverses;
        var (salaireMensuelUsd, salaireMensuelCdf) = ResolveSalaireMensuelDepuisContrat(bulletin);
        var synthese = BulletinSyntheseHelper.Construire(bulletin);

        return new BulletinLayoutData(
            bulletin,
            detailsUtiles,
            employeeName,
            totalBrut,
            retenuesLegales,
            retenuesDiverses,
            retenuesTotales,
            salaireMensuelUsd,
            salaireMensuelCdf,
            synthese);
    }

    private static IDocument BuildBulletinDocument(BulletinPaie bulletin, BrandingInfo b)
    {
        var layout = PrepareBulletinLayout(bulletin);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.MarginVertical(10);
                page.MarginHorizontal(12);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(7.5f));
                page.Content().Element(content => ComposeBulletinBody(content, layout, b, ultraCompact: false));
            });
        });
    }

    private static IDocument BuildBulletinsDeuxParA4Document(List<BulletinLayoutData> layouts, BrandingInfo b)
    {
        return Document.Create(container =>
        {
            for (var i = 0; i < layouts.Count; i += 2)
            {
                var first = layouts[i];
                var second = i + 1 < layouts.Count ? layouts[i + 1] : null;

                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(8);
                    page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(7f));

                    page.Content().Column(col =>
                    {
                        col.Spacing(0);
                        col.Item().Height(148, Unit.Millimetre)
                            .Element(slot => ComposeBulletinBody(slot, first, b, ultraCompact: true));

                        col.Item().PaddingVertical(1).AlignCenter()
                            .Text("— — — — — — — — — — — — — — — — — — — —")
                            .FontSize(5.5f).FontColor(Muted);

                        if (second != null)
                        {
                            col.Item().Height(148, Unit.Millimetre)
                                .Element(slot => ComposeBulletinBody(slot, second, b, ultraCompact: true));
                        }
                    });
                });
            }
        });
    }

    private static void ComposeBulletinBody(IContainer container, BulletinLayoutData layout, BrandingInfo b, bool ultraCompact)
    {
        var bulletin = layout.Bulletin;
        var subtitle = $"Periode {bulletin.PeriodePaie?.Mois:D2}/{bulletin.PeriodePaie?.Annee}";
        var spacing = ultraCompact ? 4f : 6f;
        var netFontSize = ultraCompact ? 16f : 22f;
        var netLabelSize = ultraCompact ? 8f : 10f;
        var libelleMax = ultraCompact ? 55 : 80;

        container.Column(col =>
        {
            col.Spacing(spacing);

            col.Item().Element(header =>
            {
                if (ultraCompact)
                    ComposeHeaderBandCompact(header, b, "BULLETIN DE PAIE", subtitle);
                else
                    ComposeHeaderBandCompact(header, b, "BULLETIN DE PAIE", subtitle, medium: true);
            });

            col.Item().Border(1).BorderColor(BorderColor).Padding(ultraCompact ? 4 : 6).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                AddInfoCell(t, "Employe", layout.EmployeeName, ultraCompact);
                AddInfoCell(t, "Matricule", bulletin.Employe?.Matricule ?? "—", ultraCompact);
                AddInfoCell(t, "Departement", Clip(bulletin.Employe?.Departement?.NomDepartement, ultraCompact ? 35 : 50), ultraCompact);
                AddInfoCell(t, "Date emission", bulletin.DateGeneration.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), ultraCompact);
                AddInfoCell(t, "Numero bulletin", bulletin.NumeroBulletin ?? "—", ultraCompact);
                AddInfoCell(t, "CNSS salarie", bulletin.Employe?.NumCnss ?? "—", ultraCompact);
            });

            col.Item().Border(1).BorderColor("#DCE8FF").Background("#F5F9FF").Padding(ultraCompact ? 4 : 6).Row(r =>
            {
                r.RelativeItem().Text("Salaire mensuel").SemiBold().FontSize(ultraCompact ? 7f : 8f).FontColor("#1E3A5F");
                r.ConstantItem(ultraCompact ? 95 : 120).AlignRight().Text($"{FormatMoney(layout.SalaireMensuelUsd)} USD")
                    .SemiBold().FontSize(ultraCompact ? 8f : 10f).FontColor("#0D47A1");
                r.ConstantItem(ultraCompact ? 95 : 120).AlignRight().Text($"{FormatMoney(layout.SalaireMensuelCdf)} CDF")
                    .SemiBold().FontSize(ultraCompact ? 8f : 10f).FontColor("#0B8043");
            });

            col.Item().Text("Elements de paie").FontSize(ultraCompact ? 7.5f : 8.5f).SemiBold().FontColor(b.PrimaryHex);
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
                    HeaderCell(h.Cell(), "Rubrique", b.PrimaryHex, ultraCompact: ultraCompact);
                    HeaderCell(h.Cell(), "Quantite", b.PrimaryHex, true, ultraCompact);
                    HeaderCell(h.Cell(), "Montant", b.PrimaryHex, true, ultraCompact);
                });

                var i = 0;
                foreach (var d in layout.DetailsUtiles)
                {
                    var bg = i++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                    DataCell(t.Cell(), Clip(d.Libelle, libelleMax), bg, ultraCompact: ultraCompact);

                    var quantite = d.BaseCalcul > 0 && d.Taux > 0
                        ? $"{d.BaseCalcul:N2} x {d.Taux:N2}"
                        : d.Taux > 0
                            ? $"{d.Taux:N2}"
                            : "—";
                    DataCell(t.Cell(), quantite, bg, true, ultraCompact);

                    var montant = d.Gain > 0.0001m
                        ? $"+ {FormatMoney(d.Gain)}"
                        : d.Retenue > 0.0001m
                            ? $"- {FormatMoney(d.Retenue)}"
                            : d.BaseCalcul > 0.0001m
                                ? FormatMoney(d.BaseCalcul)
                                : "—";
                    DataCell(t.Cell(), montant, bg, true, ultraCompact);
                }
            });

            col.Item().Border(1).BorderColor(BorderColor).Padding(ultraCompact ? 4 : 6).Column(s =>
            {
                s.Spacing(ultraCompact ? 2 : 3);
                var syn = layout.Synthese;

                s.Item().Text("Synthese de paie").FontSize(ultraCompact ? 7.5f : 8.5f).SemiBold().FontColor(b.PrimaryHex);
                SummaryLine(s, "Montant total (brut)", syn.MontantTotal, ultraCompact);
                SummaryLine(s, "Quinzaine / acomptes", syn.Quinzaine, ultraCompact);
                SummaryLine(s, "Pret / avances", syn.Pret, ultraCompact);
                SummaryLine(s, "Retenue (CNSS)", syn.RetenueSociale, ultraCompact);
                SummaryLine(s, "Impot (IPR net)", syn.Impot, ultraCompact);
                if (syn.Sanctions > 0.0001m)
                    SummaryLine(s, "Sanctions / retards", syn.Sanctions, ultraCompact);
                if (syn.AutresRetenues > 0.0001m)
                    SummaryLine(s, "Autres retenues", syn.AutresRetenues, ultraCompact);

                s.Item().PaddingTop(ultraCompact ? 2 : 4).Text(Clip(syn.FormuleSolde, ultraCompact ? 90 : 120))
                    .FontSize(ultraCompact ? 5.5f : 6.5f).FontColor(Muted);

                s.Item().PaddingTop(ultraCompact ? 4 : 6).Background("#0D47A1").Padding(ultraCompact ? 6 : 8).Column(net =>
                {
                    var memeDevise = Math.Abs(bulletin.NetAPayer - bulletin.NetAPayerDeviseLocale) < 0.01m;
                    var suffix = memeDevise ? " CDF" : " USD";
                    net.Item().AlignCenter().Text("SOLDE A PAYER").Bold().FontSize(netLabelSize).FontColor("#E3F2FD");
                    net.Item().AlignCenter().Text($"{FormatMoney(syn.Solde)}{suffix}")
                        .Bold().FontSize(netFontSize).FontColor("#FFFFFF");
                });
            });
        });
    }

    private static void ComposeHeaderBandCompact(IContainer container, BrandingInfo b, string title, string subtitle, bool medium = false)
    {
        var logoSize = medium ? 40f : 32f;
        var logoHeight = medium ? 28f : 22f;
        var raisonSize = medium ? 10f : 8.5f;
        var titleSize = medium ? 10f : 8.5f;

        container.Column(col =>
        {
            col.Item().Background(b.PrimaryHex).Padding(medium ? 6 : 4).Row(row =>
            {
                if (!string.IsNullOrWhiteSpace(b.LogoPath))
                {
                    try
                    {
                        row.ConstantItem(logoSize).Height(logoHeight).Image(b.LogoPath).FitArea();
                    }
                    catch
                    {
                    }
                }

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(Clip(b.RaisonSociale ?? "Entreprise", medium ? 100 : 70)).FontSize(raisonSize).Bold().FontColor(HeaderOnPrimary);
                    if (!string.IsNullOrWhiteSpace(b.Adresse))
                        c.Item().Text(Clip(b.Adresse, medium ? 120 : 80)).FontSize(medium ? 6.5f : 6f).SemiBold().FontColor("#C7D5E8");
                });

                row.ConstantItem(medium ? 130 : 95).AlignRight().Column(c =>
                {
                    c.Item().Text(title).Bold().FontSize(titleSize).FontColor(HeaderOnPrimary);
                    c.Item().Text(subtitle).SemiBold().FontSize(medium ? 7.5f : 7f).FontColor("#C7D5E8");
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
                page.Size(PageSizes.A5);
                page.Margin(16);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8));

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

    private static void AddInfoCell(TableDescriptor table, string label, string value, bool compact = false)
    {
        var fontSize = compact ? 6.5f : 7.5f;
        table.Cell().Padding(compact ? 3 : 4).BorderBottom(0.5f).BorderColor(BorderColor)
            .Text($"{label} : {Clip(value, compact ? 70 : 110)}").FontSize(fontSize);
    }

    private static void HeaderCell(IContainer cell, string text, string backgroundHex, bool right = false, bool ultraCompact = false)
    {
        var fontSize = ultraCompact ? 6f : 7f;
        var padding = ultraCompact ? 3f : 4f;
        if (right)
        {
            cell.Background(backgroundHex)
                .Padding(padding)
                .BorderBottom(1)
                .BorderColor(backgroundHex)
                .AlignRight()
                .Text(text)
                .SemiBold()
                .FontColor(HeaderOnPrimary)
                .FontSize(fontSize);
        }
        else
        {
            cell.Background(backgroundHex)
                .Padding(padding)
                .BorderBottom(1)
                .BorderColor(backgroundHex)
                .Text(text)
                .SemiBold()
                .FontColor(HeaderOnPrimary)
                .FontSize(fontSize);
        }
    }

    private static void DataCell(IContainer cell, string text, string bgHex, bool right = false, bool ultraCompact = false)
    {
        var fontSize = ultraCompact ? 6f : 7f;
        var padding = ultraCompact ? 2f : 3f;
        if (right)
        {
            cell.Background(bgHex)
                .Padding(padding)
                .BorderBottom(0.5f)
                .BorderColor(BorderColor)
                .AlignRight()
                .Text(text)
                .FontSize(fontSize);
        }
        else
        {
            cell.Background(bgHex)
                .Padding(padding)
                .BorderBottom(0.5f)
                .BorderColor(BorderColor)
                .Text(text)
                .FontSize(fontSize);
        }
    }

    private static void SummaryLine(ColumnDescriptor column, string label, decimal value, bool compact = false)
    {
        var fontSize = compact ? 6.5f : 7.5f;
        column.Item().Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(fontSize);
            r.ConstantItem(compact ? 90 : 120).AlignRight().Text(FormatMoney(value)).FontSize(fontSize).SemiBold();
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
