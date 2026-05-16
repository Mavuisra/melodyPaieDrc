using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>Règles horaires LTservices configurables (issues des paramètres application).</summary>
public sealed class LtServicesRegles
{
    public static LtServicesRegles Defaut => new();

    public TimeSpan HeureDebutTravail { get; init; } = new(7, 30, 0);
    public TimeSpan HeureLimiteTolerance { get; init; } = new(7, 40, 0);
    public TimeSpan HeureDebutPause { get; init; } = new(12, 0, 0);
    public TimeSpan HeureFinPause { get; init; } = new(13, 0, 0);
    public TimeSpan HeureFinSemaine { get; init; } = new(16, 0, 0);
    public TimeSpan HeureFinSamedi { get; init; } = new(12, 30, 0);

    public TimeSpan DureePauseStandard => HeureFinPause > HeureDebutPause
        ? HeureFinPause - HeureDebutPause
        : TimeSpan.Zero;

    public decimal HeuresNormalesJourSemaine => decimal.Round(
        (decimal)Math.Max(0, (HeureFinSemaine - HeureDebutTravail - DureePauseStandard).TotalHours), 2, MidpointRounding.AwayFromZero);

    public decimal HeuresNormalesSamedi => decimal.Round(
        (decimal)Math.Max(0, (HeureFinSamedi - HeureDebutTravail).TotalHours), 2, MidpointRounding.AwayFromZero);

    public static LtServicesRegles DepuisParametres(ParametresApplication? p)
    {
        if (p == null)
            return Defaut;

        return new LtServicesRegles
        {
            HeureDebutTravail = ParseOuDefaut(p.LtHeureDebutTravail, Defaut.HeureDebutTravail),
            HeureLimiteTolerance = ParseOuDefaut(p.LtHeureLimiteTolerance, Defaut.HeureLimiteTolerance),
            HeureDebutPause = ParseOuDefaut(p.LtHeureDebutPause, Defaut.HeureDebutPause),
            HeureFinPause = ParseOuDefaut(p.LtHeureFinPause, Defaut.HeureFinPause),
            HeureFinSemaine = ParseOuDefaut(p.LtHeureFinSemaine, Defaut.HeureFinSemaine),
            HeureFinSamedi = ParseOuDefaut(p.LtHeureFinSamedi, Defaut.HeureFinSamedi)
        };
    }

    private static TimeSpan ParseOuDefaut(string? s, TimeSpan defaut)
    {
        if (TimeSpan.TryParse(s, out var t))
            return t;
        return defaut;
    }
}
