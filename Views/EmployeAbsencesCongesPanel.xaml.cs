using System.Windows;
using System.Windows.Controls;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class EmployeAbsencesCongesPanel : UserControl
{
    private AbsencesCongesViewModel? _vm;
    private int _employeId;

    public EmployeAbsencesCongesPanel()
    {
        InitializeComponent();
        AppSessionEvents.EntrepriseCouranteChanged += OnEntrepriseCouranteChanged;
        AppSessionEvents.SessionUtilisateurChanged += OnSessionUtilisateurChanged;
        Unloaded += (_, _) =>
        {
            AppSessionEvents.EntrepriseCouranteChanged -= OnEntrepriseCouranteChanged;
            AppSessionEvents.SessionUtilisateurChanged -= OnSessionUtilisateurChanged;
        };
    }

    public void SynchroniserEmploye(int? employeId) =>
        Dispatcher.Invoke(() => ChargerEmploye(employeId));

    private void ChargerEmploye(int? employeId)
    {
        var id = employeId.GetValueOrDefault();
        if (id <= 0)
        {
            _employeId = 0;
            _vm = null;
            DataContext = null;
            EtatVide.Visibility = Visibility.Visible;
            ContenuPanel.Visibility = Visibility.Collapsed;
            return;
        }

        if (_employeId == id && _vm != null)
        {
            _vm.Charger();
            return;
        }

        _employeId = id;
        _vm = new AbsencesCongesViewModel(new PaieDbContext(), id);
        _vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        DataContext = _vm;
        _vm.Charger();
        EtatVide.Visibility = Visibility.Collapsed;
        ContenuPanel.Visibility = Visibility.Visible;
    }

    private void OnEntrepriseCouranteChanged() =>
        Dispatcher.Invoke(() => ChargerEmploye(_employeId > 0 ? _employeId : null));

    private void OnSessionUtilisateurChanged() =>
        Dispatcher.Invoke(() => { if (_employeId > 0) ChargerEmploye(_employeId); });
}
