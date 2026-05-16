using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class TauxSociauxWindow : Window
{
    public TauxSociauxWindow()
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new TauxSociauxViewModel(db);
        DataContext = vm;
        vm.OnFermer = () => { DialogResult = true; Close(); };
        vm.OnErreur = msg => MessageBox.Show(msg, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        Loaded += (_, _) => vm.Charger();
    }
}
