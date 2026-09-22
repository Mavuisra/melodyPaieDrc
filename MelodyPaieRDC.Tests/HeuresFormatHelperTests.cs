using MelodyPaieRDC.Helpers;

namespace MelodyPaieRDC.Tests;

public class HeuresFormatHelperTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(7.5, "7:30")]
    [InlineData(7.75, "7:45")]
    [InlineData(8, "8:00")]
    [InlineData(0.25, "0:15")]
    public void VersHhMm_convertit_decimal_en_h_mm(decimal heures, string attendu)
        => Assert.Equal(attendu, HeuresFormatHelper.VersHhMm(heures));
}
