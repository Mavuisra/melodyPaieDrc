using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class EmployeWindow : Window
{
    private readonly PaieDbContext _db;
    private readonly EmployeViewModel _viewModel;

    /// <param name="employeId">Null pour créer un nouvel employé, valeur pour modifier un employé existant.</param>
    public EmployeWindow(int? employeId = null)
    {
        InitializeComponent();
        _db = new PaieDbContext();
        _viewModel = new EmployeViewModel(_db, employeId);
        DataContext = _viewModel;

        _viewModel.OnEnregistreReussi = () =>
        {
            DialogResult = true;
            Close();
        };
        _viewModel.OnAnnuler = () =>
        {
            DialogResult = false;
            Close();
        };
        _viewModel.OnErreurValidation = msg => MessageBox.Show(msg, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
        _viewModel.OnInfo = msg => UiFeedback.Info(msg);

        Loaded += (_, _) => _viewModel.ChargerDepartements();
    }
}
