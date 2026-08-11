using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Views;

public partial class LoginWindow : Window
{
    private bool _syncMotDePasse;

    public LoginWindow()
    {
        InitializeComponent();
        ChargerIconeFenetre();
        TxtLogin.Text = "admin";
        Loaded += (_, _) => TxtPassword.Focus();
    }

    private void ChargerIconeFenetre()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/MelodyPaieRDC;component/Assets/Icon_MelodyPaie.png", UriKind.Absolute);
            if (Application.GetResourceStream(uri) != null)
                Icon = BitmapFrame.Create(uri);
        }
        catch { /* ignorer */ }
    }

    private void Annuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AfficherMotDePasse_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncMotDePasse) return;

        _syncMotDePasse = true;
        try
        {
            var afficher = ChkAfficherMotDePasse.IsChecked == true;
            if (afficher)
            {
                TxtPasswordVisible.Text = TxtPassword.Password;
                TxtPasswordVisible.Visibility = Visibility.Visible;
                TxtPassword.Visibility = Visibility.Collapsed;
                TxtPasswordVisible.Focus();
            }
            else
            {
                TxtPassword.Password = TxtPasswordVisible.Text;
                TxtPasswordVisible.Visibility = Visibility.Collapsed;
                TxtPassword.Visibility = Visibility.Visible;
                TxtPassword.Focus();
            }
        }
        finally
        {
            _syncMotDePasse = false;
        }
    }

    private string ObtenirMotDePasseSaisi() =>
        TxtPassword.Visibility == Visibility.Visible ? TxtPassword.Password : TxtPasswordVisible.Text;

    private void Connexion_Click(object sender, RoutedEventArgs e)
    {
        var login = TxtLogin.Text?.Trim();
        if (string.IsNullOrEmpty(login))
        {
            MessageBox.Show(this, "Veuillez saisir l'identifiant.", "Connexion", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtLogin.Focus();
            return;
        }

        var motDePasse = ObtenirMotDePasseSaisi() ?? "";
        if (string.IsNullOrEmpty(motDePasse))
        {
            MessageBox.Show(this, "Veuillez saisir le mot de passe.", "Connexion", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (TxtPassword.Visibility == Visibility.Visible)
                TxtPassword.Focus();
            else
                TxtPasswordVisible.Focus();
            return;
        }

        var user = AuthService.Login(login, motDePasse);
        if (user == null)
        {
            MessageBox.Show(this,
                "Identifiant ou mot de passe incorrect.\n\n" +
                "• Identifiant : en minuscules (ex. admin)\n" +
                "• Le mot de passe n'est pas « admin » sauf si défini ainsi à l'installation\n" +
                "• Vérifiez Verrou maj et le clavier AZERTY\n" +
                "• Cochez « Afficher le mot de passe » pour contrôler la saisie",
                "Connexion", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (TxtPassword.Visibility == Visibility.Visible)
            {
                TxtPassword.Clear();
                TxtPassword.Focus();
            }
            else
            {
                TxtPasswordVisible.Clear();
                TxtPasswordVisible.Focus();
            }
            return;
        }

        DialogResult = true;
        Close();
    }
}
