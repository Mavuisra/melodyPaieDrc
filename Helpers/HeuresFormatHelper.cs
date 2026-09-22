using System;
using System.Globalization;

namespace MelodyPaieRDC.Helpers;

/// <summary>Affichage des heures décimales au format exact h:mm (ex. 7,5 → 7:30).</summary>
public static class HeuresFormatHelper
{
    public static string VersHhMm(decimal heuresDecimales)
    {
        if (heuresDecimales <= 0) return "0:00";
        var totalMinutes = (int)Math.Round(heuresDecimales * 60m, MidpointRounding.AwayFromZero);
        if (totalMinutes < 0) totalMinutes = 0;
        var h = totalMinutes / 60;
        var m = totalMinutes % 60;
        return string.Create(CultureInfo.InvariantCulture, $"{h}:{m:D2}");
    }

    public static string VersHhMmOuTiret(decimal heuresDecimales)
        => heuresDecimales <= 0 ? "—" : VersHhMm(heuresDecimales);
}
