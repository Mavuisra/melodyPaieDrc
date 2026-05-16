using System.ComponentModel.DataAnnotations;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Utilisateur de l'application (connexion et rôle).
/// Rôles : Admin, Gestionnaire, Lecture.
/// </summary>
public class Utilisateur
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(80)]
    public string Login { get; set; } = string.Empty;

    /// <summary>Hash du mot de passe (salt + SHA256).</summary>
    [Required]
    [MaxLength(128)]
    public string MotDePasseHash { get; set; } = string.Empty;

    /// <summary>Salt pour le hash (stocké en clair).</summary>
    [Required]
    [MaxLength(64)]
    public string Salt { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? NomComplet { get; set; }

    /// <summary>Admin = tout + gestion utilisateurs ; Gestionnaire = tout sauf gestion users ; Lecture = consultation seule.</summary>
    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "Gestionnaire";

    public bool Actif { get; set; } = true;

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;

    public const string RoleAdmin = "Admin";
    public const string RoleGestionnaire = "Gestionnaire";
    public const string RoleLecture = "Lecture";

    public static IReadOnlyList<string> RolesDisponibles { get; } = new[] { RoleAdmin, RoleGestionnaire, RoleLecture };
}
