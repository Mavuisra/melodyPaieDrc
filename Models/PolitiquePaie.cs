using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Politique de paie versionnée par entreprise (barème, règles, rubriques).
/// </summary>
public class PolitiquePaie
{
    [Key]
    public int Id { get; set; }

    public int EntrepriseId { get; set; }

    [Required]
    [MaxLength(120)]
    public string Libelle { get; set; } = "Politique par défaut";

    public DateTime DateEffet { get; set; } = DateTime.Today;

    public bool Actif { get; set; } = true;

    [MaxLength(20)]
    public string Version { get; set; } = "1.0";

    public DateTime? UpdatedAtUtc { get; set; }

    public Entreprise? Entreprise { get; set; }
    public ICollection<ParametrePolitiquePaie> Parametres { get; set; } = new List<ParametrePolitiquePaie>();
    public ICollection<RubriqueBulletin> Rubriques { get; set; } = new List<RubriqueBulletin>();
}
