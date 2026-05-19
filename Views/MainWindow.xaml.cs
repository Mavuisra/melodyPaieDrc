using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.ViewModels;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.Services.Export;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private SuiviJournalierWindow? _suiviJournalierWindow;
    private DispatcherTimer? _notificationTimer;

    public MainWindow()
    {
        InitializeComponent();
        ChargerIconeFenetre();
        _viewModel = new MainViewModel();
        _viewModel.OnOuvrirNouvelEmploye = OuvrirNouvelEmploye;
        _viewModel.OnOuvrirModifierEmploye = OuvrirModifierEmploye;
        _viewModel.OnOuvrirContrats = OuvrirContrats;
        _viewModel.OnOuvrirAyantsDroit = OuvrirAyantsDroit;
        _viewModel.OnOuvrirPretsAvances = OuvrirPretsAvances;
        _viewModel.OnOuvrirPrimesIndemnites = OuvrirPrimesIndemnites;
        _viewModel.OnOuvrirHeuresMoisEmploye = OuvrirHeuresMoisEmploye;
        _viewModel.OnOuvrirCentreConfiguration = OuvrirCentreConfiguration;
        _viewModel.OnOuvrirParametresIpr = OuvrirParametresIpr;
        _viewModel.OnOuvrirTauxSociaux = OuvrirTauxSociaux;
        _viewModel.OnOuvrirPeriodesPaie = OuvrirPeriodesPaie;
        _viewModel.OnOuvrirInfosEntreprise = OuvrirInfosEntreprise;
        _viewModel.OnOuvrirAssistantConfiguration = OuvrirAssistantConfiguration;
        _viewModel.OnCreerNouvelleEntreprise = CreerNouvelleEntreprise;
        _viewModel.OnForcerAssistantProchainDemarrage = ForcerAssistantProchainDemarrage;
        _viewModel.OnOuvrirConfigPrimesIndemnites = OuvrirConfigPrimesIndemnites;
        _viewModel.OnOuvrirEtablissementsDepartements = OuvrirEtablissementsDepartements;
        _viewModel.OnImporterFicheSalaireExcel = ImporterFicheSalaireExcel;
        _viewModel.OnOuvrirGestionUtilisateurs = OuvrirGestionUtilisateurs;
        _viewModel.OnSauvegarderBase = SauvegarderBase;
        _viewModel.OnRestaurerBase = RestaurerBase;
        _viewModel.OnReinitialiserApplication = ReinitialiserApplication;
        _viewModel.OnVerifierMiseAJour = () => MiseAJourWindow.Afficher(this);
        _viewModel.OnOuvrirCalendrierTravail = OuvrirCalendrierTravail;
        _viewModel.OnOuvrirSaisiePaieMois = OuvrirSaisiePaieMois;
        _viewModel.OnOuvrirSuiviJournalier = OuvrirSuiviJournalier;
        _viewModel.OnOuvrirChampsComplementairesEmploye = OuvrirChampsComplementairesEmploye;
        _viewModel.OnOuvrirFormulairesDynamiques = () => DynamicFormNavigator.OuvrirGestionnaireDefinitions(this);
        _viewModel.OnOuvrirChampsComplementairesEntreprise = () => DynamicFormNavigator.OuvrirChampsComplementairesEntreprise(this);
        _viewModel.OnErreurCalculPaie = msg => NotifierAvertissement(msg);
        _viewModel.OnSuccessCalculPaie = msg => AfficherNotification(msg, NotificationKind.Success);
        _viewModel.OnSuccesTauxChange = msg => AfficherNotification(msg, NotificationKind.Success);
        _viewModel.OnErreurTauxChange = msg => NotifierAvertissement(msg);
        _viewModel.OnMessageZkSettings = msg => AfficherNotification(msg, NotificationKind.Success);
        _viewModel.OnErreurZkSettings = msg => NotifierAvertissement(msg);
        _viewModel.DemanderConfirmationChangementEntreprise = (_, nouveau) =>
            MessageBox.Show(this,
                "Changer d'entreprise recharge les données affichées. Les fenêtres ouvertes peuvent ne plus correspondre.\n\nContinuer ?",
                "Changer d'entreprise",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
        _viewModel.OnDemandeDeconnexion = ExecuterDeconnexion;
        AppNotificationService.NotificationPubliee += OnNotificationService;
        AppSessionEvents.EntrepriseCouranteChanged += OnEntrepriseCouranteChanged;
        AppSessionEvents.DonneesMetierModifiees += OnDonneesMetierModifiees;
        _viewModel.OnVoirBulletin = OuvrirBulletin;
        _viewModel.OnTelechargerBulletin = TelechargerBulletinPdf;
        _viewModel.OnTelechargerTousBulletins = TelechargerTousBulletinsPdf;
        _viewModel.OnExporterFichier = ExporterFichier;
        _viewModel.OnExporterLivrePaiePdf = ExporterLivrePaiePdf;
        _viewModel.OnExporterLivrePaieExcel = ExporterLivrePaieExcel;
        _viewModel.OnExporterDeclarationCnssExcel = ExporterDeclarationCnssExcel;
        _viewModel.OnExporterDeclarationIprExcel = ExporterDeclarationIprExcel;
        _viewModel.OnExporterCnssEdeclarationExcel = ExporterCnssEdeclarationExcel;
        _viewModel.OnExporterFeuillePaieCnss = ExporterFeuillePaieCnss;
        _viewModel.OnExporterDgiIprExcel = ExporterDgiIprExcel;
        _viewModel.OnExporterLivreReglementairePdf = ExporterLivreReglementairePdf;
        _viewModel.OnExporterLivreReglementaireExcel = ExporterLivreReglementaireExcel;
        _viewModel.OnOuvrirGuideCnss = () => GuideExportWindow.AfficherCnss(this);
        _viewModel.OnOuvrirGuideIpr = () => GuideExportWindow.AfficherIpr(this);
        _viewModel.OnOuvrirCloturePeriode = OuvrirCloturePeriode;
        _viewModel.OnExporterRapportPaieExcel = ExporterRapportPaieExcel;
        DataContext = _viewModel;
        Loaded += (_, _) =>
        {
            _viewModel.ChargerDonnees();
            _viewModel.NotifierChangementSessionUtilisateur();
            Title = $"Melody Paie RDC — {_viewModel.EntrepriseCouranteLibelle}";
            ZktecoSynchronisationService.Reconfigurer();
            ConfigurationUiHelper.AppliquerColonnesListeEmployes(GrilleEmployes);
            AfficherTableauDeBordEnPremier();
            AppliquerIdentiteVisuelleCourante();
            AfficherBandeauRestaurationSiNecessaire();
            _ = ProposerMiseAJourAuDemarrageAsync();
        };
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
    }

    private void OnNotificationService(string message, NotificationKind kind) =>
        Dispatcher.Invoke(() => AfficherNotification(message, kind));

    private void AfficherNotification(string message, NotificationKind kind = NotificationKind.Info)
    {
        TexteNotification.Text = message;
        BandeauNotification.Visibility = Visibility.Visible;
        BandeauNotification.BorderBrush = kind switch
        {
            NotificationKind.Success => new SolidColorBrush(Color.FromRgb(0xA5, 0xD6, 0xA7)),
            NotificationKind.Warning => new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x80)),
            _ => new SolidColorBrush(Color.FromRgb(0x90, 0xCA, 0xF9))
        };
        BandeauNotification.Background = kind switch
        {
            NotificationKind.Success => new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9)),
            NotificationKind.Warning => new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xE0)),
            _ => new SolidColorBrush(Color.FromRgb(0xE3, 0xF2, 0xFD))
        };
        IconeNotification.Kind = kind switch
        {
            NotificationKind.Success => MaterialDesignThemes.Wpf.PackIconKind.CheckCircleOutline,
            NotificationKind.Warning => MaterialDesignThemes.Wpf.PackIconKind.AlertOutline,
            _ => MaterialDesignThemes.Wpf.PackIconKind.InformationOutline
        };

        _notificationTimer?.Stop();
        _notificationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        _notificationTimer.Tick += (_, _) =>
        {
            _notificationTimer?.Stop();
            BandeauNotification.Visibility = Visibility.Collapsed;
        };
        _notificationTimer.Start();
    }

    private void NotifierSucces(string message) => AfficherNotification(message, NotificationKind.Success);

    private void NotifierInfo(string message) => AfficherNotification(message, NotificationKind.Info);

    private void NotifierAvertissement(string message) => AfficherNotification(message, NotificationKind.Warning);

    private void OnEntrepriseCouranteChanged()
    {
        Dispatcher.Invoke(() =>
        {
            _viewModel.ChargerContexteEntreprise();
            Title = $"Melody Paie RDC — {_viewModel.EntrepriseCouranteLibelle}";
            AppliquerIdentiteVisuelleCourante();
            RafraichirPanneauxPointageEtHeures();
            _viewModel.RafraichirChecklistMoisPaie();
        });
    }

    private void OnDonneesMetierModifiees()
    {
        Dispatcher.Invoke(() =>
        {
            _viewModel.RafraichirChecklistMoisPaie();
            if (_viewModel.MenuSelectionne == 0)
                _viewModel.ChargerTableauDeBord();
        });
    }

    private void AppliquerIdentiteVisuelleCourante() =>
        EntrepriseBrandingService.AppliquerIdentiteVisuelleGlobale();

    private void RafraichirPanneauxPointageEtHeures()
    {
        PanneauSuiviJournalier?.RafraichirPourEntrepriseCourante();
        PanneauHeuresPrestees?.RafraichirPourEntrepriseCourante();
    }

    private void ExecuterDeconnexion()
    {
        var confirmer = MessageBox.Show(this,
            "Voulez-vous vous déconnecter ?",
            "Déconnexion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmer != MessageBoxResult.Yes)
            return;

        AuthService.Logout();
        AppNotificationService.NotificationPubliee -= OnNotificationService;
        AppSessionEvents.EntrepriseCouranteChanged -= OnEntrepriseCouranteChanged;
        AppSessionEvents.DonneesMetierModifiees -= OnDonneesMetierModifiees;
        Hide();
        var login = new LoginWindow();
        if (login.ShowDialog() == true)
        {
            AppNotificationService.NotificationPubliee += OnNotificationService;
            AppSessionEvents.EntrepriseCouranteChanged += OnEntrepriseCouranteChanged;
        AppSessionEvents.DonneesMetierModifiees += OnDonneesMetierModifiees;
            _viewModel.ChargerDonnees();
            _viewModel.NotifierChangementSessionUtilisateur();
            Title = $"Melody Paie RDC — {_viewModel.EntrepriseCouranteLibelle}";
            Show();
            AfficherNotification($"Bon retour, {AuthService.UtilisateurCourant?.Login ?? "utilisateur"}.", NotificationKind.Success);
            return;
        }

        Application.Current.Shutdown();
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.MenuSelectionne) && _viewModel.MenuSelectionne == 0)
            AfficherTableauDeBordEnPremier();
        if (e.PropertyName == nameof(MainViewModel.EntrepriseCouranteLibelle))
            Title = $"Melody Paie RDC — {_viewModel.EntrepriseCouranteLibelle}";
    }

    protected override void OnClosed(EventArgs e)
    {
        AppNotificationService.NotificationPubliee -= OnNotificationService;
        AppSessionEvents.EntrepriseCouranteChanged -= OnEntrepriseCouranteChanged;
        AppSessionEvents.DonneesMetierModifiees -= OnDonneesMetierModifiees;
        base.OnClosed(e);
    }

    private void AfficherTableauDeBordEnPremier()
    {
        if (_viewModel.MenuSelectionne != 0)
            _viewModel.MenuSelectionne = 0;
        MainContentScrollViewer?.ScrollToVerticalOffset(0);
    }

    private void OuvrirCalendrierTravail()
    {
        var win = new CalendrierTravailWindow { Owner = this };
        win.ShowDialog();
    }

    private void ImporterFicheSalaireExcel()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var dlg = new OpenFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx|Tous les fichiers (*.*)|*.*",
            Title = "Fiche salaire (feuille « SALAIRE ET TAXE »)",
            InitialDirectory = Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var db = new PaieDbContext();
            var r = FicheSalaireExcelImportService.Importer(dlg.FileName, db);
            var lignes = r.Messages.Take(25).ToList();
            var msg = lignes.Count > 0 ? string.Join(Environment.NewLine, lignes) : "Aucun message.";
            if (r.Messages.Count > 25)
                msg += Environment.NewLine + "…";
            msg += $"{Environment.NewLine}{Environment.NewLine}Créés : {r.EmployesCrees} — Ignorés : {r.LignesIgnorees} — Affectations primes/indemnités : {r.AffectationsCreees}";
            if (r.EmployesCrees > 0)
                NotifierSucces($"Import terminé : {r.EmployesCrees} employé(s) créé(s).");
            else
                NotifierAvertissement(msg.Length > 220 ? msg[..220] + "…" : msg);
            _viewModel.ChargerEmployes();
            _viewModel.ChargerStatistiques();
            AppSessionEvents.NotifierDonneesMetierModifiees();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Import fiche salaire", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ChargerIconeFenetre()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/MelodyPaieRDC;component/Assets/Icon_MelodyPaie.png", UriKind.Absolute);
            if (Application.GetResourceStream(uri) != null)
                Icon = BitmapFrame.Create(uri);
        }
        catch { /* ignorer si l'icône ne charge pas */ }
    }

    private void ExporterFichier(string contenu, string nomSuggere)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv|Tous les fichiers (*.*)|*.*",
            FileName = nomSuggere,
            DefaultExt = ".csv"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, contenu, Encoding.UTF8);
            NotifierSucces("Fichier exporté enregistré.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExporterLivrePaiePdf(int periodeId)
    {
        var p = _viewModel.PeriodeSelectionneePourDeclarations;
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf|Tous les fichiers (*.*)|*.*",
            FileName = p != null ? $"Livre_de_paie_{p.Mois:D2}_{p.Annee}.pdf" : "Livre_de_paie.pdf",
            DefaultExt = ".pdf"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var db = new PaieDbContext();
            var decl = new DeclarationsService(db);
            var resume = decl.GetResumePourPeriode(periodeId);
            var pdfService = new ExportPdfService();
            pdfService.ExporterLivrePaiePdf(resume.Bulletins, resume.Mois, resume.Annee, dlg.FileName);
            NotifierSucces("Livre de paie exporté en PDF.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur export PDF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExporterLivrePaieExcel(int periodeId)
    {
        var p = _viewModel.PeriodeSelectionneePourDeclarations;
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx|Tous les fichiers (*.*)|*.*",
            FileName = p != null ? $"Livre_de_paie_{p.Mois:D2}_{p.Annee}.xlsx" : "Livre_de_paie.xlsx",
            DefaultExt = ".xlsx"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var db = new PaieDbContext();
            var service = new LivrePaieExportService(db);
            service.ExporterExcel(periodeId, dlg.FileName);
            NotifierSucces("Livre de paie exporté en Excel.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur export Excel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExporterRapportPaieExcel(int periodeId)
    {
        var p = _viewModel.PeriodeSelectionneePourRapport;
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx|Tous les fichiers (*.*)|*.*",
            FileName = p != null ? $"Rapport_paie_{p.Mois:D2}_{p.Annee}.xlsx" : "Rapport_paie.xlsx",
            DefaultExt = ".xlsx"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var db = new PaieDbContext();
            var service = new RapportPaieExportService(db);
            service.ExporterExcel(periodeId, dlg.FileName);
            NotifierSucces("Rapport de paie exporté en Excel.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur export Excel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ReinitialiserApplication()
    {
        var confirm = MessageBox.Show(
            "Cette action va réinitialiser Melody Paie :\n\n" +
            "- Toutes les données (employés, contrats, bulletins, périodes, etc.) seront définitivement supprimées.\n" +
            "- L'application redémarrera comme au premier lancement (assistant de configuration + compte administrateur à définir).\n\n" +
            "Êtes-vous sûr de vouloir continuer ?",
            "Réinitialiser l'application",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            using (var db = new PaieDbContext())
            {
                db.Database.EnsureDeleted();
            }

            MessageBox.Show(
                "La base de données a été supprimée.\n\nL'application va maintenant se fermer. Relancez-la pour repartir de zéro.",
                "Réinitialisation terminée",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Impossible de réinitialiser complètement la base : {ex.Message}",
                "Erreur de réinitialisation",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OuvrirSuiviJournalier()
    {
        if (_suiviJournalierWindow != null && _suiviJournalierWindow.IsLoaded)
        {
            if (_suiviJournalierWindow.WindowState == WindowState.Minimized)
                _suiviJournalierWindow.WindowState = WindowState.Normal;
            _suiviJournalierWindow.Activate();
            _suiviJournalierWindow.Focus();
            return;
        }

        _suiviJournalierWindow = new SuiviJournalierWindow
        {
            Owner = this
        };
        _suiviJournalierWindow.Closed += (_, _) =>
        {
            _suiviJournalierWindow = null;
            AppSessionEvents.NotifierDonneesMetierModifiees();
        };
        _suiviJournalierWindow.Show();
    }

    private void OuvrirSaisiePaieMois(int periodePaieId)
    {
        var win = new SaisiePaieMoisWindow(periodePaieId) { Owner = this };
        if (win.ShowDialog() == true)
            AppSessionEvents.NotifierDonneesMetierModifiees();
    }

    private void ExporterDeclarationCnssExcel(int periodeId)
    {
        var p = _viewModel.PeriodeSelectionneePourDeclarations;
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx|Tous les fichiers (*.*)|*.*",
            FileName = p != null ? $"Declaration_CNSS_{p.Mois:D2}_{p.Annee}.xlsx" : "Declaration_CNSS.xlsx",
            DefaultExt = ".xlsx"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var db = new PaieDbContext();
            var service = new DeclarationsService(db);
            service.ExporterDeclarationCnssExcel(periodeId, dlg.FileName);
            NotifierSucces("Déclaration CNSS exportée en Excel.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur export Excel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExporterDeclarationIprExcel(int periodeId)
    {
        var p = _viewModel.PeriodeSelectionneePourDeclarations;
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx|Tous les fichiers (*.*)|*.*",
            FileName = p != null ? $"Declaration_IPR_{p.Mois:D2}_{p.Annee}.xlsx" : "Declaration_IPR.xlsx",
            DefaultExt = ".xlsx"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var db = new PaieDbContext();
            var service = new DeclarationsService(db);
            service.ExporterDeclarationIprExcel(periodeId, dlg.FileName);
            NotifierSucces("Déclaration IPR exportée en Excel.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur export Excel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OuvrirNouvelEmploye()
    {
        var win = new EmployeWindow();
        if (win.ShowDialog() == true)
        {
            _viewModel.ChargerEmployes();
            AppSessionEvents.NotifierDonneesMetierModifiees();
        }
    }

    private void OuvrirModifierEmploye(int employeId)
    {
        var win = new EmployeWindow(employeId) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _viewModel.ChargerEmployes();
            AppSessionEvents.NotifierDonneesMetierModifiees();
        }
    }

    private void OuvrirChampsComplementairesEmploye()
    {
        if (_viewModel.EmployeSelectionne == null) return;
        DynamicFormNavigator.OuvrirChampsComplementairesEmploye(this, _viewModel.EmployeSelectionne);
    }

    private void OuvrirContrats(int employeId)
    {
        var win = new ContratsWindow(employeId) { Owner = this };
        win.ShowDialog();
    }

    private void OuvrirAyantsDroit(int employeId)
    {
        var win = new AyantsDroitWindow(employeId) { Owner = this };
        win.ShowDialog();
    }

    private void OuvrirPretsAvances(int employeId)
    {
        var win = new PretsAvancesWindow(employeId) { Owner = this };
        win.ShowDialog();
    }

    private void OuvrirPrimesIndemnites(int employeId)
    {
        var win = new PrimesIndemnitesEmployeWindow(employeId) { Owner = this };
        win.ShowDialog();
    }

    private void OuvrirHeuresMoisEmploye(int employeId)
    {
        var win = new EmployeHeuresMoisWindow(employeId) { Owner = this };
        win.ShowDialog();
    }

    private void OuvrirConfigPrimesIndemnites()
    {
        var win = new ConfigPrimesIndemnitesWindow { Owner = this };
        win.ShowDialog();
    }

    private void OuvrirEtablissementsDepartements()
    {
        var win = new EtablissementsDepartementsWindow { Owner = this };
        if (win.ShowDialog() == true)
        {
            _viewModel.ChargerEmployes();
            AppSessionEvents.NotifierDonneesMetierModifiees();
        }
    }

    private void OuvrirGestionUtilisateurs()
    {
        var win = new UtilisateursWindow { Owner = this };
        win.ShowDialog();
    }

    private void AfficherBandeauRestaurationSiNecessaire()
    {
        var etat = DatabaseBackupService.ConsommerRestaurationEnAttente();
        if (etat == null) return;
        NotifierSucces(DatabaseBackupService.FormaterMessageBandeau(etat));
    }

    private void SauvegarderBase()
    {
        var dbPath = PaieDbContext.GetDatabasePath();
        if (!File.Exists(dbPath))
        {
            NotifierAvertissement("Aucun fichier de base trouvé.");
            return;
        }
        var defaultName = $"PaieRDC_backup_{DateTime.Now:yyyy-MM-dd_HHmmss}.db";
        var dlg = new SaveFileDialog
        {
            Filter = "Base SQLite (*.db)|*.db|Tous les fichiers (*.*)|*.*",
            FileName = defaultName,
            DefaultExt = ".db"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (!DatabaseBackupService.EstIntegriteValide(dbPath))
            {
                NotifierAvertissement(
                    "La base actuelle est endommagée. Utilisez une copie de sauvegarde (.db) via « Restaurer » ou contactez le support.");
                return;
            }

            SqliteConnection.ClearAllPools();
            DatabaseBackupService.AssurerSchemaAvantSauvegarde();
            DatabaseBackupService.ExporterCopieCoherente(dbPath, dlg.FileName);
            var verification = DatabaseBackupService.ValiderFichierBackup(dlg.FileName);
            if (!verification.EstValide)
            {
                NotifierAvertissement(
                    $"Fichier créé, mais la vérification a échoué : {verification.MessageErreur}");
                return;
            }

            NotifierSucces($"Sauvegarde enregistrée : {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur sauvegarde", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void RestaurerBase()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Base SQLite (*.db)|*.db|Tous les fichiers (*.*)|*.*",
            Title = "Choisir le fichier de sauvegarde"
        };
        if (dlg.ShowDialog(this) != true) return;

        var validation = DatabaseBackupService.ValiderFichierBackup(dlg.FileName);
        if (!validation.EstValide)
        {
            NotifierAvertissement(validation.MessageErreur ?? "Fichier de sauvegarde invalide.");
            return;
        }

        if (!DatabaseBackupService.TablePresenteDansFichier(dlg.FileName, "ParametresApplication"))
        {
            var cont = MessageBox.Show(this,
                "Cette sauvegarde provient d'une ancienne version (paramètres globaux absents).\n\n" +
                "La restauration est possible : Melody recréera les paramètres au démarrage.\n\nContinuer ?",
                "Restaurer la base",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (cont != MessageBoxResult.Yes)
                return;
        }

        var horodatage = File.GetLastWriteTime(dlg.FileName);
        var tailleMo = new FileInfo(dlg.FileName).Length / (1024.0 * 1024.0);
        var result = MessageBox.Show(this,
            $"Fichier : {Path.GetFileName(dlg.FileName)}\n" +
            $"Date : {horodatage:dd/MM/yyyy HH:mm} — {tailleMo:0.##} Mo\n\n" +
            "La base actuelle sera remplacée. Une copie de sécurité sera créée automatiquement " +
            $"dans :\n{PaieDbContext.GetDataDirectory()}\n\n" +
            "L'application va redémarrer.\n\nContinuer ?",
            "Restaurer la base",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            var args = Environment.GetCommandLineArgs();
            var exe = Environment.ProcessPath ?? (args.Length > 0 ? args[0] : null);
            if (string.IsNullOrEmpty(exe))
            {
                MessageBox.Show(this, "Impossible de déterminer le chemin de l'application.", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--restore-from \"{dlg.FileName}\"",
                UseShellExecute = true
            };
            Process.Start(startInfo);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Impossible de lancer la restauration : " + ex.Message, "Erreur",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OuvrirCentreConfiguration()
    {
        var hub = new CentreConfigurationWindow { Owner = this };
        hub.OuvrirPolitiquePaie = () => { hub.Close(); new PolitiquePaieWindow { Owner = this }.ShowDialog(); };
        hub.OuvrirIpr = () => { hub.Close(); OuvrirParametresIpr(); };
        hub.OuvrirTauxSociaux = () => { hub.Close(); OuvrirTauxSociaux(); };
        hub.OuvrirPrimes = () => { hub.Close(); OuvrirConfigPrimesIndemnites(); };
        hub.OuvrirEntreprise = () => { hub.Close(); OuvrirInfosEntreprise(); };
        hub.OuvrirEtablissements = () => { hub.Close(); OuvrirEtablissementsDepartements(); };
        hub.OuvrirColonnes = () =>
        {
            hub.Close();
            if (new ColonnesEmployesWindow { Owner = this }.ShowDialog() == true)
                ConfigurationUiHelper.AppliquerColonnesListeEmployes(GrilleEmployes);
        };
        hub.OuvrirChamps = () => { hub.Close(); new DefinitionChampsWindow { Owner = this }.ShowDialog(); };
        hub.OuvrirFormulaires = () => { hub.Close(); DynamicFormNavigator.OuvrirGestionnaireDefinitions(this); };
        hub.OuvrirPeriodes = () => { hub.Close(); OuvrirPeriodesPaie(); };
        hub.OuvrirExportsPaie = () => { hub.Close(); new ConfigurationExportsPaieWindow { Owner = this }.ShowDialog(); };
        hub.OuvrirAssistant = () => { hub.Close(); OuvrirAssistantConfiguration(); };
        hub.ShowDialog();
    }

    private void OuvrirCloturePeriode(int periodeId)
    {
        if (new CloturePeriodeWindow(periodeId) { Owner = this }.ShowDialog() == true)
        {
            _viewModel.ChargerPeriodes();
            _viewModel.ChargerDeclarations();
            AppSessionEvents.NotifierDonneesMetierModifiees();
        }
    }

    private void ExporterCnssEdeclarationExcel(int periodeId)
    {
        var p = _viewModel.PeriodeSelectionneePourDeclarations;
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = p != null ? $"CNSS_edeclaration_{p.Mois:D2}_{p.Annee}.xlsx" : "CNSS_edeclaration.xlsx",
            DefaultExt = ".xlsx"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var db = new PaieDbContext();
            new CnssEDeclarationExportService(db).ExporterExcel(periodeId, dlg.FileName);
            NotifierSucces("Export CNSS e-déclaration enregistré.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ExporterFeuillePaieCnss(int periodeId)
    {
        var p = _viewModel.PeriodeSelectionneePourDeclarations;
        var dlg = new SaveFileDialog
        {
            Filter = "Document Word (*.docx)|*.docx",
            FileName = p != null
                ? FeuillePaieCnssExportService.ObtenirNomFichierSuggere(p)
                : "Feuille_paie_CNSS.docx",
            DefaultExt = ".docx"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var db = new PaieDbContext();
            new FeuillePaieCnssExportService(db).ExporterWord(periodeId, dlg.FileName);
            NotifierSucces("Feuille de paie CNSS exportée en Word. Enregistrez-la en PDF avant e-déclaration.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ExporterDgiIprExcel(int periodeId)
    {
        var p = _viewModel.PeriodeSelectionneePourDeclarations;
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = p != null ? $"DGI_IPR_{p.Mois:D2}_{p.Annee}.xlsx" : "DGI_IPR.xlsx",
            DefaultExt = ".xlsx"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var db = new PaieDbContext();
            new DgiIprDeclarationExportService(db).ExporterExcel(periodeId, dlg.FileName);
            NotifierSucces("Export DGI IPR enregistré.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ExporterLivreReglementairePdf(int periodeId)
    {
        var p = _viewModel.PeriodeSelectionneePourDeclarations;
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = p != null ? $"Livre_reglementaire_{p.Mois:D2}_{p.Annee}.pdf" : "Livre_reglementaire.pdf",
            DefaultExt = ".pdf"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var db = new PaieDbContext();
            new LivrePaieReglementaireExportService(db).ExporterPdf(periodeId, dlg.FileName);
            NotifierSucces("Livre de paie réglementaire exporté en PDF.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ExporterLivreReglementaireExcel(int periodeId)
    {
        var p = _viewModel.PeriodeSelectionneePourDeclarations;
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = p != null ? $"Livre_reglementaire_{p.Mois:D2}_{p.Annee}.xlsx" : "Livre_reglementaire.xlsx",
            DefaultExt = ".xlsx"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var db = new PaieDbContext();
            new LivrePaieReglementaireExportService(db).ExporterExcel(periodeId, dlg.FileName);
            NotifierSucces("Livre de paie réglementaire exporté en Excel.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void OuvrirParametresIpr()
    {
        var win = new IprConfigWindow { Owner = this };
        win.ShowDialog();
    }

    private void OuvrirTauxSociaux()
    {
        var win = new TauxSociauxWindow { Owner = this };
        win.ShowDialog();
    }

    private void OuvrirPeriodesPaie()
    {
        var win = new PeriodesPaieWindow { Owner = this };
        win.Closed += (_, _) =>
        {
            _viewModel.ChargerPeriodes();
            AppSessionEvents.NotifierDonneesMetierModifiees();
        };
        win.ShowDialog();
        _viewModel.ChargerPeriodes();
    }

    private void OuvrirAssistantConfiguration()
    {
        var win = new AssistantConfigurationWindow { Owner = this };
        if (win.ShowDialog() == true)
        {
            using var db = new PaieDbContext();
            ConfigurationEntrepriseService.MarquerConfigurationTerminee(db);
            RafraichirApresChangementEntreprise();
        }
    }

    private void CreerNouvelleEntreprise()
    {
        var dlg = new NouvelleEntrepriseWindow { Owner = this };
        if (dlg.ShowDialog() != true || dlg.EntrepriseCreeeId is not int newId)
            return;

        ContexteEntrepriseService.DefinirEntrepriseCourante(newId);
        _viewModel.EntrepriseCouranteId = newId;

        var assistant = new AssistantConfigurationWindow { Owner = this };
        if (assistant.ShowDialog() == true)
        {
            using var db = new PaieDbContext();
            ConfigurationEntrepriseService.MarquerConfigurationTerminee(db);
        }

        RafraichirApresChangementEntreprise();
    }

    private void ForcerAssistantProchainDemarrage()
    {
        using var db = new PaieDbContext();
        ParametresApplicationHelper.SetForcerAssistantProchainDemarrage(db, true);
        NotifierInfo("L'assistant s'affichera au prochain démarrage. Fermez puis relancez l'application.");
    }

    private void RafraichirApresChangementEntreprise()
    {
        _viewModel.ChargerDonnees();
        Title = $"Melody Paie RDC — {_viewModel.EntrepriseCouranteLibelle}";
        AppliquerIdentiteVisuelleCourante();
    }

    private void OuvrirInfosEntreprise()
    {
        var win = new EntrepriseWindow { Owner = this };
        win.ShowDialog();
        _viewModel.ChargerContexteEntreprise();
        AppliquerIdentiteVisuelleCourante();
    }

    private void SupprimerBulletinsSelection_Click(object sender, RoutedEventArgs e)
    {
        if (BulletinsGeneresDataGrid.SelectedItems == null || BulletinsGeneresDataGrid.SelectedItems.Count == 0)
        {
            NotifierInfo("Sélectionnez au moins un bulletin dans la liste.");
            return;
        }

        var selected = BulletinsGeneresDataGrid.SelectedItems
            .Cast<object>()
            .OfType<BulletinPaie>()
            .ToList();

        if (selected.Count == 0)
        {
            NotifierAvertissement("Aucun bulletin valide sélectionné.");
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Vous allez supprimer {selected.Count} bulletin(s).\n\nCette action est définitive. Continuer ?",
            "Confirmer la suppression en masse",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            var ids = selected.Select(x => x.Id).Distinct().ToList();
            var supprimes = 0;
            using (var db = new PaieDbContext())
            {
                var maintenance = new BulletinMaintenanceService(db);
                supprimes = maintenance.SupprimerBulletins(ids);
            }
            if (supprimes == 0)
            {
                NotifierInfo("Les bulletins sélectionnés n'existent plus.");
                _viewModel.ChargerTousBulletins();
                return;
            }

            _viewModel.ChargerTousBulletins();
            _viewModel.ChargerTableauDeBord();
            _viewModel.ChargerStatistiques();
            _viewModel.ChargerDeclarations();
            _viewModel.ChargerRapportPaie();
            _viewModel.ChargerBulletinsPeriodeCalculPaie();

            NotifierSucces($"{supprimes} bulletin(s) supprimé(s).");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Impossible de supprimer la sélection : {ex.Message}", "Suppression en masse", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static BulletinPaie? ChargerBulletinComplet(int bulletinId)
    {
        using var db = new PaieDbContext();
        return db.BulletinsPaie
            .Include(b => b.Employe).ThenInclude(e => e!.Departement)
            .Include(b => b.PeriodePaie)
            .Include(b => b.Details)
            .FirstOrDefault(b => b.Id == bulletinId);
    }

    private void OuvrirBulletin(BulletinPaie b)
    {
        var bulletin = ChargerBulletinComplet(b.Id);
        if (bulletin == null)
        {
            NotifierAvertissement("Bulletin introuvable.");
            return;
        }
        AfficherFenetreBulletin(bulletin);
    }

    private void TelechargerBulletinPdf(BulletinPaie b)
    {
        var bulletin = ChargerBulletinComplet(b.Id);
        if (bulletin == null)
        {
            NotifierAvertissement("Bulletin introuvable.");
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf|Tous les fichiers (*.*)|*.*",
            FileName = $"Bulletin_{bulletin.Employe?.Matricule}_{bulletin.PeriodePaie?.Mois}_{bulletin.PeriodePaie?.Annee}.pdf",
            DefaultExt = ".pdf"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var service = new ExportPdfService();
            service.ExporterBulletin(bulletin, dlg.FileName);
            NotifierSucces("Bulletin exporté en PDF.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur export PDF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TelechargerTousBulletinsPdf(List<BulletinPaie> bulletins)
    {
        if (bulletins == null || bulletins.Count == 0)
        {
            NotifierInfo("Aucun bulletin à exporter.");
            return;
        }

        var dlg = new OpenFolderDialog
        {
            Title = "Choisissez le dossier de destination des bulletins PDF"
        };

        if (dlg.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dlg.FolderName))
            return;

        try
        {
            var service = new ExportPdfService();
            var succes = 0;
            var erreurs = new List<string>();

            foreach (var item in bulletins.OrderBy(b => b.PeriodePaie?.Annee).ThenBy(b => b.PeriodePaie?.Mois).ThenBy(b => b.Employe?.Matricule))
            {
                var bulletin = ChargerBulletinComplet(item.Id);
                if (bulletin == null)
                {
                    erreurs.Add($"ID {item.Id} introuvable");
                    continue;
                }

                var fileName = $"Bulletin_{SanitizeFileName(bulletin.Employe?.Matricule ?? "NA")}_{bulletin.PeriodePaie?.Mois:D2}_{bulletin.PeriodePaie?.Annee}_{SanitizeFileName(bulletin.NumeroBulletin ?? bulletin.Id.ToString())}.pdf";
                var outputPath = Path.Combine(dlg.FolderName, fileName);
                service.ExporterBulletin(bulletin, outputPath);
                succes++;
            }

            if (erreurs.Count == 0)
            {
                NotifierSucces($"{succes} bulletin(s) exporté(s).");
            }
            else
            {
                var resumeErreurs = string.Join(Environment.NewLine, erreurs.Take(10));
                if (erreurs.Count > 10)
                    resumeErreurs += $"{Environment.NewLine}... ({erreurs.Count - 10} autres erreurs)";

                NotifierAvertissement($"{succes} bulletin(s) exporté(s), {erreurs.Count} erreur(s).");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur export groupé", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "NA";

        var clean = value.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            clean = clean.Replace(c, '_');
        return clean;
    }

    private void AfficherFenetreBulletin(BulletinPaie bulletin)
    {
        var bulletinView = new BulletinView { DataContext = bulletin };

        var printButton = new System.Windows.Controls.Button
        {
            Content = "Imprimer",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6, 12, 6)
        };

        var exportButton = new System.Windows.Controls.Button
        {
            Content = "Exporter en PDF",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6, 12, 6)
        };

        var emailButton = new System.Windows.Controls.Button
        {
            Content = "Envoyer par e-mail",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6, 12, 6)
        };

        var closeButton = new System.Windows.Controls.Button
        {
            Content = "Fermer",
            Padding = new Thickness(12, 6, 12, 6)
        };

        var buttonsPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 8)
        };
        buttonsPanel.Children.Add(printButton);
        buttonsPanel.Children.Add(exportButton);
        buttonsPanel.Children.Add(emailButton);
        buttonsPanel.Children.Add(closeButton);

        var grid = new System.Windows.Controls.Grid();
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        System.Windows.Controls.Grid.SetRow(buttonsPanel, 0);
        System.Windows.Controls.Grid.SetRow(bulletinView, 1);
        grid.Children.Add(buttonsPanel);
        grid.Children.Add(bulletinView);

        var win = new Window
        {
            Owner = this,
            Title = "Bulletin de paie",
            Content = grid,
            Width = 800,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        printButton.Click += (_, _) =>
        {
            try
            {
                if (ImpressionBulletinService.Imprimer(bulletin, win))
                    UiFeedback.Succes("Bulletin envoyé à l'imprimante.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(win, ex.Message, "Erreur impression", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        exportButton.Click += (_, _) =>
        {
            var dlg = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"Bulletin_{bulletin.Employe?.Matricule}_{bulletin.PeriodePaie?.Mois}_{bulletin.PeriodePaie?.Annee}.pdf"
            };

            if (dlg.ShowDialog(win) == true)
            {
                try
                {
                    var service = new ExportPdfService();
                    service.ExporterBulletin(bulletin, dlg.FileName);
                    UiFeedback.Succes("Bulletin exporté en PDF.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(win, ex.Message, "Erreur export PDF", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        };

        emailButton.Click += (_, _) =>
        {
            try
            {
                var emailWin = new EnvoiEmailBulletinWindow(bulletin) { Owner = win };
                emailWin.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(win, ex.Message, "Erreur envoi e-mail", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        closeButton.Click += (_, _) => win.Close();

        win.ShowDialog();
    }

    private void ImpactEntreprisesSite_Click(object sender, RoutedEventArgs e) =>
        BrowseUriHelper.OpenImpactEntreprises();

    private async Task ProposerMiseAJourAuDemarrageAsync()
    {
        try
        {
            var config = Helpers.UpdateConfigHelper.Charger();
            if (!config.VerifierAuDemarrage)
                return;

            var result = await ApplicationUpdateService.VerifierAsync().ConfigureAwait(true);
            if (result.Kind != UpdateCheckResultKind.UpdateAvailable)
                return;

            var reponse = MessageBox.Show(
                this,
                $"{result.Message}\n\nOuvrir l'assistant de téléchargement maintenant ?",
                "Mise à jour disponible",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (reponse == MessageBoxResult.Yes)
                MiseAJourWindow.Afficher(this);
        }
        catch
        {
            // Ne pas bloquer le démarrage si le serveur de mises à jour est injoignable.
        }
    }
}
