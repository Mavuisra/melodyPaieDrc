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

        /// <summary>CALENDAIRE (défaut) ou DECALEE (ex. 26→25).</summary>
        public const string TypePeriodePaie = "TYPE_PERIODE_PAIE";
        public const string JourDebutPeriodeDecalee = "JOUR_DEBUT_PERIODE";
        public const string JourFinPeriodeDecalee = "JOUR_FIN_PERIODE";

        /// <summary>Samedi payé/présent sans pointage (tous les employés).</summary>
        public const string ForcerSamediOuvre = "FORCER_SAMEDI_OUVRE";
        /// <summary>Jours ouvrés sans saisie = heures calendrier (aligne paie et grille).</summary>
        public const string CompleterJoursSansSaisie = "COMPLETER_JOURS_SANS_SAISIE";

        public const string RetardSanctionActive = "RETARD_SANCTION_ACTIVE";
        public const string RetardSeuilMinutes = "RETARD_SEUIL_MINUTES";
        public const string RetardModeSanction = "RETARD_MODE_SANCTION";
    }

    public const string TypePeriodeCalendaire = "CALENDAIRE";
    public const string TypePeriodeDecalee = "DECALEE";

    public const string RetardModeAucun = "AUCUN";
    public const string RetardModeHoraire = "HORAIRE";
    public const string RetardModeDemiJour = "DEMI_JOUR";

    public const string ModePresencePointages = "POINTAGES_TERMINAL";
    public const string ModePresenceSaisieJours = "SAISIE_JOURS";
    public const string ModePresenceHybride = "HYBRIDE";
}
