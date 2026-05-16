using System.Globalization;
using System.Text.Json;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Sérialise les horodatages de pointage (terminal ou saisie) pour recalcul automatique des heures LTservices.
/// </summary>
public static class PointagesJournalierSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public static string? Serialiser(IReadOnlyList<DateTime> pointages)
    {
        if (pointages == null || pointages.Count == 0)
            return null;
        var list = pointages.OrderBy(t => t).Select(t => t.ToString("o")).ToList();
        return JsonSerializer.Serialize(list, JsonOptions);
    }

    public static IReadOnlyList<DateTime> Deserialiser(string? json, DateTime jour)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<DateTime>();
        try
        {
            var arr = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            if (arr == null || arr.Count == 0)
                return Array.Empty<DateTime>();
            var result = new List<DateTime>();
            foreach (var s in arr)
            {
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                    result.Add(dt);
                else if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var ts))
                    result.Add(jour.Date + ts);
            }
            return result.OrderBy(t => t).ToList();
        }
        catch
        {
            return Array.Empty<DateTime>();
        }
    }

    /// <summary>Recalcule les heures prestées à partir du JSON stocké (règles LTservices).</summary>
    public static decimal CalculerHeuresLt(string? pointagesJson, DateTime jour, LtServicesRegles? regles = null)
    {
        var list = Deserialiser(pointagesJson, jour);
        return LtServicesPointageCalcul.CalculerHeuresPrestees(list, jour, regles);
    }
}
