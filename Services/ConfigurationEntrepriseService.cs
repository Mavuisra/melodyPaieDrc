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

    public sealed class EtatConfiguration
    {
        public bool IdentiteRenseignee { get; init; }
        public bool StructureOrganisationnelle { get; init; }
        public bool PolitiquePaieActive { get; init; }
        public bool EstComplete => IdentiteRenseignee && StructureOrganisationnelle && PolitiquePaieActive;

        public string Resume =>
            EstComplete
                ? "Configuration terminée."
                : "Étapes restantes : " + string.Join(", ",
                    new[]
                    {
                        IdentiteRenseignee ? null : "identité entreprise",
                        StructureOrganisationnelle ? null : "site et département",
                        PolitiquePaieActive ? null : "politique de paie"
                    }.Where(x => x != null)!);
    }

    public static EtatConfiguration Evaluer(PaieDbContext db)
    {
        var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(db);
        if (entrepriseId <= 0)
        {
            return new EtatConfiguration
            {
                IdentiteRenseignee = false,
                StructureOrganisationnelle = false,
                PolitiquePaieActive = false
            };
        }

        var ent = db.Entreprises.AsNoTracking().FirstOrDefault(e => e.Id == entrepriseId);
        var identite = ent != null &&
                       !string.IsNullOrWhiteSpace(ent.RaisonSociale) &&
                       !string.Equals(ent.RaisonSociale.Trim(), RaisonSocialePlaceholder, StringComparison.OrdinalIgnoreCase);

        var structure = ContexteEntrepriseService.EtablissementsEntrepriseCourante(db).Any() &&
                        ContexteEntrepriseService.DepartementsEntrepriseCourante(db).Any();

        var politique = db.PolitiquesPaie.AsNoTracking()
            .Any(p => p.EntrepriseId == entrepriseId && p.Actif);

        return new EtatConfiguration
        {
            IdentiteRenseignee = identite,
            StructureOrganisationnelle = structure,
            PolitiquePaieActive = politique
        };
    }

    public static bool EstConfigurationComplete(PaieDbContext db) => Evaluer(db).EstComplete;
}
