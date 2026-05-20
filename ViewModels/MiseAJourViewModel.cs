using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

public class MiseAJourViewModel : INotifyPropertyChanged
{
    private string _versionInstallee = "";
    private string _statut = "Cliquez sur « Vérifier » pour rechercher une mise à jour.";
    private string _notesVersion = "";
    private string _urlManifeste = "";
    private double _progression;
    private bool _estOccupe;
    private bool _miseAJourDisponible;
    private bool _telechargementTermine;
    private UpdateManifest? _manifest;
    private string? _cheminInstallateur;
    private CancellationTokenSource? _cts;

    public MiseAJourViewModel()
    {
        var config = UpdateConfigHelper.Charger();
        _urlManifeste = config.ManifestUrl;
        OnPropertyChanged(nameof(UrlManifeste));
        _versionInstallee = ApplicationUpdateService.FormaterVersion(ApplicationUpdateService.ObtenirVersionInstallee());

        VerifierCommand = new RelayCommand(async _ => await VerifierAsync(), _ => !EstOccupe);
        TelechargerCommand = new RelayCommand(async _ => await TelechargerAsync(), _ => !EstOccupe && MiseAJourDisponible && !TelechargementTermine);
        InstallerCommand = new RelayCommand(_ => Installer(), _ => !EstOccupe && TelechargementTermine && !string.IsNullOrEmpty(_cheminInstallateur));
        FermerCommand = new RelayCommand(_ => DemanderFermeture?.Invoke(), _ => !EstOccupe);
    }

    public string VersionInstallee
    {
        get => _versionInstallee;
        private set { _versionInstallee = value; OnPropertyChanged(); }
    }

    public string Statut
    {
        get => _statut;
        private set { _statut = value; OnPropertyChanged(); }
    }

    public string NotesVersion
    {
        get => _notesVersion;
        private set { _notesVersion = value; OnPropertyChanged(); }
    }

    public string UrlManifeste
    {
        get => _urlManifeste;
        set
        {
            if (_urlManifeste == value) return;
            _urlManifeste = value ?? "";
            OnPropertyChanged();
        }
    }

    public double Progression
    {
        get => _progression;
        private set { _progression = value; OnPropertyChanged(); }
    }

    public bool EstOccupe
    {
        get => _estOccupe;
        private set
        {
            if (_estOccupe == value) return;
            _estOccupe = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool MiseAJourDisponible
    {
        get => _miseAJourDisponible;
        private set
        {
            if (_miseAJourDisponible == value) return;
            _miseAJourDisponible = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool TelechargementTermine
    {
        get => _telechargementTermine;
        private set
        {
            if (_telechargementTermine == value) return;
            _telechargementTermine = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand VerifierCommand { get; }
    public ICommand TelechargerCommand { get; }
    public ICommand InstallerCommand { get; }
    public ICommand FermerCommand { get; }

    public Action? DemanderFermeture { get; set; }
    public Action? DemanderArretApplication { get; set; }

    public async Task VerifierAuChargementAsync() => await VerifierAsync();

    private async Task VerifierAsync()
    {
        AnnulerOperation();
        _cts = new CancellationTokenSource();
        EstOccupe = true;
        MiseAJourDisponible = false;
        TelechargementTermine = false;
        _cheminInstallateur = null;
        Progression = 0;
        NotesVersion = "";

        try
        {
            UpdateConfigHelper.Sauvegarder(new UpdateConfigDto
            {
                ManifestUrl = UrlManifeste.Trim(),
                VerifierAuDemarrage = UpdateConfigHelper.Charger().VerifierAuDemarrage
            });

            Statut = "Vérification en cours…";
            var result = await ApplicationUpdateService.VerifierAsync(_cts.Token).ConfigureAwait(true);

            if (result.VersionInstallee != null)
                VersionInstallee = ApplicationUpdateService.FormaterVersion(result.VersionInstallee);

            Statut = result.Message;
            _manifest = result.Manifest;

            if (result.Kind == UpdateCheckResultKind.UpdateAvailable && result.Manifest != null)
            {
                MiseAJourDisponible = true;
                NotesVersion = result.Manifest.ReleaseNotes?.Trim() ?? "Aucune note de version.";
            }
            else
            {
                MiseAJourDisponible = false;
            }
        }
        catch (OperationCanceledException)
        {
            Statut = "Vérification annulée.";
        }
        finally
        {
            EstOccupe = false;
            (TelechargerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (InstallerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (VerifierCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (FermerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private async Task TelechargerAsync()
    {
        if (_manifest == null) return;

        AnnulerOperation();
        _cts = new CancellationTokenSource();
        EstOccupe = true;
        TelechargementTermine = false;
        Progression = 0;

        try
        {
            Statut = "Téléchargement en cours…";
            var progress = new Progress<double>(p =>
            {
                Application.Current?.Dispatcher.Invoke(() => Progression = p);
            });

            var result = await ApplicationUpdateService.TelechargerAsync(_manifest, progress, _cts.Token)
                .ConfigureAwait(true);

            Statut = result.Message;
            if (result.Success && !string.IsNullOrEmpty(result.CheminInstallateur))
            {
                _cheminInstallateur = result.CheminInstallateur;
                TelechargementTermine = true;
                Statut = $"Installateur prêt : {Path.GetFileName(result.CheminInstallateur)}";
            }
        }
        catch (OperationCanceledException)
        {
            Statut = "Téléchargement annulé.";
        }
        finally
        {
            EstOccupe = false;
            (TelechargerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (InstallerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (VerifierCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (FermerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private void Installer()
    {
        if (string.IsNullOrEmpty(_cheminInstallateur)) return;

        if (MessageBox.Show(
                "Melody Paie RDC va se fermer et l'installateur va démarrer.\n\n" +
                "Vos données (base SQLite dans AppData) seront conservées.\n\nContinuer ?",
                "Installer la mise à jour",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        if (!ApplicationUpdateService.LancerInstallateur(_cheminInstallateur, out var msg))
        {
            MessageBox.Show(msg, "Mise à jour", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DemanderArretApplication?.Invoke();
    }

    private void AnnulerOperation()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        catch
        {
            // ignore
        }
        _cts = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
