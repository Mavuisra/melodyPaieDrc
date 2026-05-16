using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Affectation d'une prime ou indemnité à un employé (montant mensuel).
/// </summary>
public class AffectationPrimeIndemnite
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Employe))]
    public int EmployeId { get; set; }

    public Employe? Employe { get; set; }

    [ForeignKey(nameof(PrimeIndemnite))]
    public int PrimeIndemniteId { get; set; }

    public PrimeIndemnite? PrimeIndemnite { get; set; }

    /// <summary>Montant mensuel de la prime pour cet employé.</summary>
    public decimal Montant { get; set; }
}
