using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Saisie de paie mensuelle par employé et période (jours et ajustements).
/// </summary>
public class SaisiePaie
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Employe))]
    public int EmployeId { get; set; }

    public Employe? Employe { get; set; }

    [ForeignKey(nameof(PeriodePaie))]
    public int PeriodePaieId { get; set; }

    public PeriodePaie? PeriodePaie { get; set; }

    /// <summary>Nombre de jours prestés sur la période (0 => utiliser le calcul automatique).</summary>
    public int JoursPrestes { get; set; }

    /// <summary>Autres gains imposables (heures sup, primes exceptionnelles).</summary>
    public decimal AutresGainsImposables { get; set; }

    /// <summary>Autres gains non imposables.</summary>
    public decimal AutresGainsNonImposables { get; set; }

    /// <summary>Autres retenues (amendes, divers non classés).</summary>
    public decimal AutresRetenues { get; set; }

    /// <summary>Acomptes sur salaire versés pendant la période (retenues spécifiques).</summary>
    public decimal AcomptesSalaire { get; set; }

    /// <summary>Sanctions disciplinaires (retenues décidées par l'employeur).</summary>
    public decimal SanctionsDisciplinaires { get; set; }

    [MaxLength(255)]
    public string? Commentaire { get; set; }
}

