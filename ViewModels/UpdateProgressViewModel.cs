using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

public class UpdateProgressViewModel : INotifyPropertyChanged
{
    private string _statut = "Préparation…";
    private double _progression;

    public string Statut
    {
        get => _statut;
        private set { _statut = value; OnPropertyChanged(); }
    }

    public double Progression
    {
        get => _progression;
        private set { _progression = value; OnPropertyChanged(); }
    }

    public string? CheminInstallateur { get; private set; }

    public async Task<bool> ExecuterAsync(UpdateManifest manifest)
    {
        Progression = 0;
        Statut = "Téléchargement de la mise à jour…";

        var progress = new Progress<double>(p =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Progression = Math.Min(90, p * 0.9);
                if (p >= 100)
                    Statut = "Téléchargement terminé. Installation…";
            });
        });

        var result = await ApplicationUpdateService.TelechargerAsync(manifest, progress).ConfigureAwait(true);

        if (!result.Success || string.IsNullOrEmpty(result.CheminInstallateur))
        {
            Statut = result.Message;
            Progression = 0;
            return false;
        }

        CheminInstallateur = result.CheminInstallateur;
        Progression = 95;
        Statut = $"Installation de {Path.GetFileName(result.CheminInstallateur)}…";
        await Task.Delay(400).ConfigureAwait(true);
        Progression = 100;
        Statut = "Redémarrage de l'application…";
        await Task.Delay(300).ConfigureAwait(true);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
