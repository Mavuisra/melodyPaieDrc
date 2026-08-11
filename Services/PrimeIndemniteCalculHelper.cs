using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>Calcul du montant mensuel d'une prime ou indemnité (FIXE ou prorata jours).</summary>
public static class PrimeIndemniteCalculHelper
{
    public static (decimal Montant, decimal BaseAffichee, decimal TauxEffectif) CalculerMontant(
        decimal montantMensuel,
        string modeCalcul,
        decimal joursPointes,
        decimal joursReferencePaie,
        decimal joursPayesSalaire)
    {
        if (montantMensuel <= 0m || joursPayesSalaire <= 0m)
            return (0m, 0m, 0m);

        var prorata = string.Equals(modeCalcul, PrimeIndemnite.ModeProrataJours, StringComparison.OrdinalIgnoreCase);
        if (!prorata)
            return (RoundPaie(montantMensuel), RoundPaie(montantMensuel), 1m);

        if (joursReferencePaie <= 0m || joursPointes <= 0m)
            return (0m, RoundPaie(montantMensuel), 0m);

        var montant = RoundPaie(montantMensuel * joursPointes / joursReferencePaie);
        var baseJournaliere = RoundPaie(montantMensuel / joursReferencePaie);
        return (montant, baseJournaliere, joursPointes);
    }

    private static decimal RoundPaie(decimal value, int decimals = 2)
        => decimal.Round(value, decimals, MidpointRounding.AwayFromZero);
}
