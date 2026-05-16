using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Jour de travail dans le calendrier (ouvré, férié, repos) pour une année donnée.
/// </summary>
public class JourTravailCalendrier
{
    [Key]
    public int Id { get; set; }

    public int? EntrepriseId { get; set; }

    public int Annee { get; set; }

    public DateTime DateJour { get; set; }

    /// <summary>
    /// Type de jour : Ouvre, Ferie, Repos (libellés simples pour l'UI).
    /// </summary>
    [MaxLength(20)]
    public string TypeJour { get; set; } = "Ouvre";

    /// <summary>Libellé (ex : Jour de l'an, Fête du travail, etc.).</summary>
    [MaxLength(255)]
    public string? Libelle { get; set; }
}

