using System.Windows;

namespace MelodyPaieRDC.Views;

public partial class ChangerMotDePasseWindow : Window
{
    public ChangerMotDePasseWindow(string login)
    {
        InitializeComponent();
        TxtUtilisateur.Text = $"Utilisateur : {login}";
    }

    public string NouveauMotDePasse => TxtNouveau.Password;

    private void Annuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Enregistrer_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtNouveau.Password))
        {
            MessageBox.Show(this, "Veuillez saisir le nouveau mot de passe.", "Mot de passe", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNouveau.Focus();
            return;
        }
        DialogResult = true;
        Close();
    }
}
