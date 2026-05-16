using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class EtablissementsDepartementsWindow : Window
{
    public EtablissementsDepartementsWindow()
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new EtablissementsDepartementsViewModel(db);
        DataContext = vm;
        vm.OnFermer = () => { DialogResult = true; Close(); };
        vm.OnErreur = msg => MessageBox.Show(msg, "Etablissements et départements", MessageBoxButton.OK, MessageBoxImage.Warning);
        Loaded += (_, _) => vm.Charger();
    }
}
