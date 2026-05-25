using System.Windows;
using System.Windows.Controls;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class HeuresPresteesTotauxPanel : UserControl
{
    private HeuresPresteesTotauxViewModel? _vm;

    public HeuresPresteesTotauxPanel()
    {
        InitializeComponent();
        _vm = new HeuresPresteesTotauxViewModel(new PaieDbContext());
        DataContext = _vm;
        AppSessionEvents.EntrepriseCouranteChanged += OnEntrepriseCouranteChanged;
        AppSessionEvents.SessionUtilisateurChanged += OnSessionUtilisateurChanged;
        AppSessionEvents.ReglesLtModifiees += OnReglesLtModifiees;
        Unloaded += (_, _) =>
        {
            AppSessionEvents.EntrepriseCouranteChanged -= OnEntrepriseCouranteChanged;
            AppSessionEvents.SessionUtilisateurChanged -= OnSessionUtilisateurChanged;
            AppSessionEvents.ReglesLtModifiees -= OnReglesLtModifiees;
        };
    }

    private void OnSessionUtilisateurChanged() =>
        Dispatcher.Invoke(() => _vm?.NotifierDroitsModification());

    public HeuresPresteesTotauxViewModel? TotauxViewModel => DataContext as HeuresPresteesTotauxViewModel;

    public void RafraichirPourEntrepriseCourante()
    {
        _vm?.RechargerPourEntrepriseCourante();
    }

    private void OnEntrepriseCouranteChanged() =>
        Dispatcher.Invoke(RafraichirPourEntrepriseCourante);

    private void OnReglesLtModifiees() =>
        Dispatcher.Invoke(() => _vm?.RafraichirApresChangementReglesLt());
}
