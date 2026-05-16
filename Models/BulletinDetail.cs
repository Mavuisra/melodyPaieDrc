using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Détail de bulletin (ligne de gain ou de retenue).
/// </summary>
public class BulletinDetail
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(BulletinPaie))]
    public int BulletinPaieId { get; set; }

    public BulletinPaie? BulletinPaie { get; set; }

    [Required]
    [MaxLength(255)]
    public string Libelle { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseCalcul { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal Taux { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Gain { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Retenue { get; set; }
}

