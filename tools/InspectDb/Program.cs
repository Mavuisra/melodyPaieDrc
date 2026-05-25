using Microsoft.Data.Sqlite;

static void Run(string label, string path)
{
    Console.WriteLine($"=== {label} ===");
    Console.WriteLine(path);
    if (!File.Exists(path)) { Console.WriteLine("(fichier absent)"); return; }
    Console.WriteLine($"Taille: {new FileInfo(path).Length} octets");
    using var conn = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
    conn.Open();
    Q(conn, "PRAGMA integrity_check");
    Q(conn, "SELECT COUNT(*) AS Employes FROM Employes");
    Q(conn, "SELECT COUNT(*) AS Entreprises FROM Entreprises");
    Q(conn, "SELECT Id, RaisonSociale FROM Entreprises");
    try { Q(conn, "SELECT EntrepriseId, COUNT(*) AS Nb FROM Employes GROUP BY EntrepriseId"); }
    catch (Exception ex) { Console.WriteLine($"Employes.EntrepriseId: {ex.Message}"); }
    Q(conn, "PRAGMA table_info(Employes)");
    Q(conn, "PRAGMA table_info(Etablissements)");
    try { Q(conn, "SELECT EntrepriseId, COUNT(*) AS Nb FROM Etablissements GROUP BY EntrepriseId"); }
    catch (Exception ex) { Console.WriteLine($"Etablissements.EntrepriseId: {ex.Message}"); }
    Q(conn, @"SELECT COUNT(*) AS employes_visibles FROM Employes e
      JOIN Departements d ON e.DepartementId = d.Id
      JOIN Etablissements et ON d.EtablissementId = et.Id
      WHERE et.EntrepriseId = 1");
    try {
        Q(conn, @"SELECT COUNT(*) FROM Employes e
      LEFT JOIN Departements d ON e.DepartementId = d.Id
      LEFT JOIN Etablissements et ON d.EtablissementId = et.Id
      WHERE et.EntrepriseId IS NULL OR et.EntrepriseId = 0");
    } catch { }
    try { Q(conn, "SELECT DerniereEntrepriseActiveId FROM ParametresApplication WHERE Id=1"); }
    catch { Console.WriteLine("(ParametresApplication absent ou colonne manquante)"); }
    Console.WriteLine();
}

static void Q(SqliteConnection conn, string sql)
{
    Console.WriteLine($"-- {sql}");
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        var vals = new object[r.FieldCount];
        r.GetValues(vals);
        Console.WriteLine(string.Join(" | ", vals));
    }
}

var backup = args.Length > 0 ? args[0] : @"c:\Users\luyey\Downloads\PaieRDC_backup_2026-05-19_163228.db";
var live = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MelodyPaieRDC", "Data", "PaieRDC.db");
if (args.Contains("--fix-null-entreprise-id", StringComparer.OrdinalIgnoreCase))
{
    var livePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MelodyPaieRDC", "Data", "PaieRDC.db");
    using var conn = new SqliteConnection($"Data Source={livePath}");
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        UPDATE Employes
        SET EntrepriseId = COALESCE(
            (SELECT et.EntrepriseId FROM Departements d
             INNER JOIN Etablissements et ON et.Id = d.EtablissementId
             WHERE d.Id = Employes.DepartementId),
            (SELECT Id FROM Entreprises ORDER BY Id LIMIT 1))
        WHERE EntrepriseId IS NULL OR EntrepriseId = 0
        """;
    var n = cmd.ExecuteNonQuery();
    Console.WriteLine($"Corrigé {n} employé(s) (EntrepriseId NULL → valeur rattachement).");
    return;
}

Run("BACKUP", backup);
Run("BASE ACTIVE", live);
