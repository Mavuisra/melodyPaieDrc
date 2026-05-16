using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Views;

public partial class UtilisateursWindow : Window
{
    private readonly List<Utilisateur> _liste = new();

    public UtilisateursWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Charger();
    }

    private void Charger()
    {
        _liste.Clear();
        using (var db = new PaieDbContext())
        {
            foreach (var u in db.Utilisateurs.OrderBy(x => x.Login))
                _liste.Add(u);
        }
        GridUtilisateurs.ItemsSource = null;
        GridUtilisateurs.ItemsSource = _liste;
    }

    private Utilisateur? GetSelectionne()
    {
        return GridUtilisateurs.SelectedItem as Utilisateur;
    }

    private void GridUtilisateurs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Optionnel : activer/désactiver boutons selon sélection
    }

    private void Ajouter_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new UtilisateurEditWindow(null) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        using (var db = new PaieDbContext())
        {
            var (hash, salt) = AuthService.HashMotDePasse(dlg.MotDePasse);
            var u = new Utilisateur
            {
                Login = dlg.Login.Trim(),
                MotDePasseHash = hash,
                Salt = salt,
                NomComplet = string.IsNullOrWhiteSpace(dlg.NomComplet) ? null : dlg.NomComplet.Trim(),
                Role = dlg.Role,
                Actif = dlg.Actif,
                DateCreation = System.DateTime.UtcNow
            };
            db.Utilisateurs.Add(u);
            db.SaveChanges();
        }
        Charger();
    }

    private void Modifier_Click(object sender, RoutedEventArgs e)
    {
        var u = GetSelectionne();
        if (u == null)
        {
            MessageBox.Show(this, "Sélectionnez un utilisateur.", "Modifier", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new UtilisateurEditWindow(u) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        using (var db = new PaieDbContext())
        {
            var entite = db.Utilisateurs.Find(u.Id);
            if (entite == null) return;
            entite.Login = dlg.Login.Trim();
            entite.NomComplet = string.IsNullOrWhiteSpace(dlg.NomComplet) ? null : dlg.NomComplet.Trim();
            entite.Role = dlg.Role;
            entite.Actif = dlg.Actif;
            if (!string.IsNullOrEmpty(dlg.MotDePasse))
            {
                var (hash, salt) = AuthService.HashMotDePasse(dlg.MotDePasse);
                entite.MotDePasseHash = hash;
                entite.Salt = salt;
            }
            db.SaveChanges();
        }
        Charger();
    }

    private void ChangerMotDePasse_Click(object sender, RoutedEventArgs e)
    {
        var u = GetSelectionne();
        if (u == null)
        {
            MessageBox.Show(this, "Sélectionnez un utilisateur.", "Mot de passe", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new ChangerMotDePasseWindow(u.Login) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        using (var db = new PaieDbContext())
        {
            var entite = db.Utilisateurs.Find(u.Id);
            if (entite == null) return;
            var (hash, salt) = AuthService.HashMotDePasse(dlg.NouveauMotDePasse);
            entite.MotDePasseHash = hash;
            entite.Salt = salt;
            db.SaveChanges();
        }
        MessageBox.Show(this, "Mot de passe modifié.", "Mot de passe", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Supprimer_Click(object sender, RoutedEventArgs e)
    {
        var u = GetSelectionne();
        if (u == null)
        {
            MessageBox.Show(this, "Sélectionnez un utilisateur.", "Supprimer", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (AuthService.UtilisateurCourant?.Id == u.Id)
        {
            MessageBox.Show(this, "Vous ne pouvez pas supprimer votre propre compte.", "Supprimer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show(this, $"Supprimer l'utilisateur \"{u.Login}\" ?", "Confirmer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        using (var db = new PaieDbContext())
        {
            var entite = db.Utilisateurs.Find(u.Id);
            if (entite != null)
            {
                db.Utilisateurs.Remove(entite);
                db.SaveChanges();
            }
        }
        Charger();
    }
}
