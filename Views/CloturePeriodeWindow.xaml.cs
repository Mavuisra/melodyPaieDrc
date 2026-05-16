using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class CloturePeriodeWindow : Window
{
    private readonly PaieDbContext _db = new();

    public CloturePeriodeWindow(int periodePaieId)
    {
        InitializeComponent();
        var vm = new CloturePeriodeViewModel(_db, periodePaieId);
        vm.OnErreur = msg => MessageBox.Show(this, msg, "Clôture", MessageBoxButton.OK, MessageBoxImage.Warning);
        vm.OnSucces = msg => MessageBox.Show(this, msg, "Clôture", MessageBoxButton.OK, MessageBoxImage.Information);
        vm.OnClotureEffectuee = () => DialogResult = true;
        DataContext = vm;
    }

    protected override void OnClosed(EventArgs e)
    {
        _db.Dispose();
        base.OnClosed(e);
    }
}
