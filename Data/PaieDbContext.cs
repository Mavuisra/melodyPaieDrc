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
            var ent = new Entreprise();
            LtServicesDonneesEntreprise.RemplirModeleVide(ent);
            Entreprises.Add(ent);
            SaveChanges();

            var etab = new Etablissement { EntrepriseId = ent.Id, NomSite = "Siège" };
            Etablissements.Add(etab);
            SaveChanges();

            foreach (var nomDept in LtServicesEffectifsSeeder.NomsDepartements)
            {
                Departements.Add(new Departement { EtablissementId = etab.Id, NomDepartement = nomDept });
            }
            SaveChanges();
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

        // Catégories professionnelles pour les contrats (Cadre, Maîtrise, Exécution)
        if (!CategoriesProfessionnelles.Any())
        {
            // Référence SMIG mensuel (CDF) issue du barème de classification commerce.
            CategoriesProfessionnelles.Add(new CategorieProfessionnelle { Libelle = "Cadre", SmigApplique = 2454270m });
            CategoriesProfessionnelles.Add(new CategorieProfessionnelle { Libelle = "Maîtrise", SmigApplique = 1379820m });
            CategoriesProfessionnelles.Add(new CategorieProfessionnelle { Libelle = "Exécution", SmigApplique = 377000m });
            SaveChanges();
        }
        else
        {
            // Mise à niveau pour les anciennes bases où SmigApplique était resté à 0.
            var categories = CategoriesProfessionnelles.ToList();
            var misAJour = false;
            foreach (var c in categories)
            {
                if (c.SmigApplique > 0)
                    continue;

                if (string.Equals(c.Libelle, "Cadre", StringComparison.OrdinalIgnoreCase))
                {
                    c.SmigApplique = 2454270m;
                    misAJour = true;
                }
                else if (string.Equals(c.Libelle, "Maîtrise", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(c.Libelle, "Maitrise", StringComparison.OrdinalIgnoreCase))
                {
                    c.SmigApplique = 1379820m;
                    misAJour = true;
                }
                else if (string.Equals(c.Libelle, "Exécution", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(c.Libelle, "Execution", StringComparison.OrdinalIgnoreCase))
                {
                    c.SmigApplique = 377000m;
                    misAJour = true;
                }
            }

            if (misAJour)
                SaveChanges();
        }

        // Barème IPR : LOI DE FINANCES 2020 (tranches mensuelles en CDF), modifiable ensuite via l'écran Paramètres IPR.
        if (!GrillesIpr.Any())
        {
            GrillesIpr.Add(new GrilleIPR
            {
                BorneInf = 0m,
                BorneSup = 162000m,
                Taux = 3m
            });
            GrillesIpr.Add(new GrilleIPR
            {
                BorneInf = 162001m,
                BorneSup = 1800000m,
                Taux = 15m
            });
            GrillesIpr.Add(new GrilleIPR
            {
                BorneInf = 1800001m,
                BorneSup = 3600000m,
                Taux = 30m
            });
            // Dernière tranche : "40 % pour le surplus" → borne supérieure = 0 pour signifier "illimitée"
            GrillesIpr.Add(new GrilleIPR
            {
                BorneInf = 3600001m,
                BorneSup = 0m,
                Taux = 40m
            });
            SaveChanges();
        }

        // Paramètres généraux IPR par défaut (modifiables) – ici, pas de plafond effectif ni de réduction fixe automatique.
        if (!ParametresIpr.Any())
        {
            ParametresIpr.Add(new ParametreIPR
            {
                TauxEffectifMaximum = 0m, // pas de plafond global appliqué par défaut
                ReductionParEnfant = 0m   // la réduction 2% par enfant reste à modéliser si souhaitée
            });
            SaveChanges();
        }

        // Taux sociaux (cotisations employé / employeur) — valeurs courantes RDC, modifiables dans Paramètres.
        if (!TauxSociaux.Any())
        {
            TauxSociaux.AddRange(new[]
            {
                new TauxSociaux { Code = "CNSS_Ouvrier", Pourcentage = 5m },
                new TauxSociaux { Code = "CNSS_Patronal", Pourcentage = 13m },
                new TauxSociaux { Code = "INPP", Pourcentage = 3m },
                new TauxSociaux { Code = "ONEM", Pourcentage = 0.5m }
            });
            SaveChanges();
        }

        // Calendrier de travail : au besoin, on pourra pré-remplir quelques jours fériés standards (laissé vide par défaut pour laisser la main à l'utilisateur).

        // Primes / indemnités de base les plus courantes en RDC
        if (!PrimesIndemnites.Any())
        {
            PrimesIndemnites.AddRange(new[]
            {
                // Primes (A = avantage, généralement imposables et cotisables)
                new PrimeIndemnite { Libelle = "Prime d'ancienneté", EstImposable = true, EstCotisable = true, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Prime de rendement", EstImposable = true, EstCotisable = true, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Prime d'assiduité", EstImposable = true, EstCotisable = true, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Prime de responsabilité", EstImposable = true, EstCotisable = true, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Prime de risque", EstImposable = true, EstCotisable = true, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Prime de fin d'année", EstImposable = true, EstCotisable = true, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },

            // Indemnités (souvent avantages non imposables / non cotisables selon politique ; modifiables ensuite)
                new PrimeIndemnite { Libelle = "Indemnité de transport", EstImposable = false, EstCotisable = false, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Indemnité de logement", EstImposable = true, EstCotisable = true, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Indemnité de déplacement", EstImposable = false, EstCotisable = false, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Indemnité de représentation", EstImposable = true, EstCotisable = true, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Prime / Indemnité panier", EstImposable = false, EstCotisable = false, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Indemnité de licenciement", EstImposable = false, EstCotisable = false, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Indemnité de congé", EstImposable = true, EstCotisable = true, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Allocations familiales", EstImposable = false, EstCotisable = false, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Congé de circonstance", EstImposable = true, EstCotisable = true, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage },
                new PrimeIndemnite { Libelle = "Indemnité maladie", EstImposable = true, EstCotisable = true, ModeCalcul = PrimeIndemnite.ModeFixe, TypeLigne = PrimeIndemnite.TypeAvantage }
            });
            SaveChanges();
        }
    }
}
