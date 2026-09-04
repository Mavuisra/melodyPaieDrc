using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Impression directe du bulletin de paie vers la imprimante (PrintDialog).
/// </summary>
public static class ImpressionBulletinService
{
    /// <summary>
    /// Affiche la boîte de dialogue d'impression et imprime le bulletin si l'utilisateur valide.
    /// Charge le bulletin avec Employe, PeriodePaie, Details si nécessaire.
    /// </summary>
    public static bool Imprimer(BulletinPaie bulletin, Window? owner = null)
    {
        if (bulletin == null) return false;

        using var db = new PaieDbContext();
        var b = db.BulletinsPaie
            .Include(x => x.Employe)
            .ThenInclude(e => e!.Departement)
            .Include(x => x.PeriodePaie)
            .Include(x => x.Details)
            .FirstOrDefault(x => x.Id == bulletin.Id);
        if (b == null) return false;

        var doc = ConstruireFlowDocument(b);
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return false;

        // Format A5 (148 × 210 mm) pour l'impression bulletin.
        const double a5WidthMm = 148.0;
        const double a5HeightMm = 210.0;
        printDialog.PrintTicket.PageMediaSize = new System.Printing.PageMediaSize(
            a5WidthMm / 25.4 * 96.0,
            a5HeightMm / 25.4 * 96.0);

        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        printDialog.PrintDocument(paginator, $"Bulletin de paie {b.NumeroBulletin ?? b.Id.ToString()}");
        return true;
    }

    private static FlowDocument ConstruireFlowDocument(BulletinPaie b)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(28),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 9,
            PageWidth = 148.0 / 25.4 * 96.0,
            PageHeight = 210.0 / 25.4 * 96.0
        };

        var nomComplet = $"{b.Employe?.Nom} {b.Employe?.Postnom} {b.Employe?.Prenom}".Trim();
        var periode = b.PeriodePaie != null ? $"{b.PeriodePaie.Mois:D2} / {b.PeriodePaie.Annee}" : "";

        // Titre
        var pTitre = new Paragraph(new Run("BULLETIN DE PAIE")) { FontSize = 18, FontWeight = FontWeights.Bold };
        doc.Blocks.Add(pTitre);
        if (!string.IsNullOrEmpty(b.NumeroBulletin))
            doc.Blocks.Add(new Paragraph(new Run($"N° {b.NumeroBulletin}")) { FontSize = 12 });
        doc.Blocks.Add(new Paragraph());

        // Infos employé et période
        var pInfos = new Paragraph();
        pInfos.Inlines.Add(new Run($"Employé : {nomComplet}\n"));
        pInfos.Inlines.Add(new Run($"Matricule : {b.Employe?.Matricule ?? ""}\n"));
        pInfos.Inlines.Add(new Run($"Département : {b.Employe?.Departement?.NomDepartement ?? ""}\n"));
        pInfos.Inlines.Add(new Run($"Période : {periode}  |  Généré le : {b.DateGeneration:dd/MM/yyyy}"));
        doc.Blocks.Add(pInfos);
        doc.Blocks.Add(new Paragraph());

        // Tableau des détails
        var table = new Table { BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), CellSpacing = 0 };
        table.Columns.Add(new TableColumn());
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });

        var headerRow = new TableRow { Background = Brushes.LightGray };
        headerRow.Cells.Add(Cell("Libellé"));
        headerRow.Cells.Add(Cell("Base"));
        headerRow.Cells.Add(Cell("Taux"));
        headerRow.Cells.Add(Cell("Gain"));
        headerRow.Cells.Add(Cell("Retenue"));
        table.RowGroups.Add(new TableRowGroup());
        table.RowGroups[0].Rows.Add(headerRow);

        foreach (var d in (b.Details ?? Enumerable.Empty<BulletinDetail>()).OrderBy(x => x.Id))
        {
            var row = new TableRow();
            row.Cells.Add(Cell(d.Libelle));
            row.Cells.Add(Cell(d.BaseCalcul.ToString("N2")));
            row.Cells.Add(Cell(d.Taux.ToString("N2")));
            row.Cells.Add(Cell(d.Gain.ToString("N2")));
            row.Cells.Add(Cell(d.Retenue.ToString("N2")));
            table.RowGroups[0].Rows.Add(row);
        }

        doc.Blocks.Add(table);
        doc.Blocks.Add(new Paragraph());

        var synthese = BulletinSyntheseHelper.Construire(b);
        var pSyn = new Paragraph(new Run("SYNTHÈSE DE PAIE")) { FontSize = 12, FontWeight = FontWeights.Bold };
        doc.Blocks.Add(pSyn);
        AjouterLigneSynthese(doc, "Montant total (brut)", synthese.MontantTotal, true);
        AjouterLigneSynthese(doc, "Quinzaine / acomptes", synthese.Quinzaine);
        AjouterLigneSynthese(doc, "Prêt / avances", synthese.Pret);
        AjouterLigneSynthese(doc, "Retenue (CNSS)", synthese.RetenueSociale);
        AjouterLigneSynthese(doc, "Impôt (IPR net)", synthese.Impot);
        if (synthese.Sanctions > 0) AjouterLigneSynthese(doc, "Sanctions / retards", synthese.Sanctions);
        if (synthese.AutresRetenues > 0) AjouterLigneSynthese(doc, "Autres retenues", synthese.AutresRetenues);
        doc.Blocks.Add(new Paragraph(new Run(synthese.FormuleSolde)) { FontSize = 8, Foreground = Brushes.Gray });
        doc.Blocks.Add(new Paragraph());

        // Solde à payer
        var pNet = new Paragraph();
        pNet.Inlines.Add(new Run($"Solde à payer : ") { FontWeight = FontWeights.Bold });
        pNet.Inlines.Add(new Run(synthese.Solde.ToString("N2")) { FontWeight = FontWeights.Bold, FontSize = 14 });
        doc.Blocks.Add(pNet);
        if (Math.Abs(b.NetAPayer - b.NetAPayerDeviseLocale) > 0.01m)
            doc.Blocks.Add(new Paragraph(new Run($"Net en devise locale : {b.NetAPayerDeviseLocale:N2}")));
        doc.Blocks.Add(new Paragraph());
        doc.Blocks.Add(new Paragraph(new Run("Signature employeur ___________________    Signature employé ___________________")) { FontSize = 9 });

        return doc;
    }

    private static void AjouterLigneSynthese(FlowDocument doc, string libelle, decimal montant, bool bold = false)
    {
        var p = new Paragraph();
        p.Inlines.Add(new Run($"{libelle} : ") { FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal });
        p.Inlines.Add(new Run(montant.ToString("N2")) { FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal });
        doc.Blocks.Add(p);
    }

    private static TableCell Cell(string text)
    {
        var cell = new TableCell(new Paragraph(new Run(text))) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0.5), Padding = new Thickness(4, 2, 4, 2) };
        return cell;
    }
}
