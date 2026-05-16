using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Contrat de travail (CDI, CDD, Journalier, ...).
/// </summary>
public class Contrat
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Employe))]
    public int EmployeId { get; set; }

    public Employe? Employe { get; set; }

    [Required]
    [MaxLength(30)]
    public string TypeContrat { get; set; } = string.Empty; // CDI, CDD, Journalier

    [Required]
    public DateTime DateDebut { get; set; }

    public DateTime? DateFin { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SalaireBase { get; set; }

    [Required]
    [MaxLength(10)]
    public string DeviseBase { get; set; } = "USD"; // USD / CDF

    [ForeignKey(nameof(CategorieProfessionnelle))]
    public int CategorieProfessionnelleId { get; set; }

    public CategorieProfessionnelle? CategorieProfessionnelle { get; set; }

    /// <summary>Majoration heures supplémentaires en pourcentage (ex : 50 = +50%).</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal TauxMajorationHeuresSup { get; set; }

    /// <summary>Majoration travail de nuit en pourcentage.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal TauxMajorationNuit { get; set; }

    /// <summary>Majoration jours fériés / dimanche en pourcentage.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal TauxMajorationJourFerie { get; set; }

    /// <summary>Base de calcul du préavis (nombre de mois de salaire de base).</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal PreavisMoisBase { get; set; }

    /// <summary>Base de calcul de l'indemnité de licenciement (nombre de mois de salaire de base).</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal IndemniteLicenciementMoisBase { get; set; }

    [NotMapped]
    public decimal SalaireJour => SalaireBase > 0 ? decimal.Round(SalaireBase / 26m, 2) : 0m;

    [NotMapped]
    public decimal SalaireHeure => SalaireBase > 0 ? decimal.Round(SalaireBase / 26m / 8m, 2) : 0m;
}
