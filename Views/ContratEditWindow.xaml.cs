using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class ContratEditWindow : Window
{
    private readonly ContratEditViewModel _vm;

    public ContratEditWindow(int contratId)
    {
        InitializeComponent();
        var db = new PaieDbContext();
        _vm = new ContratEditViewModel(db, contratId);
        DataContext = _vm;
        _vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        _vm.OnEnregistre = () =>
        {
            DialogResult = true;
            Close();
        };
        Loaded += (_, _) =>
        {
            _vm.Charger();
            Title = "Modifier le contrat — " + (string.IsNullOrEmpty(_vm.NomEmploye) ? "Employé" : _vm.NomEmploye);
        };
    }

    private void Annuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
