using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Entreprise : informations légales et administratives.
/// </summary>
public class Entreprise
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string RaisonSociale { get; set; } = string.Empty;

    /// <summary>Numéro d'Identification Fiscale.</summary>
    [MaxLength(50)]
    public string? Nif { get; set; }

    [MaxLength(50)]
    public string? Nrc { get; set; } // RCCM

    [MaxLength(50)]
    public string? IdNat { get; set; }

    [MaxLength(50)]
    public string? NumCnss { get; set; }

    [MaxLength(50)]
    public string? NumInpp { get; set; }

    [MaxLength(255)]
    public string? Adresse { get; set; }

    [MaxLength(50)]
    public string? Telephone { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(255)]
    public string? SiteWeb { get; set; }

    /// <summary>Numéro d’affiliation CNSS de l’employeur (distinct du n° employeur).</summary>
    [MaxLength(50)]
    public string? NumeroAffiliationCnss { get; set; }

    /// <summary>Nom du fichier logo (ex: Logo.png), stocké dans le dossier Data de l'application.</summary>
    [MaxLength(260)]
    public string? Logo { get; set; }

    /// <summary>Couleur principale (hex, ex: #1E3A5F) pour en-têtes et bandeaux des documents.</summary>
    [MaxLength(20)]
    public string? CouleurPrincipale { get; set; }

    /// <summary>Couleur secondaire (hex) pour accents optionnels.</summary>
    [MaxLength(20)]
    public string? CouleurSecondaire { get; set; }

    public ICollection<Etablissement> Etablissements { get; set; } = new List<Etablissement>();
}

