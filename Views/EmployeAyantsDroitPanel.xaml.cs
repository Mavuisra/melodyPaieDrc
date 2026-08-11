using System.Windows;
using System.Windows.Controls;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class EmployeAyantsDroitPanel : UserControl
{
    private AyantsDroitViewModel? _vm;

    public EmployeAyantsDroitPanel()
    {
        InitializeComponent();
        _vm = new AyantsDroitViewModel(new PaieDbContext());
        DataContext = _vm;
        WireHandlers(_vm);
        AppSessionEvents.EntrepriseCouranteChanged += OnEntrepriseCouranteChanged;
        AppSessionEvents.SessionUtilisateurChanged += OnSessionUtilisateurChanged;
        Loaded += (_, _) => _vm?.NotifierDroitsModification();
        Unloaded += (_, _) =>
        {
            AppSessionEvents.EntrepriseCouranteChanged -= OnEntrepriseCouranteChanged;
            AppSessionEvents.SessionUtilisateurChanged -= OnSessionUtilisateurChanged;
        };
    }

    public AyantsDroitViewModel? AyantsDroitViewModel => DataContext as AyantsDroitViewModel ?? _vm;

    public void SynchroniserEmployeDepuisRepertoire(int? employeId) =>
        _vm?.ChargerPourEmploye(employeId);

    private void WireHandlers(AyantsDroitViewModel vm)
    {
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        vm.OnDemandeMotDePasseAdmin = () =>
        {
            var owner = Window.GetWindow(this);
            var win = new ConfirmationMotDePasseWindow { Owner = owner };
            return win.ShowDialog() == true ? win.MotDePasse : null;
        };
    }

    private void OnEntrepriseCouranteChanged() =>
        Dispatcher.Invoke(() => _vm?.Charger());

    private void OnSessionUtilisateurChanged() =>
        Dispatcher.Invoke(() => _vm?.NotifierDroitsModification());
}
