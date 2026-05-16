using System;
using System.Linq;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Data;

/// <summary>
/// Données officielles STE LTSERVICES SARL (source : fiche de configuration client).
/// </summary>
public static class LtServicesDonneesEntreprise
{
    public const string RaisonSociale = "STE LTSERVICES SARL";
    public const string Adresse = "01 ave de l'OUA, Concession Procoki, C/Ngaliema";
    public const string Nif = "A1400758Q";
    public const string Nrc = "CD/KIN/RCCM/13-B-01332";
    public const string IdNat = "01-28400-N77802L";
    /// <summary>Numéro employeur CNSS.</summary>
    public const string NumCnssEmployeur = "1001735300";
    /// <summary>Numéro d’affiliation CNSS de l’établissement.</summary>
    public const string NumeroAffiliationCnss = "010303376U1";
    public const string Telephone = "0818155792";
    public const string Email = "commercial@ltservices.eu";
    public const string SiteWeb = "ltservices.eu";

    /// <summary>
    /// Valeurs par défaut pour un formulaire vide (avant premier enregistrement).
    /// </summary>
    public static void RemplirModeleVide(Entreprise e)
    {
        e.RaisonSociale = RaisonSociale;
        e.Adresse = Adresse;
        e.Nif = Nif;
        e.Nrc = Nrc;
        e.IdNat = IdNat;
        e.NumCnss = NumCnssEmployeur;
        e.NumeroAffiliationCnss = NumeroAffiliationCnss;
        e.Telephone = Telephone;
        e.Email = Email;
        e.SiteWeb = SiteWeb;
    }

    /// <summary>
    /// Met à jour la première entreprise si elle est encore au gabarit générique « Mon Entreprise ».
    /// </summary>
    public static void AppliquerSiEntrepriseEncoreGenerique(PaieDbContext db)
    {
        var ent = db.Entreprises.OrderBy(e => e.Id).FirstOrDefault();
        if (ent == null) return;
        if (!string.Equals(ent.RaisonSociale?.Trim(), "Mon Entreprise", StringComparison.OrdinalIgnoreCase))
            return;

        RemplirModeleVide(ent);
        db.SaveChanges();
    }

    /// <summary>
    /// Complète téléphone, e-mail, site et affiliation CNSS si la raison sociale est LTS et que les champs sont encore vides (mise à jour après ajout de colonnes).
    /// </summary>
    public static void CompleterCoordonneesLtServicesSiChampsVides(PaieDbContext db)
    {
        var ent = db.Entreprises.OrderBy(e => e.Id).FirstOrDefault();
        if (ent == null) return;
        if (!string.Equals(ent.RaisonSociale?.Trim(), RaisonSociale, StringComparison.OrdinalIgnoreCase))
            return;

        var misAJour = false;
        if (string.IsNullOrWhiteSpace(ent.Telephone)) { ent.Telephone = Telephone; misAJour = true; }
        if (string.IsNullOrWhiteSpace(ent.Email)) { ent.Email = Email; misAJour = true; }
        if (string.IsNullOrWhiteSpace(ent.SiteWeb)) { ent.SiteWeb = SiteWeb; misAJour = true; }
        if (string.IsNullOrWhiteSpace(ent.NumeroAffiliationCnss)) { ent.NumeroAffiliationCnss = NumeroAffiliationCnss; misAJour = true; }
        if (string.IsNullOrWhiteSpace(ent.Nif)) { ent.Nif = Nif; misAJour = true; }
        if (string.IsNullOrWhiteSpace(ent.Nrc)) { ent.Nrc = Nrc; misAJour = true; }
        if (string.IsNullOrWhiteSpace(ent.IdNat)) { ent.IdNat = IdNat; misAJour = true; }
        if (string.IsNullOrWhiteSpace(ent.NumCnss)) { ent.NumCnss = NumCnssEmployeur; misAJour = true; }
        if (string.IsNullOrWhiteSpace(ent.Adresse)) { ent.Adresse = Adresse; misAJour = true; }

        if (misAJour)
            db.SaveChanges();
    }

    /// <summary>
    /// Aligne l'identité de la première entreprise sur les données officielles LTS
    /// si la raison sociale est encore vide, générique ou déjà LTS (mise à jour des champs légal).
    /// </summary>
    public static void SynchroniserIdentiteLtSiApplicable(PaieDbContext db)
    {
        var ent = db.Entreprises.OrderBy(e => e.Id).FirstOrDefault();
        if (ent == null) return;

        var rs = ent.RaisonSociale?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(rs) ||
            string.Equals(rs, "Mon Entreprise", StringComparison.OrdinalIgnoreCase))
        {
            RemplirModeleVide(ent);
            db.SaveChanges();
        }
    }
}
