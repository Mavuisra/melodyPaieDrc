using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

public class UpdateProgressViewModel : INotifyPropertyChanged
{
    private string _titre = "Mise à jour en cours";
    private string _consigne = "Ne fermez pas cette fenêtre. Melody Paie RDC redémarrera tout seul.";
    private string _statut = "Préparation du téléchargement…";
    private double _progression;
    private bool _estErreur;
    private bool _peutFermer;

    public string Titre
    {
        get => _titre;
        private set { _titre = value; OnPropertyChanged(); }
    }

    public string Consigne
    {
        get => _consigne;
        private set { _consigne = value; OnPropertyChanged(); }
    }

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

    public bool EstErreur
    {
        get => _estErreur;
        private set { _estErreur = value; OnPropertyChanged(); }
    }

    public bool PeutFermer
    {
        get => _peutFermer;
        private set
        {
            _peutFermer = value;
            OnPropertyChanged();
            (FermerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string? CheminInstallateur { get; private set; }

    public ICommand FermerCommand { get; }

    public Action? DemanderFermeture { get; set; }

    public UpdateProgressViewModel()
    {
        FermerCommand = new RelayCommand(_ => DemanderFermeture?.Invoke(), _ => PeutFermer);
    }

    public async Task<bool> ExecuterAsync(UpdateManifest manifest)
    {
        Progression = 2;
        EstErreur = false;
        PeutFermer = false;
        Titre = "Mise à jour en cours";
        Consigne = "Ne fermez pas cette fenêtre. Melody Paie RDC redémarrera tout seul.";
        var version = string.IsNullOrWhiteSpace(manifest.Version) ? "" : $" {manifest.Version.Trim()}";
        Statut = $"Téléchargement de la version{version}…";

        var progress = new Progress<double>(p =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Progression = Math.Min(92, Math.Max(2, p * 0.92));
                if (p >= 99)
                    Statut = "Vérification du fichier téléchargé…";
            });
        });

        var result = await ApplicationUpdateService.TelechargerAsync(manifest, progress).ConfigureAwait(true);

        if (!result.Success || string.IsNullOrEmpty(result.CheminInstallateur))
        {
            MarquerEchec(result.Message);
            return false;
        }

        CheminInstallateur = result.CheminInstallateur;
        Progression = 96;
        Titre = "Installation en cours";
        Consigne = "Ne fermez pas cette fenêtre. Melody Paie RDC va redémarrer tout seul.";
        Statut = "Préparation de l'installation…";
        await Task.Delay(250).ConfigureAwait(true);
        Progression = 100;
        Statut = "Lancement de l'installateur…";
        await Task.Delay(250).ConfigureAwait(true);
        return true;
    }

    public void AfficherEchec(string message) => MarquerEchec(message);

    private void MarquerEchec(string message)
    {
        EstErreur = true;
        PeutFermer = true;
        Progression = 0;
        Titre = "Mise à jour interrompue";
        Consigne = "Vos données n'ont pas été modifiées. Fermez cette fenêtre, puis réessayez.";
        Statut = string.IsNullOrWhiteSpace(message)
            ? "La mise à jour n'a pas pu aboutir."
            : message;
        CheminInstallateur = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
