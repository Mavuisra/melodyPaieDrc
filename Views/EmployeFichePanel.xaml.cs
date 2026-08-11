using System.Windows.Controls;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class EmployeFichePanel : UserControl
{
    private EmployeFicheViewModel? _vm;

    public EmployeFichePanel()
    {
        InitializeComponent();
        _vm = new EmployeFicheViewModel(new PaieDbContext());
        DataContext = _vm;
        AppSessionEvents.EntrepriseCouranteChanged += OnEntrepriseCouranteChanged;
        Unloaded += (_, _) => AppSessionEvents.EntrepriseCouranteChanged -= OnEntrepriseCouranteChanged;
    }

    public EmployeFicheViewModel? FicheViewModel => DataContext as EmployeFicheViewModel ?? _vm;

    public void SynchroniserEmploye(Employe? employe) =>
        _vm?.ChargerPourEmploye(employe?.Id, employe);

    private void OnEntrepriseCouranteChanged() =>
        Dispatcher.Invoke(() => _vm?.ChargerPourEmploye(null));
}
