using System.ComponentModel.DataAnnotations;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Valeur d'un champ de formulaire dynamique (extension sans colonne EF dédiée).
/// </summary>
public class FormFieldValue
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(80)]
    public string FormId { get; set; } = "";

    [Required]
    [MaxLength(40)]
    public string EntityType { get; set; } = "";

    public int EntityId { get; set; }

    [Required]
    [MaxLength(80)]
    public string FieldKey { get; set; } = "";

    public string? Value { get; set; }

    public DateTime DateModification { get; set; } = DateTime.UtcNow;
}
