using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Taux sociaux (CNSS ouvrier/patronal, INPP, ONEM, ...).
/// </summary>
public class TauxSociaux
{
    [Key]
    public int Id { get; set; }

    public int? EntrepriseId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty; // CNSS_Ouvrier, CNSS_Patronal, INPP, ONEM

    [Column(TypeName = "decimal(5,2)")]
    public decimal Pourcentage { get; set; }
}

