using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class AbsencesCongesWindow : Window
{
    public AbsencesCongesWindow(int employeId)
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new AbsencesCongesViewModel(db, employeId);
        DataContext = vm;
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        Loaded += (_, _) =>
        {
            vm.Charger();
            Title = "Absences et congés – " + (string.IsNullOrEmpty(vm.NomEmploye) ? "Employé" : vm.NomEmploye);
        };
    }
}
