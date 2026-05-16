using System.Linq;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Vérifie si l'entreprise courante est configurée avant d'utiliser paie / effectifs.
/// </summary>
public static class ConfigurationEntrepriseService
{
    public const string RaisonSocialePlaceholder = "Mon entreprise";

    /// <summary>Incrémenter pour réafficher l'assistant enrichi aux installations existantes.</summary>
    public const int VersionParcoursDemarrageCourante = 3;

    public const int NombreEtapesConfiguration = 4;

    public sealed class EtatConfiguration
    {
        public bool IdentiteRenseignee { get; init; }
        public bool IdentiteVisuelle { get; init; }
        public bool StructureOrganisationnelle { get; init; }
        public bool PolitiquePaieActive { get; init; }
        public bool CompteAdministrateur { get; init; }

        public bool EstComplete =>
            IdentiteRenseignee &&
            IdentiteVisuelle &&
            StructureOrganisationnelle &&
            PolitiquePaieActive &&
            CompteAdministrateur;

        public string Resume =>
            EstComplete
                ? "Configuration terminée."
                : "Étapes restantes : " + string.Join(", ",
                    new[]
                    {
                        IdentiteRenseignee ? null : "identité légale",
                        IdentiteVisuelle ? null : "logo et couleurs",
                        StructureOrganisationnelle ? null : "structure organisationnelle",
                        PolitiquePaieActive ? null : "paramètres de paie",
                        CompteAdministrateur ? null : "compte administrateur"
                    }.Where(x => x != null)!);
    }

    public static EtatConfiguration Evaluer(PaieDbContext db)
    {
        var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(db);
        if (entrepriseId <= 0)
        {
            return new EtatConfiguration();
        }

        var ent = db.Entreprises.AsNoTracking().FirstOrDefault(e => e.Id == entrepriseId);
        var identite = ent != null &&
                       !string.IsNullOrWhiteSpace(ent.RaisonSociale) &&
                       !string.Equals(ent.RaisonSociale.Trim(), RaisonSocialePlaceholder, StringComparison.OrdinalIgnoreCase);

        var visuel = ent != null && !string.IsNullOrWhiteSpace(ent.CouleurPrincipale);

        var structure = ContexteEntrepriseService.EtablissementsEntrepriseCourante(db).Any() &&
                        ContexteEntrepriseService.DepartementsEntrepriseCourante(db).Any();

        var politique = db.PolitiquesPaie.AsNoTracking()
            .Any(p => p.EntrepriseId == entrepriseId && p.Actif);

        var admin = db.Utilisateurs.AsNoTracking()
            .Any(u => u.Actif && u.Role == Utilisateur.RoleAdmin);

        return new EtatConfiguration
        {
            IdentiteRenseignee = identite,
            IdentiteVisuelle = visuel,
            StructureOrganisationnelle = structure,
            PolitiquePaieActive = politique,
            CompteAdministrateur = admin
        };
    }

    public static bool EstConfigurationComplete(PaieDbContext db) => Evaluer(db).EstComplete;

    public static bool DoitAfficherAssistantAuDemarrage(PaieDbContext db)
    {
        if (ParametresApplicationHelper.GetForcerAssistantProchainDemarrage(db))
            return true;
        if (ParametresApplicationHelper.GetVersionParcoursDemarrage(db) < VersionParcoursDemarrageCourante)
            return true;
        if (!AuthService.AdministrateurActifExiste(db))
            return true;
        if (AuthService.UnAdministrateurActifUtiliseIdentifiantsParDefaut(db))
            return true;
        return !EstConfigurationComplete(db);
    }

    public static string RaisonAffichageAssistant(PaieDbContext db)
    {
        if (ParametresApplicationHelper.GetForcerAssistantProchainDemarrage(db))
            return "option « forcer au prochain démarrage »";
        if (ParametresApplicationHelper.GetVersionParcoursDemarrage(db) < VersionParcoursDemarrageCourante)
            return "mise à jour du parcours de démarrage";
        if (!AuthService.AdministrateurActifExiste(db))
            return "aucun compte administrateur défini";
        if (AuthService.UnAdministrateurActifUtiliseIdentifiantsParDefaut(db))
            return "sécurité : remplacer les identifiants par défaut (admin/admin)";
        if (!EstConfigurationComplete(db))
            return "configuration entreprise incomplète";
        return "aucune";
    }

    public static void MarquerConfigurationTerminee(PaieDbContext db)
    {
        ParametresApplicationHelper.SetForcerAssistantProchainDemarrage(db, false);
        ParametresApplicationHelper.SetVersionParcoursDemarrage(db, VersionParcoursDemarrageCourante);
    }
}
