using System.IO;
using System.Net;
using System.Net.Mail;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Envoi du bulletin de paie par e-mail (pièce jointe PDF) via SMTP.
/// </summary>
public class EnvoiBulletinEmailService
{
    private readonly ExportPdfService _pdfService = new();

    /// <summary>
    /// Génère le bulletin en PDF dans un fichier temporaire, envoie l'e-mail avec pièce jointe, puis supprime le fichier.
    /// </summary>
    /// <param name="bulletin">Bulletin à envoyer (Employe, PeriodePaie, Details doivent être chargés).</param>
    /// <param name="destinataire">Adresse e-mail du destinataire.</param>
    /// <param name="objet">Objet du message (si vide, un défaut est utilisé).</param>
    /// <param name="corps">Corps du message (optionnel).</param>
    /// <param name="smtpHost">Serveur SMTP.</param>
    /// <param name="smtpPort">Port SMTP (ex. 587).</param>
    /// <param name="useSsl">Utiliser SSL/TLS.</param>
    /// <param name="senderEmail">Adresse de l'expéditeur.</param>
    /// <param name="senderPassword">Mot de passe ou mot de passe d'application (peut être vide si pas d'auth).</param>
    public void Envoyer(
        BulletinPaie bulletin,
        string destinataire,
        string? objet,
        string? corps,
        string smtpHost,
        int smtpPort,
        bool useSsl,
        string senderEmail,
        string? senderPassword)
    {
        if (bulletin == null) throw new ArgumentNullException(nameof(bulletin));
        if (string.IsNullOrWhiteSpace(destinataire)) throw new ArgumentException("Destinataire requis.", nameof(destinataire));
        if (string.IsNullOrWhiteSpace(smtpHost)) throw new ArgumentException("Serveur SMTP requis.", nameof(smtpHost));

        var nomFichierPdf = Path.Combine(Path.GetTempPath(), $"Bulletin_{bulletin.Id}_{Guid.NewGuid():N}.pdf");
        try
        {
            _pdfService.ExporterBulletin(bulletin, nomFichierPdf);

            var defautObjet = $"Bulletin de paie {bulletin.NumeroBulletin ?? ""} - {bulletin.Employe?.Matricule} - {bulletin.PeriodePaie?.Mois:D2}/{bulletin.PeriodePaie?.Annee}".Trim();
            var msg = new MailMessage
            {
                From = new MailAddress(senderEmail),
                Subject = string.IsNullOrWhiteSpace(objet) ? defautObjet : objet,
                Body = string.IsNullOrWhiteSpace(corps) ? "Veuillez trouver ci-joint votre bulletin de paie." : corps,
                IsBodyHtml = false
            };
            msg.To.Add(destinataire.Trim());
            msg.Attachments.Add(new Attachment(nomFichierPdf));

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = useSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            if (!string.IsNullOrWhiteSpace(senderPassword))
                client.Credentials = new NetworkCredential(senderEmail, senderPassword);

            client.Send(msg);
        }
        finally
        {
            if (File.Exists(nomFichierPdf))
            {
                try { File.Delete(nomFichierPdf); } catch { /* ignore */ }
            }
        }
    }
}
