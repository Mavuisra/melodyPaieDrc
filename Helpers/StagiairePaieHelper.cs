namespace MelodyPaieRDC.Helpers;

public static class StagiairePaieHelper
{
    public static bool EstStagiaire(string? typeContrat)
        => string.Equals(typeContrat, "Stage", StringComparison.OrdinalIgnoreCase)
           || string.Equals(typeContrat, "Stagiaire", StringComparison.OrdinalIgnoreCase);
}
