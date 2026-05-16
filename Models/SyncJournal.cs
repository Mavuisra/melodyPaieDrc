using System;
using System.ComponentModel.DataAnnotations;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Journal des modifications à synchroniser vers le cloud (offline-first).
/// </summary>
public class SyncJournal
{
    [Key]
    public long Id { get; set; }

    public int? EntrepriseId { get; set; }

    [Required]
    [MaxLength(80)]
    public string NomTable { get; set; } = string.Empty;

    public int EnregistrementId { get; set; }

    /// <summary>INSERT, UPDATE, DELETE.</summary>
    [MaxLength(10)]
    public string Operation { get; set; } = "UPDATE";

    public string? PayloadJson { get; set; }

    public DateTime DateModificationUtc { get; set; } = DateTime.UtcNow;

    public DateTime? DateSyncUtc { get; set; }

    public bool Conflit { get; set; }

    [MaxLength(40)]
    public string? DeviceId { get; set; }
}
