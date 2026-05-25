using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

static void AfficherAide()
{
    Console.WriteLine("""
        Réinitialisation du mot de passe de connexion Melody Paie RDC
        (fermez l'application Melody avant d'exécuter cette commande)

        Usage :
          dotnet run --project tools/ResetMotDePasse -- --list
          dotnet run --project tools/ResetMotDePasse -- --login IDENTIFIANT --password NOUVEAU_MDP

        Exemple :
          dotnet run --project tools/ResetMotDePasse -- --login admin --password MelodyReset1

        Règles : au moins 8 caractères, une lettre et un chiffre.
        Base : %LocalAppData%\MelodyPaieRDC\Data\PaieRDC.db
        """);
}

static string CheminBase()
{
    var dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MelodyPaieRDC", "Data");
    return Path.Combine(dir, "PaieRDC.db");
}

static (string Hash, string Salt) HashMotDePasse(string motDePasse)
{
    var salt = Guid.NewGuid().ToString("N")[..32];
    var bytes = Encoding.UTF8.GetBytes(salt + motDePasse);
    var hash = Convert.ToBase64String(SHA256.HashData(bytes));
    return (hash, salt);
}

static bool ValiderMotDePasse(string motDePasse, out string erreur)
{
    erreur = "";
    if (string.IsNullOrWhiteSpace(motDePasse))
    {
        erreur = "Le mot de passe est obligatoire.";
        return false;
    }

    if (motDePasse.Length < 8)
    {
        erreur = "Au moins 8 caractères.";
        return false;
    }

    if (!motDePasse.Any(char.IsLetter) || !motDePasse.Any(char.IsDigit))
    {
        erreur = "Au moins une lettre et un chiffre.";
        return false;
    }

    return true;
}

var argsList = args.ToList();
if (argsList.Count == 0 || argsList.Contains("-h") || argsList.Contains("--help"))
{
    AfficherAide();
    return 0;
}

var dbPath = CheminBase();
Console.WriteLine($"Base : {dbPath}");
if (!File.Exists(dbPath))
{
    Console.Error.WriteLine("Fichier introuvable. Melody a-t-il déjà été installé sur ce poste ?");
    return 1;
}

await using var conn = new SqliteConnection($"Data Source={dbPath}");
await conn.OpenAsync();

if (argsList.Contains("--list"))
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT Login, Role, Actif FROM Utilisateurs ORDER BY Login";
    await using var r = await cmd.ExecuteReaderAsync();
    Console.WriteLine();
    Console.WriteLine("Login          | Rôle         | Actif");
    Console.WriteLine("---------------|--------------|------");
    while (await r.ReadAsync())
    {
        var login = r.GetString(0);
        var role = r.GetString(1);
        var actif = r.GetInt64(2) != 0;
        Console.WriteLine($"{login,-14} | {role,-12} | {(actif ? "oui" : "non")}");
    }

    return 0;
}

string? loginCible = null;
string? password = null;
for (var i = 0; i < argsList.Count; i++)
{
    if (argsList[i] == "--login" && i + 1 < argsList.Count)
        loginCible = argsList[++i];
    else if (argsList[i] == "--password" && i + 1 < argsList.Count)
        password = argsList[++i];
}

if (string.IsNullOrWhiteSpace(loginCible) || string.IsNullOrWhiteSpace(password))
{
    Console.Error.WriteLine("Indiquez --login et --password (ou --list).");
    AfficherAide();
    return 1;
}

if (!ValiderMotDePasse(password, out var erreurPolitique))
{
    Console.Error.WriteLine(erreurPolitique);
    return 1;
}

await using (var find = conn.CreateCommand())
{
    find.CommandText = "SELECT Id FROM Utilisateurs WHERE Login = $login COLLATE NOCASE";
    find.Parameters.AddWithValue("$login", loginCible.Trim());
    var id = await find.ExecuteScalarAsync();
    if (id == null || id == DBNull.Value)
    {
        Console.Error.WriteLine($"Utilisateur « {loginCible.Trim()} » introuvable. Utilisez --list.");
        return 1;
    }

    var (hash, salt) = HashMotDePasse(password);
    await using var upd = conn.CreateCommand();
    upd.CommandText = """
        UPDATE Utilisateurs
        SET MotDePasseHash = $hash, Salt = $salt, Actif = 1
        WHERE Id = $id
        """;
    upd.Parameters.AddWithValue("$hash", hash);
    upd.Parameters.AddWithValue("$salt", salt);
    upd.Parameters.AddWithValue("$id", Convert.ToInt64(id, CultureInfo.InvariantCulture));
    await upd.ExecuteNonQueryAsync();
}

Console.WriteLine();
Console.WriteLine($"Mot de passe réinitialisé pour « {loginCible.Trim()} ».");
Console.WriteLine("Connexion possible avec le nouveau mot de passe.");
return 0;
