using System.ComponentModel.DataAnnotations;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Paramètre clé/valeur d'une politique de paie (jours de référence, mode présence, etc.).
/// </summary>
public class ParametrePolitiquePaie
{
    [Key]
    public int Id { get; set; }

    public int PolitiquePaieId { get; set; }

    [Required]
    [MaxLength(80)]
    public string Cle { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Valeur { get; set; } = string.Empty;

    public PolitiquePaie? PolitiquePaie { get; set; }

    public static class Cles
    {
        public const string JoursReferencePaie = "JOURS_REFERENCE_PAIE";
        public const string HeuresParJour = "HEURES_PAR_JOUR";
        public const string SalaireContratEnNet = "SALAIRE_CONTRAT_EN_NET";
        public const string ModeCalculPresence = "MODE_CALCUL_PRESENCE";
        public const string UtiliserBaremeIpr = "UTILISER_BAREME_IPR";
        public const string UtiliserTauxSociauxDb = "UTILISER_TAUX_SOCIAUX_DB";
    }

    public const string ModePresencePointages = "POINTAGES_TERMINAL";
    public const string ModePresenceSaisieJours = "SAISIE_JOURS";
    public const string ModePresenceHybride = "HYBRIDE";
}
