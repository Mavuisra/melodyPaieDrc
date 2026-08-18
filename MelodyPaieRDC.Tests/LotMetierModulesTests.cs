using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.Tests.Helpers;

namespace MelodyPaieRDC.Tests;

public class AncienneteCongeHelperTests
{
    [Fact]
    public void ResoudreDateEmbauche_prend_le_premier_contrat()
    {
        var contrats = new[]
        {
            new Contrat { DateDebut = new DateTime(2022, 6, 15) },
            new Contrat { DateDebut = new DateTime(2020, 3, 1) }
        };

        var embauche = AncienneteCongeHelper.ResoudreDateEmbauche(contrats);

        Assert.Equal(new DateTime(2020, 3, 1), embauche);
    }

    [Fact]
    public void CalculerJoursCongesAnnuels_applique_1_5_jour_par_mois_et_plancher_12()
    {
        var embauche = new DateTime(2024, 1, 1);

        Assert.Equal(0m, AncienneteCongeHelper.CalculerJoursCongesAnnuels(embauche, new DateTime(2024, 1, 15)));
        Assert.Equal(9m, AncienneteCongeHelper.CalculerJoursCongesAnnuels(embauche, new DateTime(2024, 7, 1)));
        Assert.Equal(18m, AncienneteCongeHelper.CalculerJoursCongesAnnuels(embauche, new DateTime(2025, 1, 1)));
    }

    [Fact]
    public void FormaterAnciennete_affiche_annees_mois_jours()
    {
        var texte = AncienneteCongeHelper.FormaterAnciennete(new DateTime(2023, 1, 1), new DateTime(2025, 3, 1));
        Assert.Equal("2 an(s), 2 mois, 0 jour(s)", texte);
    }
}

public class ContratSuppressionGuardTests : IDisposable
{
    private readonly PaieTestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void Autorise_si_aucun_bulletin_ni_pret()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        scenario.Db.SetTenant(scenario.EntrepriseId);

        var diagnostic = ContratSuppressionGuard.Analyser(scenario.Db, scenario.EmployeId);

        Assert.True(diagnostic.PeutSupprimer);
        Assert.False(diagnostic.DemanderConfirmationPrimes);
    }

    [Fact]
    public void Bloque_si_bulletin_existe()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        scenario.DefinirModePresenceSaisieJours(26);
        scenario.GenererBulletin();

        var diagnostic = ContratSuppressionGuard.Analyser(scenario.Db, scenario.EmployeId);

        Assert.False(diagnostic.PeutSupprimer);
        Assert.Contains("bulletins", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bloque_si_pret_en_cours()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        scenario.Db.SetTenant(scenario.EntrepriseId);
        scenario.Db.PretsAvances.Add(new PretAvance
        {
            EmployeId = scenario.EmployeId,
            MontantTotal = 1_000m,
            DateOctroi = new DateTime(2024, 1, 1),
            DateDebutEcheance = new DateTime(2024, 1, 1),
            NbEcheances = 4,
            MontantMensuel = 250m,
            SoldeRestant = 1_000m,
            Statut = "En cours"
        });
        scenario.Db.SaveChanges();

        var diagnostic = ContratSuppressionGuard.Analyser(scenario.Db, scenario.EmployeId);

        Assert.False(diagnostic.PeutSupprimer);
        Assert.Contains("prêt", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Confirme_si_primes_sans_bloquer()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        scenario.AjouterPrime("Prime test", 50_000m);

        var diagnostic = ContratSuppressionGuard.Analyser(scenario.Db, scenario.EmployeId);

        Assert.True(diagnostic.PeutSupprimer);
        Assert.True(diagnostic.DemanderConfirmationPrimes);
    }
}

public class QuinzaineOctroiServiceTests : IDisposable
{
    private readonly PaieTestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void SynchroniserAcomptes_somme_les_octrois_et_persiste()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        scenario.Db.SetTenant(scenario.EntrepriseId);
        scenario.Db.QuinzaineOctrois.AddRange(
            new QuinzaineOctroi
            {
                EmployeId = scenario.EmployeId,
                PeriodePaieId = scenario.PeriodeId,
                DateOctroi = new DateTime(2024, 1, 10),
                Montant = 100m,
                Commentaire = "1re quinzaine"
            },
            new QuinzaineOctroi
            {
                EmployeId = scenario.EmployeId,
                PeriodePaieId = scenario.PeriodeId,
                DateOctroi = new DateTime(2024, 1, 25),
                Montant = 50m
            });
        scenario.Db.SaveChanges();

        QuinzaineOctroiService.SynchroniserAcomptesPeriode(scenario.Db, scenario.EmployeId, scenario.PeriodeId);
        scenario.Db.SaveChanges();

        var saisie = scenario.Db.SaisiesPaie.Single(s => s.EmployeId == scenario.EmployeId && s.PeriodePaieId == scenario.PeriodeId);
        Assert.Equal(150m, saisie.AcomptesSalaire);

        using var db2 = _factory.CreateContext();
        db2.SetTenant(scenario.EntrepriseId);
        var recharge = db2.SaisiesPaie.Single(s => s.EmployeId == scenario.EmployeId && s.PeriodePaieId == scenario.PeriodeId);
        Assert.Equal(150m, recharge.AcomptesSalaire);
        Assert.Equal(2, db2.QuinzaineOctrois.Count(q => q.EmployeId == scenario.EmployeId));
    }

    [Fact]
    public void Suppression_octroi_remet_acomptes_a_jour()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        scenario.Db.SetTenant(scenario.EntrepriseId);
        var octroi = new QuinzaineOctroi
        {
            EmployeId = scenario.EmployeId,
            PeriodePaieId = scenario.PeriodeId,
            DateOctroi = new DateTime(2024, 1, 10),
            Montant = 80m
        };
        scenario.Db.QuinzaineOctrois.Add(octroi);
        scenario.Db.SaveChanges();
        QuinzaineOctroiService.SynchroniserAcomptesPeriode(scenario.Db, scenario.EmployeId, scenario.PeriodeId);
        scenario.Db.SaveChanges();

        scenario.Db.QuinzaineOctrois.Remove(octroi);
        scenario.Db.SaveChanges();
        QuinzaineOctroiService.SynchroniserAcomptesPeriode(scenario.Db, scenario.EmployeId, scenario.PeriodeId);
        scenario.Db.SaveChanges();

        Assert.Equal(0m, scenario.Db.SaisiesPaie.Single().AcomptesSalaire);
    }
}

public class LivrePaieSyncServiceTests : IDisposable
{
    private readonly PaieTestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void Echec_si_aucun_bulletin()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        var resultat = LivrePaieSyncService.Synchroniser(scenario.Db, scenario.PeriodeId);

        Assert.False(resultat.Ok);
        Assert.Contains("Aucun bulletin", resultat.Message);
    }

    [Fact]
    public void Succes_recharge_les_montants_et_horodate()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        scenario.DefinirModePresenceSaisieJours(26);
        scenario.GenererBulletin();

        var resultat = LivrePaieSyncService.Synchroniser(scenario.Db, scenario.PeriodeId);

        Assert.True(resultat.Ok);
        Assert.Equal(1, resultat.NbBulletins);
        Assert.NotNull(resultat.HorodatageUtc);
        var param = scenario.Db.ParametresApplication.First(p => p.Id == ParametresApplication.SingletonId);
        Assert.NotNull(param.LivrePaieDerniereSyncUtc);
    }
}

public class HistoriquePointagePeriodeServiceTests : IDisposable
{
    private readonly PaieTestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void Filtre_employe_affiche_tous_les_jours_y_compris_sans_donnee()
    {
        var scenario = PaieTestScenario.Creer(_factory, anneePeriode: 2024, moisPeriode: 1);
        scenario.Db.SetTenant(scenario.EntrepriseId);
        scenario.AjouterSuiviPointages(
            new DateTime(2024, 1, 8),
            new[] { new DateTime(2024, 1, 8, 8, 0, 0), new DateTime(2024, 1, 8, 16, 0, 0) });

        var periode = scenario.Db.PeriodesPaie.First(p => p.Id == scenario.PeriodeId);
        var lignes = HistoriquePointagePeriodeService.Charger(scenario.Db, periode, scenario.EmployeId, null);

        Assert.True(lignes.Count >= 28);
        Assert.Contains(lignes, l => l.DateJour == new DateTime(2024, 1, 8) && !l.AucuneDonnee);
        Assert.Contains(lignes, l => l.AucuneDonnee && l.Statut == "Aucune donnée");
    }

    [Fact]
    public void Deduplique_les_pointages_a_la_meme_minute()
    {
        var jour = new DateTime(2024, 1, 8, 8, 0, 12);
        var doublon = new DateTime(2024, 1, 8, 8, 0, 45);
        var sortie = new DateTime(2024, 1, 8, 16, 0, 0);

        var fusion = PointagesJournalierSerializer.DedupliquerParMinute(new[] { jour, doublon, sortie });

        Assert.Equal(2, fusion.Count);
        Assert.Equal(jour, fusion[0]);
        Assert.Equal(sortie, fusion[1]);
    }
}

public class PretDateDebutEcheanceTests : IDisposable
{
    private readonly PaieTestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void Retenue_ignoree_avant_le_mois_de_debut_echeance()
    {
        var scenario = PaieTestScenario.Creer(_factory, anneePeriode: 2024, moisPeriode: 1, salaireBase: 2_600_000m);
        scenario.DefinirModePresenceSaisieJours(26);
        scenario.Db.PretsAvances.Add(new PretAvance
        {
            EmployeId = scenario.EmployeId,
            MontantTotal = 400m,
            DateOctroi = new DateTime(2024, 1, 5),
            DateDebutEcheance = new DateTime(2024, 3, 1),
            NbEcheances = 4,
            MontantMensuel = 100m,
            SoldeRestant = 400m,
            Statut = "En cours"
        });
        scenario.Db.SaveChanges();

        var bulletin = scenario.GenererBulletin();
        var lignePret = bulletin.Details.FirstOrDefault(d =>
            d.Libelle.Contains("Prêt", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(lignePret);
        Assert.Equal(0m, lignePret!.Retenue);
        Assert.Equal(400m, scenario.Db.PretsAvances.Single().SoldeRestant);
    }

    [Fact]
    public void Retenue_appliquee_a_partir_du_mois_de_debut_echeance()
    {
        var scenario = PaieTestScenario.Creer(_factory, anneePeriode: 2024, moisPeriode: 1, salaireBase: 2_600_000m);
        scenario.DefinirModePresenceSaisieJours(26);
        scenario.Db.PretsAvances.Add(new PretAvance
        {
            EmployeId = scenario.EmployeId,
            MontantTotal = 400m,
            DateOctroi = new DateTime(2023, 12, 1),
            DateDebutEcheance = new DateTime(2024, 1, 10),
            NbEcheances = 4,
            MontantMensuel = 100m,
            SoldeRestant = 400m,
            Statut = "En cours"
        });
        scenario.Db.SaveChanges();

        var bulletin = scenario.GenererBulletin();
        var lignePret = bulletin.Details.First(d =>
            d.Libelle.Contains("Prêt", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(100m, lignePret.Retenue);
        Assert.Equal(300m, scenario.Db.PretsAvances.Single().SoldeRestant);
    }
}
