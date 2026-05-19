namespace MelodyPaieRDC.Services;

/// <summary>Retours utilisateur non bloquants (bandeau principal).</summary>
public static class UiFeedback
{
    public static void Succes(string message) => AppNotificationService.Succes(message);

    public static void Info(string message) => AppNotificationService.Afficher(message, NotificationKind.Info);

    public static void Avertissement(string message) => AppNotificationService.Avertissement(message);
}
