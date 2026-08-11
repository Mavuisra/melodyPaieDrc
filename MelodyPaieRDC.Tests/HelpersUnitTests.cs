using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Tests;

public class SalaireReferenceHelperTests
{
    [Fact]
    public void SalaireJour_utilise_jours_reference_politique()
    {
        var jour = SalaireReferenceHelper.SalaireJour(2_200_000m, 22m);
        Assert.Equal(100_000m, jour);
    }

    [Fact]
    public void SalaireHeure_utilise_jours_et_heures_reference()
    {
        var heure = SalaireReferenceHelper.SalaireHeure(2_200_000m, 22m, 7m);
        Assert.Equal(14_285.71m, heure);
    }

    [Fact]
    public void Valeurs_defaut_sont_26_jours_et_8_heures()
    {
        Assert.Equal(26m, SalaireReferenceHelper.JoursDefaut);
        Assert.Equal(8m, SalaireReferenceHelper.HeuresDefaut);
    }
}

public class PrimeIndemniteCalculHelperTests
{
    [Fact]
    public void ModeFixe_retourne_montant_mensuel_complet_si_jours_payes()
    {
        var (montant, baseAffichee, taux) = PrimeIndemniteCalculHelper.CalculerMontant(
            260_000m, PrimeIndemnite.ModeFixe, 22m, 26m, 22m);

        Assert.Equal(260_000m, montant);
        Assert.Equal(260_000m, baseAffichee);
        Assert.Equal(1m, taux);
    }

    [Fact]
    public void ModeProrataJours_proratise_sur_jours_pointes()
    {
        var (montant, baseAffichee, taux) = PrimeIndemniteCalculHelper.CalculerMontant(
            260_000m, PrimeIndemnite.ModeProrataJours, 22m, 26m, 22m);

        Assert.Equal(220_000m, montant);
        Assert.Equal(10_000m, baseAffichee);
        Assert.Equal(22m, taux);
    }

    [Fact]
    public void Retourne_zero_si_aucun_jour_paye()
    {
        var (montant, _, _) = PrimeIndemniteCalculHelper.CalculerMontant(
            260_000m, PrimeIndemnite.ModeFixe, 22m, 26m, 0m);

        Assert.Equal(0m, montant);
    }
}

public class BulletinCnssBaseResolverTests
{
    [Fact]
    public void Priorise_ligne_detail_cnss_ouvrier()
    {
        var bulletin = new BulletinPaie
        {
            TotalGainImposable = 1_000_000m,
            TotalGainNonImposable = 50_000m,
            Details = new List<BulletinDetail>
            {
                new() { Libelle = "CNSS ouvrier", BaseCalcul = 900_000m, Retenue = 45_000m },
                new() { Libelle = "IPR", BaseCalcul = 1_000_000m, Retenue = 100_000m }
            }
        };

        Assert.Equal(900_000m, BulletinCnssBaseResolver.ObtenirBaseCnss(bulletin));
    }

    [Fact]
    public void Fallback_sur_total_gains_sans_ligne_cnss()
    {
        var bulletin = new BulletinPaie
        {
            TotalGainImposable = 1_000_000m,
            TotalGainNonImposable = 50_000m,
            Details = new List<BulletinDetail>()
        };

        Assert.Equal(1_050_000m, BulletinCnssBaseResolver.ObtenirBaseCnss(bulletin));
    }
}

public class BulletinSyntheseHelperTests
{
    [Fact]
    public void Construire_calcule_solde_selon_exemple_metier()
    {
        var bulletin = new BulletinPaie
        {
            TotalGainImposable = 1000m,
            TotalGainNonImposable = 0m,
            MontantIprNet = 15m,
            CotisationCnssOuvrier = 35m,
            CotisationInpp = 15m,
            NetAPayer = 485m,
            Details = new List<BulletinDetail>
            {
                new() { Libelle = "Acomptes salaire", Retenue = 350m },
                new() { Libelle = "Prêts / avances", Retenue = 100m }
            }
        };

        var syn = BulletinSyntheseHelper.Construire(bulletin);

        Assert.Equal(1000m, syn.MontantTotal);
        Assert.Equal(350m, syn.Quinzaine);
        Assert.Equal(100m, syn.Pret);
        Assert.Equal(50m, syn.RetenueSociale);
        Assert.Equal(15m, syn.Impot);
        Assert.Equal(485m, syn.Solde);
        Assert.Contains("485,00", syn.FormuleSolde);
    }
}
