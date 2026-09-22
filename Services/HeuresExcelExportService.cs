using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>Export Excel des heures avec affichage exact h:mm.</summary>
public sealed class HeuresExcelExportService
{
    public void ExporterTotauxPeriode(
        IReadOnlyList<(string Matricule, string NomComplet, string? Departement, decimal TotalHeures, decimal JoursEq)> lignes,
        int mois,
        int annee,
        decimal totalHeures,
        decimal totalJoursEq,
        string cheminFichier)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add($"Heures {mois:D2}-{annee}");

        ws.Cell(1, 1).Value = $"STE LTSERVICES — Heures prestées {mois:D2}/{annee}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Range(1, 1, 1, 6).Merge();

        ws.Cell(2, 1).Value = $"Généré le {DateTime.Now:dd/MM/yyyy HH:mm} — heures affichées au format exact h:mm";
        ws.Range(2, 1, 2, 6).Merge();

        var headers = new[] { "N°", "Matricule", "Nom complet", "Département", "Heures (h:mm)", "Jours équivalents" };
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(4, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var row = 5;
        var i = 1;
        foreach (var l in lignes.OrderBy(x => x.NomComplet, StringComparer.OrdinalIgnoreCase))
        {
            ws.Cell(row, 1).Value = i++;
            ws.Cell(row, 2).Value = l.Matricule;
            ws.Cell(row, 3).Value = l.NomComplet;
            ws.Cell(row, 4).Value = l.Departement ?? "";
            ws.Cell(row, 5).Value = HeuresFormatHelper.VersHhMm(l.TotalHeures);
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 6).Value = Math.Round(l.JoursEq, 2);
            ws.Cell(row, 6).Style.NumberFormat.Format = "0.00";
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 5).Value = HeuresFormatHelper.VersHhMm(totalHeures);
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 6).Value = Math.Round(totalJoursEq, 2);
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Cell(row, 6).Style.NumberFormat.Format = "0.00";

        ws.SheetView.FreezeRows(4);
        ws.Columns().AdjustToContents();
        wb.SaveAs(cheminFichier);
    }

    public void ExporterDetailEmploye(
        string matricule,
        string nomComplet,
        string? departement,
        int mois,
        int annee,
        IReadOnlyList<SuiviJournalierPdfLigne> lignes,
        string cheminFichier)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Détail heures");

        ws.Cell(1, 1).Value = $"Heures — {nomComplet} ({matricule}) — {mois:D2}/{annee}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Range(1, 1, 1, 6).Merge();
        ws.Cell(2, 1).Value = $"Département : {departement ?? "—"} — format exact h:mm";
        ws.Range(2, 1, 2, 6).Merge();

        var headers = new[] { "Date", "Jour", "Présent", "Mode", "Heures (h:mm)", "Type de jour" };
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(4, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
        }

        var row = 5;
        decimal total = 0;
        foreach (var l in lignes)
        {
            ws.Cell(row, 1).Value = l.DateAffichage;
            ws.Cell(row, 2).Value = l.JourSemaine;
            ws.Cell(row, 3).Value = l.JourCode;
            ws.Cell(row, 4).Value = l.ModeCalcul;
            ws.Cell(row, 5).Value = HeuresFormatHelper.VersHhMm(l.HeuresPrestees);
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 6).Value = l.TypeJour;
            total += l.HeuresPrestees;
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 5).Value = HeuresFormatHelper.VersHhMm(total);
        ws.Cell(row, 5).Style.Font.Bold = true;

        ws.SheetView.FreezeRows(4);
        ws.Columns().AdjustToContents();
        wb.SaveAs(cheminFichier);
    }
}
