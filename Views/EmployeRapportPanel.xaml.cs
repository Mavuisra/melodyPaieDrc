using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;
using Microsoft.Win32;

namespace MelodyPaieRDC.Views;

public partial class EmployeRapportPanel : UserControl
{
    private EmployeRapportViewModel? _vm;

    public EmployeRapportPanel()
    {
        InitializeComponent();
        _vm = new EmployeRapportViewModel(new PaieDbContext());
        DataContext = _vm;
        WireExportHandlers(_vm);
        AppSessionEvents.EntrepriseCouranteChanged += OnEntrepriseCouranteChanged;
        AppSessionEvents.SessionUtilisateurChanged += OnSessionUtilisateurChanged;
        Unloaded += (_, _) =>
        {
            if (_vm != null)
            {
                _vm.OnDemandeExportRapportAgent -= ExportRapportAgent;
                _vm.OnDemandeExportRapportQuinzaines -= ExportRapportQuinzaines;
                _vm.OnDemandeExportRapportMensuel -= ExportRapportMensuel;
            }
            AppSessionEvents.EntrepriseCouranteChanged -= OnEntrepriseCouranteChanged;
            AppSessionEvents.SessionUtilisateurChanged -= OnSessionUtilisateurChanged;
        };
    }

    public EmployeRapportViewModel? RapportViewModel => DataContext as EmployeRapportViewModel ?? _vm;

    public void RafraichirPourEntrepriseCourante() => _vm?.RechargerPourEntrepriseCourante();

    public void SynchroniserEmployeDepuisRepertoire(int? employeId) =>
        _vm?.SynchroniserEmployeDepuisRepertoire(employeId);

    private void WireExportHandlers(EmployeRapportViewModel vm)
    {
        vm.OnDemandeExportRapportAgent += ExportRapportAgent;
        vm.OnDemandeExportRapportQuinzaines += ExportRapportQuinzaines;
        vm.OnDemandeExportRapportMensuel += ExportRapportMensuel;
    }

    private void ExportRapportAgent()
    {
        var vm = RapportViewModel;
        if (vm?.PeriodeSelectionnee == null || vm.EmployeSelectionne == null)
        {
            AppNotificationService.Afficher("Sélectionnez une période et un employé.", NotificationKind.Info);
            return;
        }

        var p = vm.PeriodeSelectionnee;
        var mat = vm.EmployeSelectionne.Matricule?.Replace('/', '-') ?? "Agent";
        var dlg = new SaveFileDialog
        {
            Title = "Exporter la situation mensuelle de l'agent (PDF)",
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"Situation_{mat}_{p.Mois:D2}_{p.Annee}.pdf",
            DefaultExt = ".pdf",
            AddExtension = true
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            vm.ExporterRapportAgentPdf(dlg.FileName);
            AppNotificationService.Succes("PDF situation agent exporté.");
            OuvrirPdf(dlg.FileName);
        }
        catch (Exception ex)
        {
            AppNotificationService.Avertissement($"Export PDF : {ex.Message}");
        }
    }

    private void ExportRapportQuinzaines()
    {
        var vm = RapportViewModel;
        if (vm?.PeriodeSelectionnee == null)
        {
            AppNotificationService.Afficher("Sélectionnez une période de paie.", NotificationKind.Info);
            return;
        }

        var p = vm.PeriodeSelectionnee;
        var dlg = new SaveFileDialog
        {
            Title = "Exporter le rapport des quinzaines (PDF)",
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"Rapport_Quinzaines_{p.Mois:D2}_{p.Annee}.pdf",
            DefaultExt = ".pdf",
            AddExtension = true
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            vm.ExporterRapportQuinzainesPdf(dlg.FileName);
            AppNotificationService.Succes("Rapport quinzaines exporté.");
            OuvrirPdf(dlg.FileName);
        }
        catch (Exception ex)
        {
            AppNotificationService.Avertissement($"Export PDF : {ex.Message}");
        }
    }

    private void ExportRapportMensuel()
    {
        var vm = RapportViewModel;
        if (vm?.PeriodeSelectionnee == null)
        {
            AppNotificationService.Afficher("Sélectionnez une période de paie.", NotificationKind.Info);
            return;
        }

        var p = vm.PeriodeSelectionnee;
        var dlg = new SaveFileDialog
        {
            Title = "Exporter le rapport mensuel des salaires (PDF)",
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"Rapport_Salaires_{p.Mois:D2}_{p.Annee}.pdf",
            DefaultExt = ".pdf",
            AddExtension = true
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            vm.ExporterRapportMensuelSalairesPdf(dlg.FileName);
            AppNotificationService.Succes("Rapport mensuel exporté.");
            OuvrirPdf(dlg.FileName);
        }
        catch (Exception ex)
        {
            AppNotificationService.Avertissement($"Export PDF : {ex.Message}");
        }
    }

    private static void OuvrirPdf(string chemin)
    {
        try
        {
            Process.Start(new ProcessStartInfo(chemin) { UseShellExecute = true });
        }
        catch
        {
            // Ouverture optionnelle
        }
    }

    private void OnEntrepriseCouranteChanged() =>
        Dispatcher.Invoke(RafraichirPourEntrepriseCourante);

    private void OnSessionUtilisateurChanged() { }
}
