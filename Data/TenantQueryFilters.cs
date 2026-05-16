using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Data;

/// <summary>
/// Filtres globaux EF : chaque requête ne voit que l'entreprise active (multi-tenant).
/// Doit référencer <see cref="PaieDbContext.TenantId"/> (propriété du contexte), pas une instance capturée.
/// </summary>
public partial class PaieDbContext
{
    private void AppliquerFiltresTenant(ModelBuilder modelBuilder)
    {
        // Entreprise : pas de filtre global (liste des tenants + création de nouvelles entreprises).
        modelBuilder.Entity<Etablissement>()
            .HasQueryFilter(e => e.EntrepriseId == TenantId);

        modelBuilder.Entity<Departement>()
            .HasQueryFilter(d => d.Etablissement != null && d.Etablissement.EntrepriseId == TenantId);

        // Employé : EntrepriseId direct OU rattachement historique département → établissement.
        modelBuilder.Entity<Employe>()
            .HasQueryFilter(e => e.EntrepriseId == TenantId
                || (e.EntrepriseId <= 0
                    && e.Departement != null
                    && e.Departement.Etablissement != null
                    && e.Departement.Etablissement.EntrepriseId == TenantId));

        modelBuilder.Entity<Contrat>()
            .HasQueryFilter(c => c.Employe != null && (
                c.Employe.EntrepriseId == TenantId
                || (c.Employe.EntrepriseId <= 0 && c.Employe.Departement != null
                    && c.Employe.Departement.Etablissement != null
                    && c.Employe.Departement.Etablissement.EntrepriseId == TenantId)));

        modelBuilder.Entity<AyantDroit>()
            .HasQueryFilter(a => a.Employe != null && (
                a.Employe.EntrepriseId == TenantId
                || (a.Employe.EntrepriseId <= 0 && a.Employe.Departement != null
                    && a.Employe.Departement.Etablissement != null
                    && a.Employe.Departement.Etablissement.EntrepriseId == TenantId)));

        modelBuilder.Entity<PretAvance>()
            .HasQueryFilter(p => p.Employe != null && (
                p.Employe.EntrepriseId == TenantId
                || (p.Employe.EntrepriseId <= 0 && p.Employe.Departement != null
                    && p.Employe.Departement.Etablissement != null
                    && p.Employe.Departement.Etablissement.EntrepriseId == TenantId)));

        modelBuilder.Entity<AbsenceConge>()
            .HasQueryFilter(a => a.Employe != null && (
                a.Employe.EntrepriseId == TenantId
                || (a.Employe.EntrepriseId <= 0 && a.Employe.Departement != null
                    && a.Employe.Departement.Etablissement != null
                    && a.Employe.Departement.Etablissement.EntrepriseId == TenantId)));

        modelBuilder.Entity<SaisiePaie>()
            .HasQueryFilter(s => s.Employe != null && (
                s.Employe.EntrepriseId == TenantId
                || (s.Employe.EntrepriseId <= 0 && s.Employe.Departement != null
                    && s.Employe.Departement.Etablissement != null
                    && s.Employe.Departement.Etablissement.EntrepriseId == TenantId)));

        modelBuilder.Entity<SuiviJournalier>()
            .HasQueryFilter(s => s.Employe != null && (
                s.Employe.EntrepriseId == TenantId
                || (s.Employe.EntrepriseId <= 0 && s.Employe.Departement != null
                    && s.Employe.Departement.Etablissement != null
                    && s.Employe.Departement.Etablissement.EntrepriseId == TenantId)));

        modelBuilder.Entity<AffectationPrimeIndemnite>()
            .HasQueryFilter(a => a.Employe != null && (
                a.Employe.EntrepriseId == TenantId
                || (a.Employe.EntrepriseId <= 0 && a.Employe.Departement != null
                    && a.Employe.Departement.Etablissement != null
                    && a.Employe.Departement.Etablissement.EntrepriseId == TenantId)));

        modelBuilder.Entity<EmployeLibelleBulletin>()
            .HasQueryFilter(l => l.Employe != null && (
                l.Employe.EntrepriseId == TenantId
                || (l.Employe.EntrepriseId <= 0 && l.Employe.Departement != null
                    && l.Employe.Departement.Etablissement != null
                    && l.Employe.Departement.Etablissement.EntrepriseId == TenantId)));

        modelBuilder.Entity<PeriodePaie>()
            .HasQueryFilter(p => p.EntrepriseId == TenantId);

        modelBuilder.Entity<BulletinPaie>()
            .HasQueryFilter(b => b.Employe != null && (
                b.Employe.EntrepriseId == TenantId
                || (b.Employe.EntrepriseId <= 0 && b.Employe.Departement != null
                    && b.Employe.Departement.Etablissement != null
                    && b.Employe.Departement.Etablissement.EntrepriseId == TenantId)));

        modelBuilder.Entity<BulletinDetail>()
            .HasQueryFilter(d => d.BulletinPaie != null
                                 && d.BulletinPaie.Employe != null && (
                d.BulletinPaie.Employe.EntrepriseId == TenantId
                || (d.BulletinPaie.Employe.EntrepriseId <= 0
                    && d.BulletinPaie.Employe.Departement != null
                    && d.BulletinPaie.Employe.Departement.Etablissement != null
                    && d.BulletinPaie.Employe.Departement.Etablissement.EntrepriseId == TenantId)));

        modelBuilder.Entity<CategorieProfessionnelle>()
            .HasQueryFilter(c => c.EntrepriseId == TenantId);

        modelBuilder.Entity<PrimeIndemnite>()
            .HasQueryFilter(p => p.EntrepriseId == TenantId);

        modelBuilder.Entity<GrilleIPR>()
            .HasQueryFilter(g => g.EntrepriseId == TenantId);

        modelBuilder.Entity<TauxSociaux>()
            .HasQueryFilter(t => t.EntrepriseId == TenantId);

        modelBuilder.Entity<ParametreIPR>()
            .HasQueryFilter(p => p.EntrepriseId == TenantId);

        modelBuilder.Entity<JourTravailCalendrier>()
            .HasQueryFilter(j => j.EntrepriseId == TenantId);

        modelBuilder.Entity<PolitiquePaie>()
            .HasQueryFilter(p => p.EntrepriseId == TenantId);

        modelBuilder.Entity<ParametrePolitiquePaie>()
            .HasQueryFilter(p => p.PolitiquePaie != null && p.PolitiquePaie.EntrepriseId == TenantId);

        modelBuilder.Entity<RubriqueBulletin>()
            .HasQueryFilter(r => r.PolitiquePaie != null && r.PolitiquePaie.EntrepriseId == TenantId);

        modelBuilder.Entity<DefinitionChampDynamique>()
            .HasQueryFilter(d => d.EntrepriseId == TenantId);

        modelBuilder.Entity<ValeurChampDynamique>()
            .HasQueryFilter(v => v.DefinitionChamp != null && v.DefinitionChamp.EntrepriseId == TenantId);

        modelBuilder.Entity<SyncJournal>()
            .HasQueryFilter(s => s.EntrepriseId == TenantId);

        modelBuilder.Entity<SyncParametres>()
            .HasQueryFilter(s => s.EntrepriseId == TenantId);
    }
}
