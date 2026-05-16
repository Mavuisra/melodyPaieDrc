using System.Windows;
using System.Windows.Media.Imaging;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        ChargerIconeFenetre();
        Loaded += (_, _) => TxtLogin.Focus();
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

    private void Connexion_Click(object sender, RoutedEventArgs e)
    {
        var login = TxtLogin.Text?.Trim();
        if (string.IsNullOrEmpty(login))
        {
            MessageBox.Show(this, "Veuillez saisir l'identifiant.", "Connexion", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtLogin.Focus();
            return;
        }

        var motDePasse = TxtPassword.Password ?? "";
        if (string.IsNullOrEmpty(motDePasse))
        {
            MessageBox.Show(this, "Veuillez saisir le mot de passe.", "Connexion", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtPassword.Focus();
            return;
        }

        var user = AuthService.Login(login, motDePasse);
        if (user == null)
        {
            MessageBox.Show(this, "Identifiant ou mot de passe incorrect.", "Connexion", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtPassword.Clear();
            TxtPassword.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }
}
