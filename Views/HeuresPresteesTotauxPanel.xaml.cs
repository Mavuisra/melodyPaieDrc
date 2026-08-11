using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;
using Microsoft.Win32;

namespace MelodyPaieRDC.Views;

public partial class HeuresPresteesTotauxPanel : UserControl
{
    private HeuresPresteesTotauxViewModel? _vm;

    public HeuresPresteesTotauxPanel()
    {
        InitializeComponent();
        _vm = new HeuresPresteesTotauxViewModel(new PaieDbContext());
        DataContext = _vm;
        WireExportHandlers(_vm);
        AppSessionEvents.EntrepriseCouranteChanged += OnEntrepriseCouranteChanged;
        AppSessionEvents.SessionUtilisateurChanged += OnSessionUtilisateurChanged;
        AppSessionEvents.ReglesLtModifiees += OnReglesLtModifiees;
        Unloaded += (_, _) =>
        {
            if (_vm != null)
            {
                _vm.OnDemandeExportRapportAgent -= ExportRapportAgent;
                _vm.OnDemandeExportRapportQuinzaines -= ExportRapportQuinzaines;
                _vm.OnDemandeExportRapportMensuel -= ExportRapportMensuel;
                _vm.OnDemandeExportHeuresPeriode -= ExportHeuresPeriode;
                _vm.OnDemandeExportHeuresEmploye -= ExportHeuresEmploye;
            }
            AppSessionEvents.EntrepriseCouranteChanged -= OnEntrepriseCouranteChanged;
            AppSessionEvents.SessionUtilisateurChanged -= OnSessionUtilisateurChanged;
            AppSessionEvents.ReglesLtModifiees -= OnReglesLtModifiees;
        };
    }

    private void WireExportHandlers(HeuresPresteesTotauxViewModel vm)
    {
        vm.OnDemandeExportRapportAgent += ExportRapportAgent;
        vm.OnDemandeExportRapportQuinzaines += ExportRapportQuinzaines;
        vm.OnDemandeExportRapportMensuel += ExportRapportMensuel;
        vm.OnDemandeExportHeuresPeriode += ExportHeuresPeriode;
        vm.OnDemandeExportHeuresEmploye += ExportHeuresEmploye;
    }

    private HeuresPresteesTotauxViewModel? Vm => DataContext as HeuresPresteesTotauxViewModel ?? _vm;

    private void ExportRapportAgent()
    {
        var vm = Vm;
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
        var vm = Vm;
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

    private void ExportHeuresPeriode()
    {
        var vm = Vm;
        if (vm?.PeriodeSelectionnee == null || vm.Lignes.Count == 0)
        {
            AppNotificationService.Afficher("Sélectionnez une période avec des données d'heures.", NotificationKind.Info);
            return;
        }

        var p = vm.PeriodeSelectionnee;
        var dlg = new SaveFileDialog
        {
            Title = "Exporter les heures travaillées (PDF)",
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"Heures_Travaillees_{p.Mois:D2}_{p.Annee}.pdf",
            DefaultExt = ".pdf",
            AddExtension = true
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            vm.ExporterHeuresPeriodePdf(dlg.FileName);
            AppNotificationService.Succes("PDF heures travaillées exporté.");
            OuvrirPdf(dlg.FileName);
        }
        catch (Exception ex)
        {
            AppNotificationService.Avertissement($"Export PDF : {ex.Message}");
        }
    }

    private void ExportHeuresEmploye()
    {
        var vm = Vm;
        if (vm?.PeriodeSelectionnee == null || vm.EmployeSelectionne == null)
        {
            AppNotificationService.Afficher("Sélectionnez une période et un employé.", NotificationKind.Info);
            return;
        }

        var p = vm.PeriodeSelectionnee;
        var mat = vm.EmployeSelectionne.Matricule?.Replace('/', '-') ?? "Employe";
        var dlg = new SaveFileDialog
        {
            Title = "Exporter le détail des heures de l'employé (PDF)",
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"Heures_{mat}_{p.Mois:D2}_{p.Annee}.pdf",
            DefaultExt = ".pdf",
            AddExtension = true
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            vm.ExporterHeuresEmployePdf(dlg.FileName);
            AppNotificationService.Succes("PDF heures employé exporté.");
            OuvrirPdf(dlg.FileName);
        }
        catch (Exception ex)
        {
            AppNotificationService.Avertissement($"Export PDF : {ex.Message}");
        }
    }

    private void ExportRapportMensuel()
    {
        var vm = Vm;
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

    private void OnSessionUtilisateurChanged() =>
        Dispatcher.Invoke(() => _vm?.NotifierDroitsModification());

    public HeuresPresteesTotauxViewModel? TotauxViewModel => DataContext as HeuresPresteesTotauxViewModel;

    public void RafraichirPourEntrepriseCourante()
    {
        _vm?.RechargerPourEntrepriseCourante();
    }

    private void OnEntrepriseCouranteChanged() =>
        Dispatcher.Invoke(RafraichirPourEntrepriseCourante);

    private void OnReglesLtModifiees() =>
        Dispatcher.Invoke(() => _vm?.RafraichirApresChangementReglesLt());
}
