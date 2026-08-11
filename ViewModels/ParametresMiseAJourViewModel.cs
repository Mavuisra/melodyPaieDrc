using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

/// <summary>
/// État des mises à jour affiché dans le panneau Paramètres.
/// </summary>
public class ParametresMiseAJourViewModel : INotifyPropertyChanged
{
    private string _versionInstallee = "";
    private string _versionDisponible = "";
    private string _statut = "Vérification des mises à jour…";
    private string _notesVersion = "";
    private bool _estOccupe;
    private bool _miseAJourDisponible;
    private bool _estAJour;
    private UpdateManifest? _manifest;
    private CancellationTokenSource? _cts;

    public ParametresMiseAJourViewModel()
    {
        _versionInstallee = ApplicationUpdateService.FormaterVersion(ApplicationUpdateService.ObtenirVersionInstallee());

        VerifierCommand = new RelayCommand(async _ => await VerifierAsync(), _ => !EstOccupe);
        TelechargerEtInstallerCommand = new RelayCommand(
            _ => DemarrerMiseAJourAutomatique?.Invoke(_manifest!),
            _ => !EstOccupe && MiseAJourDisponible && _manifest != null);
    }

    public string VersionInstallee
    {
        get => _versionInstallee;
        private set { _versionInstallee = value; OnPropertyChanged(); }
    }

    public string VersionDisponible
    {
        get => _versionDisponible;
        private set { _versionDisponible = value; OnPropertyChanged(); }
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

    public bool EstAJour
    {
        get => _estAJour;
        private set
        {
            if (_estAJour == value) return;
            _estAJour = value;
            OnPropertyChanged();
        }
    }

    public ICommand VerifierCommand { get; }
    public ICommand TelechargerEtInstallerCommand { get; }

    public Action<UpdateManifest>? DemarrerMiseAJourAutomatique { get; set; }

    public async Task VerifierAuChargementAsync() => await VerifierAsync();

    private async Task VerifierAsync()
    {
        AnnulerOperation();
        _cts = new CancellationTokenSource();
        EstOccupe = true;
        MiseAJourDisponible = false;
        EstAJour = false;
        VersionDisponible = "";
        NotesVersion = "";
        _manifest = null;

        try
        {
            Statut = "Vérification en cours…";
            var result = await ApplicationUpdateService.VerifierAsync(_cts.Token).ConfigureAwait(true);

            if (result.VersionInstallee != null)
                VersionInstallee = ApplicationUpdateService.FormaterVersion(result.VersionInstallee);

            Statut = result.Message;
            _manifest = result.Manifest;

            if (result.Kind == UpdateCheckResultKind.UpdateAvailable && result.Manifest != null)
            {
                MiseAJourDisponible = true;
                VersionDisponible = result.VersionDisponible != null
                    ? ApplicationUpdateService.FormaterVersion(result.VersionDisponible)
                    : result.Manifest.Version;
                NotesVersion = result.Manifest.ReleaseNotes?.Trim() ?? "Aucune note de version.";
            }
            else if (result.Kind == UpdateCheckResultKind.UpToDate)
            {
                EstAJour = true;
                if (result.VersionDisponible != null)
                    VersionDisponible = ApplicationUpdateService.FormaterVersion(result.VersionDisponible);
            }
        }
        catch (OperationCanceledException)
        {
            Statut = "Vérification annulée.";
        }
        finally
        {
            EstOccupe = false;
            (VerifierCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (TelechargerEtInstallerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
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
