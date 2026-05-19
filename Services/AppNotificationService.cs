namespace MelodyPaieRDC.Services;

public enum NotificationKind
{
    Info,
    Success,
    Warning
}

/// <summary>Notifications non bloquantes (barre de statut principale).</summary>
public static class AppNotificationService
{
    public static event Action<string, NotificationKind>? NotificationPubliee;

    public static void Afficher(string message, NotificationKind kind = NotificationKind.Info)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        NotificationPubliee?.Invoke(message.Trim(), kind);
    }

    public static void Succes(string message) => Afficher(message, NotificationKind.Success);

    public static void Avertissement(string message) => Afficher(message, NotificationKind.Warning);
}
