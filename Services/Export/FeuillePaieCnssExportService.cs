using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services.Export;

/// <summary>
/// Annexe « Détails de la feuille de paie » (modèle CNSS / edeclaration.cnss.cd) au format Word.
/// </summary>
public sealed class FeuillePaieCnssExportService : PaieExportServiceBase
{
    public FeuillePaieCnssExportService(PaieDbContext db) : base(db) { }

    public void ExporterWord(int periodePaieId, string cheminFichier)
    {
        var lot = Factory.ChargerLot(periodePaieId);
        VerifierExportAutorise(lot.Periode);
        var lignesActives = lot.Lignes.Where(FeuillePaieCnssFiltre.InclureEmploye).ToList();
        FeuillePaieCnssWordWriter.Generer(cheminFichier, lot.Entreprise, lot.Periode, lignesActives);
    }

    public static string ObtenirNomFichierSuggere(PeriodePaie? periode) =>
        $"Feuille_paie_CNSS_{periode?.Mois:D2}_{periode?.Annee}.docx";
}
