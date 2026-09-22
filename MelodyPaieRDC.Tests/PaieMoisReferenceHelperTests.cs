using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Tests;

public class PaieMoisReferenceHelperTests
{
    [Fact]
    public void CalculerNet_sans_variables_mois_egal_net_reference()
    {
        var net = PaieMoisReferenceHelper.CalculerNet(
            900m,
            new PaieMoisReferenceHelper.VariablesMois(0, 0, 0, 26.13m),
            new PaieMoisReferenceHelper.VariablesMois(0, 0, 0, 0));
        Assert.Equal(900m, net);
    }

    [Fact]
    public void CalculerNet_restaure_quinzaine_reference_puis_applique_mois()
    {
        // Août : net 400 après quinzaine 100 → salaire plein 500
        var netSansQuinzaine = PaieMoisReferenceHelper.CalculerNet(
            400m,
            new PaieMoisReferenceHelper.VariablesMois(100, 0, 0, 0),
            new PaieMoisReferenceHelper.VariablesMois(0, 0, 0, 0));
        Assert.Equal(500m, netSansQuinzaine);

        var netAvecQuinzaine = PaieMoisReferenceHelper.CalculerNet(
            400m,
            new PaieMoisReferenceHelper.VariablesMois(100, 0, 0, 0),
            new PaieMoisReferenceHelper.VariablesMois(100, 0, 0, 0));
        Assert.Equal(400m, netAvecQuinzaine);
    }

    [Fact]
    public void AppliquerSurBulletin_copie_gains_et_recalcule_net()
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
                new() { Libelle = "Acomptes salaire", Retenue = 0m },
                new() { Libelle = "Ajustements retenues", Retenue = 26.13m },
            }
        };
        var bulletin = new BulletinPaie { Details = new List<BulletinDetail>() };

        PaieMoisReferenceHelper.AppliquerSurBulletin(
            bulletin,
            reference,
            new PaieMoisReferenceHelper.VariablesMois(50, 0, 10, 0));

        Assert.Equal(1000m, bulletin.TotalGainImposable);
        Assert.Equal(50m, bulletin.TotalGainNonImposable);
        Assert.Equal(60m, bulletin.MontantIprNet);
        Assert.Equal(30m, bulletin.CotisationCnssOuvrier);
        Assert.Equal(0m, bulletin.CotisationInpp);
        Assert.Equal(840m, bulletin.NetAPayer); // 900 - 50 - 10
        Assert.Contains(bulletin.Details, d => d.Libelle == "Acomptes salaire" && d.Retenue == 50m);
        Assert.Contains(bulletin.Details, d => d.Libelle == "Sanctions / retards" && d.Retenue == 10m);
        Assert.DoesNotContain(bulletin.Details, d => d.Libelle.Contains("Ajustements", StringComparison.OrdinalIgnoreCase));
    }
}
