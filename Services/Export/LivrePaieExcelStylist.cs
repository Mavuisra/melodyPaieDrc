using ClosedXML.Excel;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services.Export;

/// <summary>
/// Livre de paie Excel : colonnes standard et mise en forme professionnelle.
/// </summary>
public static class LivrePaieExcelStylist
{
    private const int ColCount = 9;
    private static readonly XLColor CouleurEntete = XLColor.FromHtml("#1E3A5F");
    private static readonly XLColor CouleurEnteteClair = XLColor.FromHtml("#E8EEF5");
    private static readonly XLColor CouleurLigneAlt = XLColor.FromHtml("#F7F9FC");
    private static readonly XLColor CouleurTotal = XLColor.FromHtml("#E3F2FD");

    public static void Generer(
        string cheminFichier,
        Entreprise? entreprise,
        PeriodePaie? periode,
        IReadOnlyList<LivrePaieLigneExcel> lignes)
    {
        using var workbook = new XLWorkbook();
        var titreFeuille = periode != null ? $"Paie {periode.Mois:D2}-{periode.Annee}" : "Livre de paie";
        if (titreFeuille.Length > 31) titreFeuille = titreFeuille[..31];
        var ws = workbook.Worksheets.Add(titreFeuille);

        var raison = entreprise?.RaisonSociale ?? "Entreprise";
        var periodeLibelle = periode != null ? $"{periode.Mois:D2} / {periode.Annee}" : "—";

        // Bandeau titre
        ws.Range(1, 1, 1, ColCount).Merge();
        ws.Cell(1, 1).Value = raison;
        AppliquerBandeau(ws.Range(1, 1, 1, ColCount), 14);

        ws.Range(2, 1, 2, ColCount).Merge();
        ws.Cell(2, 1).Value = $"Livre de paie — Période {periodeLibelle}";
        ws.Range(2, 1, 2, ColCount).Style.Font.FontSize = 11;
        ws.Range(2, 1, 2, ColCount).Style.Font.FontColor = XLColor.FromHtml("#546E7A");
        ws.Range(2, 1, 2, ColCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int headerRow = 4;
        var entetes = new[]
        {
            "N°", "Matricule", "Nom Employé", "Fonction", "Salaire Brut",
            "IPR", "Part CNSS Employé", "Part CNSS Employeur", "Net à payer"
        };

        for (int c = 0; c < entetes.Length; c++)
            ws.Cell(headerRow, c + 1).Value = entetes[c];

        var headerRange = ws.Range(headerRow, 1, headerRow, ColCount);
        headerRange.Style.Fill.BackgroundColor = CouleurEntete;
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.FontSize = 10;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Alignment.WrapText = true;
        ws.Row(headerRow).Height = 28;

        int row = headerRow + 1;
        int numero = 1;
        foreach (var ligne in lignes)
        {
            ws.Cell(row, 1).Value = numero++;
            ws.Cell(row, 2).Value = ligne.Matricule;
            ws.Cell(row, 3).Value = ligne.NomEmploye;
            ws.Cell(row, 4).Value = ligne.Fonction;
            ws.Cell(row, 5).Value = (double)ligne.SalaireBrut;
            ws.Cell(row, 6).Value = (double)ligne.Ipr;
            ws.Cell(row, 7).Value = (double)ligne.CnssEmploye;
            ws.Cell(row, 8).Value = (double)ligne.CnssEmployeur;
            ws.Cell(row, 9).Value = (double)ligne.NetAPayer;

            var dataRange = ws.Range(row, 1, row, ColCount);
            if (numero % 2 == 0)
                dataRange.Style.Fill.BackgroundColor = CouleurLigneAlt;

            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(row, 5, row, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Range(row, 5, row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            row++;
        }

        // Ligne totaux
        if (lignes.Count > 0)
        {
            ws.Cell(row, 1).Value = "";
            ws.Cell(row, 3).Value = "TOTAL";
            ws.Cell(row, 5).FormulaA1 = $"=SUM(E{headerRow + 1}:E{row - 1})";
            ws.Cell(row, 6).FormulaA1 = $"=SUM(F{headerRow + 1}:F{row - 1})";
            ws.Cell(row, 7).FormulaA1 = $"=SUM(G{headerRow + 1}:G{row - 1})";
            ws.Cell(row, 8).FormulaA1 = $"=SUM(H{headerRow + 1}:H{row - 1})";
            ws.Cell(row, 9).FormulaA1 = $"=SUM(I{headerRow + 1}:I{row - 1})";

            var totalRange = ws.Range(row, 1, row, ColCount);
            totalRange.Style.Fill.BackgroundColor = CouleurTotal;
            totalRange.Style.Font.Bold = true;
            totalRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            totalRange.Style.Border.TopBorderColor = CouleurEntete;
            ws.Range(row, 5, row, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Range(row, 5, row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            row++;
        }

        // Bordures tableau
        var tableRange = ws.Range(headerRow, 1, Math.Max(headerRow, row - 1), ColCount);
        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        tableRange.Style.Border.OutsideBorderColor = CouleurEntete;
        tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#CFD8DC");

        ws.SheetView.FreezeRows(headerRow);
        ws.Columns(1, 4).AdjustToContents();
        ws.Column(1).Width = Math.Max(ws.Column(1).Width, 5);
        ws.Column(2).Width = Math.Max(ws.Column(2).Width, 12);
        ws.Column(3).Width = Math.Max(ws.Column(3).Width, 28);
        ws.Column(4).Width = Math.Max(ws.Column(4).Width, 18);
        for (int c = 5; c <= ColCount; c++)
            ws.Column(c).Width = 16;

        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.SetRowsToRepeatAtTop(headerRow, headerRow);

        workbook.SaveAs(cheminFichier);
    }

    private static void AppliquerBandeau(IXLRange range, double fontSize)
    {
        range.Style.Fill.BackgroundColor = CouleurEntete;
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = fontSize;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Worksheet.Row(range.FirstRow().RowNumber()).Height = 32;
    }
}

public sealed class LivrePaieLigneExcel
{
    public string Matricule { get; init; } = "";
    public string NomEmploye { get; init; } = "";
    public string Fonction { get; init; } = "";
    public decimal SalaireBrut { get; init; }
    public decimal Ipr { get; init; }
    public decimal CnssEmploye { get; init; }
    public decimal CnssEmployeur { get; init; }
    public decimal NetAPayer { get; init; }
}
