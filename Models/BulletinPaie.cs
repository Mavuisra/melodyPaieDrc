using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Bulletin de paie par employé et période.
/// </summary>
public class BulletinPaie
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Employe))]
    public int EmployeId { get; set; }

    public Employe? Employe { get; set; }

    [ForeignKey(nameof(PeriodePaie))]
    public int PeriodePaieId { get; set; }

    public PeriodePaie? PeriodePaie { get; set; }

    /// <summary>Numéro unique du bulletin (ex. 2025-03-001) par période.</summary>
    [MaxLength(20)]
    public string? NumeroBulletin { get; set; }

    public DateTime DateGeneration { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalGainImposable { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalGainNonImposable { get; set; }

    /// <summary>Brut total (gains imposables + non imposables), non persisté.</summary>
    [NotMapped]
    public decimal SalaireBrut => TotalGainImposable + TotalGainNonImposable;

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseIpr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MontantIprBrut { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReductionFamille { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MontantIprNet { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CotisationCnssOuvrier { get; set; }

    /// <summary>Retenue INPP salariale (fiche type LTS, ex. 3 % du brut imposable CNSS).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CotisationInpp { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetAPayer { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetAPayerDeviseLocale { get; set; }

    public ICollection<BulletinDetail> Details { get; set; } = new List<BulletinDetail>();
}
