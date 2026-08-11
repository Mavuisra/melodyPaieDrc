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
        scenario.DefinirModePresenceSaisieJours(26);
        scenario.AjouterPrime("Transport NC", 100_000m, estImposable: false, estCotisable: false);
        scenario.AjouterPrime("Prime cotisable", 200_000m, estImposable: true, estCotisable: true);

        var bulletin = scenario.GenererBulletin();

        // Base CNSS = salaire + prime cotisable uniquement (1 200 000 × 5 % = 60 000)
        Assert.Equal(60_000m, bulletin.CotisationCnssOuvrier);
        Assert.Equal(100_000m, bulletin.TotalGainNonImposable);
    }

    [Fact]
    public void Heures_manuelles_incluses_dans_calcul_presence()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 2_600_000m);
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
    public void Net_vers_brut_inclut_inpp_dans_reconstitution()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 500_000m);
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.SalaireContratEnNet, "true");
        scenario.DefinirModePresenceSaisieJours(26);

        var bulletin = scenario.GenererBulletin();

        Assert.True(bulletin.TotalGainImposable > bulletin.NetAPayer);
        Assert.True(bulletin.CotisationInpp > 0m);
        Assert.InRange(bulletin.NetAPayer, 499_000m, 501_000m);
    }

    [Fact]
    public void Sanctions_retards_auto_visibles_sur_bulletin_et_synthese()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 2_600_000m);
        scenario.DefinirParametrePolitique(
            ParametrePolitiquePaie.Cles.ModeCalculPresence,
            ParametrePolitiquePaie.ModePresencePointages);
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.RetardSanctionActive, "true");
        scenario.DefinirParametrePolitique(ParametrePolitiquePaie.Cles.RetardSeuilMinutes, "120");
        scenario.DefinirParametrePolitique(
            ParametrePolitiquePaie.Cles.RetardModeSanction,
            ParametrePolitiquePaie.RetardModeDemiJour);

        var lundi = new DateTime(2024, 1, 8);
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
    public void BulletinCnssBaseResolver_aligne_exports_et_declarations()
    {
        var scenario = PaieTestScenario.Creer(_factory, salaireBase: 1_000_000m);
        scenario.DefinirModePresenceSaisieJours(26);
        scenario.AjouterPrime("Prime cotisable", 200_000m, estCotisable: true);

        var bulletin = scenario.GenererBulletin();
        var baseCnss = BulletinCnssBaseResolver.ObtenirBaseCnss(bulletin);

        var ligneCnss = bulletin.Details.First(d => d.Libelle.Contains("CNSS", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ligneCnss.BaseCalcul, baseCnss);
        Assert.Equal(1_200_000m, baseCnss);
    }
}
