using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class CloturePeriodeWindow : Window
{
    private readonly PaieDbContext _db = new();

    public CloturePeriodeWindow(int periodePaieId)
    {
        InitializeComponent();
        var vm = new CloturePeriodeViewModel(_db, periodePaieId);
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        vm.OnSucces = msg => UiFeedback.Succes(msg);
        vm.OnClotureEffectuee = () => DialogResult = true;
        DataContext = vm;
    }

    protected override void OnClosed(EventArgs e)
    {
        _db.Dispose();
        base.OnClosed(e);
    }
}
