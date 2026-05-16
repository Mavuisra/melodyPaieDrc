using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Agrège les bulletins par période et produit les déclarations CNSS / IPR (export CSV).
/// </summary>
public class DeclarationsService
{
    private readonly PaieDbContext _db;
    private readonly CotisationsSocialesService _cotisationsService;

    public DeclarationsService(PaieDbContext db)
    {
        _db = db;
        _cotisationsService = new CotisationsSocialesService(db);
    }

    /// <summary>
    /// Résumé des déclarations pour une période (totaux issus des bulletins).
    /// </summary>
    public DeclarationResume GetResumePourPeriode(int periodePaieId)
    {
        var bulletins = _db.BulletinsPaie
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Employe)
            .ThenInclude(e => e!.Departement)
            .Include(b => b.PeriodePaie)
            .Where(b => b.PeriodePaieId == periodePaieId)
            .ToList();

        decimal totalIpr = 0m, totalCnssOuvrier = 0m, masseSalariale = 0m;
        foreach (var b in bulletins)
        {
            totalIpr += b.MontantIprNet;
            totalCnssOuvrier += b.CotisationCnssOuvrier;
            masseSalariale += b.TotalGainImposable + b.TotalGainNonImposable;
        }

        return new DeclarationResume
        {
            PeriodePaieId = periodePaieId,
            Mois = bulletins.FirstOrDefault()?.PeriodePaie?.Mois ?? 0,
            Annee = bulletins.FirstOrDefault()?.PeriodePaie?.Annee ?? 0,
            NbEmployes = bulletins.Count,
            TotalIprNet = totalIpr,
            TotalCnssOuvrier = totalCnssOuvrier,
            MasseSalariale = masseSalariale,
            Bulletins = bulletins
        };
    }

    /// <summary>
    /// Exporte la déclaration CNSS pour la période en CSV (matricule, nom, salaire brut, CNSS ouvrier, CNSS patronal).
    /// </summary>
    public string ExporterDeclarationCnssCsv(int periodePaieId)
    {
        var resume = GetResumePourPeriode(periodePaieId);
        var sb = new StringBuilder();
        var sep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
        sb.AppendLine($"Matricule{sep}Nom complet{sep}Salaire brut{sep}CNSS part ouvrière{sep}CNSS part patronale");

        foreach (var b in resume.Bulletins.OrderBy(x => x.Employe?.Matricule))
        {
            var salaireBrut = b.TotalGainImposable + b.TotalGainNonImposable;
            var baseCnss = GetBaseCnss(b);
            var cotisations = _cotisationsService.Calculer(baseCnss);
            var nomComplet = $"{b.Employe?.Nom} {b.Employe?.Postnom} {b.Employe?.Prenom}".Trim();
            sb.AppendLine($"{b.Employe?.Matricule}{sep}{nomComplet}{sep}{salaireBrut:N2}{sep}{b.CotisationCnssOuvrier:N2}{sep}{cotisations.CnssPatronal:N2}");
        }

        sb.AppendLine();
        sb.AppendLine($"Total employés{sep}{resume.NbEmployes}");
        sb.AppendLine($"Masse salariale totale{sep}{resume.MasseSalariale:N2}");
        sb.AppendLine($"CNSS ouvrier total{sep}{resume.TotalCnssOuvrier:N2}");
        return sb.ToString();
    }

    /// <summary>
    /// Exporte la déclaration IPR pour la période en CSV (matricule, nom, base imposable, IPR net).
    /// </summary>
    public string ExporterDeclarationIprCsv(int periodePaieId)
    {
        var resume = GetResumePourPeriode(periodePaieId);
        var sb = new StringBuilder();
        var sep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
        sb.AppendLine($"Matricule{sep}Nom complet{sep}Base imposable{sep}IPR net");

        foreach (var b in resume.Bulletins.OrderBy(x => x.Employe?.Matricule))
        {
            var nomComplet = $"{b.Employe?.Nom} {b.Employe?.Postnom} {b.Employe?.Prenom}".Trim();
            sb.AppendLine($"{b.Employe?.Matricule}{sep}{nomComplet}{sep}{b.BaseIpr:N2}{sep}{b.MontantIprNet:N2}");
        }

        sb.AppendLine();
        sb.AppendLine($"Total employés{sep}{resume.NbEmployes}");
        sb.AppendLine($"Total IPR retenu{sep}{resume.TotalIprNet:N2}");
        return sb.ToString();
    }

    /// <summary>
    /// Exporte la déclaration CNSS pour la période en fichier Excel (.xlsx).
    /// </summary>
    public void ExporterDeclarationCnssExcel(int periodePaieId, string cheminFichier)
    {
        var resume = GetResumePourPeriode(periodePaieId);
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add($"Declaration CNSS {resume.Mois:D2}-{resume.Annee}");

        ws.Cell(1, 1).Value = "Déclaration CNSS";
        ws.Range(1, 1, 1, 5).Merge().Style.Font.Bold = true;
        ws.Cell(2, 1).Value = $"Période : {resume.Mois:D2} / {resume.Annee}";
        ws.Range(2, 1, 2, 5).Merge();

        int row = 4;
        ws.Cell(row, 1).Value = "Matricule";
        ws.Cell(row, 2).Value = "Nom complet";
        ws.Cell(row, 3).Value = "Salaire brut";
        ws.Cell(row, 4).Value = "CNSS part ouvrière";
        ws.Cell(row, 5).Value = "CNSS part patronale";
        ws.Range(row, 1, row, 5).Style.Font.Bold = true;
        row++;

        foreach (var b in resume.Bulletins.OrderBy(x => x.Employe?.Matricule))
        {
            var salaireBrut = b.TotalGainImposable + b.TotalGainNonImposable;
            var baseCnss = GetBaseCnss(b);
            var cotisations = _cotisationsService.Calculer(baseCnss);
            var nomComplet = $"{b.Employe?.Nom} {b.Employe?.Postnom} {b.Employe?.Prenom}".Trim();
            ws.Cell(row, 1).Value = b.Employe?.Matricule ?? "";
            ws.Cell(row, 2).Value = nomComplet;
            ws.Cell(row, 3).Value = (double)salaireBrut;
            ws.Cell(row, 4).Value = (double)b.CotisationCnssOuvrier;
            ws.Cell(row, 5).Value = (double)cotisations.CnssPatronal;
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 3).FormulaA1 = $"=SUM(C5:C{row - 1})";
        ws.Cell(row, 4).FormulaA1 = $"=SUM(D5:D{row - 1})";
        ws.Cell(row, 5).FormulaA1 = $"=SUM(E5:E{row - 1})";
        ws.Range(row, 3, row, 5).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Total employés";
        ws.Cell(row, 2).Value = resume.NbEmployes;
        row++;
        ws.Cell(row, 1).Value = "Masse salariale totale";
        ws.Cell(row, 2).Value = (double)resume.MasseSalariale;

        ws.Columns().AdjustToContents();
        workbook.SaveAs(cheminFichier);
    }

    /// <summary>
    /// Exporte la déclaration IPR pour la période en fichier Excel (.xlsx).
    /// </summary>
    public void ExporterDeclarationIprExcel(int periodePaieId, string cheminFichier)
    {
        var resume = GetResumePourPeriode(periodePaieId);
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add($"Declaration IPR {resume.Mois:D2}-{resume.Annee}");

        ws.Cell(1, 1).Value = "Déclaration IPR";
        ws.Range(1, 1, 1, 4).Merge().Style.Font.Bold = true;
        ws.Cell(2, 1).Value = $"Période : {resume.Mois:D2} / {resume.Annee}";
        ws.Range(2, 1, 2, 4).Merge();

        int row = 4;
        ws.Cell(row, 1).Value = "Matricule";
        ws.Cell(row, 2).Value = "Nom complet";
        ws.Cell(row, 3).Value = "Base imposable";
        ws.Cell(row, 4).Value = "IPR net";
        ws.Range(row, 1, row, 4).Style.Font.Bold = true;
        row++;

        foreach (var b in resume.Bulletins.OrderBy(x => x.Employe?.Matricule))
        {
            var nomComplet = $"{b.Employe?.Nom} {b.Employe?.Postnom} {b.Employe?.Prenom}".Trim();
            ws.Cell(row, 1).Value = b.Employe?.Matricule ?? "";
            ws.Cell(row, 2).Value = nomComplet;
            ws.Cell(row, 3).Value = (double)b.BaseIpr;
            ws.Cell(row, 4).Value = (double)b.MontantIprNet;
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 3).FormulaA1 = $"=SUM(C5:C{row - 1})";
        ws.Cell(row, 4).FormulaA1 = $"=SUM(D5:D{row - 1})";
        ws.Range(row, 3, row, 4).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Total employés";
        ws.Cell(row, 2).Value = resume.NbEmployes;
        row++;
        ws.Cell(row, 1).Value = "Total IPR retenu";
        ws.Cell(row, 2).Value = (double)resume.TotalIprNet;

        ws.Columns().AdjustToContents();
        workbook.SaveAs(cheminFichier);
    }

    private static decimal GetBaseCnss(BulletinPaie bulletin)
    {
        if (bulletin.Details != null)
        {
            var ligneCnss = bulletin.Details
                .FirstOrDefault(d => string.Equals(d.Libelle, "CNSS (part ouvrière)", StringComparison.OrdinalIgnoreCase));
            if (ligneCnss != null && ligneCnss.BaseCalcul > 0)
                return ligneCnss.BaseCalcul;
        }

        return bulletin.TotalGainImposable + bulletin.TotalGainNonImposable;
    }
}

/// <summary>
/// Résumé des déclarations pour une période.
/// </summary>
public class DeclarationResume
{
    public int PeriodePaieId { get; set; }
    public int Mois { get; set; }
    public int Annee { get; set; }
    public int NbEmployes { get; set; }
    public decimal TotalIprNet { get; set; }
    public decimal TotalCnssOuvrier { get; set; }
    public decimal MasseSalariale { get; set; }
    public List<BulletinPaie> Bulletins { get; set; } = new();
}
