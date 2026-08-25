using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Tests;

public class TransportAbsencePaieHelperTests
{
    [Theory]
    [InlineData("Indemnité de transport", true)]
    [InlineData("TRANSPORT", true)]
    [InlineData("Indemnité KM / transport", false)]
    [InlineData("Prime logement", false)]
    public void EstIndemniteTransport_filtre_libelles(string libelle, bool attendu)
        => Assert.Equal(attendu, TransportAbsencePaieHelper.EstIndemniteTransport(libelle));

    [Fact]
    public void CalculerCoupe_exemple_client_2_40_par_jour()
    {
        // 62,40 / 26 = 2,40 ; 4 jours non présents → 9,60
        var (retenue, taux, jours) = TransportAbsencePaieHelper.CalculerCoupe(62.40m, 22m, 26m);

        Assert.Equal(2.40m, taux);
        Assert.Equal(4m, jours);
        Assert.Equal(9.60m, retenue);
    }

    [Fact]
    public void CalculerCoupe_presence_complete_zero()
    {
        var (retenue, _, jours) = TransportAbsencePaieHelper.CalculerCoupe(62.40m, 26m, 26m);

        Assert.Equal(0m, retenue);
        Assert.Equal(0m, jours);
    }

    [Fact]
    public void CalculerCoupe_plafonne_au_montant_mensuel()
    {
        var (retenue, _, _) = TransportAbsencePaieHelper.CalculerCoupe(62.40m, 0m, 26m);

        Assert.Equal(62.40m, retenue);
    }
}
