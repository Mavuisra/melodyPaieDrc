using System.Diagnostics;
using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Views;

public partial class FormDefinitionsWindow : Window
{
    public FormDefinitionsWindow()
    {
        InitializeComponent();
        FormsPathRun.Text = PaieDbContext.GetFormsDirectory();
        RechargerListe();
    }

    private void RechargerListe()
    {
        FormDefinitionLoader.AssurerDossierEtModelesParDefaut();
        FormsGrid.ItemsSource = FormDefinitionLoader.ListerFormulaires().ToList();
    }

    private void Recharger_Click(object sender, RoutedEventArgs e) => RechargerListe();

    private void OuvrirDossier_Click(object sender, RoutedEventArgs e)
    {
        FormDefinitionLoader.AssurerDossierEtModelesParDefaut();
        Process.Start(new ProcessStartInfo
        {
            FileName = PaieDbContext.GetFormsDirectory(),
            UseShellExecute = true
        });
    }

    private void Apercu_Click(object sender, RoutedEventArgs e)
    {
        if (FormsGrid.SelectedItem is not FormDefinitionSummary summary)
        {
            UiFeedback.Info("Sélectionnez un formulaire dans la liste.");
            return;
        }

        var def = FormDefinitionLoader.ChargerParId(summary.FormId);
        if (def == null)
        {
            UiFeedback.Avertissement("Impossible de charger la définition.");
            return;
        }

        var win = new DynamicFormWindow(def, entityId: 0, sousTitre: "Aperçu (lecture seule — entité #0)")
        {
            Owner = this
        };
        win.ShowDialog();
    }
}
