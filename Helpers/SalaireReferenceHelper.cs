namespace MelodyPaieRDC.Helpers;

/// <summary>Jours et heures de référence paie (politique entreprise ou valeurs par défaut RDC).</summary>
public static class SalaireReferenceHelper
{
    public const decimal JoursDefaut = 26m;
    public const decimal HeuresDefaut = 8m;

    public static decimal SalaireJour(decimal mensuel, decimal joursReference)
        => mensuel > 0 && joursReference > 0
            ? decimal.Round(mensuel / joursReference, 2, MidpointRounding.AwayFromZero)
            : 0m;

    public static decimal SalaireHeure(decimal mensuel, decimal joursReference, decimal heuresParJour)
        => mensuel > 0 && joursReference > 0 && heuresParJour > 0
            ? decimal.Round(mensuel / joursReference / heuresParJour, 2, MidpointRounding.AwayFromZero)
            : 0m;
}
