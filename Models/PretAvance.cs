using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Prêt / avance sur salaire.
/// </summary>
public class PretAvance
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Employe))]
    public int EmployeId { get; set; }

    public Employe? Employe { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MontantTotal { get; set; }

    public DateTime DateOctroi { get; set; }

    public int NbEcheances { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MontantMensuel { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SoldeRestant { get; set; }

    [MaxLength(50)]
    public string? Statut { get; set; } // En cours, Terminé
}

