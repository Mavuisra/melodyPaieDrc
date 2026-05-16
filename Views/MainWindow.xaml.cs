using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.ViewModels;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private SuiviJournalierWindow? _suiviJournalierWindow;

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
        _viewModel.OnOuvrirParametresIpr = OuvrirParametresIpr;
        _viewModel.OnOuvrirTauxSociaux = OuvrirTauxSociaux;
        _viewModel.OnOuvrirPeriodesPaie = OuvrirPeriodesPaie;
        _viewModel.OnOuvrirInfosEntreprise = OuvrirInfosEntreprise;
        _viewModel.OnOuvrirConfigPrimesIndemnites = OuvrirConfigPrimesIndemnites;
        _viewModel.OnOuvrirEtablissementsDepartements = OuvrirEtablissementsDepartements;
        _viewModel.OnImporterFicheSalaireExcel = ImporterFicheSalaireExcel;
        _viewModel.OnOuvrirGestionUtilisateurs = OuvrirGestionUtilisateurs;
        _viewModel.OnSauvegarderBase = SauvegarderBase;
        _viewModel.OnRestaurerBase = RestaurerBase;
        _viewModel.OnReinitialiserApplication = ReinitialiserApplication;
        _viewModel.OnOuvrirCalendrierTravail = OuvrirCalendrierTravail;
        _viewModel.OnOuvrirSaisiePaieMois = OuvrirSaisiePaieMois;
        _viewModel.OnOuvrirSuiviJournalier = OuvrirSuiviJournalier;
        _viewModel.OnOuvrirChampsComplementairesEmploye = OuvrirChampsComplementairesEmploye;
        _viewModel.OnOuvrirFormulairesDynamiques = () => DynamicFormNavigator.OuvrirGestionnaireDefinitions(this);
        _viewModel.OnOuvrirChampsComplementairesEntreprise = () => DynamicFormNavigator.OuvrirChampsComplementairesEntreprise(this);
        _viewModel.OnErreurCalculPaie = msg => MessageBox.Show(msg, "Calcul de paie", MessageBoxButton.OK, MessageBoxImage.Warning);
        _viewModel.OnSuccessCalculPaie = msg => MessageBox.Show(msg, "Calcul de paie", MessageBoxButton.OK, MessageBoxImage.Information);
        _viewModel.OnSuccesTauxChange = msg => MessageBox.Show(this, msg, "Taux de change", MessageBoxButton.OK, MessageBoxImage.Information);
        _viewModel.OnErreurTauxChange = msg => MessageBox.Show(this, msg, "Taux de change", MessageBoxButton.OK, MessageBoxImage.Warning);
        _viewModel.OnMessageZkSettings = msg => MessageBox.Show(this, msg, "Paramètres ZKTeco", MessageBoxButton.OK, MessageBoxImage.Information);
        _viewModel.OnErreurZkSettings = msg => MessageBox.Show(this, msg, "Paramètres ZKTeco", MessageBoxButton.OK, MessageBoxImage.Warning);
        _viewModel.OnVoirBulletin = OuvrirBulletin;
        _viewModel.OnTelechargerBulletin = TelechargerBulletinPdf;
        _viewModel.OnTelechargerTousBulletins = TelechargerTousBulletinsPdf;
        _viewModel.OnExporterFichier = ExporterFichier;
        _viewModel.OnExporterLivrePaiePdf = ExporterLivrePaiePdf;
        _viewModel.OnExporterLivrePaieExcel = ExporterLivrePaieExcel;
        _viewModel.OnExporterDeclarationCnssExcel = ExporterDeclarationCnssExcel;
        _viewModel.OnExporterDeclarationIprExcel = ExporterDeclarationIprExcel;
        _viewModel.OnExporterRapportPaieExcel = ExporterRapportPaieExcel;
        DataContext = _viewModel;
        Loaded += (_, _) =>
        {
            _viewModel.ChargerDonnees();
            ZktecoSynchronisationService.Reconfigurer();
            AfficherTableauDeBordEnPremier();
        };
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.MenuSelectionne) && _viewModel.MenuSelectionne == 0)
            AfficherTableauDeBordEnPremier();
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
            MessageBox.Show(this, msg, "Import fiche salaire", MessageBoxButton.OK,
                r.EmployesCrees > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            _viewModel.ChargerEmployes();
            _viewModel.ChargerStatistiques();
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
            MessageBox.Show(this, "Fichier enregistré.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
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
            MessageBox.Show(this, "Livre de paie exporté en PDF.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
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
            MessageBox.Show(this, "Livre de paie exporté en Excel.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
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
            MessageBox.Show(this, "Rapport de paie exporté en Excel.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
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
            "- L'application redémarrera comme au premier lancement (structure minimale + utilisateur admin/admin).\n\n" +
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
        _suiviJournalierWindow.Closed += (_, _) => _suiviJournalierWindow = null;
        _suiviJournalierWindow.Show();
    }

    private void OuvrirSaisiePaieMois(int periodePaieId)
    {
        var win = new SaisiePaieMoisWindow(periodePaieId) { Owner = this };
        win.ShowDialog();
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
            MessageBox.Show(this, "Déclaration CNSS exportée en Excel.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
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
            MessageBox.Show(this, "Déclaration IPR exportée en Excel.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
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
            _viewModel.ChargerEmployes();
    }

    private void OuvrirModifierEmploye(int employeId)
    {
        var win = new EmployeWindow(employeId) { Owner = this };
        if (win.ShowDialog() == true)
            _viewModel.ChargerEmployes();
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
            _viewModel.ChargerEmployes();
    }

    private void OuvrirGestionUtilisateurs()
    {
        var win = new UtilisateursWindow { Owner = this };
        win.ShowDialog();
    }

    private void SauvegarderBase()
    {
        var dbPath = PaieDbContext.GetDatabasePath();
        if (!File.Exists(dbPath))
        {
            MessageBox.Show(this, "Aucun fichier de base trouvé.", "Sauvegarde", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            File.Copy(dbPath, dlg.FileName, overwrite: true);
            MessageBox.Show(this, "Sauvegarde effectuée : " + dlg.FileName, "Sauvegarde", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur sauvegarde", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestaurerBase()
    {
        var result = MessageBox.Show(this,
            "La base actuelle sera remplacée par la sauvegarde. L'application va se fermer puis rouvrir.\n\nContinuer ?",
            "Restaurer la base",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        var dlg = new OpenFileDialog
        {
            Filter = "Base SQLite (*.db)|*.db|Tous les fichiers (*.*)|*.*",
            Title = "Choisir le fichier de sauvegarde"
        };
        if (dlg.ShowDialog(this) != true) return;
        if (!File.Exists(dlg.FileName))
        {
            MessageBox.Show(this, "Fichier introuvable.", "Restauration", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            var args = Environment.GetCommandLineArgs();
            var exe = Environment.ProcessPath ?? (args.Length > 0 ? args[0] : null);
            if (string.IsNullOrEmpty(exe))
            {
                MessageBox.Show(this, "Impossible de déterminer le chemin de l'application.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show(this, "Impossible de lancer la restauration : " + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        win.ShowDialog();
        _viewModel.ChargerPeriodes();
    }

    private void OuvrirInfosEntreprise()
    {
        var win = new EntrepriseWindow { Owner = this };
        win.ShowDialog();
    }

    private void SupprimerBulletinsSelection_Click(object sender, RoutedEventArgs e)
    {
        if (BulletinsGeneresDataGrid.SelectedItems == null || BulletinsGeneresDataGrid.SelectedItems.Count == 0)
        {
            MessageBox.Show(this, "Sélectionnez au moins un bulletin dans la liste.", "Suppression en masse", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selected = BulletinsGeneresDataGrid.SelectedItems
            .Cast<object>()
            .OfType<BulletinPaie>()
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Aucun bulletin valide sélectionné.", "Suppression en masse", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show(this, "Les bulletins sélectionnés n'existent plus.", "Suppression en masse", MessageBoxButton.OK, MessageBoxImage.Information);
                _viewModel.ChargerTousBulletins();
                return;
            }

            _viewModel.ChargerTousBulletins();
            _viewModel.ChargerTableauDeBord();
            _viewModel.ChargerStatistiques();
            _viewModel.ChargerDeclarations();
            _viewModel.ChargerRapportPaie();
            _viewModel.ChargerBulletinsPeriodeCalculPaie();

            MessageBox.Show(this, $"{supprimes} bulletin(s) supprimé(s) avec succès.", "Suppression en masse", MessageBoxButton.OK, MessageBoxImage.Information);
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
            MessageBox.Show(this, "Bulletin introuvable.", "Bulletin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        AfficherFenetreBulletin(bulletin);
    }

    private void TelechargerBulletinPdf(BulletinPaie b)
    {
        var bulletin = ChargerBulletinComplet(b.Id);
        if (bulletin == null)
        {
            MessageBox.Show(this, "Bulletin introuvable.", "Téléchargement", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show(this, "Bulletin exporté en PDF.", "Téléchargement", MessageBoxButton.OK, MessageBoxImage.Information);
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
            MessageBox.Show(this, "Aucun bulletin à exporter.", "Export groupé", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show(this, $"{succes} bulletin(s) exporté(s) dans :\n{dlg.FolderName}", "Export groupé", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var resumeErreurs = string.Join(Environment.NewLine, erreurs.Take(10));
                if (erreurs.Count > 10)
                    resumeErreurs += $"{Environment.NewLine}... ({erreurs.Count - 10} autres erreurs)";

                MessageBox.Show(this,
                    $"{succes} bulletin(s) exporté(s).{Environment.NewLine}{erreurs.Count} erreur(s).{Environment.NewLine}{resumeErreurs}",
                    "Export groupé",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
                    MessageBox.Show(win, "Bulletin envoyé à l'imprimante.", "Impression", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show(win, "Bulletin exporté en PDF.", "Export PDF", MessageBoxButton.OK, MessageBoxImage.Information);
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
}
