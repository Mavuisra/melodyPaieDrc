using System.Windows;
using System.Windows.Controls;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Helpers;

/// <summary>
/// Applique la configuration des colonnes employés au DataGrid principal.
/// </summary>
public static class ConfigurationUiHelper
{
    private static readonly Dictionary<string, string> ColonneParHeader = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Matricule"] = "Matricule",
        ["Nom"] = "Nom",
        ["Postnom"] = "Postnom",
        ["Prénom"] = "Prenom",
        ["Sexe"] = "Sexe",
        ["Téléphone"] = "Telephone",
        ["Département"] = "Departement",
        ["Salaire base (USD)"] = "SalaireMensuelUsd",
        ["Salaire base (FC)"] = "SalaireMensuelCdf",
        ["Salaire / jour (USD)"] = "SalaireJourUsd",
        ["Salaire / jour (FC)"] = "SalaireJourCdf",
        ["Salaire / h (USD)"] = "SalaireHeureUsd",
        ["Salaire / h (FC)"] = "SalaireHeureCdf"
    };

    public static void AppliquerColonnesListeEmployes(DataGrid? grille)
    {
        if (grille == null) return;
        using var db = new PaieDbContext();
        foreach (var col in grille.Columns.OfType<DataGridColumn>())
        {
            if (col.Header is not string header) continue;
            if (!ColonneParHeader.TryGetValue(header, out var code)) continue;
            col.Visibility = ConfigurationPlateformeService.ColonneEmployeVisible(db, code)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
