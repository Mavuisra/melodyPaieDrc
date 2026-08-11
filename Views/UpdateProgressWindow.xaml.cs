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
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    public bool Succes { get; private set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Succes = await _viewModel.ExecuterAsync(_manifest).ConfigureAwait(true);
        if (Succes)
        {
            if (!ApplicationUpdateService.LancerMiseAJourSilencieuseEtRelancer(
                    _viewModel.CheminInstallateur!, out _))
            {
                MessageBox.Show(
                    "Impossible de lancer l'installateur. Relancez la mise à jour depuis Paramètres.",
                    "Mise à jour",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                DialogResult = false;
                Close();
                return;
            }

            Application.Current.Shutdown();
            return;
        }

        MessageBox.Show(
            _viewModel.Statut,
            "Mise à jour",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        DialogResult = false;
        Close();
    }
}
