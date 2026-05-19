using System.IO;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>Identité visuelle de l'entreprise courante (logo, couleurs, thème Material Design).</summary>
public static class EntrepriseBrandingService
{
    public const string CouleurPrincipaleParDefaut = "#1E3A5F";
    public const string CouleurSecondaireParDefaut = "#00A6B8";

    public sealed record ProfilEntreprise(
        string? RaisonSociale,
        string? CheminLogo,
        string CouleurPrincipale,
        string CouleurSecondaire);

    public static ProfilEntreprise ChargerProfilCourant(PaieDbContext? dbExistant = null)
    {
        var db = dbExistant;
        var possedeDb = db != null;
        db ??= new PaieDbContext();

        try
        {
            var id = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(db);
            if (id <= 0)
                return new ProfilEntreprise(null, null, CouleurPrincipaleParDefaut, CouleurSecondaireParDefaut);

            var ent = db.Entreprises.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefault(e => e.Id == id);
            if (ent == null)
                return new ProfilEntreprise(null, null, CouleurPrincipaleParDefaut, CouleurSecondaireParDefaut);

            string? logo = null;
            if (!string.IsNullOrWhiteSpace(ent.Logo))
            {
                var fullPath = Path.Combine(PaieDbContext.GetDataDirectory(), Path.GetFileName(ent.Logo));
                if (File.Exists(fullPath))
                    logo = fullPath;
            }

            return new ProfilEntreprise(
                ent.RaisonSociale,
                logo,
                NormaliserCouleurHex(ent.CouleurPrincipale, CouleurPrincipaleParDefaut),
                NormaliserCouleurHex(ent.CouleurSecondaire, CouleurSecondaireParDefaut));
        }
        finally
        {
            if (!possedeDb)
                db.Dispose();
        }
    }

    /// <summary>Applique le thème Material Design et les ressources de marque sur toute l'application.</summary>
    public static void AppliquerIdentiteVisuelleGlobale()
    {
        var profil = ChargerProfilCourant();
        AppliquerCouleursHex(profil.CouleurPrincipale, profil.CouleurSecondaire);
    }

    /// <summary>Aperçu temps réel (assistant configuration, avant enregistrement).</summary>
    public static void AppliquerApercuCouleurs(string? couleurPrincipaleHex, string? couleurSecondaireHex) =>
        AppliquerCouleursHex(couleurPrincipaleHex, couleurSecondaireHex);

    private static void AppliquerCouleursHex(string? couleurPrincipaleHex, string? couleurSecondaireHex)
    {
        if (!EssayerConvertirCouleur(couleurPrincipaleHex ?? CouleurPrincipaleParDefaut, out var primaire))
            primaire = (Color)ColorConverter.ConvertFromString(CouleurPrincipaleParDefaut)!;

        if (!EssayerConvertirCouleur(couleurSecondaireHex ?? CouleurSecondaireParDefaut, out var secondaire))
            secondaire = (Color)ColorConverter.ConvertFromString(CouleurSecondaireParDefaut)!;

        AppliquerThemeMaterialDesign(primaire, secondaire);
        AppliquerRessourcesMarque(Application.Current.Resources, primaire, secondaire);
    }

    /// <summary>Compatibilité : applique sur la fenêtre (délègue au thème global).</summary>
    public static void AppliquerCouleursSurFenetre(Window fenetre)
    {
        AppliquerIdentiteVisuelleGlobale();
    }

    public static void AppliquerCouleursSurRessources(ResourceDictionary ressources, string couleurPrincipaleHex)
    {
        if (!EssayerConvertirCouleur(couleurPrincipaleHex, out var primaire))
            return;
        if (!EssayerConvertirCouleur(CouleurSecondaireParDefaut, out var secondaire))
            secondaire = (Color)ColorConverter.ConvertFromString(CouleurSecondaireParDefaut)!;
        AppliquerRessourcesMarque(ressources, primaire, secondaire);
    }

    public static string NormaliserCouleurHex(string? valeur, string defaut)
    {
        if (string.IsNullOrWhiteSpace(valeur))
            return defaut;
        var s = valeur.Trim();
        if (s.Length == 0)
            return defaut;
        return s.StartsWith('#') ? s : "#" + s;
    }

    private static void AppliquerThemeMaterialDesign(Color primaire, Color secondaire)
    {
        try
        {
            var palette = new PaletteHelper();
            var theme = palette.GetTheme();
            theme.SetPrimaryColor(primaire);
            theme.SetSecondaryColor(secondaire);
            palette.SetTheme(theme);
        }
        catch
        {
            // Thème Material Design optionnel : les ressources Brand* restent appliquées.
        }
    }

    private static void AppliquerRessourcesMarque(ResourceDictionary ressources, Color primaire, Color secondaire)
    {
        var primaireFonce = ColorBlendHelper.Assombrir(primaire, 0.55);
        var primaireClair = ColorBlendHelper.Eclaircir(primaire, 0.22);
        var sidebarFond = ColorBlendHelper.Assombrir(primaire, 0.42);
        var sidebarSurvol = ColorBlendHelper.Melanger(sidebarFond, primaire, 0.35);

        DefinirCouleur(ressources, "BrandPrimaryColor", primaire);
        DefinirCouleur(ressources, "BrandPrimaryDarkColor", primaireFonce);
        DefinirCouleur(ressources, "BrandPrimaryLightColor", primaireClair);
        DefinirCouleur(ressources, "BrandSecondaryColor", secondaire);

        DefinirBrush(ressources, "BrandPrimaryBrush", primaire);
        DefinirBrush(ressources, "BrandPrimaryDarkBrush", primaireFonce);
        DefinirBrush(ressources, "BrandSecondaryBrush", secondaire);
        DefinirBrush(ressources, "BrandOnPrimaryBrush", Colors.White);

        DefinirBrush(ressources, "SidebarBrush", sidebarFond);
        DefinirBrush(ressources, "SidebarHoverBrush", sidebarSurvol);
        DefinirBrush(ressources, "SidebarSelectedBrush", primaire);
        DefinirBrush(ressources, "SidebarAccentBrush", ColorBlendHelper.Eclaircir(secondaire, 0.15));
    }

    private static void DefinirCouleur(ResourceDictionary ressources, string cle, Color couleur) =>
        ressources[cle] = couleur;

    private static void DefinirBrush(ResourceDictionary ressources, string cle, Color couleur) =>
        ressources[cle] = new SolidColorBrush(couleur);

    private static bool EssayerConvertirCouleur(string hex, out Color couleur)
    {
        couleur = default;
        try
        {
            var normalise = NormaliserCouleurHex(hex, CouleurPrincipaleParDefaut);
            couleur = (Color)ColorConverter.ConvertFromString(normalise)!;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
