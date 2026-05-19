using System.IO;
using System.Text.Json;
using MelodyPaieRDC.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>Sauvegarde et restauration SQLite (cohérence WAL, validation, sécurité).</summary>
public static class DatabaseBackupService
{
    private const string FichierEtatRestauration = "restauration_pending.json";

    /// <summary>Tables minimales pour reconnaître une base Melody Paie (restauration).</summary>
    private static readonly string[] TablesObligatoires =
    [
        "Entreprises",
        "Employes",
        "Utilisateurs"
    ];

    public sealed record ValidationResult(bool EstValide, string? MessageErreur);

    public sealed record RestaurationResultat(
        string CheminBackup,
        string? CheminSecurite,
        DateTime HorodatageBackup,
        long TailleOctets);

    public sealed record EtatRestaurationAffichage(
        string CheminBackup,
        string NomFichierBackup,
        DateTime HorodatageBackup,
        string? NomFichierSecurite,
        DateTime RestaureLe);

    /// <summary>Met à jour le schéma de la base active avant export (tables manquantes sur anciennes bases).</summary>
    public static void AssurerSchemaAvantSauvegarde()
    {
        if (!EstIntegriteValide(PaieDbContext.GetDatabasePath()))
            throw new InvalidOperationException("La base actuelle est endommagée ; sauvegarde annulée pour éviter d'aggraver le problème.");

        using var db = new PaieDbContext();
        SchemaSqliteApplicator.AjouterTableUtilisateursSiNecessaire(db);
        SchemaSqliteApplicator.AjouterTableParametresApplicationSiNecessaire(db);
        SchemaSqliteApplicator.AjouterColonnesParametresZktecoSiNecessaire(db);
        SchemaSqliteApplicatorExtensible.AppliquerSiNecessaire(db);
        ParametresApplicationHelper.EnsureRow(db);
        db.SaveChanges();
    }

    /// <summary>Vérifie l'intégrité SQLite (PRAGMA integrity_check).</summary>
    public static bool EstIntegriteValide(string cheminBase)
    {
        if (!File.Exists(cheminBase))
            return true;

        try
        {
            using var conn = new SqliteConnection($"Data Source={cheminBase}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check";
            var result = cmd.ExecuteScalar()?.ToString()?.Trim();
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool EstErreurBaseCorrompue(Exception ex) =>
        ex is SqliteException { SqliteErrorCode: 11 }
        || ex.Message.Contains("malformed", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("corrupt", StringComparison.OrdinalIgnoreCase)
        || (ex.InnerException != null && EstErreurBaseCorrompue(ex.InnerException));

    /// <summary>Exporte une copie cohérente via l'API Backup SQLite (sûr pendant que l'app utilise la base).</summary>
    public static void ExporterCopieCoherente(string cheminSource, string cheminDestination)
    {
        if (!File.Exists(cheminSource))
            throw new FileNotFoundException("Base introuvable.", cheminSource);

        if (!EstIntegriteValide(cheminSource))
            throw new InvalidOperationException("La base source est endommagée (fichier SQLite illisible).");

        var repertoire = Path.GetDirectoryName(cheminDestination);
        if (!string.IsNullOrEmpty(repertoire) && !Directory.Exists(repertoire))
            Directory.CreateDirectory(repertoire);

        if (File.Exists(cheminDestination))
            File.Delete(cheminDestination);

        SqliteConnection.ClearAllPools();
        ExecuterCheckpointWal(cheminSource);

        using var source = new SqliteConnection($"Data Source={cheminSource}");
        using var dest = new SqliteConnection($"Data Source={cheminDestination}");
        source.Open();
        dest.Open();
        source.BackupDatabase(dest);
        SupprimerFichiersAuxiliairesSqlite(cheminDestination);
    }

    /// <summary>Restaure la dernière copie valide du dossier Data (écrase PaieRDC.db sans déplacement préalable).</summary>
    public static bool TenterRecuperationAutomatique(
        string cheminBaseCible,
        out string messageUtilisateur,
        out string? cheminCopieTrouvee)
    {
        messageUtilisateur = "";
        cheminCopieTrouvee = null;
        var dataDir = Path.GetDirectoryName(cheminBaseCible) ?? PaieDbContext.GetDataDirectory();
        var copie = TrouverDerniereCopieRecuperable(dataDir);
        if (copie == null)
        {
            messageUtilisateur =
                "Aucune copie automatique valide trouvée dans le dossier des données.\n" +
                $"({dataDir})\n\nChoisissez un fichier .db de sauvegarde manuellement.";
            return false;
        }

        cheminCopieTrouvee = copie;
        try
        {
            var archive = CopierAncienneBaseCorrompueSiPossible(cheminBaseCible, dataDir);
            RestaurerDepuisBackup(copie, cheminBaseCible);
            messageUtilisateur = $"Base récupérée depuis :\n{Path.GetFileName(copie)}";
            if (!string.IsNullOrEmpty(archive))
                messageUtilisateur += $"\n\nAncienne base conservée : {archive}";
            return true;
        }
        catch (Exception ex)
        {
            messageUtilisateur =
                $"Copie trouvée ({Path.GetFileName(copie)}) mais restauration impossible.\n\n{FormaterErreurFichierUtilise(ex)}";
            return false;
        }
    }

    /// <summary>Relance Melody avec restauration au démarrage (fichier non verrouillé par ce processus).</summary>
    public static bool RelancerApplicationAvecRestauration(string cheminBackup)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            return false;

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--restore-from \"{cheminBackup}\"",
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(startInfo);
        return true;
    }

    public static string FormaterErreurFichierUtilise(Exception ex)
    {
        if (ex is IOException or UnauthorizedAccessException
            || ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("utilisé par un autre processus", StringComparison.OrdinalIgnoreCase))
        {
            return "Le fichier de base est utilisé par un autre programme.\n\n" +
                   "1. Fermez toutes les fenêtres Melody Paie RDC\n" +
                   "2. Fermez « ZktecoPullWorker » dans le Gestionnaire des tâches si présent\n" +
                   "3. Relancez Melody Paie\n\n" +
                   "Vous pouvez aussi choisir « Oui » pour relancer l'application et restaurer automatiquement.";
        }

        return ex.Message;
    }

    /// <summary>Dernier fichier .db valide (sauvegardes auto avant restauration, backups, etc.).</summary>
    public static string? TrouverDerniereCopieRecuperable(string dataDir)
    {
        if (!Directory.Exists(dataDir))
            return null;

        var candidats = Directory.EnumerateFiles(dataDir, "*.db", SearchOption.TopDirectoryOnly)
            .Where(f =>
            {
                var nom = Path.GetFileName(f);
                return !string.Equals(nom, "PaieRDC.db", StringComparison.OrdinalIgnoreCase)
                       && !nom.StartsWith("PaieRDC_corrompu_", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        foreach (var chemin in candidats)
        {
            if (!EstIntegriteValide(chemin))
                continue;
            var validation = ValiderFichierBackup(chemin);
            if (validation.EstValide)
                return chemin;
        }

        return null;
    }

    public static bool TablePresenteDansFichier(string cheminFichier, string nomTable)
    {
        if (!File.Exists(cheminFichier))
            return false;
        try
        {
            using var conn = new SqliteConnection($"Data Source={cheminFichier};Mode=ReadOnly");
            conn.Open();
            return TableSqliteExiste(conn, nomTable);
        }
        catch
        {
            return false;
        }
    }

    public static ValidationResult ValiderFichierBackup(string cheminBackup)
    {
        if (string.IsNullOrWhiteSpace(cheminBackup) || !File.Exists(cheminBackup))
            return new ValidationResult(false, "Fichier introuvable.");

        var info = new FileInfo(cheminBackup);
        if (info.Length < 4096)
            return new ValidationResult(false, "Le fichier est trop petit pour être une base Melody Paie valide.");

        if (!string.Equals(info.Extension, ".db", StringComparison.OrdinalIgnoreCase))
            return new ValidationResult(false, "Choisissez un fichier SQLite (.db).");

        try
        {
            using var conn = new SqliteConnection($"Data Source={cheminBackup};Mode=ReadOnly");
            conn.Open();

            foreach (var table in TablesObligatoires)
            {
                if (!TableSqliteExiste(conn, table))
                    return new ValidationResult(false,
                        $"Ce fichier n'est pas une base Melody Paie reconnue (table « {table} » absente).");
            }

            return new ValidationResult(true, null);
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Fichier SQLite invalide ou corrompu : {ex.Message}");
        }
    }

    /// <summary>Restaure la sauvegarde sur la base active (à appeler au démarrage, avant EF).</summary>
    public static RestaurationResultat RestaurerDepuisBackup(string cheminBackup, string cheminBaseCible)
    {
        var validation = ValiderFichierBackup(cheminBackup);
        if (!validation.EstValide)
            throw new InvalidOperationException(validation.MessageErreur ?? "Sauvegarde invalide.");

        var dataDir = Path.GetDirectoryName(cheminBaseCible) ?? PaieDbContext.GetDataDirectory();
        if (!Directory.Exists(dataDir))
            Directory.CreateDirectory(dataDir);

        string? cheminSecurite = null;
        if (File.Exists(cheminBaseCible) && EstIntegriteValide(cheminBaseCible))
        {
            cheminSecurite = Path.Combine(dataDir,
                $"PaieRDC_avant_restauration_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            try
            {
                ExporterCopieCoherente(cheminBaseCible, cheminSecurite);
            }
            catch
            {
                cheminSecurite = null;
            }
        }

        LibererConnexionsSqliteLocales();
        SupprimerFichiersAuxiliairesSqlite(cheminBaseCible);
        CopierFichierAvecRetry(cheminBackup, cheminBaseCible, overwrite: true);
        SupprimerFichiersAuxiliairesSqlite(cheminBaseCible);

        if (!EstIntegriteValide(cheminBaseCible))
            throw new InvalidOperationException("La restauration s'est terminée mais la base reste illisible.");

        var horodatage = File.GetLastWriteTime(cheminBackup);
        return new RestaurationResultat(cheminBackup, cheminSecurite, horodatage, new FileInfo(cheminBackup).Length);
    }

    public static void EnregistrerRestaurationPourAffichage(RestaurationResultat resultat)
    {
        var etat = new EtatRestaurationAffichage(
            resultat.CheminBackup,
            Path.GetFileName(resultat.CheminBackup),
            resultat.HorodatageBackup,
            resultat.CheminSecurite != null ? Path.GetFileName(resultat.CheminSecurite) : null,
            DateTime.Now);

        var chemin = CheminFichierEtatRestauration();
        var json = JsonSerializer.Serialize(etat);
        File.WriteAllText(chemin, json);
    }

    public static EtatRestaurationAffichage? ConsommerRestaurationEnAttente()
    {
        var chemin = CheminFichierEtatRestauration();
        if (!File.Exists(chemin))
            return null;

        try
        {
            var json = File.ReadAllText(chemin);
            var etat = JsonSerializer.Deserialize<EtatRestaurationAffichage>(json);
            File.Delete(chemin);
            return etat;
        }
        catch
        {
            try { File.Delete(chemin); } catch { /* ignore */ }
            return null;
        }
    }

    public static string FormaterMessageBandeau(EtatRestaurationAffichage etat)
    {
        var dateBackup = etat.HorodatageBackup.ToString("dd/MM/yyyy HH:mm");
        var secours = string.IsNullOrEmpty(etat.NomFichierSecurite)
            ? ""
            : $" — copie de sécurité : {etat.NomFichierSecurite}";
        return $"Restauration terminée ({etat.NomFichierBackup}, backup du {dateBackup}){secours}.";
    }

    private static string CheminFichierEtatRestauration() =>
        Path.Combine(PaieDbContext.GetDataDirectory(), FichierEtatRestauration);

    private static void ExecuterCheckpointWal(string cheminBase)
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={cheminBase}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            /* meilleur effort */
        }
    }

    private static bool TableSqliteExiste(SqliteConnection conn, string nomTable)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name COLLATE NOCASE LIMIT 1";
        cmd.Parameters.AddWithValue("$name", nomTable);
        return cmd.ExecuteScalar() != null;
    }

    public static void LibererConnexionsSqliteLocales()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        SqliteConnection.ClearAllPools();
    }

    private static void CopierFichierAvecRetry(string source, string destination, bool overwrite)
    {
        const int maxEssais = 8;
        Exception? derniere = null;

        for (var essai = 0; essai < maxEssais; essai++)
        {
            try
            {
                LibererConnexionsSqliteLocales();
                File.Copy(source, destination, overwrite);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                derniere = ex;
                Thread.Sleep(250 + essai * 150);
            }
        }

        throw new IOException(FormaterErreurFichierUtilise(derniere ?? new IOException("Accès refusé.")), derniere);
    }

    /// <summary>Copie best-effort de la base actuelle avant écrasement (ne bloque pas si verrouillée).</summary>
    private static string? CopierAncienneBaseCorrompueSiPossible(string cheminBaseCible, string dataDir)
    {
        if (!File.Exists(cheminBaseCible))
            return null;

        var archive = Path.Combine(dataDir, $"PaieRDC_corrompu_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        try
        {
            File.Copy(cheminBaseCible, archive, overwrite: false);
            return Path.GetFileName(archive);
        }
        catch
        {
            return null;
        }
    }

    private static void DeplacerFichierBaseEtAuxiliaires(string cheminSource, string cheminDestination)
    {
        if (File.Exists(cheminDestination))
            File.Delete(cheminDestination);

        File.Move(cheminSource, cheminDestination);
        foreach (var suffixe in new[] { "-wal", "-shm", "-journal" })
        {
            var src = cheminSource + suffixe;
            if (!File.Exists(src)) continue;
            var dst = cheminDestination + suffixe;
            if (File.Exists(dst)) File.Delete(dst);
            try { File.Move(src, dst); } catch { /* ignore */ }
        }
    }

    private static void SupprimerFichiersAuxiliairesSqlite(string cheminBase)
    {
        foreach (var suffixe in new[] { "-wal", "-shm", "-journal" })
        {
            var p = cheminBase + suffixe;
            if (File.Exists(p))
            {
                try { File.Delete(p); } catch { /* ignore */ }
            }
        }
    }
}
