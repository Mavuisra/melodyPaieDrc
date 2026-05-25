using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
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
        vm.OnErreur = msg => AppNotificationService.Avertissement(msg);
        vm.OnMessageInformation = msg => AppNotificationService.Afficher(msg, NotificationKind.Info);
        vm.OnSauvegardeReussie = () =>
        {
            AppNotificationService.Succes("Pointage journalier enregistré.");
            AppSessionEvents.NotifierDonneesMetierModifiees();
        };
        DataContext = vm;
        vm.PropertyChanged += ViewModelOnPropertyChanged;
        Loaded += (_, _) =>
        {
            vm.RafraichirAffichageTerminalDepuisBase();
            vm.SelectionnerPeriodeMoisCourant();
            AppliquerVisibiliteColonnesPresence();
        };
        IsVisibleChanged += (_, _) =>
        {
            if (DataContext is not SuiviJournalierViewModel v)
                return;
            if (IsVisible)
            {
                PointageLiveNotificationService.ReinitialiserBadge();
                v.DemarrerSurveillancePresenceAutomatique();
                AppliquerVisibiliteColonnesPresence();
            }
            else
                v.ArreterSurveillancePresenceAutomatique();
        };
        AppSessionEvents.EntrepriseCouranteChanged += OnEntrepriseCouranteChanged;
        AppSessionEvents.SessionUtilisateurChanged += OnSessionUtilisateurChanged;
        AppSessionEvents.ReglesLtModifiees += OnReglesLtModifiees;
        Unloaded += (_, _) =>
        {
            if (DataContext is SuiviJournalierViewModel v)
                v.ArreterSurveillancePresenceAutomatique();
            AppSessionEvents.EntrepriseCouranteChanged -= OnEntrepriseCouranteChanged;
            AppSessionEvents.SessionUtilisateurChanged -= OnSessionUtilisateurChanged;
            AppSessionEvents.ReglesLtModifiees -= OnReglesLtModifiees;
        };
    }

    private void OnSessionUtilisateurChanged() =>
        Dispatcher.Invoke(() => SuiviViewModel?.NotifierDroitsModification());

    public void RafraichirPourEntrepriseCourante()
    {
        if (DataContext is not SuiviJournalierViewModel vm)
            return;
        vm.ChargerEmployes();
        vm.ChargerPeriodes();
        vm.RafraichirAffichageTerminalDepuisBase();
        vm.RafraichirApresChangementReglesLt();
        if (IsVisible)
            vm.DemarrerSurveillancePresenceAutomatique();
    }

    private void OnEntrepriseCouranteChanged() =>
        Dispatcher.Invoke(RafraichirPourEntrepriseCourante);

    private void OnReglesLtModifiees() =>
        Dispatcher.Invoke(() =>
        {
            if (DataContext is not SuiviJournalierViewModel vm)
                return;
            vm.RafraichirApresChangementReglesLt();
            AppliquerVisibiliteColonnesPresence();
        });

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SuiviJournalierViewModel.PresenceAfficherColonnesPause)
            or nameof(SuiviJournalierViewModel.PresenceAfficherColonneFinPause)
            or nameof(SuiviJournalierViewModel.PresenceEnteteColonnePause))
            AppliquerVisibiliteColonnesPresence();
    }

    /// <summary>Les colonnes DataGrid n’héritent pas du DataContext : visibilité pilotée en code.</summary>
    private void GrillePresenceSynthese_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SuiviViewModel == null || GrillePresenceSynthese.SelectedItem is not PresenceEmployeSyntheseLigne ligne)
            return;
        SuiviViewModel.SelectionnerEmployeParMatricule(ligne.Matricule);
    }

    private void AppliquerVisibiliteColonnesPresence()
    {
        if (DataContext is not SuiviJournalierViewModel vm)
            return;

        ColDebutPause.Visibility = vm.PresenceAfficherColonnesPause ? Visibility.Visible : Visibility.Collapsed;
        ColFinPause.Visibility = vm.PresenceAfficherColonneFinPause ? Visibility.Visible : Visibility.Collapsed;
        ColDebutPause.Header = vm.PresenceEnteteColonnePause;
    }

    /// <summary>En mode fenêtre modale : fermer la fenêtre après succès au lieu du simple message (réécrit l'action par défaut).</summary>
    public void ConfigurerModeFenetreModale(Window fenetre)
    {
        if (DataContext is not SuiviJournalierViewModel vm) return;
        vm.OnSauvegardeReussie = () =>
        {
            AppNotificationService.Succes("Pointage journalier enregistré.");
            AppSessionEvents.NotifierDonneesMetierModifiees();
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
            AppNotificationService.Succes("PDF du jour exporté.");
        }
        catch (Exception ex)
        {
            AppNotificationService.Avertissement(ex.Message);
        }
    }

    private void ExporterPdfTousEmployes_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SuiviJournalierViewModel vm)
            return;
        if (vm.PeriodeSelectionnee == null)
        {
            AppNotificationService.Afficher("Sélectionnez d'abord une période de paie.", NotificationKind.Info);
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
            AppNotificationService.Succes("PDF pointage (tous employés) exporté.");
        }
        catch (Exception ex)
        {
            AppNotificationService.Avertissement(ex.Message);
        }
    }
}
