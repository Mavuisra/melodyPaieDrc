using System;

namespace MelodyPaieRDC.Services;

/// <summary>Notifie les écrans (ex. pointage) lorsque les paramètres terminal ZKTeco sont modifiés depuis un autre contexte (onglet Paramètres).</summary>
public static class ZkTerminalParametresNotifier
{
    public static event EventHandler? ParametresModifies;

    public static void Raise(object? sender = null) =>
        ParametresModifies?.Invoke(sender, EventArgs.Empty);
}
