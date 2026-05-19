using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

namespace MelodyPaieRDC;

public partial class App : Application
{
    public App()
    {
        // Afficher toute exception non gérée pour éviter que l'app se ferme sans message
        DispatcherUnhandledException += (_, args) =>
        {
            StartupLog.Append("DispatcherUnhandledException", args.Exception);
            MessageBox.Show(
                $"{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                StartupLog.Append("UnhandledException (AppDomain)", ex);
                MessageBox.Show($"{ex.Message}\n\n{ex.StackTrace}", "Erreur fatale", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        StartupLog.Append("Démarrage : Application_Startup (tfm net8-windows, self-contained attendu côté déploiement)");

        // L'app ne s'arrête que lorsque la fenêtre principale (MainWindow) est fermée, pas à la fermeture du login.
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Licence QuestPDF (gratuite pour usage communautaire / open source)
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.EnableDebugging = false;

        Views.SplashScreenWindow? splash = null;
        var splashStart = DateTime.UtcNow;

        try
        {
            splash = new Views.SplashScreenWindow();
            splash.Show();
            StartupLog.Append("Splash affiché");

            var dataDir = PaieDbContext.GetDataDirectory();
            var dbPath = Path.Combine(dataDir, "PaieRDC.db");
            StartupLog.Append($"Dossier données : {dataDir}");

            // Restauration depuis une sauvegarde (argument --restore-from "chemin")
            var args = Environment.GetCommandLineArgs();
            for (var i = 1; i < args.Length - 1; i++)
            {
                if (args[i] != "--restore-from" || string.IsNullOrWhiteSpace(args[i + 1])) continue;
                var backupPath = args[i + 1].Trim().Trim('"');
                var validation = DatabaseBackupService.ValiderFichierBackup(backupPath);
                if (!validation.EstValide)
                {
                    splash?.Close();
                    MessageBox.Show(validation.MessageErreur ?? "Sauvegarde invalide.", "Restauration",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    Shutdown(1);
                    return;
                }

                try
                {
                    var resultat = DatabaseBackupService.RestaurerDepuisBackup(backupPath, dbPath);
                    DatabaseBackupService.EnregistrerRestaurationPourAffichage(resultat);
                    StartupLog.Append($"Restauration effectuée depuis {backupPath}");
                }
                catch (Exception ex)
                {
                    splash?.Close();
                    StartupLog.Append("Échec restauration", ex);
                    MessageBox.Show($"Restauration impossible : {ex.Message}", "Restauration",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                    return;
                }

                break;
            }

            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);

            FormDefinitionLoader.AssurerDossierEtModelesParDefaut();

            if (!GererBaseCorrompueAuDemarrage(dbPath, ref splash, ref splashStart))
            {
                Shutdown(1);
                return;
            }

            try
            {
                AssurerBaseEtDonneesInitiales(dbPath);
            }
            catch (Exception ex) when (DatabaseBackupService.EstErreurBaseCorrompue(ex))
            {
                StartupLog.Append("Base corrompue lors de l'initialisation", ex);
                if (!GererBaseCorrompueAuDemarrage(dbPath, ref splash, ref splashStart))
                {
                    Shutdown(1);
                    return;
                }

                AssurerBaseEtDonneesInitiales(dbPath);
            }

            StartupLog.Append("Base SQLite initialisée");

            // Splash minimum sans Thread.Sleep sur le thread UI (évite « ne répond pas » / gel perçu).
            var elapsed = DateTime.UtcNow - splashStart;
            var minimum = TimeSpan.FromSeconds(2);
            if (elapsed < minimum)
                AttendreAvecPompageMessages(minimum - elapsed);

            // Créer la fenêtre principale d'abord et la définir comme MainWindow pour que
            // la fermeture de la fenêtre de login ne déclenche pas l'arrêt de l'application.
            var mainWin = new Views.MainWindow();
            MainWindow = mainWin;
            StartupLog.Append("MainWindow créée (avant login)");

            // Le splash a servi uniquement pendant l'initialisation (base / schéma).
            splash?.Close();

            // Parcours « grandes applications » : configurer le tenant AVANT la connexion et l'usage métier.
            var assistantAffiche = false;
            using (var dbCtx = new PaieDbContext())
            {
                ContexteEntrepriseService.InitialiserDepuisBase(dbCtx);
                EntrepriseBrandingService.AppliquerIdentiteVisuelleGlobale();
                if (ContexteEntrepriseService.EntrepriseCouranteId is int tenantId && tenantId > 0)
                    dbCtx.SetTenant(tenantId);
                if (ConfigurationEntrepriseService.DoitAfficherAssistantAuDemarrage(dbCtx))
                {
                    var raison = ConfigurationEntrepriseService.RaisonAffichageAssistant(dbCtx);
                    StartupLog.Append($"Affichage assistant avant connexion ({raison})");
                    var configWin = new Views.AssistantConfigurationWindow
                    {
                        Topmost = true,
                        Title = "Configuration — étape 1 sur 2 (entreprise)"
                    };
                    assistantAffiche = true;
                    if (configWin.ShowDialog() != true)
                    {
                        StartupLog.Append("Fermeture : configuration initiale non terminée");
                        Shutdown(0);
                        return;
                    }
                    ConfigurationEntrepriseService.MarquerConfigurationTerminee(dbCtx);
                    StartupLog.Append("Configuration initiale terminée");
                }
                else
                {
                    StartupLog.Append("Configuration complète : assistant ignoré au démarrage");
                }
            }

            using (var dbAuth = new PaieDbContext())
            {
                if (!AuthService.AdministrateurActifExiste(dbAuth))
                {
                    MessageBox.Show(
                        "Aucun compte administrateur n'est configuré.\n\nRelancez l'application et terminez l'assistant de configuration (étape « Compte administrateur »).",
                        "Connexion impossible",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    Shutdown(0);
                    return;
                }
            }

            // Connexion une fois l'environnement (entreprise / politique paie) prêt.
            var loginWin = new Views.LoginWindow
            {
                Title = assistantAffiche
                    ? "Étape 2/2 — Connexion"
                    : "Connexion - Melody Paie RDC"
            };
            if (loginWin.ShowDialog() != true)
            {
                StartupLog.Append("Fermeture : écran de connexion annulé ou identifiants non validés (comportement normal si l'utilisateur ferme la fenêtre)");
                Shutdown(0);
                return;
            }

            mainWin.Show();
            StartupLog.Append("Ouverture réussie après connexion");
        }
        catch (Exception ex)
        {
            splash?.Close();
            StartupLog.Append("Échec démarrage (voir exception)", ex);
            var logHint = StartupLog.CheminFichier();

            if (DatabaseBackupService.EstErreurBaseCorrompue(ex))
            {
                MessageBox.Show(
                    "La base de données est endommagée et Melody Paie ne peut pas démarrer.\n\n" +
                    $"Dossier des données :\n{PaieDbContext.GetDataDirectory()}\n\n" +
                    "Relancez l'application pour tenter une récupération automatique ou choisir un fichier .db de sauvegarde." +
                    (string.IsNullOrEmpty(logHint) ? "" : $"\n\nJournal : {logHint}"),
                    "Base endommagée",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                var detail = string.IsNullOrEmpty(logHint)
                    ? ex.StackTrace
                    : $"{ex.StackTrace}\n\nJournal : {logHint}";
                MessageBox.Show($"Démarrage : {ex.Message}\n\n{detail}", "Erreur", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            Shutdown(1);
        }
    }

    /// <summary>Base illisible : récupération auto, choix d'un backup, ou arrêt.</summary>
    private static bool GererBaseCorrompueAuDemarrage(
        string dbPath,
        ref Views.SplashScreenWindow? splash,
        ref DateTime splashStart)
    {
        if (!File.Exists(dbPath) || DatabaseBackupService.EstIntegriteValide(dbPath))
            return true;

        splash?.Close();
        StartupLog.Append("Détection base SQLite corrompue");

        if (DatabaseBackupService.TenterRecuperationAutomatique(dbPath, out var msgRecup, out var cheminCopieAuto))
        {
            MessageBox.Show(msgRecup, "Récupération automatique", MessageBoxButton.OK, MessageBoxImage.Information);
            splash = new Views.SplashScreenWindow();
            splash.Show();
            splashStart = DateTime.UtcNow;
            return true;
        }

        if (!string.IsNullOrEmpty(cheminCopieAuto) && ProposerRelanceAvecRestauration(cheminCopieAuto))
            return false;

        var reponse = MessageBox.Show(
            $"{msgRecup}\n\nVoulez-vous sélectionner un fichier de sauvegarde (.db) maintenant ?",
            "Base de données endommagée",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (reponse != MessageBoxResult.Yes)
        {
            MessageBox.Show(
                $"Relancez Melody Paie pour réessayer.\n\nDossier des données :\n{PaieDbContext.GetDataDirectory()}",
                "Arrêt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        var dlg = new OpenFileDialog
        {
            Title = "Restaurer Melody Paie depuis une sauvegarde",
            Filter = "Base SQLite (*.db)|*.db|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true
        };

        if (dlg.ShowDialog() != true)
            return false;

        var validation = DatabaseBackupService.ValiderFichierBackup(dlg.FileName);
        if (!validation.EstValide)
        {
            MessageBox.Show(validation.MessageErreur ?? "Sauvegarde invalide.", "Restauration",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        try
        {
            var dataDir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dataDir) && !Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);

            DatabaseBackupService.RestaurerDepuisBackup(dlg.FileName, dbPath);
            MessageBox.Show(
                $"Base restaurée depuis :\n{Path.GetFileName(dlg.FileName)}",
                "Restauration",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            splash = new Views.SplashScreenWindow();
            splash.Show();
            splashStart = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            if (ProposerRelanceAvecRestauration(dlg.FileName, ex))
                return false;

            MessageBox.Show($"Restauration impossible : {ex.Message}", "Erreur",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private static bool ProposerRelanceAvecRestauration(string cheminBackup, Exception? ex = null)
    {
        var detail = ex != null
            ? DatabaseBackupService.FormaterErreurFichierUtilise(ex)
            : "Melody va redémarrer et appliquer la sauvegarde.";

        var relancer = MessageBox.Show(
            $"{detail}\n\nRelancer Melody Paie maintenant pour restaurer automatiquement ?",
            "Fichier de base verrouillé",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (relancer != MessageBoxResult.Yes)
            return false;

        if (!DatabaseBackupService.RelancerApplicationAvecRestauration(cheminBackup))
        {
            MessageBox.Show("Impossible de relancer l'application.", "Erreur",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        Current.Shutdown(0);
        return true;
    }

    /// <summary>
    /// Attend tout en laissant la dispatcher traiter les messages (splash animé, OS « répond »).
    /// </summary>
    private static void AttendreAvecPompageMessages(TimeSpan duree)
    {
        if (duree <= TimeSpan.Zero) return;
        var frame = new DispatcherFrame();
        var fin = DateTime.UtcNow + duree;
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        timer.Tick += (_, _) =>
        {
            if (DateTime.UtcNow >= fin)
            {
                timer.Stop();
                frame.Continue = false;
            }
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    /// <summary>
    /// Crée la base si besoin ; si les tables n'existent pas, applique le schéma via SQL puis seed.
    /// </summary>
    private static void AssurerBaseEtDonneesInitiales(string dbPath)
    {
        using var db = new PaieDbContext();
        db.Database.EnsureCreated();
        SchemaSqliteApplicator.AjouterTableUtilisateursSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesIdentiteVisuelleSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonneNumeroBulletinSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesPrimesIndemnitesSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableAffectationsPrimesIndemnitesSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableEmployesLibellesBulletinSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableSaisiesPaieSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesSaisiesPaieAcomptesSanctionsSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonneNumCnssEmployeSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonneZkUserIdEmployeSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesEmployesReferencesFicheSalaireSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesEmployesCnssEdeclarationSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonneCotisationInppBulletinSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesContratsRemunerationAvanceeSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableJoursTravailCalendrierSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableSuivisJournaliersSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesSuiviJournalierPointagesSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableParametresApplicationSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesParametresZktecoSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableFormFieldValuesSiNecessaire(db);
        SchemaSqliteApplicatorExtensible.AppliquerSiNecessaire(db);
        ContexteEntrepriseService.InitialiserDepuisBase(db);
        if (ContexteEntrepriseService.EntrepriseCouranteId is int entrepriseId && entrepriseId > 0)
            db.SetTenant(entrepriseId);

        BackfillNumeroBulletinSiNecessaire(db);

        try
        {
            db.SeedSiVide();
            TenantDataBackfill.AppliquerSiNecessaire(db);
            ReinitialiserTenantApresBackfill(db);
            return;
        }
        catch (SqliteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            // Fichier .db existant mais sans tables : on crée les tables via script SQL (sans supprimer le fichier)
        }

        SchemaSqliteApplicator.AppliquerSchema(db);
        SchemaSqliteApplicator.AjouterColonnesIdentiteVisuelleSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableUtilisateursSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonneNumeroBulletinSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesPrimesIndemnitesSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableAffectationsPrimesIndemnitesSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableEmployesLibellesBulletinSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableSaisiesPaieSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesSaisiesPaieAcomptesSanctionsSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonneNumCnssEmployeSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonneZkUserIdEmployeSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesEmployesReferencesFicheSalaireSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesEmployesCnssEdeclarationSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonneCotisationInppBulletinSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesContratsRemunerationAvanceeSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableJoursTravailCalendrierSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableSuivisJournaliersSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesSuiviJournalierPointagesSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableParametresApplicationSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesParametresZktecoSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableFormFieldValuesSiNecessaire(db);
        SchemaSqliteApplicatorExtensible.AppliquerSiNecessaire(db);
        BackfillNumeroBulletinSiNecessaire(db);
        db.SeedSiVide();
        TenantDataBackfill.AppliquerSiNecessaire(db);
        ReinitialiserTenantApresBackfill(db);
    }

    private static void ReinitialiserTenantApresBackfill(PaieDbContext db)
    {
        ContexteEntrepriseService.InitialiserDepuisBase(db);
        if (ContexteEntrepriseService.EntrepriseCouranteId is int entrepriseId && entrepriseId > 0)
            db.SetTenant(entrepriseId);
    }

    /// <summary>
    /// Attribue un numéro unique (ex. 2025-03-001) aux bulletins qui n'en ont pas encore.
    /// </summary>
    private static void BackfillNumeroBulletinSiNecessaire(PaieDbContext db)
    {
        var sansNumero = db.BulletinsPaie
            .Include(b => b.PeriodePaie)
            .Where(b => string.IsNullOrEmpty(b.NumeroBulletin))
            .OrderBy(b => b.PeriodePaieId)
            .ThenBy(b => b.Id)
            .ToList();
        if (sansNumero.Count == 0) return;

        foreach (var groupe in sansNumero.GroupBy(b => b.PeriodePaieId))
        {
            var periode = groupe.First().PeriodePaie;
            var annee = periode?.Annee ?? DateTime.Today.Year;
            var mois = periode?.Mois ?? DateTime.Today.Month;
            var seq = 1;
            foreach (var b in groupe.OrderBy(x => x.Id))
            {
                b.NumeroBulletin = $"{annee}-{mois:D2}-{seq:D3}";
                seq++;
            }
        }
        db.SaveChanges();
    }
}
