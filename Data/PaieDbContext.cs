using System;
using System.IO;
using System.Linq;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Data;

/// <summary>
/// Contexte Entity Framework Core pour la base de données Paie RDC.
/// </summary>
public class PaieDbContext : DbContext
{
    public PaieDbContext()
    {
    }

    public PaieDbContext(DbContextOptions<PaieDbContext> options) : base(options)
    {
    }

    // Structure organisationnelle
    public DbSet<Entreprise> Entreprises { get; set; } = null!;
    public DbSet<Etablissement> Etablissements { get; set; } = null!;
    public DbSet<Departement> Departements { get; set; } = null!;

    // Référentiel employé & contrat
    public DbSet<Employe> Employes { get; set; } = null!;
    public DbSet<AyantDroit> AyantsDroit { get; set; } = null!;
    public DbSet<Contrat> Contrats { get; set; } = null!;
    public DbSet<CategorieProfessionnelle> CategoriesProfessionnelles { get; set; } = null!;

    // Variables, prêts, absences
    public DbSet<PretAvance> PretsAvances { get; set; } = null!;
    public DbSet<PrimeIndemnite> PrimesIndemnites { get; set; } = null!;
    public DbSet<AffectationPrimeIndemnite> AffectationsPrimesIndemnites { get; set; } = null!;
    public DbSet<EmployeLibelleBulletin> EmployesLibellesBulletin { get; set; } = null!;
    public DbSet<AbsenceConge> AbsencesConges { get; set; } = null!;
    public DbSet<SaisiePaie> SaisiesPaie { get; set; } = null!;
    public DbSet<SuiviJournalier> SuivisJournaliers { get; set; } = null!;

    // Paramètres globaux & légaux
    public DbSet<ParametresApplication> ParametresApplication { get; set; } = null!;
    public DbSet<PeriodePaie> PeriodesPaie { get; set; } = null!;
    public DbSet<GrilleIPR> GrillesIpr { get; set; } = null!;
    public DbSet<TauxSociaux> TauxSociaux { get; set; } = null!;
    public DbSet<ParametreIPR> ParametresIpr { get; set; } = null!;
    public DbSet<JourTravailCalendrier> JoursTravailCalendrier { get; set; } = null!;

    // Sorties
    public DbSet<BulletinPaie> BulletinsPaie { get; set; } = null!;
    public DbSet<BulletinDetail> BulletinsDetails { get; set; } = null!;

    // Multi-utilisateurs / rôles
    public DbSet<Utilisateur> Utilisateurs { get; set; } = null!;

    // Politique de paie extensible
    public DbSet<PolitiquePaie> PolitiquesPaie { get; set; } = null!;
    public DbSet<ParametrePolitiquePaie> ParametresPolitiquePaie { get; set; } = null!;
    public DbSet<RubriqueBulletin> RubriquesBulletin { get; set; } = null!;

    // Champs dynamiques (EAV)
    public DbSet<DefinitionChampDynamique> DefinitionsChampsDynamiques { get; set; } = null!;
    public DbSet<ValeurChampDynamique> ValeursChampsDynamiques { get; set; } = null!;

    // Synchronisation offline / cloud
    public DbSet<SyncJournal> SyncJournaux { get; set; } = null!;
    public DbSet<SyncParametres> SyncParametres { get; set; } = null!;

    // Formulaires dynamiques (métadonnées JSON + valeurs extensibles)
    public DbSet<FormFieldValue> FormFieldValues { get; set; } = null!;

    /// <summary>
    /// Dossier des données (base, logos, config). Toujours dans AppData pour éviter les refus d'accès (Program Files).
    /// </summary>
    public static string GetDataDirectory()
    {
        var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MelodyPaieRDC", "Data");
        return appDataDir;
    }

    /// <summary>
    /// Chemin du fichier base de données. Utilisé pour sauvegarde / restauration.
    /// </summary>
    public static string GetDatabasePath()
    {
        return Path.Combine(GetDataDirectory(), "PaieRDC.db");
    }

    /// <summary>
    /// Configure la connexion SQLite vers Data/PaieRDC.db.
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite($"Data Source={GetDatabasePath()}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Exemple de configuration fluide supplémentaire (optionnel, car beaucoup de choses sont déjà dans les Data Annotations)

        modelBuilder.Entity<Employe>()
            .HasIndex(e => e.Matricule)
            .IsUnique();

        modelBuilder.Entity<Entreprise>()
            .HasMany(e => e.Etablissements)
            .WithOne(s => s.Entreprise)
            .HasForeignKey(s => s.EntrepriseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Etablissement>()
            .HasMany(e => e.Departements)
            .WithOne(d => d.Etablissement)
            .HasForeignKey(d => d.EtablissementId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Departement>()
            .HasMany(d => d.Employes)
            .WithOne(e => e.Departement)
            .HasForeignKey(e => e.DepartementId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employe>()
            .HasMany(e => e.Contrats)
            .WithOne(c => c.Employe)
            .HasForeignKey(c => c.EmployeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employe>()
            .HasMany(e => e.AyantsDroit)
            .WithOne(a => a.Employe)
            .HasForeignKey(a => a.EmployeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Employe>()
            .HasMany(e => e.PretsAvances)
            .WithOne(p => p.Employe)
            .HasForeignKey(p => p.EmployeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employe>()
            .HasMany(e => e.AbsencesConges)
            .WithOne(a => a.Employe)
            .HasForeignKey(a => a.EmployeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employe>()
            .HasMany(e => e.SuivisJournaliers)
            .WithOne(s => s.Employe)
            .HasForeignKey(s => s.EmployeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employe>()
            .HasMany(e => e.AffectationsPrimesIndemnites)
            .WithOne(a => a.Employe)
            .HasForeignKey(a => a.EmployeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Employe>()
            .HasMany(e => e.LibellesBulletin)
            .WithOne(l => l.Employe)
            .HasForeignKey(l => l.EmployeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PrimeIndemnite>()
            .HasMany(p => p.Affectations)
            .WithOne(a => a.PrimeIndemnite)
            .HasForeignKey(a => a.PrimeIndemniteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PeriodePaie>()
            .HasMany(p => p.Bulletins)
            .WithOne(b => b.PeriodePaie)
            .HasForeignKey(b => b.PeriodePaieId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BulletinPaie>()
            .HasMany(b => b.Details)
            .WithOne(d => d.BulletinPaie)
            .HasForeignKey(d => d.BulletinPaieId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FormFieldValue>()
            .HasIndex(f => new { f.FormId, f.EntityType, f.EntityId, f.FieldKey })
            .IsUnique();

        modelBuilder.Entity<PolitiquePaie>()
            .HasMany(p => p.Parametres)
            .WithOne(x => x.PolitiquePaie)
            .HasForeignKey(x => x.PolitiquePaieId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PolitiquePaie>()
            .HasMany(p => p.Rubriques)
            .WithOne(r => r.PolitiquePaie)
            .HasForeignKey(r => r.PolitiquePaieId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DefinitionChampDynamique>()
            .HasMany(d => d.Valeurs)
            .WithOne(v => v.DefinitionChamp)
            .HasForeignKey(v => v.DefinitionChampId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// Dossier des définitions de formulaires JSON (modifiable sans recompilation).
    /// </summary>
    public static string GetFormsDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MelodyPaieRDC",
            "Forms");
    }

    /// <summary>
    /// Insère une structure minimale (Entreprise, Etablissement, Département) si la base est vide.
    /// Assure aussi qu'il existe au moins une période de paie pour générer des bulletins.
    /// </summary>
    public void SeedSiVide()
    {
        ParametresApplicationHelper.EnsureRow(this);

        if (!Entreprises.Any())
        {
            var ent = new Entreprise { RaisonSociale = "Mon entreprise" };
            Entreprises.Add(ent);
            SaveChanges();

            var etab = new Etablissement { EntrepriseId = ent.Id, NomSite = "Siège" };
            Etablissements.Add(etab);
            SaveChanges();

            Departements.Add(new Departement { EtablissementId = etab.Id, NomDepartement = "Direction générale" });
            SaveChanges();

            ContexteEntrepriseService.EntrepriseCouranteId = ent.Id;
        }

        var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(this);
        if (entrepriseId > 0)
        {
            if (!PolitiquesPaie.Any(p => p.EntrepriseId == entrepriseId))
            {
                PolitiquesPaie.Add(DonneesPaieReferenceSeed.CreerPolitiqueParDefaut(entrepriseId));
                SaveChanges();
            }
        }

        // Premier utilisateur par défaut (admin / admin) si aucun utilisateur
        if (!Utilisateurs.Any())
        {
            var (hash, salt) = AuthService.HashMotDePasse("admin");
            Utilisateurs.Add(new Utilisateur
            {
                Login = "admin",
                MotDePasseHash = hash,
                Salt = salt,
                NomComplet = "Administrateur",
                Role = Utilisateur.RoleAdmin,
                Actif = true,
                DateCreation = DateTime.UtcNow
            });
            SaveChanges();
        }

        // Toujours avoir au moins une période de paie (mois en cours) pour le calcul de paie
        if (!PeriodesPaie.Any())
        {
            var now = DateTime.Today;
            var taux = ParametresApplicationHelper.GetTauxCdfParUsd(this);
            PeriodesPaie.Add(new PeriodePaie { Mois = now.Month, Annee = now.Year, TauxChangeBudget = taux, Cloturee = false });
            SaveChanges();
        }

        DonneesPaieReferenceSeed.SeedCategoriesSiVide(this, null);
        DonneesPaieReferenceSeed.SeedReferentielLegalSiVide(this, null);
        DonneesPaieReferenceSeed.SeedPrimesCourantesSiVide(this, null);

        if (entrepriseId > 0)
        {
            DonneesPaieReferenceSeed.SeedReferentielLegalSiVide(this, entrepriseId);
            DonneesPaieReferenceSeed.SeedPrimesCourantesSiVide(this, entrepriseId);
        }
    }
}
