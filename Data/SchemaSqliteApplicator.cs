using System.Data;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Data;

/// <summary>
/// Applique le schéma SQLite (CREATE TABLE / INDEX) quand EnsureCreated ne suffit pas
/// (fichier .db existant mais vide ou sans tables).
/// </summary>
public static class SchemaSqliteApplicator
{
    /// <summary>
    /// Ajoute les colonnes d'identité visuelle à la table Entreprises si elles n'existent pas.
    /// </summary>
    public static void AjouterColonnesIdentiteVisuelleSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        var columns = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Entreprises)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        void AddColumnIfMissing(string name, string sql)
        {
            if (columns.Contains(name)) return;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        AddColumnIfMissing("Nif", "ALTER TABLE Entreprises ADD COLUMN \"Nif\" TEXT");
        AddColumnIfMissing("CouleurPrincipale", "ALTER TABLE Entreprises ADD COLUMN \"CouleurPrincipale\" TEXT");
        AddColumnIfMissing("CouleurSecondaire", "ALTER TABLE Entreprises ADD COLUMN \"CouleurSecondaire\" TEXT");
        AddColumnIfMissing("Telephone", "ALTER TABLE Entreprises ADD COLUMN \"Telephone\" TEXT");
        AddColumnIfMissing("Email", "ALTER TABLE Entreprises ADD COLUMN \"Email\" TEXT");
        AddColumnIfMissing("SiteWeb", "ALTER TABLE Entreprises ADD COLUMN \"SiteWeb\" TEXT");
        AddColumnIfMissing("NumeroAffiliationCnss", "ALTER TABLE Entreprises ADD COLUMN \"NumeroAffiliationCnss\" TEXT");
    }

    /// <summary>
    /// Crée la table Utilisateurs si elle n'existe pas (multi-utilisateurs / rôles).
    /// </summary>
    public static void AjouterTableUtilisateursSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Utilisateurs'";
            var name = cmd.ExecuteScalar() as string;
            if (!string.IsNullOrEmpty(name)) return;
        }

        const string sql = @"CREATE TABLE IF NOT EXISTS ""Utilisateurs"" (
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""Login"" TEXT NOT NULL,
            ""MotDePasseHash"" TEXT NOT NULL,
            ""Salt"" TEXT NOT NULL,
            ""NomComplet"" TEXT,
            ""Role"" TEXT NOT NULL,
            ""Actif"" INTEGER NOT NULL,
            ""DateCreation"" TEXT NOT NULL
        )";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Utilisateurs_Login\" ON \"Utilisateurs\" (\"Login\")";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Ajoute la colonne NumeroBulletin à la table BulletinsPaie si elle n'existe pas.
    /// </summary>
    public static void AjouterColonneNumeroBulletinSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        var columns = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(BulletinsPaie)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        if (columns.Contains("NumeroBulletin")) return;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "ALTER TABLE BulletinsPaie ADD COLUMN \"NumeroBulletin\" TEXT";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Ajoute les colonnes avancées au référentiel PrimesIndemnites si elles n'existent pas
    /// (ModeCalcul, TypeLigne, NumeroCompte).
    /// </summary>
    public static void AjouterColonnesPrimesIndemnitesSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        var columns = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(PrimesIndemnites)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        void AddColumnIfMissing(string name, string sql)
        {
            if (columns.Contains(name)) return;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        AddColumnIfMissing("ModeCalcul", "ALTER TABLE PrimesIndemnites ADD COLUMN \"ModeCalcul\" TEXT NOT NULL DEFAULT 'FIXE'");
        AddColumnIfMissing("TypeLigne", "ALTER TABLE PrimesIndemnites ADD COLUMN \"TypeLigne\" TEXT NOT NULL DEFAULT 'A'");
        AddColumnIfMissing("NumeroCompte", "ALTER TABLE PrimesIndemnites ADD COLUMN \"NumeroCompte\" TEXT");
    }

    /// <summary>
    /// Ajoute la colonne NumCnss à la table Employes si elle n'existe pas.
    /// </summary>
    public static void AjouterColonneNumCnssEmployeSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        var columns = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Employes)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        if (columns.Contains("NumCnss")) return;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "ALTER TABLE Employes ADD COLUMN \"NumCnss\" TEXT";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Références fiche salaire Excel (CDF) sur l’employé : brut imposable, IPR, CNSS, INPP.
    /// </summary>
    public static void AjouterColonnesEmployesReferencesFicheSalaireSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        var columns = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Employes)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        void AddColumnIfMissing(string name, string sql)
        {
            if (columns.Contains(name)) return;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
            columns.Add(name);
        }

        AddColumnIfMissing("ReferenceBrutImposableCnssCdf",
            "ALTER TABLE Employes ADD COLUMN \"ReferenceBrutImposableCnssCdf\" REAL");
        AddColumnIfMissing("ReferenceIprNetCdf", "ALTER TABLE Employes ADD COLUMN \"ReferenceIprNetCdf\" REAL");
        AddColumnIfMissing("ReferenceCnssOuvrierCdf", "ALTER TABLE Employes ADD COLUMN \"ReferenceCnssOuvrierCdf\" REAL");
        AddColumnIfMissing("ReferenceInppCdf", "ALTER TABLE Employes ADD COLUMN \"ReferenceInppCdf\" REAL");
    }

    /// <summary>Champs portail CNSS e-déclaration (commune, type travailleur).</summary>
    public static void AjouterColonnesEmployesCnssEdeclarationSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        var columns = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Employes)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        void AddColumnIfMissing(string name, string sql)
        {
            if (columns.Contains(name)) return;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
            columns.Add(name);
        }

        AddColumnIfMissing("CommuneAffectation", "ALTER TABLE Employes ADD COLUMN \"CommuneAffectation\" TEXT");
        AddColumnIfMissing("TypeTravailleurCnss", "ALTER TABLE Employes ADD COLUMN \"TypeTravailleurCnss\" INTEGER NOT NULL DEFAULT 1");
    }

    /// <summary>
    /// Ajoute la colonne ZkUserId sur Employes si elle n'existe pas.
    /// </summary>
    public static void AjouterColonneZkUserIdEmployeSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        var columns = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Employes)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        if (columns.Contains("ZkUserId")) return;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "ALTER TABLE Employes ADD COLUMN \"ZkUserId\" TEXT";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Cotisation INPP retenue sur le bulletin (colonne dédiée, export PDF / déclarations).
    /// </summary>
    public static void AjouterColonneCotisationInppBulletinSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        var columns = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(BulletinsPaie)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        if (columns.Contains("CotisationInpp")) return;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "ALTER TABLE BulletinsPaie ADD COLUMN \"CotisationInpp\" REAL NOT NULL DEFAULT 0";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Ajoute les colonnes de rémunération avancée au contrat (heures sup, nuit, fériés, préavis, indemnité licenciement) si elles n'existent pas.
    /// </summary>
    public static void AjouterColonnesContratsRemunerationAvanceeSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        var columns = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Contrats)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        void AddColumnIfMissing(string name, string sql)
        {
            if (columns.Contains(name)) return;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        AddColumnIfMissing("TauxMajorationHeuresSup", "ALTER TABLE Contrats ADD COLUMN \"TauxMajorationHeuresSup\" REAL NOT NULL DEFAULT 0");
        AddColumnIfMissing("TauxMajorationNuit", "ALTER TABLE Contrats ADD COLUMN \"TauxMajorationNuit\" REAL NOT NULL DEFAULT 0");
        AddColumnIfMissing("TauxMajorationJourFerie", "ALTER TABLE Contrats ADD COLUMN \"TauxMajorationJourFerie\" REAL NOT NULL DEFAULT 0");
        AddColumnIfMissing("PreavisMoisBase", "ALTER TABLE Contrats ADD COLUMN \"PreavisMoisBase\" REAL NOT NULL DEFAULT 0");
        AddColumnIfMissing("IndemniteLicenciementMoisBase", "ALTER TABLE Contrats ADD COLUMN \"IndemniteLicenciementMoisBase\" REAL NOT NULL DEFAULT 0");
    }

    /// <summary>
    /// Crée la table SaisiesPaie (et son index) si elle n'existe pas.
    /// Utile pour les anciennes bases créées avant l'ajout de cette table.
    /// </summary>
    public static void AjouterTableSaisiesPaieSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='SaisiesPaie'";
            var name = cmd.ExecuteScalar() as string;
            if (!string.IsNullOrEmpty(name)) return;
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE TABLE IF NOT EXISTS ""SaisiesPaie"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EmployeId"" INTEGER NOT NULL, ""PeriodePaieId"" INTEGER NOT NULL, ""JoursPrestes"" INTEGER NOT NULL, ""AutresGainsImposables"" REAL NOT NULL, ""AutresGainsNonImposables"" REAL NOT NULL, ""AutresRetenues"" REAL NOT NULL, ""AcomptesSalaire"" REAL NOT NULL DEFAULT 0, ""SanctionsDisciplinaires"" REAL NOT NULL DEFAULT 0, ""Commentaire"" TEXT, CONSTRAINT ""FK_SaisiesPaie_Employes_EmployeId"" FOREIGN KEY (""EmployeId"") REFERENCES ""Employes"" (""Id""), CONSTRAINT ""FK_SaisiesPaie_PeriodesPaie_PeriodePaieId"" FOREIGN KEY (""PeriodePaieId"") REFERENCES ""PeriodesPaie"" (""Id""))";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE INDEX IF NOT EXISTS ""IX_SaisiesPaie_EmployeId_PeriodePaieId"" ON ""SaisiesPaie"" (""EmployeId"",""PeriodePaieId"")";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Ajoute les colonnes AcomptesSalaire et SanctionsDisciplinaires à la table SaisiesPaie si elles n'existent pas.
    /// </summary>
    public static void AjouterColonnesSaisiesPaieAcomptesSanctionsSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        var columns = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(SaisiesPaie)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        if (columns.Contains("AcomptesSalaire") && columns.Contains("SanctionsDisciplinaires")) return;

        if (!columns.Contains("AcomptesSalaire"))
        {
            using var cmd1 = conn.CreateCommand();
            cmd1.CommandText = "ALTER TABLE SaisiesPaie ADD COLUMN \"AcomptesSalaire\" REAL NOT NULL DEFAULT 0";
            cmd1.ExecuteNonQuery();
        }
        if (!columns.Contains("SanctionsDisciplinaires"))
        {
            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = "ALTER TABLE SaisiesPaie ADD COLUMN \"SanctionsDisciplinaires\" REAL NOT NULL DEFAULT 0";
            cmd2.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Crée la table AffectationsPrimesIndemnites (et ses index) si elle n'existe pas.
    /// Utile pour les anciennes bases créées avant l'ajout de cette table.
    /// </summary>
    public static void AjouterTableAffectationsPrimesIndemnitesSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='AffectationsPrimesIndemnites'";
            var name = cmd.ExecuteScalar() as string;
            if (!string.IsNullOrEmpty(name)) return;
        }

        var statements = new[]
        {
            @"CREATE TABLE IF NOT EXISTS ""AffectationsPrimesIndemnites"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EmployeId"" INTEGER NOT NULL, ""PrimeIndemniteId"" INTEGER NOT NULL, ""Montant"" REAL NOT NULL, CONSTRAINT ""FK_AffectationsPrimesIndemnites_Employes_EmployeId"" FOREIGN KEY (""EmployeId"") REFERENCES ""Employes"" (""Id"") ON DELETE CASCADE, CONSTRAINT ""FK_AffectationsPrimesIndemnites_PrimesIndemnites_PrimeIndemniteId"" FOREIGN KEY (""PrimeIndemniteId"") REFERENCES ""PrimesIndemnites"" (""Id""))",
            @"CREATE INDEX IF NOT EXISTS ""IX_AffectationsPrimesIndemnites_EmployeId"" ON ""AffectationsPrimesIndemnites"" (""EmployeId"")",
            @"CREATE INDEX IF NOT EXISTS ""IX_AffectationsPrimesIndemnites_PrimeIndemniteId"" ON ""AffectationsPrimesIndemnites"" (""PrimeIndemniteId"")"
        };

        using (var cmd = conn.CreateCommand())
        {
            foreach (var sql in statements)
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Crée la table des libellés de bulletin par employé si elle n'existe pas.
    /// </summary>
    public static void AjouterTableEmployesLibellesBulletinSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='EmployesLibellesBulletin'";
            var name = cmd.ExecuteScalar() as string;
            if (!string.IsNullOrEmpty(name)) return;
        }

        var statements = new[]
        {
            @"CREATE TABLE IF NOT EXISTS ""EmployesLibellesBulletin"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EmployeId"" INTEGER NOT NULL, ""CodeRubrique"" TEXT NOT NULL, ""Libelle"" TEXT NOT NULL, CONSTRAINT ""FK_EmployesLibellesBulletin_Employes_EmployeId"" FOREIGN KEY (""EmployeId"") REFERENCES ""Employes"" (""Id"") ON DELETE CASCADE)",
            @"CREATE INDEX IF NOT EXISTS ""IX_EmployesLibellesBulletin_EmployeId_CodeRubrique"" ON ""EmployesLibellesBulletin"" (""EmployeId"",""CodeRubrique"")"
        };

        using (var cmd = conn.CreateCommand())
        {
            foreach (var sql in statements)
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Crée la table SuivisJournaliers (et son index) si elle n'existe pas.
    /// </summary>
    public static void AjouterTableSuivisJournaliersSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='SuivisJournaliers'";
            var name = cmd.ExecuteScalar() as string;
            if (!string.IsNullOrEmpty(name)) return;
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE TABLE IF NOT EXISTS ""SuivisJournaliers"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EmployeId"" INTEGER NOT NULL, ""Date"" TEXT NOT NULL, ""HeuresPrestees"" REAL NOT NULL DEFAULT 0, ""TypeJour"" TEXT NOT NULL DEFAULT 'Normal', CONSTRAINT ""FK_SuivisJournaliers_Employes_EmployeId"" FOREIGN KEY (""EmployeId"") REFERENCES ""Employes"" (""Id""))";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE INDEX IF NOT EXISTS ""IX_SuivisJournaliers_EmployeId_Date"" ON ""SuivisJournaliers"" (""EmployeId"",""Date"")";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Ajoute PointagesJson et HeuresManuelles à SuivisJournaliers (recalcul auto LTservices + saisie manuelle).
    /// </summary>
    public static void AjouterColonnesSuiviJournalierPointagesSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        var columns = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(SuivisJournaliers)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        void AddColumnIfMissing(string name, string sql)
        {
            if (columns.Contains(name)) return;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        AddColumnIfMissing("PointagesJson", "ALTER TABLE SuivisJournaliers ADD COLUMN \"PointagesJson\" TEXT");
        AddColumnIfMissing("HeuresManuelles", "ALTER TABLE SuivisJournaliers ADD COLUMN \"HeuresManuelles\" INTEGER NOT NULL DEFAULT 0");
    }

    /// <summary>
    /// Paramètres de connexion au terminal ZKTeco (pointeuse réseau) dans ParametresApplication.
    /// </summary>
    public static void AjouterColonnesParametresZktecoSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        var columns = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(ParametresApplication)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }

        void AddColumnIfMissing(string name, string sql)
        {
            if (columns.Contains(name)) return;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
            columns.Add(name);
        }

        AddColumnIfMissing("ZkTerminalIp", "ALTER TABLE ParametresApplication ADD COLUMN \"ZkTerminalIp\" TEXT");
        AddColumnIfMissing("ZkTerminalPort", "ALTER TABLE ParametresApplication ADD COLUMN \"ZkTerminalPort\" INTEGER NOT NULL DEFAULT 4370");
        AddColumnIfMissing("ZkMachineNumber", "ALTER TABLE ParametresApplication ADD COLUMN \"ZkMachineNumber\" INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing("ZkCommPassword", "ALTER TABLE ParametresApplication ADD COLUMN \"ZkCommPassword\" INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing("ZkSyncActif", "ALTER TABLE ParametresApplication ADD COLUMN \"ZkSyncActif\" INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing("ZkIntervalleSecondes", "ALTER TABLE ParametresApplication ADD COLUMN \"ZkIntervalleSecondes\" INTEGER NOT NULL DEFAULT 60");
        AddColumnIfMissing("ZkDerniereSyncUtc", "ALTER TABLE ParametresApplication ADD COLUMN \"ZkDerniereSyncUtc\" TEXT");
        AddColumnIfMissing("LtHeureDebutTravail", "ALTER TABLE ParametresApplication ADD COLUMN \"LtHeureDebutTravail\" TEXT NOT NULL DEFAULT '07:30'");
        AddColumnIfMissing("LtHeureLimiteTolerance", "ALTER TABLE ParametresApplication ADD COLUMN \"LtHeureLimiteTolerance\" TEXT NOT NULL DEFAULT '07:40'");
        AddColumnIfMissing("LtHeureDebutPause", "ALTER TABLE ParametresApplication ADD COLUMN \"LtHeureDebutPause\" TEXT NOT NULL DEFAULT '12:00'");
        AddColumnIfMissing("LtHeureFinPause", "ALTER TABLE ParametresApplication ADD COLUMN \"LtHeureFinPause\" TEXT NOT NULL DEFAULT '13:00'");
        AddColumnIfMissing("LtHeureFinSemaine", "ALTER TABLE ParametresApplication ADD COLUMN \"LtHeureFinSemaine\" TEXT NOT NULL DEFAULT '16:00'");
        AddColumnIfMissing("LtHeureFinSamedi", "ALTER TABLE ParametresApplication ADD COLUMN \"LtHeureFinSamedi\" TEXT NOT NULL DEFAULT '12:30'");
    }

    /// <summary>
    /// Table des paramètres globaux (taux de change courant), une ligne Id = 1.
    /// </summary>
    public static void AjouterTableParametresApplicationSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='ParametresApplication'";
            var name = cmd.ExecuteScalar() as string;
            if (!string.IsNullOrEmpty(name)) return;
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE TABLE IF NOT EXISTS ""ParametresApplication"" (""Id"" INTEGER NOT NULL PRIMARY KEY, ""TauxCdfParUsd"" REAL NOT NULL, ""DateDerniereModification"" TEXT)";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Crée la table JoursTravailCalendrier (et son index) si elle n'existe pas.
    /// Utile pour les anciennes bases créées avant l'ajout de cette table.
    /// </summary>
    public static void AjouterTableJoursTravailCalendrierSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='JoursTravailCalendrier'";
            var name = cmd.ExecuteScalar() as string;
            if (!string.IsNullOrEmpty(name)) return;
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE TABLE IF NOT EXISTS ""JoursTravailCalendrier"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""Annee"" INTEGER NOT NULL, ""DateJour"" TEXT NOT NULL, ""TypeJour"" TEXT NOT NULL, ""Libelle"" TEXT)";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE INDEX IF NOT EXISTS ""IX_JoursTravailCalendrier_Annee_DateJour"" ON ""JoursTravailCalendrier"" (""Annee"",""DateJour"")";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Exécute les CREATE TABLE IF NOT EXISTS et CREATE INDEX IF NOT EXISTS dans l'ordre.
    /// </summary>
    public static void AppliquerSchema(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using var cmd = conn.CreateCommand();
        var statements = GetSchemaStatements();
        foreach (var sql in statements)
        {
            if (string.IsNullOrWhiteSpace(sql)) continue;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    private static IEnumerable<string> GetSchemaStatements()
    {
        return new[]
        {
            @"CREATE TABLE IF NOT EXISTS ""Entreprises"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""RaisonSociale"" TEXT NOT NULL, ""Nif"" TEXT, ""Nrc"" TEXT, ""IdNat"" TEXT, ""NumCnss"" TEXT, ""NumInpp"" TEXT, ""Adresse"" TEXT, ""Telephone"" TEXT, ""Email"" TEXT, ""SiteWeb"" TEXT, ""NumeroAffiliationCnss"" TEXT, ""Logo"" TEXT, ""CouleurPrincipale"" TEXT, ""CouleurSecondaire"" TEXT)",
            @"CREATE TABLE IF NOT EXISTS ""Etablissements"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EntrepriseId"" INTEGER NOT NULL, ""NomSite"" TEXT NOT NULL, CONSTRAINT ""FK_Etablissements_Entreprises_EntrepriseId"" FOREIGN KEY (""EntrepriseId"") REFERENCES ""Entreprises"" (""Id""))",
            @"CREATE INDEX IF NOT EXISTS ""IX_Etablissements_EntrepriseId"" ON ""Etablissements"" (""EntrepriseId"")",
            @"CREATE TABLE IF NOT EXISTS ""Departements"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EtablissementId"" INTEGER NOT NULL, ""NomDepartement"" TEXT NOT NULL, CONSTRAINT ""FK_Departements_Etablissements_EtablissementId"" FOREIGN KEY (""EtablissementId"") REFERENCES ""Etablissements"" (""Id""))",
            @"CREATE INDEX IF NOT EXISTS ""IX_Departements_EtablissementId"" ON ""Departements"" (""EtablissementId"")",
            @"CREATE TABLE IF NOT EXISTS ""CategoriesProfessionnelles"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""Libelle"" TEXT NOT NULL, ""SmigApplique"" REAL NOT NULL)",
            @"CREATE TABLE IF NOT EXISTS ""PrimesIndemnites"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""Libelle"" TEXT NOT NULL, ""EstImposable"" INTEGER NOT NULL, ""EstCotisable"" INTEGER NOT NULL, ""ModeCalcul"" TEXT NOT NULL DEFAULT 'FIXE', ""TypeLigne"" TEXT NOT NULL DEFAULT 'A', ""NumeroCompte"" TEXT)",
            @"CREATE TABLE IF NOT EXISTS ""Employes"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""Matricule"" TEXT NOT NULL, ""ZkUserId"" TEXT, ""Nom"" TEXT NOT NULL, ""Postnom"" TEXT, ""Prenom"" TEXT, ""Sexe"" TEXT, ""EtatCivil"" TEXT, ""DateNaissance"" TEXT, ""Telephone"" TEXT, ""Adresse"" TEXT, ""DepartementId"" INTEGER NOT NULL, CONSTRAINT ""FK_Employes_Departements_DepartementId"" FOREIGN KEY (""DepartementId"") REFERENCES ""Departements"" (""Id""))",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Employes_Matricule"" ON ""Employes"" (""Matricule"")",
            @"CREATE INDEX IF NOT EXISTS ""IX_Employes_DepartementId"" ON ""Employes"" (""DepartementId"")",
            @"CREATE TABLE IF NOT EXISTS ""Contrats"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EmployeId"" INTEGER NOT NULL, ""TypeContrat"" TEXT NOT NULL, ""DateDebut"" TEXT NOT NULL, ""DateFin"" TEXT, ""SalaireBase"" REAL NOT NULL, ""DeviseBase"" TEXT NOT NULL, ""CategorieProfessionnelleId"" INTEGER NOT NULL, CONSTRAINT ""FK_Contrats_Employes_EmployeId"" FOREIGN KEY (""EmployeId"") REFERENCES ""Employes"" (""Id""), CONSTRAINT ""FK_Contrats_CategoriesProfessionnelles_CategorieProfessionnelleId"" FOREIGN KEY (""CategorieProfessionnelleId"") REFERENCES ""CategoriesProfessionnelles"" (""Id""))",
            @"CREATE INDEX IF NOT EXISTS ""IX_Contrats_EmployeId"" ON ""Contrats"" (""EmployeId"")",
            @"CREATE INDEX IF NOT EXISTS ""IX_Contrats_CategorieProfessionnelleId"" ON ""Contrats"" (""CategorieProfessionnelleId"")",
            @"CREATE TABLE IF NOT EXISTS ""AffectationsPrimesIndemnites"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EmployeId"" INTEGER NOT NULL, ""PrimeIndemniteId"" INTEGER NOT NULL, ""Montant"" REAL NOT NULL, CONSTRAINT ""FK_AffectationsPrimesIndemnites_Employes_EmployeId"" FOREIGN KEY (""EmployeId"") REFERENCES ""Employes"" (""Id"") ON DELETE CASCADE, CONSTRAINT ""FK_AffectationsPrimesIndemnites_PrimesIndemnites_PrimeIndemniteId"" FOREIGN KEY (""PrimeIndemniteId"") REFERENCES ""PrimesIndemnites"" (""Id""))",
            @"CREATE INDEX IF NOT EXISTS ""IX_AffectationsPrimesIndemnites_EmployeId"" ON ""AffectationsPrimesIndemnites"" (""EmployeId"")",
            @"CREATE INDEX IF NOT EXISTS ""IX_AffectationsPrimesIndemnites_PrimeIndemniteId"" ON ""AffectationsPrimesIndemnites"" (""PrimeIndemniteId"")",
            @"CREATE TABLE IF NOT EXISTS ""AyantsDroit"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EmployeId"" INTEGER NOT NULL, ""Nom"" TEXT NOT NULL, ""LienParente"" TEXT NOT NULL, ""DateNaissance"" TEXT, CONSTRAINT ""FK_AyantsDroit_Employes_EmployeId"" FOREIGN KEY (""EmployeId"") REFERENCES ""Employes"" (""Id"") ON DELETE CASCADE)",
            @"CREATE INDEX IF NOT EXISTS ""IX_AyantsDroit_EmployeId"" ON ""AyantsDroit"" (""EmployeId"")",
            @"CREATE TABLE IF NOT EXISTS ""PretsAvances"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EmployeId"" INTEGER NOT NULL, ""MontantTotal"" REAL NOT NULL, ""DateOctroi"" TEXT NOT NULL, ""NbEcheances"" INTEGER NOT NULL, ""MontantMensuel"" REAL NOT NULL, ""SoldeRestant"" REAL NOT NULL, ""Statut"" TEXT, CONSTRAINT ""FK_PretsAvances_Employes_EmployeId"" FOREIGN KEY (""EmployeId"") REFERENCES ""Employes"" (""Id""))",
            @"CREATE INDEX IF NOT EXISTS ""IX_PretsAvances_EmployeId"" ON ""PretsAvances"" (""EmployeId"")",
            @"CREATE TABLE IF NOT EXISTS ""AbsencesConges"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EmployeId"" INTEGER NOT NULL, ""Type"" TEXT NOT NULL, ""DateDebut"" TEXT NOT NULL, ""DateFin"" TEXT NOT NULL, ""EstPaye"" INTEGER NOT NULL, CONSTRAINT ""FK_AbsencesConges_Employes_EmployeId"" FOREIGN KEY (""EmployeId"") REFERENCES ""Employes"" (""Id""))",
            @"CREATE INDEX IF NOT EXISTS ""IX_AbsencesConges_EmployeId"" ON ""AbsencesConges"" (""EmployeId"")",
            @"CREATE TABLE IF NOT EXISTS ""PeriodesPaie"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""Mois"" INTEGER NOT NULL, ""Annee"" INTEGER NOT NULL, ""TauxChangeBudget"" REAL NOT NULL, ""Cloturee"" INTEGER NOT NULL)",
            @"CREATE TABLE IF NOT EXISTS ""GrillesIpr"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""BorneInf"" REAL NOT NULL, ""BorneSup"" REAL NOT NULL, ""Taux"" REAL NOT NULL)",
            @"CREATE TABLE IF NOT EXISTS ""TauxSociaux"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""Code"" TEXT NOT NULL, ""Pourcentage"" REAL NOT NULL)",
            @"CREATE TABLE IF NOT EXISTS ""ParametresIpr"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""TauxEffectifMaximum"" REAL NOT NULL, ""ReductionParEnfant"" REAL NOT NULL)",
            @"CREATE TABLE IF NOT EXISTS ""ParametresApplication"" (""Id"" INTEGER NOT NULL PRIMARY KEY, ""TauxCdfParUsd"" REAL NOT NULL, ""DateDerniereModification"" TEXT)",
            @"CREATE TABLE IF NOT EXISTS ""BulletinsPaie"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EmployeId"" INTEGER NOT NULL, ""PeriodePaieId"" INTEGER NOT NULL, ""NumeroBulletin"" TEXT, ""DateGeneration"" TEXT NOT NULL, ""TotalGainImposable"" REAL NOT NULL, ""TotalGainNonImposable"" REAL NOT NULL, ""BaseIpr"" REAL NOT NULL, ""MontantIprBrut"" REAL NOT NULL, ""ReductionFamille"" REAL NOT NULL, ""MontantIprNet"" REAL NOT NULL, ""CotisationCnssOuvrier"" REAL NOT NULL, ""NetAPayer"" REAL NOT NULL, ""NetAPayerDeviseLocale"" REAL NOT NULL, CONSTRAINT ""FK_BulletinsPaie_Employes_EmployeId"" FOREIGN KEY (""EmployeId"") REFERENCES ""Employes"" (""Id""), CONSTRAINT ""FK_BulletinsPaie_PeriodesPaie_PeriodePaieId"" FOREIGN KEY (""PeriodePaieId"") REFERENCES ""PeriodesPaie"" (""Id""))",
            @"CREATE INDEX IF NOT EXISTS ""IX_BulletinsPaie_EmployeId"" ON ""BulletinsPaie"" (""EmployeId"")",
            @"CREATE INDEX IF NOT EXISTS ""IX_BulletinsPaie_PeriodePaieId"" ON ""BulletinsPaie"" (""PeriodePaieId"")",
            @"CREATE TABLE IF NOT EXISTS ""BulletinsDetails"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""BulletinPaieId"" INTEGER NOT NULL, ""Libelle"" TEXT NOT NULL, ""BaseCalcul"" REAL NOT NULL, ""Taux"" REAL NOT NULL, ""Gain"" REAL NOT NULL, ""Retenue"" REAL NOT NULL, CONSTRAINT ""FK_BulletinsDetails_BulletinsPaie_BulletinPaieId"" FOREIGN KEY (""BulletinPaieId"") REFERENCES ""BulletinsPaie"" (""Id"") ON DELETE CASCADE)",
            @"CREATE INDEX IF NOT EXISTS ""IX_BulletinsDetails_BulletinPaieId"" ON ""BulletinsDetails"" (""BulletinPaieId"")",
            @"CREATE TABLE IF NOT EXISTS ""SaisiesPaie"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EmployeId"" INTEGER NOT NULL, ""PeriodePaieId"" INTEGER NOT NULL, ""JoursPrestes"" INTEGER NOT NULL, ""AutresGainsImposables"" REAL NOT NULL, ""AutresGainsNonImposables"" REAL NOT NULL, ""AutresRetenues"" REAL NOT NULL, ""AcomptesSalaire"" REAL NOT NULL DEFAULT 0, ""SanctionsDisciplinaires"" REAL NOT NULL DEFAULT 0, ""Commentaire"" TEXT, CONSTRAINT ""FK_SaisiesPaie_Employes_EmployeId"" FOREIGN KEY (""EmployeId"") REFERENCES ""Employes"" (""Id""), CONSTRAINT ""FK_SaisiesPaie_PeriodesPaie_PeriodePaieId"" FOREIGN KEY (""PeriodePaieId"") REFERENCES ""PeriodesPaie"" (""Id""))",
            @"CREATE INDEX IF NOT EXISTS ""IX_SaisiesPaie_EmployeId_PeriodePaieId"" ON ""SaisiesPaie"" (""EmployeId"",""PeriodePaieId"")",
            @"CREATE TABLE IF NOT EXISTS ""JoursTravailCalendrier"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""Annee"" INTEGER NOT NULL, ""DateJour"" TEXT NOT NULL, ""TypeJour"" TEXT NOT NULL, ""Libelle"" TEXT)",
            @"CREATE INDEX IF NOT EXISTS ""IX_JoursTravailCalendrier_Annee_DateJour"" ON ""JoursTravailCalendrier"" (""Annee"",""DateJour"")",
            @"CREATE TABLE IF NOT EXISTS ""SuivisJournaliers"" (""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ""EmployeId"" INTEGER NOT NULL, ""Date"" TEXT NOT NULL, ""HeuresPrestees"" REAL NOT NULL DEFAULT 0, ""TypeJour"" TEXT NOT NULL DEFAULT 'Normal', CONSTRAINT ""FK_SuivisJournaliers_Employes_EmployeId"" FOREIGN KEY (""EmployeId"") REFERENCES ""Employes"" (""Id""))",
            @"CREATE INDEX IF NOT EXISTS ""IX_SuivisJournaliers_EmployeId_Date"" ON ""SuivisJournaliers"" (""EmployeId"",""Date"")",
        };
    }

    /// <summary>
    /// Crée la table FormFieldValues pour les champs de formulaires dynamiques.
    /// </summary>
    public static void AjouterTableFormFieldValuesSiNecessaire(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='FormFieldValues'";
            var name = cmd.ExecuteScalar() as string;
            if (!string.IsNullOrEmpty(name)) return;
        }

        const string sql = @"CREATE TABLE IF NOT EXISTS ""FormFieldValues"" (
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""FormId"" TEXT NOT NULL,
            ""EntityType"" TEXT NOT NULL,
            ""EntityId"" INTEGER NOT NULL,
            ""FieldKey"" TEXT NOT NULL,
            ""Value"" TEXT,
            ""DateModification"" TEXT NOT NULL
        )";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_FormFieldValues_Form_Entity_Field""
                ON ""FormFieldValues"" (""FormId"", ""EntityType"", ""EntityId"", ""FieldKey"")";
            cmd.ExecuteNonQuery();
        }
    }
}
