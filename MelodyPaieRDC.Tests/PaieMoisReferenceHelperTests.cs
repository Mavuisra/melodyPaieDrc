using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Tests;

public class PaieMoisReferenceHelperTests
{
    [Fact]
    public void CalculerNet_sans_saisie_mois_remet_dettes_aout()
    {
        // Net août 900 après quinzaine 100 → sans dette mois = 1000
        var varsRef = new PaieMoisReferenceHelper.VariablesMois(100, 0, 0, 26.13m);
        var net = PaieMoisReferenceHelper.CalculerNet(
            900m,
            varsRef,
            new PaieMoisReferenceHelper.VariablesMois(0, 0, 0, 0));
        Assert.Equal(1000m, net);
    }

    [Fact]
    public void AppliquerSurBulletin_ne_clone_pas_quinzaine_ni_sanctions_aout()
    {
        var reference = new BulletinPaie
        {
            TotalGainImposable = 1000m,
            TotalGainNonImposable = 50m,
            MontantIprNet = 60m,
            CotisationCnssOuvrier = 30m,
            NetAPayer = 900m,
            NetAPayerDeviseLocale = 900m,
            Details = new List<BulletinDetail>
            {
                new() { Libelle = "Salaire de base", Gain = 600m },
                new() { Libelle = "IPR", Retenue = 60m },
                new() { Libelle = "CNSS ouvrier", Retenue = 30m },
                new() { Libelle = "Acomptes salaire", Retenue = 100m },
                new() { Libelle = "Sanctions / retards", Retenue = 13.40m },
                new() { Libelle = "Ajustements retenues", Retenue = 26.13m },
            }
        };
        var bulletin = new BulletinPaie { Details = new List<BulletinDetail>() };

        PaieMoisReferenceHelper.AppliquerSurBulletin(
            bulletin,
            reference,
            new PaieMoisReferenceHelper.VariablesMois(0, 0, 0, 0),
            aucuneSaisieVariablesMois: true);

        // 900 + 100 + 13.40 = 1013.40 (dettes aout rendues, pas reclones)
        Assert.Equal(1013.40m, bulletin.NetAPayer);
        Assert.Contains(bulletin.Details, d => d.Libelle == "Ajustements retenues" && d.Retenue == 26.13m);
        Assert.DoesNotContain(bulletin.Details, d => d.Libelle == "Acomptes salaire");
        Assert.DoesNotContain(bulletin.Details, d => d.Libelle.Contains("Sanctions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AppliquerSurBulletin_applique_variables_du_mois_seulement()
    {
        var reference = new BulletinPaie
        {
            TotalGainImposable = 1000m,
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
