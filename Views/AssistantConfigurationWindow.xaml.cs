using System.Windows;
using Microsoft.Win32;
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
        _vm.OnDemandeChoisirLogo = () =>
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Tous les fichiers (*.*)|*.*",
                Title = "Choisir le logo de l'entreprise"
            };
            if (dlg.ShowDialog(this) == true)
                _vm.DefinirLogoDepuisFichier(dlg.FileName);
        };
        _vm.ObtenirMotsDePasseAdmin = () => (PwdAdmin.Password ?? "", PwdAdminConfirm.Password ?? "");
        DataContext = _vm;
    }
}
