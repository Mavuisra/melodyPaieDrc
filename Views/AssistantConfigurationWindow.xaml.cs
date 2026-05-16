using System.Windows;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class AssistantConfigurationWindow : Window
{
    private readonly AssistantConfigurationViewModel _vm;

    public AssistantConfigurationWindow()
    {
        InitializeComponent();
        _vm = new AssistantConfigurationViewModel();
        _vm.OnErreur = msg => MessageBox.Show(this, msg, "Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
        _vm.OnConfigurationTerminee = () =>
        {
            DialogResult = true;
            Close();
        };
        DataContext = _vm;
    }
}
