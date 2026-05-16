using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Valeur d'un champ dynamique pour une instance d'entité (Id de l'entité cible).
/// </summary>
public class ValeurChampDynamique
{
    [Key]
    public int Id { get; set; }

    public int DefinitionChampId { get; set; }

    public int EntiteId { get; set; }

    [MaxLength(2000)]
    public string? ValeurTexte { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? ValeurNombre { get; set; }

    public DateTime? ValeurDate { get; set; }

    public bool? ValeurBooleen { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public DefinitionChampDynamique? DefinitionChamp { get; set; }
}
