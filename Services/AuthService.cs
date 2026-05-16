using System.Security.Cryptography;
using System.Text;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Authentification : hash/vérification mot de passe et session courante.
/// </summary>
public static class AuthService
{
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
