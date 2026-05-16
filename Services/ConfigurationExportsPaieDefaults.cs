using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>Valeurs par défaut des profils d'export (modifiables par entreprise sans recompilation).</summary>
public static class ConfigurationExportsPaieDefaults
{
    public static ConfigurationExportsPaie Creer()
    {
        return new ConfigurationExportsPaie
        {
            ExportCnssEdeclaration = CreerProfilCnss(),
            ExportIprDgi = CreerProfilIprDgi(),
            LivrePaieReglementaire = CreerProfilLivrePaie(),
            BulletinReglementaire = CreerProfilBulletin(),
            ProfilsVirement = CreerProfilsVirement(),
            CodeProfilVirementParDefaut = "GENERIQUE_CSV",
            Cloture = CreerCloture()
        };
    }

    public static ConfigurationExportsPaie Fusionner(ConfigurationExportsPaie? existant)
    {
        var defaut = Creer();
        if (existant is null) return defaut;

        if (string.IsNullOrWhiteSpace(existant.ExportCnssEdeclaration?.Code))
            existant.ExportCnssEdeclaration = defaut.ExportCnssEdeclaration;
        else if (existant.ExportCnssEdeclaration.Colonnes.Count == 0
                 || ProfilCnssUtiliseAncienFormat(existant.ExportCnssEdeclaration))
            existant.ExportCnssEdeclaration = defaut.ExportCnssEdeclaration;

        if (string.IsNullOrWhiteSpace(existant.ExportIprDgi?.Code))
            existant.ExportIprDgi = defaut.ExportIprDgi;
        else if (existant.ExportIprDgi.Colonnes.Count == 0)
            existant.ExportIprDgi.Colonnes = defaut.ExportIprDgi.Colonnes;

        if (string.IsNullOrWhiteSpace(existant.LivrePaieReglementaire?.Code))
            existant.LivrePaieReglementaire = defaut.LivrePaieReglementaire;
        else if (existant.LivrePaieReglementaire.Colonnes.Count == 0)
            existant.LivrePaieReglementaire.Colonnes = defaut.LivrePaieReglementaire.Colonnes;

        if (string.IsNullOrWhiteSpace(existant.BulletinReglementaire?.Code))
            existant.BulletinReglementaire = defaut.BulletinReglementaire;

        if (existant.ProfilsVirement.Count == 0)
            existant.ProfilsVirement = defaut.ProfilsVirement;

        if (existant.Cloture.Controles.Count == 0)
            existant.Cloture = defaut.Cloture;

        if (string.IsNullOrWhiteSpace(existant.CodeProfilVirementParDefaut))
            existant.CodeProfilVirementParDefaut = defaut.CodeProfilVirementParDefaut;

        return existant;
    }

    private static ProfilExportConfig CreerProfilCnss() => new()
    {
        Code = "CNSS_EDECLARATION",
        Libelle = "CNSS — e-déclaration (modèle portail officiel)",
        TypeFormat = "Excel",
        Separateur = ";",
        ExtensionFichier = "xlsx",
        InclureLignesEnteteEmployeur = false,
        Colonnes = ColonnesCnss()
    };

    /// <summary>Modèle officiel edeclaration.cnss.cd (12 colonnes A–L).</summary>
    private static List<ColonneExportConfig> ColonnesCnss() =>
    [
        Col("MATRICULE", "Matricule travailleur", "Employe.Matricule", 1),
        Col("NUM_CNSS", "Num Immatriculation CNSS", "Employe.NumCnss", 2),
        Col("NOMS", "Noms", "Employe.Nom", 3),
        Col("POSTNOMS", "Post noms", "Employe.Postnom", 4),
        Col("PRENOMS", "Prenoms", "Employe.Prenom", 5),
        Col("TYPE_TRAV", "Type travailleur(1=Travailleur , 2=Assimile)", "Employe.TypeTravailleurCnss", 6, "N0"),
        Col("COMMUNE", "Commune ou Territoire affectation", "Employe.CommuneAffectation", 7),
        Col("PERIODE", "Periode Cotisee (jj/mm/aaaa)", "Periode.CotiseeJjMmAaaa", 8),
        Col("MONTANT_COTISE", "Montant Cotise", "Bulletin.MontantCotiseCnss", 9, "N2"),
        Col("JOURS", "Nbre De Jours de travail", "Saisie.JoursPrestes", 10, "N0"),
        Col("HEURES", "Nbre De heure de travail", "Stat.HeuresTravail", 11, "N2"),
        Col("BRUT_IMP", "Montant Brut Imposable", "Bulletin.BrutImposableCnss", 12, "N2")
    ];

    private static bool ProfilCnssUtiliseAncienFormat(ProfilExportConfig profil) =>
        profil.Colonnes.Any(c =>
            string.Equals(c.Code, "ORDRE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Code, "CNSS_PATRONAL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Code, "SALAIRE_BASE", StringComparison.OrdinalIgnoreCase));

    private static ProfilExportConfig CreerProfilIprDgi() => new()
    {
        Code = "DGI_IPR",
        Libelle = "DGI — déclaration IPR (retenue à la source)",
        TypeFormat = "Csv",
        Separateur = ";",
        ExtensionFichier = "csv",
        InclureLignesEnteteEmployeur = true,
        LignesEnteteEmployeur =
        {
            new() { Libelle = "Raison sociale", SourceDonnee = "Entreprise.RaisonSociale" },
            new() { Libelle = "NIF", SourceDonnee = "Entreprise.Nif" },
            new() { Libelle = "Période", SourceDonnee = "Periode.Libelle" },
            new() { Libelle = "Période (AAAAMM)", SourceDonnee = "Periode.AaaaMm" }
        },
        Colonnes = ColonnesIpr()
    };

    private static List<ColonneExportConfig> ColonnesIpr() =>
    [
        Col("ORDRE", "N° ordre", "Stat.NumeroOrdre", 1),
        Col("MATRICULE", "Matricule", "Employe.Matricule", 2),
        Col("NIF_SALARIE", "NIF salarié", "Employe.Nif", 3),
        Col("NOM_COMPLET", "Nom complet", "Employe.NomComplet", 4),
        Col("BASE_IMP", "Base imposable", "Bulletin.BaseIpr", 5, "N2"),
        Col("IPR_BRUT", "IPR brut", "Bulletin.IprBrut", 6, "N2"),
        Col("REDUC_FAMILLE", "Réduction famille", "Bulletin.ReductionFamille", 7, "N2"),
        Col("IPR_NET", "IPR net retenu", "Bulletin.IprNet", 8, "N2"),
        Col("REMUNERATION", "Rémunération brute", "Bulletin.SalaireBrut", 9, "N2")
    ];

    private static ProfilExportConfig CreerProfilLivrePaie() => new()
    {
        Code = "LIVRE_2008",
        Libelle = "Livre de paie — arrêté 08/08/2008 (colonnes configurables)",
        TypeFormat = "Excel",
        ExtensionFichier = "xlsx",
        Colonnes = ColonnesLivrePaie()
    };

    private static List<ColonneExportConfig> ColonnesLivrePaie() =>
    [
        Col("ORDRE", "N°", "Stat.NumeroOrdre", 1),
        Col("MATRICULE", "Matricule", "Employe.Matricule", 2),
        Col("NOM_COMPLET", "Nom Employé", "Employe.NomComplet", 3),
        Col("EMPLOI", "Fonction", "Employe.Fonction", 4),
        Col("REMUNERATION", "Salaire Brut", "Bulletin.SalaireBrut", 5, "N2"),
        Col("IPR", "IPR", "Bulletin.IprNet", 6, "N2"),
        Col("CNSS", "Part CNSS Employé", "Bulletin.CnssOuvrier", 7, "N2"),
        Col("CNSS_PAT", "Part CNSS Employeur", "Bulletin.CnssPatronal", 8, "N2"),
        Col("NET", "Net à payer", "Bulletin.NetAPayer", 9, "N2")
    ];

    /// <summary>Colonnes détaillées arrêté 2008 (profil étendu optionnel).</summary>
    public static List<ColonneExportConfig> ColonnesLivrePaieDetail2008() =>
    [
        Col("ORDRE", "N° ordre", "Stat.NumeroOrdre", 1),
        Col("MATRICULE", "Matricule", "Employe.Matricule", 2),
        Col("NOM_COMPLET", "Noms et prénoms", "Employe.NomCompletMajuscules", 3),
        Col("EMPLOI", "Emploi / fonction", "Employe.Fonction", 4),
        Col("NUM_CNSS", "N° affiliation CNSS", "Employe.NumCnss", 5),
        Col("REMUNERATION", "Rémunération totale", "Bulletin.SalaireBrut", 6, "N2"),
        Col("HEURES_SUP", "Heures supplémentaires", "Detail.MontantHeuresSup", 7, "N2"),
        Col("CNSS", "Cotisation pension", "Bulletin.CnssOuvrier", 8, "N2"),
        Col("IPR", "Retenue fiscale IPR", "Bulletin.IprNet", 9, "N2"),
        Col("NET", "Net à payer", "Bulletin.NetAPayer", 10, "N2")
    ];

    private static ProfilExportConfig CreerProfilBulletin() => new()
    {
        Code = "BULLETIN_2008",
        Libelle = "Mentions bulletin (arrêté 2008)",
        TypeFormat = "Pdf",
        Colonnes = ColonnesLivrePaie()
    };

    private static List<ProfilBanqueVirement> CreerProfilsVirement() =>
    [
        new ProfilBanqueVirement
        {
            Code = "GENERIQUE_CSV",
            Libelle = "Virement générique (CSV)",
            TypeFormat = "Csv",
            Separateur = ";",
            ExtensionFichier = "csv",
            PrefixeReference = "PAIE",
            Colonnes =
            [
                Col("REF", "Référence", "Virement.Reference", 1),
                Col("BANQUE", "Code banque", "Employe.CodeBanque", 2),
                Col("COMPTE", "N° compte", "Employe.NumeroCompteBancaire", 3),
                Col("TITULAIRE", "Titulaire", "Employe.TitulaireCompte", 4),
                Col("MONTANT", "Montant", "Bulletin.NetAPayer", 5, "N2"),
                Col("DEVISE", "Devise", "Employe.DeviseCompte", 6),
                Col("LIBELLE", "Libellé", "Virement.Libelle", 7)
            ]
        },
        new ProfilBanqueVirement
        {
            Code = "RAWBANK",
            Libelle = "Rawbank (CSV)",
            TypeFormat = "Csv",
            Separateur = ";",
            ExtensionFichier = "csv",
            PrefixeReference = "RAW",
            Colonnes =
            [
                Col("COMPTE", "Compte bénéficiaire", "Employe.NumeroCompteBancaire", 1),
                Col("NOM", "Nom bénéficiaire", "Employe.TitulaireCompte", 2),
                Col("MONTANT", "Montant", "Bulletin.NetAPayer", 3, "N2"),
                Col("REF", "Référence", "Virement.Reference", 4),
                Col("MOTIF", "Motif", "Virement.Libelle", 5)
            ]
        },
        new ProfilBanqueVirement
        {
            Code = "EQUITY_BCDC",
            Libelle = "Equity / BCDC (CSV large)",
            TypeFormat = "Csv",
            Separateur = ",",
            ExtensionFichier = "csv",
            PrefixeReference = "SAL",
            Colonnes =
            [
                Col("MATRICULE", "Matricule", "Employe.Matricule", 1),
                Col("COMPTE", "Compte", "Employe.NumeroCompteBancaire", 2),
                Col("MONTANT", "Montant net", "Bulletin.NetAPayer", 3, "N2"),
                Col("DEVISE", "Devise", "Employe.DeviseCompte", 4),
                Col("REF", "Référence paiement", "Virement.Reference", 5)
            ]
        }
    ];

    private static CloturePaieConfig CreerCloture() => new()
    {
        ExigerControlesSansErreur = true,
        ExportsOfficielsExigentPeriodeCloturee = false,
        BloquerSaisiePaieSiCloturee = true,
        BloquerSuppressionBulletinSiCloturee = true,
        Controles =
        [
            Ctrl("AUCUN_BULLETIN", "Au moins un bulletin doit exister pour la période", "Erreur"),
            Ctrl("EMPLOYE_SANS_BULLETIN", "Chaque employé actif doit avoir un bulletin", "Erreur"),
            Ctrl("EMPLOYE_SANS_CNSS", "Numéro CNSS manquant sur un ou plusieurs salariés", "Avertissement"),
            Ctrl("NET_NEGATIF", "Aucun bulletin avec net à payer négatif", "Erreur"),
            Ctrl("TOTAL_CNSS", "Cohérence totaux CNSS (bulletins vs recalcul)", "Avertissement"),
            Ctrl("TOTAL_IPR", "Cohérence totaux IPR", "Avertissement"),
            Ctrl("COMPTE_BANQUE_MANQUANT", "Compte bancaire manquant (si virement prévu)", "Avertissement")
        ]
    };

    private static ColonneExportConfig Col(string code, string libelle, string source, int ordre, string? format = null) =>
        new()
        {
            Code = code,
            Libelle = libelle,
            SourceDonnee = source,
            Ordre = ordre,
            Actif = true,
            FormatNombre = format
        };

    private static ControleClotureConfig Ctrl(string code, string libelle, string severite) =>
        new() { Code = code, Libelle = libelle, Severite = severite, Actif = true };
}
