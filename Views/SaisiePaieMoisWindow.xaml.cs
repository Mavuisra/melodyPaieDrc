using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class SaisiePaieMoisWindow : Window
{
    public SaisiePaieMoisWindow(int periodePaieId)
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new SaisiePaieMoisViewModel(db, periodePaieId);
        DataContext = vm;
        vm.OnFermer = () => { DialogResult = true; Close(); };
        vm.OnErreur = msg => MessageBox.Show(this, msg, "Saisie paie", MessageBoxButton.OK, MessageBoxImage.Error);
        Loaded += (_, _) => vm.Charger();
    }
}

