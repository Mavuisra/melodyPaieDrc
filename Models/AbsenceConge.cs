using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Absences et congés (maladie, circonstances, annuel, ...).
/// </summary>
public class AbsenceConge
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Employe))]
    public int EmployeId { get; set; }

    public Employe? Employe { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // Congé annuel, congé circonstanciel, maladie, maternité, mission, sans solde, ...

    public DateTime DateDebut { get; set; }

    public DateTime DateFin { get; set; }

    public bool EstPaye { get; set; }
}

