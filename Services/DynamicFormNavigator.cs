using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Forms.Metadata;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Views;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Ouvre les formulaires dynamiques selon le contexte métier.
/// </summary>
public static class DynamicFormNavigator
{
    public static void OuvrirGestionnaireDefinitions(Window owner)
    {
        var win = new FormDefinitionsWindow { Owner = owner };
        win.ShowDialog();
    }

    public static bool OuvrirFormulaire(Window owner, string formId, int entityId, string? sousTitre = null)
    {
        FormDefinitionLoader.AssurerDossierEtModelesParDefaut();
        var def = FormDefinitionLoader.ChargerParId(formId);
        if (def == null)
        {
            MessageBox.Show(
                $"Formulaire « {formId} » introuvable.\n\nDossier : {PaieDbContext.GetFormsDirectory()}",
                "Formulaire dynamique",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (entityId <= 0 && !string.Equals(def.EntityType, "Global", StringComparison.OrdinalIgnoreCase))
        {
            UiFeedback.Info("Sélectionnez d'abord un enregistrement ou créez l'entité principale.");
            return false;
        }

        var win = new DynamicFormWindow(def, entityId, sousTitre) { Owner = owner };
        return win.ShowDialog() == true;
    }

    public static void OuvrirChampsComplementairesEmploye(Window owner, Employe employe)
    {
        var sousTitre = $"{employe.Matricule} — {employe.Nom} {employe.Prenom}";
        OuvrirFormulaire(owner, "employe-extension", employe.Id, sousTitre);
    }

    public static void OuvrirChampsComplementairesEntreprise(Window owner)
    {
        using var db = new PaieDbContext();
        var ent = db.Entreprises.AsNoTracking().OrderBy(e => e.Id).FirstOrDefault();
        if (ent == null)
        {
            UiFeedback.Info("Aucune entreprise enregistrée.");
            return;
        }

        OuvrirFormulaire(owner, "entreprise-extension", ent.Id, ent.RaisonSociale);
    }

    /// <summary>
    /// Liste les formulaires applicables à un type d'entité.
    /// </summary>
    public static IReadOnlyList<FormDefinitionSummary> ListerPourEntite(string entityType) =>
        FormDefinitionLoader.ListerFormulaires()
            .Where(f => string.Equals(f.EntityType, entityType, StringComparison.OrdinalIgnoreCase))
            .ToList();
}
