using System.Windows;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class UpdateProgressWindow : Window
{
    private readonly UpdateProgressViewModel _viewModel;
    private readonly UpdateManifest _manifest;

    public UpdateProgressWindow(UpdateManifest manifest)
    {
        InitializeComponent();
        _manifest = manifest;
        _viewModel = new UpdateProgressViewModel();
        _viewModel.DemanderFermeture = Close;
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public bool Succes { get; private set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Succes = await _viewModel.ExecuterAsync(_manifest).ConfigureAwait(true);
        if (!Succes)
            return;

        if (!ApplicationUpdateService.LancerMiseAJourSilencieuseEtRelancer(
                _viewModel.CheminInstallateur!, out var message))
        {
            Succes = false;
            _viewModel.AfficherEchec(string.IsNullOrWhiteSpace(message)
                ? "Impossible de lancer l'installation. Réessayez depuis Paramètres > Mises à jour."
                : message);
            return;
        }

        Application.Current.Shutdown();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!Succes && !_viewModel.PeutFermer)
            e.Cancel = true;
    }
}
