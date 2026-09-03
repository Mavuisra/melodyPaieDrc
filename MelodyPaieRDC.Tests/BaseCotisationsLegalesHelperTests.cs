using MelodyPaieRDC.Helpers;

namespace MelodyPaieRDC.Tests;

public class BaseCotisationsLegalesHelperTests
{
    [Fact]
    public void Base_exclut_transport_km_logement()
    {
        var baseLegale = BaseCotisationsLegalesHelper.CalculerBase(231.40m, 69m);
        Assert.Equal(300.40m, baseLegale);
    }

    [Fact]
    public void Cnss_5_pourcent_et_ipr_10_pourcent()
    {
        Assert.Equal(31.20m, BaseCotisationsLegalesHelper.CalculerCnss(624m));
        Assert.Equal(62.40m, BaseCotisationsLegalesHelper.CalculerIpr(624m));
    }

    [Fact]
    public void EstPrimeAnciennete_reconnait_libelle()
    {
        Assert.True(BaseCotisationsLegalesHelper.EstPrimeAnciennete("Prime d'ancienneté"));
        Assert.False(BaseCotisationsLegalesHelper.EstPrimeAnciennete("Indemnité de transport"));
    }
}
