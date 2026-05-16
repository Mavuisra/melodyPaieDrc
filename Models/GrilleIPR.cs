using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Grille IPR (barème d'imposition).
/// </summary>
public class GrilleIPR
{
    [Key]
    public int Id { get; set; }

    /// <summary>Null = barème global / héritage.</summary>
    public int? EntrepriseId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BorneInf { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BorneSup { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal Taux { get; set; }
}

