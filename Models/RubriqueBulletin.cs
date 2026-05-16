using System.ComponentModel.DataAnnotations;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Rubrique configurable affichée sur le bulletin (libellé, ordre, type).
/// </summary>
public class RubriqueBulletin
{
    [Key]
    public int Id { get; set; }

    public int PolitiquePaieId { get; set; }

    [Required]
    [MaxLength(60)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Libelle { get; set; } = string.Empty;

    /// <summary>GAIN, RETENUE, INFO.</summary>
    [MaxLength(20)]
    public string TypeLigne { get; set; } = TypeInfo;

    public int OrdreAffichage { get; set; }

    /// <summary>
    /// Source de calcul : AUCUNE, IPR_BAREME, CNSS_OUVRIER, INPP, SAISIE, PRIME, etc.
    /// </summary>
    [MaxLength(40)]
    public string SourceCalcul { get; set; } = SourceAucune;

    public bool AfficherSurBulletin { get; set; } = true;

    public PolitiquePaie? PolitiquePaie { get; set; }

    public const string TypeGain = "GAIN";
    public const string TypeRetenue = "RETENUE";
    public const string TypeInfo = "INFO";

    public const string SourceAucune = "AUCUNE";
    public const string SourceIprBareme = "IPR_BAREME";
    public const string SourceCnssOuvrier = "CNSS_OUVRIER";
    public const string SourceInpp = "INPP";
}
