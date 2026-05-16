using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services.Export;

public static class ExportTabulaireWriter
{
    public static string GenererCsv(
        ProfilExportConfig profil,
        IEnumerable<IReadOnlyList<string>> lignesDonnees,
        IEnumerable<IReadOnlyList<string>>? lignesEntete = null)
    {
        var sb = new StringBuilder();
        var sep = profil.Separateur;
        if (profil.InclureBomUtf8)
            sb.Append('\uFEFF');

        if (lignesEntete != null)
        {
            foreach (var ligne in lignesEntete)
                sb.AppendLine(string.Join(sep, ligne.Select(c => ExportDonneesPaieResolver.EchapperCsv(c, sep))));
        }

        foreach (var ligne in lignesDonnees)
            sb.AppendLine(string.Join(sep, ligne.Select(c => ExportDonneesPaieResolver.EchapperCsv(c, sep))));

        return sb.ToString();
    }

    public static void GenererExcel(
        ProfilExportConfig profil,
        string cheminFichier,
        string titreFeuille,
        IEnumerable<IReadOnlyList<string>> lignesDonnees,
        IEnumerable<IReadOnlyList<string>>? lignesEntete = null)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(titreFeuille.Length > 31 ? titreFeuille[..31] : titreFeuille);
        int row = 1;

        if (lignesEntete != null)
        {
            foreach (var ligne in lignesEntete)
            {
                for (int col = 0; col < ligne.Count; col++)
                    ws.Cell(row, col + 1).Value = ligne[col];
                row++;
            }
            row++;
        }

        var colonnes = profil.Colonnes.Where(c => c.Actif).OrderBy(c => c.Ordre).ToList();
        for (int col = 0; col < colonnes.Count; col++)
        {
            ws.Cell(row, col + 1).Value = colonnes[col].Libelle;
            ws.Cell(row, col + 1).Style.Font.Bold = true;
        }
        row++;

        foreach (var ligne in lignesDonnees)
        {
            for (int col = 0; col < ligne.Count; col++)
            {
                var texte = ligne[col];
                if (decimal.TryParse(texte, NumberStyles.Any, CultureInfo.GetCultureInfo("fr-FR"), out var nombre))
                    ws.Cell(row, col + 1).Value = (double)nombre;
                else
                    ws.Cell(row, col + 1).Value = texte;
            }
            row++;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(cheminFichier);
    }

    public static List<string> ConstruireLigne(ExportDonneesPaieContext ctx, ProfilExportConfig profil)
    {
        var colonnes = profil.Colonnes.Where(c => c.Actif).OrderBy(c => c.Ordre).ToList();
        var ligne = new List<string>(colonnes.Count);
        foreach (var col in colonnes)
            ligne.Add(ExportDonneesPaieResolver.Resoudre(ctx, col.SourceDonnee, col));
        return ligne;
    }

    public static List<List<string>> ConstruireLignesEnteteEmployeur(
        Entreprise? entreprise,
        PeriodePaie? periode,
        ProfilExportConfig profil)
    {
        var result = new List<List<string>>();
        if (!profil.InclureLignesEnteteEmployeur) return result;

        foreach (var h in profil.LignesEnteteEmployeur)
        {
            var valeur = ExportDonneesPaieResolver.ResoudreEntete(entreprise, periode, h);
            result.Add(new List<string> { h.Libelle, valeur });
        }
        return result;
    }
}
