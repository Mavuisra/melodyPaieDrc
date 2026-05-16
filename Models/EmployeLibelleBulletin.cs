using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Libellé de ligne de bulletin personnalisable et rattaché à un employé.
/// </summary>
public class EmployeLibelleBulletin
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Employe))]
    public int EmployeId { get; set; }

    public Employe? Employe { get; set; }

    /// <summary>Code technique stable de la rubrique (ex : IPR, CNSS, ABSENCE).</summary>
    [Required]
    [MaxLength(50)]
    public string CodeRubrique { get; set; } = string.Empty;

    /// <summary>Libellé affiché dans le bulletin pour cet employé.</summary>
    [Required]
    [MaxLength(255)]
    public string Libelle { get; set; } = string.Empty;
}

