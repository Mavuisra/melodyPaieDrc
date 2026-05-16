using System;
using System.ComponentModel.DataAnnotations;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Paramètres de synchronisation cloud par entreprise / poste.
/// </summary>
public class SyncParametres
{
    [Key]
    public int Id { get; set; }

    public int EntrepriseId { get; set; }

    [MaxLength(80)]
    public string? EndpointUrl { get; set; }

    [MaxLength(40)]
    public string DeviceId { get; set; } = Guid.NewGuid().ToString("N")[..16];

    public bool SyncActive { get; set; }

    public DateTime? DerniereSyncUtc { get; set; }

    [MaxLength(80)]
    public string? JetonAcces { get; set; }
}
