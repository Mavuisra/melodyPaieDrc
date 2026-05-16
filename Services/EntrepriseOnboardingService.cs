using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Création d'une nouvelle entreprise (tenant) et parcours de configuration.
/// </summary>
public static class EntrepriseOnboardingService
{
    /// <summary>
    /// Crée une entreprise vide (sans politique de paie) et la définit comme entreprise courante.
    /// </summary>
    public static int CreerNouvelleEntreprise(PaieDbContext db, string raisonSociale)
    {
        var nom = raisonSociale.Trim();
        if (string.IsNullOrWhiteSpace(nom))
            throw new ArgumentException("La raison sociale est obligatoire.", nameof(raisonSociale));

        if (string.Equals(nom, ConfigurationEntrepriseService.RaisonSocialePlaceholder, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choisissez un nom d'entreprise distinct de « Mon entreprise ».", nameof(raisonSociale));

        var ent = new Entreprise { RaisonSociale = nom };
        db.Entreprises.Add(ent);
        db.SaveChanges();

        db.SetTenant(ent.Id);

        if (!db.Etablissements.IgnoreQueryFilters().Any(e => e.EntrepriseId == ent.Id))
        {
            var etab = new Etablissement { EntrepriseId = ent.Id, NomSite = "Siège" };
            db.Etablissements.Add(etab);
            db.SaveChanges();
            db.Departements.Add(new Departement { EtablissementId = etab.Id, NomDepartement = "Direction générale" });
            db.SaveChanges();
        }

        DonneesPaieReferenceSeed.SeedCategoriesSiVide(db, ent.Id);
        DonneesPaieReferenceSeed.SeedReferentielLegalSiVide(db, ent.Id);
        DonneesPaieReferenceSeed.SeedPrimesCourantesSiVide(db, ent.Id);
        if (!db.PolitiquesPaie.Any(p => p.EntrepriseId == ent.Id))
        {
            db.PolitiquesPaie.Add(DonneesPaieReferenceSeed.CreerPolitiqueParDefaut(ent.Id));
            db.SaveChanges();
        }

        ParametresApplicationHelper.EnsureRowForEntreprise(db, ent.Id);
        ParametresApplicationHelper.SetForcerAssistantProchainDemarrage(db, true);

        return ent.Id;
    }
}
