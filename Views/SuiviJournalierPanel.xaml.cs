using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;
using Microsoft.Win32;

namespace MelodyPaieRDC.Views;

/// <summary>
/// Contenu de saisie du pointage journalier (employé + période + grille). Utilisé dans MainWindow et dans SuiviJournalierWindow.
/// </summary>
public partial class SuiviJournalierPanel : UserControl
{
    public SuiviJournalierViewModel? SuiviViewModel => DataContext as SuiviJournalierViewModel;

    public SuiviJournalierPanel()
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new SuiviJournalierViewModel(db);
        vm.ChargerEmployes();
        vm.ChargerPeriodes();
        vm.OnErreur = msg => MessageBox.Show(msg, "Pointage journalier", MessageBoxButton.OK, MessageBoxImage.Warning);
        vm.OnMessageInformation = msg => MessageBox.Show(msg, "Terminal ZKTeco", MessageBoxButton.OK, MessageBoxImage.Information);
        vm.OnSauvegardeReussie = () =>
            MessageBox.Show("Les données du pointage journalier ont été enregistrées.", "Pointage journalier",
                MessageBoxButton.OK, MessageBoxImage.Information);
        DataContext = vm;
        Loaded += (_, _) =>
        {
            vm.RafraichirAffichageTerminalDepuisBase();
            vm.SelectionnerPremiersParDefaut();
        };
        IsVisibleChanged += (_, _) =>
        {
            if (DataContext is not SuiviJournalierViewModel v)
                return;
            if (IsVisible)
                v.DemarrerSurveillancePresenceAutomatique();
            else
                v.ArreterSurveillancePresenceAutomatique();
        };
        Unloaded += (_, _) =>
        {
            if (DataContext is SuiviJournalierViewModel v)
                v.ArreterSurveillancePresenceAutomatique();
        };
    }

    /// <summary>En mode fenêtre modale : fermer la fenêtre après succès au lieu du simple message (réécrit l'action par défaut).</summary>
    public void ConfigurerModeFenetreModale(Window fenetre)
    {
        if (DataContext is not SuiviJournalierViewModel vm) return;
        vm.OnSauvegardeReussie = () =>
        {
            MessageBox.Show(fenetre, "Les données du pointage journalier ont été enregistrées.", "Pointage journalier",
                MessageBoxButton.OK, MessageBoxImage.Information);
            fenetre.DialogResult = true;
            fenetre.Close();
        };
    }

    private void ExporterPdf_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SuiviJournalierViewModel vm)
            return;
        var aujourdHui = DateTime.Today;
        var dlg = new SaveFileDialog
        {
            Title = "Exporter les personnels pointés aujourd'hui (PDF)",
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"Pointage_Pointes_AujourdHui_{aujourdHui:yyyyMMdd}.pdf",
            DefaultExt = ".pdf",
            AddExtension = true
        };

        if (dlg.ShowDialog() != true)
            return;

        try
        {
            vm.ExporterPointesAujourdhuiPdf(dlg.FileName);
            MessageBox.Show(
                $"Le fichier PDF a été enregistré :{Environment.NewLine}{dlg.FileName}",
                "Exporter PDF",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Exporter PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ExporterPdfTousEmployes_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SuiviJournalierViewModel vm)
            return;
        if (vm.PeriodeSelectionnee == null)
        {
            MessageBox.Show("Sélectionnez d’abord une période de paie.", "PDF tous employés",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Exporter le pointage de tous les employés (PDF)",
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"Pointage_TousEmployes_{vm.PeriodeSelectionnee.Mois:D2}_{vm.PeriodeSelectionnee.Annee}.pdf",
            DefaultExt = ".pdf",
            AddExtension = true
        };

        if (dlg.ShowDialog() != true)
            return;

        try
        {
            vm.ExporterSuiviJournalierPdfTousEmployes(dlg.FileName);
            MessageBox.Show(
                $"Le fichier PDF a été enregistré :{Environment.NewLine}{dlg.FileName}",
                "PDF tous employés",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "PDF tous employés", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
