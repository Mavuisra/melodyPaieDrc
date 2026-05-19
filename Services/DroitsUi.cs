namespace MelodyPaieRDC.Services;

/// <summary>Droits d'interface (RBAC) pour ViewModels et fenêtres.</summary>
public static class DroitsUi
{
    public static bool PeutModifier => AuthService.PeutModifierDonnees;

    public static bool PeutAdministrer => AuthService.PeutAdministrerApplication;

    public static bool EstLectureSeule => AuthService.EstLectureSeule;
}
