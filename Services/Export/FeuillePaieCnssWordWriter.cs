using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services.Export;

/// <summary>Génère le document Word « Annexe : Détails de la feuille de paie » (modèle CNSS).</summary>
public static class FeuillePaieCnssWordWriter
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private const int ColCount = 12;

    private static readonly string[] Entetes =
    [
        "Matricule", "Prénom", "Post Nom", "Nom", "Salaire de Base",
        "Indemnités de vie chère", "Primes", "Gratifications",
        "Allocations de congés", "Avantages en nature", "Commissions", "Autres * Indemnités"
    ];

    public static void Generer(
        string cheminFichier,
        Entreprise? entreprise,
        PeriodePaie? periode,
        IReadOnlyList<ExportDonneesPaieContext> lignes)
    {
        using var doc = WordprocessingDocument.Create(cheminFichier, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        var periodeLibelle = periode != null ? $"{periode.Mois:D2}/{periode.Annee}" : "…";
        var affiliation = entreprise?.NumeroAffiliationCnss ?? entreprise?.NumCnss ?? "";
        var raison = entreprise?.RaisonSociale ?? "";

        body.Append(CreerParagraphe($"Annexe : Détails de la feuille de paie pour la période {periodeLibelle}", gras: true));
        body.Append(CreerParagraphe(""));
        body.Append(CreerParagraphe("Informations Employeur", gras: true));
        body.Append(CreerParagraphe($"Numéro Affiliation : {affiliation}"));
        body.Append(CreerParagraphe($"Raison Sociale : {raison}"));
        body.Append(CreerParagraphe(""));
        body.Append(CreerParagraphe("Informations Travailleur", gras: true));

        var table = new Table();
        table.AppendChild(CreerProprietesTableau());
        table.Append(CreerLigneEntete());

        foreach (var ctx in lignes)
        {
            var e = ctx.Employe;
            var cols = FeuillePaieCnssColonnesMapper.Repartir(ctx.Bulletin, ctx.Contrat);
            table.Append(CreerLigneDonnees(
                e?.Matricule ?? "",
                e?.Prenom ?? "",
                e?.Postnom ?? "",
                e?.Nom ?? "",
                cols.SalaireBase,
                cols.IndemniteVieChere,
                cols.Primes,
                cols.Gratifications,
                cols.AllocationsConges,
                cols.AvantagesNature,
                cols.Commissions,
                cols.AutresIndemnites));
        }

        body.Append(table);
        body.Append(CreerParagraphe(""));
        body.Append(CreerParagraphe("* : Sommer les autres indemnités"));
        body.Append(CreerParagraphe("Enregistrer le document en PDF avant de charger sur la plateforme", italique: true));

        body.Append(CreerSectionPaysage());
        mainPart.Document.Save();
    }

    private static TableProperties CreerProprietesTableau() =>
        new(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }));

    private static TableRow CreerLigneEntete()
    {
        var row = new TableRow();
        foreach (var libelle in Entetes)
            row.Append(CreerCellule(libelle, gras: true, centrer: true));
        return row;
    }

    private static TableRow CreerLigneDonnees(
        string matricule, string prenom, string postnom, string nom,
        decimal salaireBase, decimal vieChere, decimal primes, decimal gratifications,
        decimal conges, decimal avantages, decimal commissions, decimal autres)
    {
        var row = new TableRow();
        row.Append(CreerCellule(matricule));
        row.Append(CreerCellule(prenom));
        row.Append(CreerCellule(postnom));
        row.Append(CreerCellule(nom));
        row.Append(CreerCelluleMontant(salaireBase));
        row.Append(CreerCelluleMontant(vieChere));
        row.Append(CreerCelluleMontant(primes));
        row.Append(CreerCelluleMontant(gratifications));
        row.Append(CreerCelluleMontant(conges));
        row.Append(CreerCelluleMontant(avantages));
        row.Append(CreerCelluleMontant(commissions));
        row.Append(CreerCelluleMontant(autres));
        return row;
    }

    private static TableCell CreerCellule(string texte, bool gras = false, bool centrer = false)
    {
        var cell = new TableCell();
        var p = CreerParagraphe(texte, gras, centrer: centrer);
        cell.Append(p);
        cell.Append(new TableCellProperties(
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
        return cell;
    }

    private static TableCell CreerCelluleMontant(decimal valeur) =>
        CreerCellule(valeur == 0 ? "" : FormaterMontant(valeur), centrer: false);

    private static string FormaterMontant(decimal valeur) =>
        valeur.ToString("N2", Fr);

    private static Paragraph CreerParagraphe(string texte, bool gras = false, bool italique = false, bool centrer = false)
    {
        var run = new Run();
        var props = new RunProperties();
        if (gras) props.Append(new Bold());
        if (italique) props.Append(new Italic());
        if (gras || italique) run.Append(props);
        run.Append(new Text(texte) { Space = SpaceProcessingModeValues.Preserve });

        var para = new Paragraph(run);
        var pPr = new ParagraphProperties();
        if (centrer)
            pPr.Append(new Justification { Val = JustificationValues.Center });
        para.PrependChild(pPr);
        return para;
    }

    private static SectionProperties CreerSectionPaysage() =>
        new(
            new PageSize
            {
                Width = (UInt32Value)16838U,
                Height = (UInt32Value)11906U,
                Orient = PageOrientationValues.Landscape
            },
            new PageMargin
            {
                Top = 1417,
                Right = (UInt32Value)1417U,
                Bottom = 1417,
                Left = (UInt32Value)1417U,
                Header = (UInt32Value)708U,
                Footer = (UInt32Value)708U,
                Gutter = (UInt32Value)0U
            });
}
