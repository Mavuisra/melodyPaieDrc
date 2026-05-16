using System.Linq;
using ClosedXML.Excel;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Export du livre de paie (PDF via ExportPdfService, Excel ici).
/// </summary>
public class LivrePaieExportService
{
    private readonly PaieDbContext _db;

    public LivrePaieExportService(PaieDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Exporte le livre de paie pour la période en fichier Excel (.xlsx).
    /// </summary>
    public void ExporterExcel(int periodePaieId, string cheminFichier)
    {
        var bulletins = _db.BulletinsPaie
            .Include(b => b.Employe)
            .ThenInclude(e => e!.Departement)
            .Include(b => b.PeriodePaie)
            .Where(b => b.PeriodePaieId == periodePaieId)
            .OrderBy(b => b.Employe != null ? b.Employe.Matricule : "")
            .ToList();

        var periode = bulletins.FirstOrDefault()?.PeriodePaie;
        var mois = periode?.Mois ?? 0;
        var annee = periode?.Annee ?? 0;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add($"Livre paie {mois:D2}-{annee}");

        ws.Cell(1, 1).Value = "Livre de paie";
        ws.Range(1, 1, 1, 9).Merge().Style.Font.Bold = true;
        ws.Cell(2, 1).Value = $"Période : {mois:D2} / {annee}";
        ws.Range(2, 1, 2, 9).Merge();

        int row = 4;
        ws.Cell(row, 1).Value = "N° bulletin";
        ws.Cell(row, 2).Value = "Matricule";
        ws.Cell(row, 3).Value = "Nom";
        ws.Cell(row, 4).Value = "Département";
        ws.Cell(row, 5).Value = "Salaire brut";
        ws.Cell(row, 6).Value = "IPR net";
        ws.Cell(row, 7).Value = "CNSS ouvrier";
        ws.Cell(row, 8).Value = "Net à payer";
        ws.Range(row, 1, row, 8).Style.Font.Bold = true;
        row++;

        foreach (var b in bulletins)
        {
            var brut = b.TotalGainImposable + b.TotalGainNonImposable;
            var nom = $"{b.Employe?.Nom} {b.Employe?.Postnom} {b.Employe?.Prenom}".Trim();
            ws.Cell(row, 1).Value = b.NumeroBulletin ?? "";
            ws.Cell(row, 2).Value = b.Employe?.Matricule ?? "";
            ws.Cell(row, 3).Value = nom;
            ws.Cell(row, 4).Value = b.Employe?.Departement?.NomDepartement ?? "";
            ws.Cell(row, 5).Value = brut;
            ws.Cell(row, 6).Value = b.MontantIprNet;
            ws.Cell(row, 7).Value = b.CotisationCnssOuvrier;
            ws.Cell(row, 8).Value = b.NetAPayer;
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 5).FormulaA1 = $"=SUM(E5:E{row - 1})";
        ws.Cell(row, 6).FormulaA1 = $"=SUM(F5:F{row - 1})";
        ws.Cell(row, 7).FormulaA1 = $"=SUM(G5:G{row - 1})";
        ws.Cell(row, 8).FormulaA1 = $"=SUM(H5:H{row - 1})";
        ws.Range(row, 5, row, 8).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();

        workbook.SaveAs(cheminFichier);
    }
}
