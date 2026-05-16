using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Authentification : hash/vérification mot de passe et session courante.
/// </summary>
public static class AuthService
{
    public const int LongueurMotDePasseMin = 8;

    private static readonly string[] MotsDePasseInterdits =
    {
        "admin", "password", "motdepasse", "12345678", "123456789", "qwerty123"
    };

    private static Utilisateur? _utilisateurCourant;

    /// <summary>Utilisateur connecté (null si non connecté).</summary>
    public static Utilisateur? UtilisateurCourant => _utilisateurCourant;

    public static bool EstConnecte => _utilisateurCourant != null;
    public static bool EstAdmin => _utilisateurCourant?.Role == Utilisateur.RoleAdmin;
    public static bool EstLectureSeule => _utilisateurCourant?.Role == Utilisateur.RoleLecture;

    /// <summary>Hash (SHA256) avec salt. Retourne (hashBase64, salt).</summary>
    public static (string Hash, string Salt) HashMotDePasse(string motDePasse)
    {
        if (string.IsNullOrEmpty(motDePasse)) throw new ArgumentNullException(nameof(motDePasse));
        var salt = Guid.NewGuid().ToString("N")[..32];
        var hash = ComputeHash(motDePasse, salt);
        return (Convert.ToBase64String(hash), salt);
    }

    /// <summary>Vérifie que le mot de passe correspond au hash stocké.</summary>
    public static bool VerifierMotDePasse(string motDePasse, string hashBase64, string salt)
    {
        if (string.IsNullOrEmpty(motDePasse) || string.IsNullOrEmpty(hashBase64) || string.IsNullOrEmpty(salt))
            return false;
        var computed = ComputeHash(motDePasse, salt);
        var stored = Convert.FromBase64String(hashBase64);
        return computed.Length == stored.Length && CryptographicOperations.FixedTimeEquals(computed, stored);
    }

    private static byte[] ComputeHash(string motDePasse, string salt)
    {
        var bytes = Encoding.UTF8.GetBytes(salt + motDePasse);
        return SHA256.HashData(bytes);
    }

    /// <summary>Au moins un compte administrateur actif existe en base.</summary>
    public static bool AdministrateurActifExiste(PaieDbContext db) =>
        db.Utilisateurs.Any(u => u.Actif && u.Role == Utilisateur.RoleAdmin);

    /// <summary>Identifiants d'installation par défaut encore actifs (à remplacer).</summary>
    public static bool UtiliseIdentifiantsParDefaut(Utilisateur u) =>
        string.Equals(u.Login, "admin", StringComparison.OrdinalIgnoreCase)
        && VerifierMotDePasse("admin", u.MotDePasseHash, u.Salt);

    /// <summary>Vérifie en mémoire (hash non traduisible en SQL par EF).</summary>
    public static bool UnAdministrateurActifUtiliseIdentifiantsParDefaut(PaieDbContext db)
    {
        var admins = db.Utilisateurs.AsNoTracking()
            .Where(u => u.Actif && u.Role == Utilisateur.RoleAdmin)
            .ToList();
        return admins.Any(UtiliseIdentifiantsParDefaut);
    }

    /// <summary>Politique mot de passe : longueur, complexité minimale, pas de mots courants.</summary>
    public static bool ValiderPolitiqueMotDePasse(string motDePasse, out string messageErreur)
    {
        messageErreur = "";
        if (string.IsNullOrWhiteSpace(motDePasse))
        {
            messageErreur = "Le mot de passe est obligatoire.";
            return false;
        }

        if (motDePasse.Length < LongueurMotDePasseMin)
        {
            messageErreur = $"Le mot de passe doit contenir au moins {LongueurMotDePasseMin} caractères.";
            return false;
        }

        if (!motDePasse.Any(char.IsLetter) || !motDePasse.Any(char.IsDigit))
        {
            messageErreur = "Le mot de passe doit contenir au moins une lettre et un chiffre.";
            return false;
        }

        if (MotsDePasseInterdits.Any(m => string.Equals(motDePasse, m, StringComparison.OrdinalIgnoreCase)))
        {
            messageErreur = "Ce mot de passe est trop courant. Choisissez un mot de passe plus sûr.";
            return false;
        }

        return true;
    }

    /// <summary>Tente de connecter l'utilisateur. Retourne l'utilisateur si succès, null sinon.</summary>
    public static Utilisateur? Login(string login, string motDePasse)
    {
        if (string.IsNullOrWhiteSpace(login)) return null;
        using var db = new PaieDbContext();
        var u = db.Utilisateurs.FirstOrDefault(x => x.Login == login.Trim());
        if (u == null || !u.Actif) return null;
        if (!VerifierMotDePasse(motDePasse ?? "", u.MotDePasseHash, u.Salt)) return null;
        _utilisateurCourant = u;
        return u;
    }

    public static void Logout()
    {
        _utilisateurCourant = null;
    }

    /// <summary>Définit la session (après chargement depuis la base, ex. pour affichage).</summary>
    internal static void SetSession(Utilisateur? u)
    {
        _utilisateurCourant = u;
    }
}
