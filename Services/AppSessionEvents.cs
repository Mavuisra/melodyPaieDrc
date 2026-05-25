namespace MelodyPaieRDC.Services;

/// <summary>Événements globaux de session (changement d'entreprise, profil modifié, etc.).</summary>
public static class AppSessionEvents
{
    public static event Action? EntrepriseCouranteChanged;
    public static event Action? SessionUtilisateurChanged;
    /// <summary>Employés, pointages, bulletins, périodes — rafraîchir checklist tableau de bord.</summary>
    public static event Action? DonneesMetierModifiees;
    /// <summary>Mode de pointage ou horaires LT modifiés — rafraîchir grilles pointage et totaux heures.</summary>
    public static event Action? ReglesLtModifiees;

    public static void NotifierEntrepriseCouranteChanged() =>
        EntrepriseCouranteChanged?.Invoke();

    public static void NotifierSessionUtilisateurChanged() =>
        SessionUtilisateurChanged?.Invoke();

    public static void NotifierDonneesMetierModifiees() =>
        DonneesMetierModifiees?.Invoke();

    public static void NotifierReglesLtModifiees() =>
        ReglesLtModifiees?.Invoke();
}
