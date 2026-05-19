using System.Windows;
using System.Windows.Input;
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
        _vm.ObtenirMotsDePasseAdmin = LireMotsDePasseDepuisInterface;
        DataContext = _vm;
    }

    private (string MotDePasse, string Confirmation) LireMotsDePasseDepuisInterface()
    {
        // Valider la composition clavier (IME) avant lecture
        if (PwdAdmin.IsKeyboardFocused)
            Keyboard.ClearFocus();
        else if (PwdAdminConfirm.IsKeyboardFocused)
            Keyboard.ClearFocus();

        return (PwdAdmin.Password ?? "", PwdAdminConfirm.Password ?? "");
    }

    private void PwdAdmin_PasswordChanged(object sender, RoutedEventArgs e) =>
        _vm.AdminMotDePasse = PwdAdmin.Password ?? "";

    private void PwdAdminConfirm_PasswordChanged(object sender, RoutedEventArgs e) =>
        _vm.AdminMotDePasseConfirm = PwdAdminConfirm.Password ?? "";

    private void PwdAdmin_LostFocus(object sender, RoutedEventArgs e) =>
        _vm.AdminMotDePasse = PwdAdmin.Password ?? "";

    private void PwdAdminConfirm_LostFocus(object sender, RoutedEventArgs e) =>
        _vm.AdminMotDePasseConfirm = PwdAdminConfirm.Password ?? "";

    private void TerminerConfiguration_Click(object sender, RoutedEventArgs e)
    {
        PwdAdmin.UpdateLayout();
        PwdAdminConfirm.UpdateLayout();
        var (pwd, confirm) = LireMotsDePasseDepuisInterface();

        if (pwd.Length != confirm.Length && pwd != confirm)
        {
            MessageBox.Show(this,
                $"Les mots de passe ne sont pas identiques.\n\n" +
                $"• Mot de passe : {pwd.Length} caractère(s)\n" +
                $"• Confirmation : {confirm.Length} caractère(s)\n\n" +
                "Recopiez le même mot de passe dans les deux champs (ex. MonMotDePasse1).",
                "Configuration",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            PwdAdminConfirm.Focus();
            return;
        }

        _vm.TerminerAvecMotsDePasse(pwd, confirm);
    }
}
