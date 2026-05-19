using System.Windows;
using Microsoft.EntityFrameworkCore;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Views;

public partial class EnvoiEmailBulletinWindow : Window
{
    private readonly BulletinPaie _bulletin;

    public EnvoiEmailBulletinWindow(BulletinPaie bulletin)
    {
        InitializeComponent();
        _bulletin = bulletin ?? throw new ArgumentNullException(nameof(bulletin));
        Loaded += OnLoaded;
    }

    /// <summary>Recharge le bulletin avec Employe, PeriodePaie, Details pour l'export PDF et l'envoi.</summary>
    private static BulletinPaie? ChargerBulletinComplet(int bulletinId)
    {
        using var db = new PaieDbContext();
        return db.BulletinsPaie
            .Include(b => b.Employe).ThenInclude(e => e!.Departement)
            .Include(b => b.PeriodePaie)
            .Include(b => b.Details)
            .FirstOrDefault(b => b.Id == bulletinId);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var config = SmtpConfigHelper.Charger();
        if (config != null)
        {
            TxtSmtpHost.Text = config.SmtpHost;
            TxtSmtpPort.Text = config.SmtpPort.ToString();
            ChkSsl.IsChecked = config.UseSsl;
            TxtSenderEmail.Text = config.SenderEmail;
        }
        if (string.IsNullOrEmpty(TxtObjet.Text))
            TxtObjet.Text = $"Bulletin de paie {_bulletin.NumeroBulletin ?? ""} - {_bulletin.Employe?.Matricule} - {_bulletin.PeriodePaie?.Mois:D2}/{_bulletin.PeriodePaie?.Annee}".Trim();
    }

    private void Annuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Envoyer_Click(object sender, RoutedEventArgs e)
    {
        var destinataire = TxtDestinataire.Text?.Trim();
        if (string.IsNullOrEmpty(destinataire))
        {
            MessageBox.Show(this, "Veuillez saisir l'adresse du destinataire.", "Envoi e-mail", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtDestinataire.Focus();
            return;
        }

        var smtpHost = TxtSmtpHost.Text?.Trim();
        if (string.IsNullOrEmpty(smtpHost))
        {
            MessageBox.Show(this, "Veuillez saisir le serveur SMTP.", "Envoi e-mail", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtSmtpHost.Focus();
            return;
        }

        if (!int.TryParse(TxtSmtpPort.Text, out var port) || port <= 0 || port > 65535)
        {
            MessageBox.Show(this, "Port SMTP invalide.", "Envoi e-mail", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtSmtpPort.Focus();
            return;
        }

        var senderEmail = TxtSenderEmail.Text?.Trim();
        if (string.IsNullOrEmpty(senderEmail))
        {
            MessageBox.Show(this, "Veuillez saisir l'adresse de l'expéditeur.", "Envoi e-mail", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtSenderEmail.Focus();
            return;
        }

        var bulletinComplet = ChargerBulletinComplet(_bulletin.Id);
        if (bulletinComplet == null)
        {
            MessageBox.Show(this, "Bulletin introuvable.", "Envoi e-mail", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SmtpConfigHelper.Sauvegarder(new SmtpConfigDto
            {
                SmtpHost = smtpHost,
                SmtpPort = port,
                UseSsl = ChkSsl.IsChecked == true,
                SenderEmail = senderEmail
            });

            var service = new EnvoiBulletinEmailService();
            service.Envoyer(
                bulletinComplet,
                destinataire,
                TxtObjet.Text?.Trim(),
                TxtCorps.Text?.Trim(),
                smtpHost,
                port,
                ChkSsl.IsChecked == true,
                senderEmail,
                TxtPassword.Password
            );

            UiFeedback.Succes("Bulletin envoyé par e-mail.");
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur envoi e-mail", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
