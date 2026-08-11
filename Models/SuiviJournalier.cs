using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Suivi journalier : heures prestées, absences (justifiée, non justifiée), maladie.
/// Pris en compte directement dans le calcul de paie.
/// </summary>
public class SuiviJournalier
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Employe))]
    public int EmployeId { get; set; }

    public Employe? Employe { get; set; }

    public DateTime Date { get; set; }

    /// <summary>Nombre d'heures prestées (0-24). Si TypeJour = Normal, utilisé pour le calcul.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal HeuresPrestees { get; set; }

    /// <summary>Type du jour : Normal, Congé annuel, Congé de circonstance, Maladie, Absence, Préavis.</summary>
    [MaxLength(50)]
    public string TypeJour { get; set; } = "Normal";

    /// <summary>Horodatages bruts (JSON : liste ISO 8601), pour recalcul automatique LTservices.</summary>
    public string? PointagesJson { get; set; }

    /// <summary>Si true, <see cref="HeuresPrestees"/> a été modifiée manuellement et ne doit pas être recalculée depuis <see cref="PointagesJson"/>.</summary>
    public bool HeuresManuelles { get; set; }

    public const string TypeNormal = "Normal";
    public const string TypeAbsence = "Absence";
    public const string TypeCongeAnnuel = "Congé annuel";
    public const string TypeCongeCirconstance = "Congé de circonstance";
    public const string TypeMaladie = "Maladie";
    public const string TypePreavis = "Préavis";

    /// <summary>Jours spéciaux rémunérés à 100 % sans indemnités (hors pointage normal).</summary>
    public static bool EstTypeJourSpecialPaye(string? typeJour) =>
        string.Equals(typeJour, TypeMaladie, StringComparison.OrdinalIgnoreCase)
        || string.Equals(typeJour, TypeCongeCirconstance, StringComparison.OrdinalIgnoreCase)
        || string.Equals(typeJour, TypeCongeAnnuel, StringComparison.OrdinalIgnoreCase);
}
