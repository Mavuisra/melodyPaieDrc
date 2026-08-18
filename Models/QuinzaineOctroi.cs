using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>Octroi de quinzaine (acompte) rattaché à un employé et une période de paie.</summary>
public class QuinzaineOctroi
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Employe))]
    public int EmployeId { get; set; }

    public Employe? Employe { get; set; }

    [ForeignKey(nameof(PeriodePaie))]
    public int PeriodePaieId { get; set; }

    public PeriodePaie? PeriodePaie { get; set; }

    public DateTime DateOctroi { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Montant { get; set; }

    [MaxLength(255)]
    public string? Commentaire { get; set; }
}
