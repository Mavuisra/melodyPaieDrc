using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Tests;

public class PaieMoisReferenceHelperTests
{
    [Fact]
    public void CalculerNet_sans_saisie_mois_egal_net_reference()
    {
        var varsRef = new PaieMoisReferenceHelper.VariablesMois(100, 0, 0, 26.13m);
        var net = PaieMoisReferenceHelper.CalculerNet(
            900m,
            varsRef,
            varsRef, // clone : mêmes Q/P/S
            retenuesAdditionnellesMois: 0m);
        Assert.Equal(900m, net);
    }

    [Fact]
    public void CalculerNet_restaure_quinzaine_si_mois_sans_acompte()
    {
        var varsRef = new PaieMoisReferenceHelper.VariablesMois(100, 0, 0, 0);
        var net = PaieMoisReferenceHelper.CalculerNet(
            400m,
            varsRef,
            new PaieMoisReferenceHelper.VariablesMois(0, 0, 0, 0));
        Assert.Equal(500m, net);
    }

    [Fact]
    public void AppliquerSurBulletin_conserve_retenue_salaire_reference()
    {
        var reference = new BulletinPaie
        {
            TotalGainImposable = 1000m,
            TotalGainNonImposable = 50m,
            BaseIpr = 600m,
            MontantIprNet = 60m,
            CotisationCnssOuvrier = 30m,
            CotisationInpp = 18m,
            NetAPayer = 900m,
            NetAPayerDeviseLocale = 900m,
            Details = new List<BulletinDetail>
            {
                new() { Libelle = "Salaire de base", Gain = 600m },
                new() { Libelle = "IPR", Retenue = 60m },
                new() { Libelle = "CNSS ouvrier", Retenue = 30m },
                new() { Libelle = "Acomptes salaire", Retenue = 100m },
                new() { Libelle = "Ajustements retenues", Retenue = 26.13m },
            }
        };
        var bulletin = new BulletinPaie { Details = new List<BulletinDetail>() };

        // Pas de saisie mois → clone complet
        PaieMoisReferenceHelper.AppliquerSurBulletin(
            bulletin,
            reference,
            new PaieMoisReferenceHelper.VariablesMois(0, 0, 0, 0),
            aucuneSaisieVariablesMois: true);

        Assert.Equal(900m, bulletin.NetAPayer);
        Assert.Equal(0m, bulletin.CotisationInpp);
        Assert.Contains(bulletin.Details, d => d.Libelle == "Ajustements retenues" && d.Retenue == 26.13m);
        Assert.Contains(bulletin.Details, d => d.Libelle == "Acomptes salaire" && d.Retenue == 100m);
    }

    [Fact]
    public void AppliquerSurBulletin_overlay_quinzaine_garde_retenue_aout()
    {
        var reference = new BulletinPaie
        {
            TotalGainImposable = 1000m,
            TotalGainNonImposable = 0m,
            MontantIprNet = 60m,
            CotisationCnssOuvrier = 30m,
            NetAPayer = 900m,
            NetAPayerDeviseLocale = 900m,
            Details = new List<BulletinDetail>
            {
                new() { Libelle = "Salaire de base", Gain = 1000m },
                new() { Libelle = "Acomptes salaire", Retenue = 100m },
                new() { Libelle = "Ajustements retenues", Retenue = 26.13m },
            }
        };
        var bulletin = new BulletinPaie { Details = new List<BulletinDetail>() };

        PaieMoisReferenceHelper.AppliquerSurBulletin(
            bulletin,
            reference,
            new PaieMoisReferenceHelper.VariablesMois(50, 0, 10, 0),
            aucuneSaisieVariablesMois: false);

        // 900 + 100 - 50 - 10 = 940
        Assert.Equal(940m, bulletin.NetAPayer);
        Assert.Contains(bulletin.Details, d => d.Libelle == "Ajustements retenues" && d.Retenue == 26.13m);
        Assert.Contains(bulletin.Details, d => d.Libelle == "Acomptes salaire" && d.Retenue == 50m);
        Assert.Contains(bulletin.Details, d => d.Libelle == "Sanctions / retards" && d.Retenue == 10m);
    }
}
