using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Paramètres globaux de l'application (une seule ligne, Id = 1).
/// </summary>
public class ParametresApplication
{
    public const int SingletonId = 1;

    [Key]
    public int Id { get; set; } = SingletonId;

    /// <summary>Nombre de francs congolais (CDF) pour 1 dollar US — taux utilisé pour les conversions de paie.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal TauxCdfParUsd { get; set; }

    public DateTime? DateDerniereModification { get; set; }

    /// <summary>Adresse IP du terminal ZKTeco (pointeuse réseau).</summary>
    [MaxLength(80)]
    public string? ZkTerminalIp { get; set; }

    /// <summary>Port TCP du terminal (4370 par défaut).</summary>
    public int ZkTerminalPort { get; set; } = 4370;

    /// <summary>Numéro de machine côté SDK (souvent 1).</summary>
    public int ZkMachineNumber { get; set; } = 1;

    /// <summary>Clé de communication PC (menu Connexion PC), numérique — ex. 000000 → 0.</summary>
    public int ZkCommPassword { get; set; }

    /// <summary>Synchronisation périodique des pointages vers le suivi journalier.</summary>
    public bool ZkSyncActif { get; set; }

    /// <summary>Intervalle minimal entre deux lectures du terminal (secondes).</summary>
    public int ZkIntervalleSecondes { get; set; } = 60;

    /// <summary>Horodatage UTC de la dernière synchronisation réussie.</summary>
    public DateTime? ZkDerniereSyncUtc { get; set; }

    /// <summary>Heure de début du service (format HH:mm, ex. 07:30).</summary>
    [MaxLength(5)]
    public string LtHeureDebutTravail { get; set; } = "07:30";

    /// <summary>Heure limite de tolérance d'entrée (format HH:mm, ex. 07:40).</summary>
    [MaxLength(5)]
    public string LtHeureLimiteTolerance { get; set; } = "07:40";

    /// <summary>Heure de début de pause (format HH:mm, ex. 12:00).</summary>
    [MaxLength(5)]
    public string LtHeureDebutPause { get; set; } = "12:00";

    /// <summary>Heure de fin de pause (format HH:mm, ex. 13:00).</summary>
    [MaxLength(5)]
    public string LtHeureFinPause { get; set; } = "13:00";

    /// <summary>Heure de fin de service lun.-ven. (format HH:mm, ex. 16:00).</summary>
    [MaxLength(5)]
    public string LtHeureFinSemaine { get; set; } = "16:00";

    /// <summary>Heure de fin de service samedi (format HH:mm, ex. 12:30).</summary>
    [MaxLength(5)]
    public string LtHeureFinSamedi { get; set; } = "12:30";
}
