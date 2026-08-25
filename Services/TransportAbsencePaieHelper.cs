using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Coupe l'indemnité de transport pour les jours de non-présence
/// (absence, maladie, congé, ou tout jour non travaillé au sens pointages).
/// Taux journalier = montant mensuel / jours de référence (ex. 62,40 / 26 = 2,40).
/// Application métier : mois d'août uniquement.
/// </summary>
public static class TransportAbsencePaieHelper
{
    public const string LibelleRetenue = "Transport absences";

    /// <summary>Mois civil où la coupe transport et les sanctions retard auto s'appliquent (août).</summary>
    public const int MoisApplication = 8;

    public static bool EstMoisApplication(int mois) => mois == MoisApplication;

    public static bool EstIndemniteTransport(string? libelle)
        => !string.IsNullOrWhiteSpace(libelle)
           && libelle.Contains("transport", StringComparison.OrdinalIgnoreCase)
           && !libelle.Contains("km", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Calcule la retenue transport : jours non présents × (mensuel / joursRef).
    /// Les jours de présence = jours équivalents pointés (type Normal avec heures),
    /// sans les jours spéciaux payés (maladie/congé) — le transport n'est dû qu'en présence réelle.
    /// </summary>
    public static (decimal Retenue, decimal TauxJournalier, decimal JoursNonPresents) CalculerCoupe(
        decimal montantTransportMensuel,
        decimal joursPresenceReelle,
        decimal joursReferencePaie)
    {
        if (montantTransportMensuel <= 0m || joursReferencePaie <= 0m)
            return (0m, 0m, 0m);

        var tauxJournalier = decimal.Round(
            montantTransportMensuel / joursReferencePaie, 2, MidpointRounding.AwayFromZero);

        var presence = Math.Min(joursReferencePaie, Math.Max(0m, joursPresenceReelle));
        var joursNonPresents = decimal.Round(joursReferencePaie - presence, 2, MidpointRounding.AwayFromZero);
        if (joursNonPresents <= 0m)
            return (0m, tauxJournalier, 0m);

        var retenue = decimal.Round(joursNonPresents * tauxJournalier, 2, MidpointRounding.AwayFromZero);
        if (retenue > montantTransportMensuel)
            retenue = montantTransportMensuel;

        return (retenue, tauxJournalier, joursNonPresents);
    }
}
