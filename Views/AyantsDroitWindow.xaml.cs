using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class AyantsDroitWindow : Window
{
    public AyantsDroitWindow(int employeId)
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new AyantsDroitViewModel(db, employeId);
        DataContext = vm;
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        vm.OnDemandeMotDePasseAdmin = () =>
        {
            var win = new ConfirmationMotDePasseWindow { Owner = this };
            return win.ShowDialog() == true ? win.MotDePasse : null;
        };
        Loaded += (_, _) =>
        {
            vm.Charger();
            Title = "Ayants droit – " + (string.IsNullOrEmpty(vm.NomEmploye) ? "Employé" : vm.NomEmploye);
        };
    }
}
