using System.ComponentModel.DataAnnotations;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Prime ou indemnité (logement, transport, retenue, etc.).
/// </summary>
public class PrimeIndemnite
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Libelle { get; set; } = string.Empty;

    public bool EstImposable { get; set; }

    public bool EstCotisable { get; set; }

    /// <summary>
    /// Mode de calcul :
    /// - FIXE : montant fixe mensuel
    /// - PRORATA_JOURS : prorata des jours prestés
    /// </summary>
    [MaxLength(20)]
    public string ModeCalcul { get; set; } = ModeFixe;

    /// <summary>
    /// Type de ligne :
    /// - A : Avantage (ajout au salaire)
    /// - R : Retenue (soustraction)
    /// </summary>
    [MaxLength(1)]
    public string TypeLigne { get; set; } = TypeAvantage;

    /// <summary>Numéro de compte comptable associé.</summary>
    [MaxLength(50)]
    public string? NumeroCompte { get; set; }

    public const string ModeFixe = "FIXE";
    public const string ModeProrataJours = "PRORATA_JOURS";
    public const string TypeAvantage = "A";
    public const string TypeRetenue = "R";

    public ICollection<AffectationPrimeIndemnite> Affectations { get; set; } = new List<AffectationPrimeIndemnite>();
}

