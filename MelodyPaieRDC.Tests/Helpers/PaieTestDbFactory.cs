using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Tests.Helpers;

/// <summary>Base SQLite en mémoire partagée + données minimales pour les tests de paie.</summary>
public sealed class PaieTestDbFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public PaieTestDbFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var db = CreateContext();
        db.Database.EnsureCreated();
        SchemaSqliteApplicator.AppliquerSchema(db);
        SchemaSqliteApplicatorExtensible.AppliquerSiNecessaire(db);
        db.SeedSiVide();
    }

    public PaieDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaieDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new PaieDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}

public sealed class PaieTestScenario
{
    public required PaieDbContext Db { get; init; }
    public required int EntrepriseId { get; init; }
    public required int EmployeId { get; init; }
    public required int PeriodeId { get; init; }
    public required int ContratId { get; init; }
    public required int DepartementId { get; init; }

    public static PaieTestScenario Creer(
        PaieTestDbFactory factory,
        int anneePeriode = 2024,
        int moisPeriode = 1,
        decimal salaireBase = 2_600_000m,
        Action<PaieDbContext, int>? configurer = null)
    {
        var db = factory.CreateContext();
        var entrepriseId = db.Entreprises.IgnoreQueryFilters().Select(e => e.Id).First();
        db.SetTenant(entrepriseId);

        var departementId = db.Departements.Select(d => d.Id).First();

        var periode = db.PeriodesPaie
            .FirstOrDefault(p => p.Annee == anneePeriode && p.Mois == moisPeriode);
        if (periode == null)
        {
            periode = new PeriodePaie
            {
                Mois = moisPeriode,
                Annee = anneePeriode,
                TauxChangeBudget = 2800m,
                Cloturee = false,
                EntrepriseId = entrepriseId
            };
            db.PeriodesPaie.Add(periode);
            db.SaveChanges();
        }

        var categorieId = db.CategoriesProfessionnelles.Select(c => c.Id).First();
        var suffixe = Guid.NewGuid().ToString("N")[..6];

        var employe = new Employe
        {
            Nom = "Test",
            Prenom = "Employé",
            Matricule = $"TST{suffixe}",
            EntrepriseId = entrepriseId,
            DepartementId = departementId
        };
        db.Employes.Add(employe);
        db.SaveChanges();

        var dateDebut = new DateTime(anneePeriode, moisPeriode, 1);
        var contrat = new Contrat
        {
            EmployeId = employe.Id,
            TypeContrat = "CDI",
            DateDebut = dateDebut.AddMonths(-1),
            SalaireBase = salaireBase,
            DeviseBase = "CDF",
            CategorieProfessionnelleId = categorieId,
            TauxMajorationHeuresSup = 50m
        };
        db.Contrats.Add(contrat);
        db.SaveChanges();

        configurer?.Invoke(db, employe.Id);

        return new PaieTestScenario
        {
            Db = db,
            EntrepriseId = entrepriseId,
            EmployeId = employe.Id,
            PeriodeId = periode.Id,
            ContratId = contrat.Id,
            DepartementId = departementId
        };
    }

    public void DefinirModePresenceSaisieJours(int joursPrestes)
    {
        DefinirParametrePolitique(ParametrePolitiquePaie.Cles.ModeCalculPresence, ParametrePolitiquePaie.ModePresenceSaisieJours);
        Db.SaisiesPaie.Add(new SaisiePaie
        {
            EmployeId = EmployeId,
            PeriodePaieId = PeriodeId,
            JoursPrestes = joursPrestes
        });
        Db.SaveChanges();
    }

    public void DefinirParametrePolitique(string cle, string valeur)
    {
        var politique = Db.PolitiquesPaie
            .Include(p => p.Parametres)
            .First(p => p.EntrepriseId == EntrepriseId && p.Actif);

        var param = politique.Parametres.FirstOrDefault(p => p.Cle == cle);
        if (param == null)
        {
            param = new ParametrePolitiquePaie { Cle = cle, Valeur = valeur };
            politique.Parametres.Add(param);
        }
        else
        {
            param.Valeur = valeur;
        }

        Db.SaveChanges();
    }

    /// <summary>Contrat en brut : taxes déduites du salaire ; présence stricte sans complétion automatique.</summary>
    public void DefinirModeBrutClassique()
    {
        DefinirParametrePolitique(ParametrePolitiquePaie.Cles.SalaireContratEnNet, "false");
        DefinirParametrePolitique(ParametrePolitiquePaie.Cles.CompleterJoursSansSaisie, "false");
    }

    public PrimeIndemnite AjouterPrime(
        string libelle,
        decimal montantAffectation,
        string modeCalcul = PrimeIndemnite.ModeFixe,
        bool estImposable = true,
        bool estCotisable = true)
    {
        var prime = new PrimeIndemnite
        {
            EntrepriseId = EntrepriseId,
            Libelle = libelle,
            ModeCalcul = modeCalcul,
            EstImposable = estImposable,
            EstCotisable = estCotisable,
            TypeLigne = PrimeIndemnite.TypeAvantage,
            OrdreAffichage = 100
        };
        Db.PrimesIndemnites.Add(prime);
        Db.SaveChanges();

        Db.AffectationsPrimesIndemnites.Add(new AffectationPrimeIndemnite
        {
            EmployeId = EmployeId,
            PrimeIndemniteId = prime.Id,
            Montant = montantAffectation
        });
        Db.SaveChanges();
        return prime;
    }

    public void AjouterSuiviManuel(DateTime date, decimal heures)
    {
        Db.SuivisJournaliers.Add(new SuiviJournalier
        {
            EmployeId = EmployeId,
            Date = date.Date,
            TypeJour = SuiviJournalier.TypeNormal,
            HeuresPrestees = heures,
            HeuresManuelles = true
        });
        Db.SaveChanges();
    }

    public void AjouterSuiviPointages(DateTime date, IReadOnlyList<DateTime> pointages)
    {
        Db.SuivisJournaliers.Add(new SuiviJournalier
        {
            EmployeId = EmployeId,
            Date = date.Date,
            TypeJour = SuiviJournalier.TypeNormal,
            PointagesJson = PointagesJournalierSerializer.Serialiser(pointages),
            HeuresManuelles = false
        });
        Db.SaveChanges();
    }

    public BulletinPaie GenererBulletin()
    {
        var service = new CalculPaieService(Db);
        return service.GenererBulletin(EmployeId, PeriodeId);
    }
}
