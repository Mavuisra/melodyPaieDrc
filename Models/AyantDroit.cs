using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Ayant droit d'un employé (enfant, conjoint, ...).
/// </summary>
public class AyantDroit
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Employe))]
    public int EmployeId { get; set; }

    public Employe? Employe { get; set; }

    [Required]
    [MaxLength(255)]
    public string Nom { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LienParente { get; set; } = string.Empty; // Enfant, Conjoint, ...

    public DateTime? DateNaissance { get; set; }
}

