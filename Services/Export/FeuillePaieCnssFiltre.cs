using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services.Export;

/// <summary>
/// Ne conserve que les travailleurs réellement rémunérés ou ayant presté sur la période.
/// </summary>
public static class FeuillePaieCnssFiltre
{
    public static bool InclureEmploye(ExportDonneesPaieContext ctx)
    {
        var b = ctx.Bulletin;
        var brut = b.TotalGainImposable + b.TotalGainNonImposable;

        if (brut > 0) return true;
        if (b.CotisationCnssOuvrier > 0) return true;
        if (ctx.HeuresTravailPeriode > 0) return true;
        if (ctx.Saisie is { JoursPrestes: > 0 }) return true;

        var gainSalaire = b.Details?
            .Any(d => d.Gain > 0 && EstLigneSalaireBase(d.Libelle)) ?? false;
        if (gainSalaire) return true;

        var autreGain = b.Details?.Any(d => d.Gain > 0) ?? false;
        return autreGain;
    }

    private static bool EstLigneSalaireBase(string libelle)
    {
        var lib = libelle.ToLowerInvariant();
        return (lib.Contains("salaire") && lib.Contains("base"))
               || lib.Contains("salaire de base");
    }
}
