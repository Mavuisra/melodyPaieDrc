using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class IprConfigWindow : Window
{
    private readonly PaieDbContext _db;
    private readonly IprConfigViewModel _viewModel;

    public IprConfigWindow()
    {
        InitializeComponent();

        _db = new PaieDbContext();
        _viewModel = new IprConfigViewModel(_db);
        DataContext = _viewModel;

        _viewModel.OnEnregistrementReussi += () =>
        {
            UiFeedback.Succes("Paramètres IPR enregistrés.");
            DialogResult = true;
            Close();
        };

        _viewModel.OnErreur += message => UiFeedback.Avertissement(message);

        Loaded += (_, _) => _viewModel.Charger();
    }
}

