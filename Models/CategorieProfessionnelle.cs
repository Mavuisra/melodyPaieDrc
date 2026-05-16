using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Catégorie professionnelle (Cadre, Maîtrise, Exécution, ...).
/// </summary>
public class CategorieProfessionnelle
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Libelle { get; set; } = string.Empty;

    /// <summary>
    /// SMIG appliqué pour cette catégorie.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SmigApplique { get; set; }

    public ICollection<Contrat> Contrats { get; set; } = new List<Contrat>();
}

