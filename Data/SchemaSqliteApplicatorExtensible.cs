using System.Data;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Data;

/// <summary>
/// Évolution du schéma : politique de paie, champs dynamiques, multi-entreprise, sync.
/// </summary>
public static class SchemaSqliteApplicatorExtensible
{
    public static void AppliquerSiNecessaire(DbContext db)
    {
        CreerTablesExtensibles(db);
        AjouterColonnesMultiEntreprise(db);
        AjouterColonnesPrimesEtSync(db);
    }

    private static void CreerTablesExtensibles(DbContext db)
    {
        Executer(db, @"CREATE TABLE IF NOT EXISTS ""PolitiquesPaie"" (
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""EntrepriseId"" INTEGER NOT NULL,
            ""Libelle"" TEXT NOT NULL,
            ""DateEffet"" TEXT NOT NULL,
            ""Actif"" INTEGER NOT NULL,
            ""Version"" TEXT NOT NULL,
            ""UpdatedAtUtc"" TEXT,
            CONSTRAINT ""FK_PolitiquesPaie_Entreprises"" FOREIGN KEY (""EntrepriseId"") REFERENCES ""Entreprises"" (""Id"")
        )");
        Executer(db, @"CREATE INDEX IF NOT EXISTS ""IX_PolitiquesPaie_EntrepriseId_Actif"" ON ""PolitiquesPaie"" (""EntrepriseId"", ""Actif"")");

        Executer(db, @"CREATE TABLE IF NOT EXISTS ""ParametresPolitiquePaie"" (
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""PolitiquePaieId"" INTEGER NOT NULL,
            ""Cle"" TEXT NOT NULL,
            ""Valeur"" TEXT NOT NULL,
            CONSTRAINT ""FK_ParametresPolitiquePaie_Politiques"" FOREIGN KEY (""PolitiquePaieId"") REFERENCES ""PolitiquesPaie"" (""Id"") ON DELETE CASCADE
        )");
        Executer(db, @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ParametresPolitiquePaie_Politique_Cle"" ON ""ParametresPolitiquePaie"" (""PolitiquePaieId"", ""Cle"")");

        Executer(db, @"CREATE TABLE IF NOT EXISTS ""RubriquesBulletin"" (
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""PolitiquePaieId"" INTEGER NOT NULL,
            ""Code"" TEXT NOT NULL,
            ""Libelle"" TEXT NOT NULL,
            ""TypeLigne"" TEXT NOT NULL,
            ""OrdreAffichage"" INTEGER NOT NULL,
            ""SourceCalcul"" TEXT NOT NULL,
            ""AfficherSurBulletin"" INTEGER NOT NULL,
            CONSTRAINT ""FK_RubriquesBulletin_Politiques"" FOREIGN KEY (""PolitiquePaieId"") REFERENCES ""PolitiquesPaie"" (""Id"") ON DELETE CASCADE
        )");
        Executer(db, @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_RubriquesBulletin_Politique_Code"" ON ""RubriquesBulletin"" (""PolitiquePaieId"", ""Code"")");

        Executer(db, @"CREATE TABLE IF NOT EXISTS ""DefinitionsChampsDynamiques"" (
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""EntrepriseId"" INTEGER,
            ""EntiteCible"" TEXT NOT NULL,
            ""Code"" TEXT NOT NULL,
            ""Libelle"" TEXT NOT NULL,
            ""TypeDonnee"" TEXT NOT NULL,
            ""Obligatoire"" INTEGER NOT NULL,
            ""Ordre"" INTEGER NOT NULL,
            ""OptionsListe"" TEXT
        )");

        Executer(db, @"CREATE TABLE IF NOT EXISTS ""ValeursChampsDynamiques"" (
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""DefinitionChampId"" INTEGER NOT NULL,
            ""EntiteId"" INTEGER NOT NULL,
            ""ValeurTexte"" TEXT,
            ""ValeurNombre"" REAL,
            ""ValeurDate"" TEXT,
            ""ValeurBooleen"" INTEGER,
            ""UpdatedAtUtc"" TEXT,
            CONSTRAINT ""FK_ValeursChamps_Definitions"" FOREIGN KEY (""DefinitionChampId"") REFERENCES ""DefinitionsChampsDynamiques"" (""Id"") ON DELETE CASCADE
        )");
        Executer(db, @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ValeursChamps_Def_Entite"" ON ""ValeursChampsDynamiques"" (""DefinitionChampId"", ""EntiteId"")");

        Executer(db, @"CREATE TABLE IF NOT EXISTS ""SyncJournaux"" (
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""EntrepriseId"" INTEGER,
            ""NomTable"" TEXT NOT NULL,
            ""EnregistrementId"" INTEGER NOT NULL,
            ""Operation"" TEXT NOT NULL,
            ""PayloadJson"" TEXT,
            ""DateModificationUtc"" TEXT NOT NULL,
            ""DateSyncUtc"" TEXT,
            ""Conflit"" INTEGER NOT NULL,
            ""DeviceId"" TEXT
        )");
        Executer(db, @"CREATE INDEX IF NOT EXISTS ""IX_SyncJournaux_DateSync"" ON ""SyncJournaux"" (""DateSyncUtc"")");

        Executer(db, @"CREATE TABLE IF NOT EXISTS ""SyncParametres"" (
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""EntrepriseId"" INTEGER NOT NULL,
            ""EndpointUrl"" TEXT,
            ""DeviceId"" TEXT NOT NULL,
            ""SyncActive"" INTEGER NOT NULL,
            ""DerniereSyncUtc"" TEXT,
            ""JetonAcces"" TEXT
        )");
    }

    private static void AjouterColonnesMultiEntreprise(DbContext db)
    {
        AjouterColonne(db, "GrillesIpr", "EntrepriseId", "INTEGER");
        AjouterColonne(db, "TauxSociaux", "EntrepriseId", "INTEGER");
        AjouterColonne(db, "ParametresIpr", "EntrepriseId", "INTEGER");
        AjouterColonne(db, "CategoriesProfessionnelles", "EntrepriseId", "INTEGER");
        AjouterColonne(db, "PeriodesPaie", "EntrepriseId", "INTEGER");
        AjouterColonne(db, "PrimesIndemnites", "EntrepriseId", "INTEGER");
        AjouterColonne(db, "JoursTravailCalendrier", "EntrepriseId", "INTEGER");
        AjouterColonne(db, "ParametresApplication", "EntrepriseId", "INTEGER");
    }

    private static void AjouterColonnesPrimesEtSync(DbContext db)
    {
        AjouterColonne(db, "PrimesIndemnites", "CodeRubrique", "TEXT");
        AjouterColonne(db, "PrimesIndemnites", "OrdreAffichage", "INTEGER NOT NULL DEFAULT 0");
        AjouterColonne(db, "Employes", "UpdatedAtUtc", "TEXT");
        AjouterColonne(db, "BulletinsPaie", "UpdatedAtUtc", "TEXT");
    }

    private static void AjouterColonne(DbContext db, string table, string column, string sqlType)
    {
        var conn = Ouvrir(db);
        var columns = ListerColonnes(conn, table);
        if (columns.Contains(column)) return;
        Executer(conn, $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {sqlType}");
    }

    private static List<string> ListerColonnes(IDbConnection conn, string table)
    {
        var columns = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            columns.Add(r.GetString(1));
        return columns;
    }

    private static IDbConnection Ouvrir(DbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();
        return conn;
    }

    private static void Executer(DbContext db, string sql)
    {
        var conn = Ouvrir(db);
        Executer(conn, sql);
    }

    private static void Executer(IDbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
