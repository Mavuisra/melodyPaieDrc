using System.Linq;
using ClosedXML.Excel;
using MelodyPaieRDC.Data;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Export Excel du rapport de paie (synthèse par bulletin pour une période).
/// </summary>
public class RapportPaieExportService
{
    private readonly PaieDbContext _db;

    public RapportPaieExportService(PaieDbContext db)
    {
        _db = db;
    }

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
        var ws = workbook.Worksheets.Add($"Rapport {mois:D2}-{annee}");

        ws.Cell(1, 1).Value = "Rapport de paie";
        ws.Range(1, 1, 1, 9).Merge().Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"Période : {mois:D2} / {annee}";
        ws.Range(2, 1, 2, 9).Merge();

        var row = 4;
        ws.Cell(row, 1).Value = "N° bulletin";
        ws.Cell(row, 2).Value = "Matricule";
        ws.Cell(row, 3).Value = "Nom complet";
        ws.Cell(row, 4).Value = "Département";
        ws.Cell(row, 5).Value = "Salaire brut";
        ws.Cell(row, 6).Value = "IPR net";
        ws.Cell(row, 7).Value = "CNSS ouvrier";
        ws.Cell(row, 8).Value = "Net à payer";
        ws.Cell(row, 9).Value = "Date génération";
        ws.Range(row, 1, row, 9).Style.Font.Bold = true;
        row++;

        var firstDataRow = row;
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
            ws.Cell(row, 9).Value = b.DateGeneration;
            ws.Cell(row, 9).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            row++;
        }

        if (row > firstDataRow)
        {
            var lastData = row - 1;
            ws.Cell(row, 1).Value = "TOTAL";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 5).FormulaA1 = $"=SUM(E{firstDataRow}:E{lastData})";
            ws.Cell(row, 6).FormulaA1 = $"=SUM(F{firstDataRow}:F{lastData})";
            ws.Cell(row, 7).FormulaA1 = $"=SUM(G{firstDataRow}:G{lastData})";
            ws.Cell(row, 8).FormulaA1 = $"=SUM(H{firstDataRow}:H{lastData})";
            ws.Range(row, 5, row, 8).Style.Font.Bold = true;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(cheminFichier);
    }
}
