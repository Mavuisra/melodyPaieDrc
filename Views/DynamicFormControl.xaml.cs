using System.Windows.Controls;
using MelodyPaieRDC.Forms.Engine;
using MelodyPaieRDC.Forms.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class DynamicFormControl : UserControl
{
    private readonly FormRenderer _renderer = new();
    private DynamicFormViewModel? _viewModel;

    public DynamicFormControl()
    {
        InitializeComponent();
        Loaded += (_, _) => RafraichirUi();
    }

    public void Initialiser(DynamicFormViewModel viewModel)
    {
        if (_viewModel != null)
            _viewModel.OnMetadonneesRechargees -= RafraichirUi;

        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.OnMetadonneesRechargees += RafraichirUi;
        RafraichirUi();
    }

    private void RafraichirUi()
    {
        if (_viewModel == null) return;
        _renderer.Construire(FieldsPanel, _viewModel);
    }
}
