using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.Tests.Helpers;

namespace MelodyPaieRDC.Tests;

public class CalculPaieServiceIntegrationTests : IDisposable
{
    private readonly PaieTestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void PrimeFixe_montant_complet_avec_presence_partielle()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirModePresenceSaisieJours(22);
        scenario.AjouterPrime("Prime fixe test", 260_000m, PrimeIndemnite.ModeFixe);

        var bulletin = scenario.GenererBulletin();
        var lignePrime = bulletin.Details.First(d => d.Libelle == "Prime fixe test");

        Assert.Equal(260_000m, lignePrime.Gain);
    }

    [Fact]
    public void PrimeProrata_montant_proratise_sur_jours_pointes()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirModePresenceSaisieJours(22);
        scenario.AjouterPrime("Prime prorata test", 260_000m, PrimeIndemnite.ModeProrataJours);

        var bulletin = scenario.GenererBulletin();
        var lignePrime = bulletin.Details.First(d => d.Libelle == "Prime prorata test");

        Assert.Equal(220_000m, lignePrime.Gain);
    }

    [Fact]
    public void EstCotisable_exclut_prime_non_cotisable_de_la_base_cnss()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 1_000_000m);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirModePresenceSaisieJours(26);
        scenario.AjouterPrime("Transport NC", 100_000m, estImposable: false, estCotisable: false);
        scenario.AjouterPrime("Prime cotisable", 200_000m, estImposable: true, estCotisable: true);

        var bulletin = scenario.GenererBulletin();

        // CNSS 5 % sur salaire de base uniquement (pas transport ni prime cotisable hors ancienneté)
        Assert.Equal(50_000m, bulletin.CotisationCnssOuvrier);
        Assert.Equal(100_000m, bulletin.TotalGainNonImposable);
    }

    [Fact]
    public void Cnss_et_ipr_sur_salaire_base_et_anciennete_uniquement()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 624m);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirModePresenceSaisieJours(26);
        scenario.AjouterPrime("Prime d'ancienneté", 0m, estImposable: true, estCotisable: true);
        scenario.AjouterPrime("Indemnité de transport", 62.40m, estImposable: false, estCotisable: false);
        scenario.AjouterPrime("Indemnité KM", 118m, estImposable: true, estCotisable: true);

        var bulletin = scenario.GenererBulletin();

        Assert.Equal(31.20m, bulletin.CotisationCnssOuvrier);
        Assert.Equal(62.40m, bulletin.MontantIprNet);
        Assert.Equal(624m, bulletin.BaseIpr);
    }

    [Fact]
    public void Cnss_et_ipr_utilisent_ca_contrat_meme_en_mode_net()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 624m, configurer: (db, _) =>
        {
            var contrat = db.Contrats.First();
            contrat.DeviseBase = "USD";
            db.SaveChanges();
        });
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.SalaireContratEnNet, "true");
        scenario.DefinirModePresenceSaisieJours(26);
        scenario.AjouterPrime("Indemnité KM", 118m, estImposable: true, estCotisable: true);

        var bulletin = scenario.GenererBulletin();

        Assert.Equal(624m, bulletin.BaseIpr);
        Assert.Equal(31.20m, bulletin.CotisationCnssOuvrier);
        Assert.Equal(62.40m, bulletin.MontantIprNet);
    }

    [Fact]
    public void Cnss_et_ipr_sans_prorata_jours_sur_ca_et_anciennete()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 624m);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirModePresenceSaisieJours(23); // comme le client Ethan
        scenario.AjouterPrime("Prime d'ancienneté", 69m, estImposable: true, estCotisable: true);
        scenario.AjouterPrime("Indemnité KM", 118m, estImposable: true, estCotisable: true);

        var bulletin = scenario.GenererBulletin();
        var ligneCnss = bulletin.Details.First(d => d.Libelle.Contains("CNSS", StringComparison.OrdinalIgnoreCase));
        var ligneIpr = bulletin.Details.First(d => d.Libelle.Contains("IPR", StringComparison.OrdinalIgnoreCase));

        // Base legale = 624 + 69 = 693 (pas 624*23/26)
        Assert.Equal(693m, bulletin.BaseIpr);
        Assert.Equal(34.65m, bulletin.CotisationCnssOuvrier);
        Assert.Equal(69.30m, bulletin.MontantIprNet);
        Assert.Equal(693m, ligneCnss.BaseCalcul);
        Assert.Equal(5m, ligneCnss.Taux);
        Assert.Equal(693m, ligneIpr.BaseCalcul);
        Assert.Equal(10m, ligneIpr.Taux);
    }

    [Fact]
    public void Heures_manuelles_incluses_dans_calcul_presence()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 2_600_000m);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirParametrePolitique(
            ParametrePolitiquePaie.Cles.ModeCalculPresence,
            ParametrePolitiquePaie.ModePresencePointages);

        // Un lundi ouvré complet saisi manuellement (heures nominales LT)
        var heuresNominales = LtServicesRegles.Defaut.HeuresNormalesJourSemaine;
        scenario.AjouterSuiviManuel(new DateTime(2024, 1, 8), heuresNominales);

        var bulletin = scenario.GenererBulletin();

        Assert.True(bulletin.TotalGainImposable > 0m);
        Assert.Equal(100_000m, Math.Round(bulletin.TotalGainImposable, 2));
    }

    [Fact]
    public void Heures_sup_ajoutees_au_bulletin_avec_majoration()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 2_600_000m);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirParametrePolitique(
            ParametrePolitiquePaie.Cles.ModeCalculPresence,
            ParametrePolitiquePaie.ModePresencePointages);

        var lundi = new DateTime(2024, 1, 8);
        scenario.AjouterSuiviPointages(lundi, new List<DateTime>
        {
            lundi.Add(LtServicesPointageCalcul.HeureDebutTravail),
            lundi.Date.AddHours(12),
            lundi.Date.AddHours(13),
            lundi.Date.AddHours(18)
        });

        var bulletin = scenario.GenererBulletin();
        var ligneHeuresSup = bulletin.Details.FirstOrDefault(d => d.Libelle == "Heures supplémentaires");

        Assert.NotNull(ligneHeuresSup);
        Assert.True(ligneHeuresSup!.Gain > 0m);
    }

    [Fact]
    public void Ligne_absence_est_informative_sans_retenue()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 2_600_000m);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirModePresenceSaisieJours(20);

        var bulletin = scenario.GenererBulletin();
        var ligneAbsence = bulletin.Details.FirstOrDefault(d =>
            d.Libelle.Contains("Absence", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ligneAbsence);
        Assert.Equal(0m, ligneAbsence!.Retenue);
        Assert.Equal(0m, ligneAbsence.Gain);
        Assert.True(ligneAbsence.BaseCalcul > 0m);
    }

    [Fact]
    public void Sans_pointages_avec_completion_paie_mois_complet()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 99m, configurer: (db, _) =>
        {
            var contrat = db.Contrats.First();
            contrat.DeviseBase = "USD";
            db.SaveChanges();
        });
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.SalaireContratEnNet, "true");
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.CompleterJoursSansSaisie, "true");

        var bulletin = scenario.GenererBulletin();

        Assert.InRange(bulletin.NetAPayer, 98m, 105m);
        Assert.True(bulletin.TotalGainImposable > bulletin.NetAPayer);
    }

    [Fact]
    public void Salaire_usd_99_net_moins_pret_et_retard()
    {
        var scenario = PaieTestScenario.Creer(_factory, anneePeriode: 2026, moisPeriode: 8, salaireBase: 99m, configurer: (db, empId) =>
        {
            var contrat = db.Contrats.First();
            contrat.DeviseBase = "USD";
            db.SaveChanges();

            db.PretsAvances.Add(new PretAvance
            {
                EmployeId = empId,
                DateOctroi = new DateTime(2026, 8, 1),
                MontantTotal = 50m,
                MontantMensuel = 10m,
                SoldeRestant = 50m,
                NbEcheances = 5
            });
            db.SaveChanges();
        });
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.SalaireContratEnNet, "true");
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.CompleterJoursSansSaisie, "true");
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.RetardSanctionActive, "true");
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.RetardSeuilMinutes, "120");
        scenario.DefinirParametrePolitique(
            ParametrePolitiquePaie.Cles.RetardModeSanction,
            ParametrePolitiquePaie.RetardModeDemiJour);

        var lundi = new DateTime(2026, 8, 3);
        scenario.AjouterSuiviPointages(lundi, new List<DateTime>
        {
            lundi.Date.AddHours(10),
            lundi.Date.AddHours(12),
            lundi.Date.AddHours(13),
            lundi.Date.AddHours(17)
        });

        var bulletin = scenario.GenererBulletin();

        Assert.True(bulletin.NetAPayer < 99m);
        Assert.InRange(bulletin.NetAPayer, 50m, 92m);
        var ligneSanctions = bulletin.Details.FirstOrDefault(d =>
            d.Libelle.Contains("Sanctions / retards", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(ligneSanctions);
        Assert.True(ligneSanctions!.Retenue > 0m);
    }

    [Fact]
    public void Quinzaine_deduite_du_net_apres_taxes()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 99m, configurer: (db, _) =>
        {
            var contrat = db.Contrats.First();
            contrat.DeviseBase = "USD";
            db.SaveChanges();
        });
        scenario.Db.QuinzaineOctrois.Add(new QuinzaineOctroi
        {
            EmployeId = scenario.EmployeId,
            PeriodePaieId = scenario.PeriodeId,
            DateOctroi = new DateTime(2024, 1, 10),
            Montant = 30m
        });
        scenario.Db.SaveChanges();
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.SalaireContratEnNet, "true");
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.CompleterJoursSansSaisie, "true");
        scenario.DefinirModePresenceSaisieJours(26);

        var bulletin = scenario.GenererBulletin();
        var acompte = bulletin.Details.First(d =>
            d.Libelle.Contains("acompte", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(30m, acompte.Retenue);
        Assert.True(bulletin.NetAPayer < 99m);
        Assert.InRange(bulletin.NetAPayer, 74m, 80m);
    }

    [Fact]
    public void Net_vers_brut_inclut_inpp_dans_reconstitution()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 500_000m);
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.SalaireContratEnNet, "true");
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.CompleterJoursSansSaisie, "false");
        scenario.DefinirModePresenceSaisieJours(26);

        var bulletin = scenario.GenererBulletin();

        Assert.True(bulletin.TotalGainImposable > bulletin.NetAPayer);
        Assert.True(bulletin.CotisationInpp > 0m);
        Assert.InRange(bulletin.NetAPayer, 499_000m, 501_000m);
    }

    [Fact]
    public void Sanctions_retards_auto_visibles_sur_bulletin_et_synthese()
    {
        var scenario = PaieTestScenario.Creer(_factory, anneePeriode: 2026, moisPeriode: 8, salaireBase: 2_600_000m);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirParametrePolitique(
            ParametrePolitiquePaie.Cles.ModeCalculPresence,
            ParametrePolitiquePaie.ModePresencePointages);
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.RetardSanctionActive, "true");
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.RetardSeuilMinutes, "120");
        scenario.DefinirParametrePolitique(
            ParametrePolitiquePaie.Cles.RetardModeSanction,
            ParametrePolitiquePaie.RetardModeDemiJour);

        var lundi = new DateTime(2026, 8, 3);
        scenario.AjouterSuiviPointages(lundi, new List<DateTime>
        {
            lundi.Date.AddHours(10),
            lundi.Date.AddHours(12),
            lundi.Date.AddHours(13),
            lundi.Date.AddHours(17)
        });

        var bulletin = scenario.GenererBulletin();
        var ligneSanctions = bulletin.Details.FirstOrDefault(d =>
            d.Libelle.Contains("Sanctions / retards", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(ligneSanctions);
        Assert.Equal(50_000m, ligneSanctions!.Retenue);

        var synthese = BulletinSyntheseHelper.Construire(bulletin);
        Assert.Equal(50_000m, synthese.Sanctions);
    }

    [Fact]
    public void Sanctions_retards_auto_ignorees_hors_aout()
    {
        var scenario = PaieTestScenario.Creer(_factory, anneePeriode: 2026, moisPeriode: 7, salaireBase: 2_600_000m);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirParametrePolitique(
            ParametrePolitiquePaie.Cles.ModeCalculPresence,
            ParametrePolitiquePaie.ModePresencePointages);
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.RetardSanctionActive, "true");
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.RetardSeuilMinutes, "120");
        scenario.DefinirParametrePolitique(
            ParametrePolitiquePaie.Cles.RetardModeSanction,
            ParametrePolitiquePaie.RetardModeDemiJour);

        var lundi = new DateTime(2026, 7, 6);
        scenario.AjouterSuiviPointages(lundi, new List<DateTime>
        {
            lundi.Date.AddHours(10),
            lundi.Date.AddHours(12),
            lundi.Date.AddHours(13),
            lundi.Date.AddHours(17)
        });

        var bulletin = scenario.GenererBulletin();
        var ligneSanctions = bulletin.Details.FirstOrDefault(d =>
            d.Libelle.Contains("Sanctions / retards", StringComparison.OrdinalIgnoreCase)
            && d.Retenue > 0);

        Assert.Null(ligneSanctions);
    }

    [Fact]
    public void Transport_coupe_sur_jours_non_presents_visibles_bulletin()
    {
        var scenario = PaieTestScenario.Creer(_factory, anneePeriode: 2026, moisPeriode: 8, salaireBase: 2_600_000m);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirModePresenceSaisieJours(22);
        scenario.AjouterPrime("Indemnité de transport", 62.40m, estImposable: false, estCotisable: false);

        var bulletin = scenario.GenererBulletin();
        var ligneTransport = bulletin.Details.First(d =>
            d.Libelle.Contains("Indemnité de transport", StringComparison.OrdinalIgnoreCase));
        var ligneCoupe = bulletin.Details.FirstOrDefault(d =>
            d.Libelle.Contains("Transport absences", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(62.40m, ligneTransport.Gain);
        Assert.NotNull(ligneCoupe);
        Assert.Equal(9.60m, ligneCoupe!.Retenue); // 4 j × 2,40
        Assert.True(bulletin.NetAPayer < bulletin.TotalGainImposable + bulletin.TotalGainNonImposable);
    }

    [Fact]
    public void Transport_coupe_ignoree_hors_aout()
    {
        var scenario = PaieTestScenario.Creer(_factory, anneePeriode: 2026, moisPeriode: 7, salaireBase: 2_600_000m);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirModePresenceSaisieJours(22);
        scenario.AjouterPrime("Indemnité de transport", 62.40m, estImposable: false, estCotisable: false);

        var bulletin = scenario.GenererBulletin();
        var ligneCoupe = bulletin.Details.FirstOrDefault(d =>
            d.Libelle.Contains("Transport absences", StringComparison.OrdinalIgnoreCase));

        Assert.Null(ligneCoupe);
    }

    [Fact]
    public void Stagiaire_sans_cnss_ipr_inpp()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 75m, configurer: (db, _) =>
        {
            var contrat = db.Contrats.First();
            contrat.DeviseBase = "USD";
            contrat.TypeContrat = "Stage";
            db.SaveChanges();
        });
        scenario.DefinirModeBrutClassique();
        scenario.DefinirModePresenceSaisieJours(26);

        var bulletin = scenario.GenererBulletin();

        Assert.Equal(0m, bulletin.CotisationCnssOuvrier);
        Assert.Equal(0m, bulletin.MontantIprNet);
        Assert.Equal(0m, bulletin.CotisationInpp);
        Assert.Equal(75m, bulletin.NetAPayer);
    }

    [Fact]
    public void BulletinCnssBaseResolver_aligne_exports_et_declarations()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 1_000_000m);
        scenario.DefinirModeBrutClassique();
        scenario.DefinirModePresenceSaisieJours(26);
        scenario.AjouterPrime("Prime cotisable", 200_000m, estCotisable: true);

        var bulletin = scenario.GenererBulletin();
        var baseCnss = BulletinCnssBaseResolver.ObtenirBaseCnss(bulletin);

        var ligneCnss = bulletin.Details.First(d => d.Libelle.Contains("CNSS", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ligneCnss.BaseCalcul, baseCnss);
        Assert.Equal(1_000_000m, baseCnss);
    }
}
