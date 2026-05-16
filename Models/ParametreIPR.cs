using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Paramètres généraux pour le calcul de l'IPR.
/// Ces valeurs sont stockées en base pour être modifiables sans recompilation.
/// </summary>
public class ParametreIPR
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Taux effectif maximum appliqué sur la base imposable (ex : 0.30 = 30%).
    /// </summary>
    [Column(TypeName = "decimal(5,4)")]
    public decimal TauxEffectifMaximum { get; set; } = 0.30m;

    /// <summary>
    /// Réduction d'IPR par enfant à charge (montant mensuel).
    /// Laisser 0 si la réduction est gérée autrement.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal ReductionParEnfant { get; set; } = 0m;
}

