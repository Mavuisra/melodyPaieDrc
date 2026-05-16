using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class CalendrierTravailWindow : Window
{
    public CalendrierTravailWindow()
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new CalendrierTravailViewModel(db);
        DataContext = vm;
        Loaded += (_, _) => vm.Charger();
    }
}
