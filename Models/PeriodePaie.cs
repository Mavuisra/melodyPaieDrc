using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Période de paie (mois + année).
/// </summary>
public class PeriodePaie
{
    [Key]
    public int Id { get; set; }

    public int? EntrepriseId { get; set; }

    /// <summary>
    /// Mois de paie (1-12).
    /// </summary>
    public int Mois { get; set; }

    public int Annee { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TauxChangeBudget { get; set; }

    public bool Cloturee { get; set; }

    public DateTime? DateClotureUtc { get; set; }

    [MaxLength(100)]
    public string? CloturePar { get; set; }

    public ICollection<BulletinPaie> Bulletins { get; set; } = new List<BulletinPaie>();
}

