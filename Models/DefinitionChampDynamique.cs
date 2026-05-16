using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Définition d'un champ personnalisé (EAV) pour une entité métier.
/// </summary>
public class DefinitionChampDynamique
{
    [Key]
    public int Id { get; set; }

    /// <summary>Entreprise propriétaire ; null = définition globale.</summary>
    public int? EntrepriseId { get; set; }

    /// <summary>Employe, Contrat, Entreprise, BulletinPaie, …</summary>
    [Required]
    [MaxLength(40)]
    public string EntiteCible { get; set; } = string.Empty;

    [Required]
    [MaxLength(60)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Libelle { get; set; } = string.Empty;

    /// <summary>Texte, Nombre, Date, Booleen, Liste.</summary>
    [MaxLength(20)]
    public string TypeDonnee { get; set; } = TypeTexte;

    public bool Obligatoire { get; set; }

    public int Ordre { get; set; }

    [MaxLength(500)]
    public string? OptionsListe { get; set; }

    public ICollection<ValeurChampDynamique> Valeurs { get; set; } = new List<ValeurChampDynamique>();

    public const string TypeTexte = "Texte";
    public const string TypeNombre = "Nombre";
    public const string TypeDate = "Date";
    public const string TypeBooleen = "Booleen";
    public const string TypeListe = "Liste";
}
