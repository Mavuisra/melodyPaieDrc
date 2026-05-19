using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class PeriodesPaieWindow : Window
{
    public PeriodesPaieWindow()
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new PeriodesPaieViewModel(db);
        DataContext = vm;
        vm.OnSucces = msg => UiFeedback.Succes(msg);
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        vm.OuvrirAssistantCloture = id =>
        {
            if (new CloturePeriodeWindow(id) { Owner = this }.ShowDialog() == true)
                vm.Charger();
        };
        Loaded += (_, _) => vm.Charger();
    }
}
