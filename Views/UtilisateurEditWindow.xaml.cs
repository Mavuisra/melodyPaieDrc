using System.Windows;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Views;

public partial class UtilisateurEditWindow : Window
{
    private readonly Utilisateur? _utilisateur;

    public UtilisateurEditWindow(Utilisateur? utilisateur)
    {
        InitializeComponent();
        _utilisateur = utilisateur;
        CmbRole.ItemsSource = Utilisateur.RolesDisponibles;
        if (utilisateur != null)
        {
            Title = "Modifier l'utilisateur";
            TxtLogin.Text = utilisateur.Login;
            TxtNomComplet.Text = utilisateur.NomComplet;
            CmbRole.SelectedItem = utilisateur.Role;
            ChkActif.IsChecked = utilisateur.Actif;
            LblMotDePasse.Text = "Nouveau mot de passe (laisser vide pour ne pas changer)";
        }
        else
        {
            Title = "Nouvel utilisateur";
            CmbRole.SelectedIndex = 1; // Gestionnaire
        }
    }

    public string Login => TxtLogin.Text?.Trim() ?? "";
    public string NomComplet => TxtNomComplet.Text?.Trim() ?? "";
    public string Role => CmbRole.SelectedItem as string ?? Utilisateur.RoleGestionnaire;
    public bool Actif => ChkActif.IsChecked == true;
    public string MotDePasse => TxtMotDePasse.Password;

    private void Annuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Enregistrer_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtLogin.Text))
        {
            MessageBox.Show(this, "L'identifiant est obligatoire.", "Enregistrer", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtLogin.Focus();
            return;
        }
        if (_utilisateur == null && string.IsNullOrEmpty(TxtMotDePasse.Password))
        {
            MessageBox.Show(this, "Le mot de passe est obligatoire pour un nouvel utilisateur.", "Enregistrer", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtMotDePasse.Focus();
            return;
        }
        DialogResult = true;
        Close();
    }
}
