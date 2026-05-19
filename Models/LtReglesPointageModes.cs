namespace MelodyPaieRDC.Models;

/// <summary>
/// Modes de calcul des heures à partir des pointages terminal (par entreprise).
/// </summary>
public static class LtReglesPointageModes
{
    /// <summary>Entrée, début pause, fin pause, sortie (4 lectures).</summary>
    public const string QuatrePointages = "QUATRE";

    /// <summary>Entrée, pause intermédiaire, sortie (3 lectures).</summary>
    public const string TroisPointages = "TROIS";

    /// <summary>Entrée et sortie uniquement (2 lectures).</summary>
    public const string DeuxPointages = "DEUX";

    public static IReadOnlyList<(string Code, string Libelle)> OptionsUi { get; } = new[]
    {
        (QuatrePointages, "4 pointages — entrée, début/fin pause, sortie"),
        (TroisPointages, "3 pointages — entrée, pause, sortie"),
        (DeuxPointages, "2 pointages — entrée et sortie seulement")
    };

    public static string Normaliser(string? mode)
    {
        var m = (mode ?? "").Trim().ToUpperInvariant();
        return m switch
        {
            TroisPointages => TroisPointages,
            DeuxPointages => DeuxPointages,
            _ => QuatrePointages
        };
    }

    public static int NombrePointagesJourComplet(string mode) =>
        Normaliser(mode) switch
        {
            DeuxPointages => 2,
            TroisPointages => 3,
            _ => 4
        };
}
