using System.Globalization;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Lecture / écriture des champs personnalisés (EAV).
/// </summary>
public class ChampsDynamiquesService
{
    private readonly PaieDbContext _db;

    public ChampsDynamiquesService(PaieDbContext db) => _db = db;

    public IReadOnlyList<DefinitionChampDynamique> ObtenirDefinitions(string entiteCible, int? entrepriseId)
    {
        return _db.DefinitionsChampsDynamiques
            .Where(d => d.EntiteCible == entiteCible && (d.EntrepriseId == null || d.EntrepriseId == entrepriseId))
            .OrderBy(d => d.Ordre)
            .ThenBy(d => d.Libelle)
            .AsNoTracking()
            .ToList();
    }

    public Dictionary<string, string?> LireValeurs(int entiteId, string entiteCible, int? entrepriseId)
    {
        var defs = ObtenirDefinitions(entiteCible, entrepriseId);
        var defIds = defs.Select(d => d.Id).ToList();
        var valeurs = _db.ValeursChampsDynamiques
            .Where(v => v.EntiteId == entiteId && defIds.Contains(v.DefinitionChampId))
            .Include(v => v.DefinitionChamp)
            .ToList();

        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in defs)
        {
            var val = valeurs.FirstOrDefault(v => v.DefinitionChampId == def.Id);
            result[def.Code] = FormaterValeur(def, val);
        }
        return result;
    }

    public void EnregistrerValeur(int definitionChampId, int entiteId, string? texte, decimal? nombre, DateTime? date, bool? booleen)
    {
        var existant = _db.ValeursChampsDynamiques
            .FirstOrDefault(v => v.DefinitionChampId == definitionChampId && v.EntiteId == entiteId);

        if (existant == null)
        {
            _db.ValeursChampsDynamiques.Add(new ValeurChampDynamique
            {
                DefinitionChampId = definitionChampId,
                EntiteId = entiteId,
                ValeurTexte = texte,
                ValeurNombre = nombre,
                ValeurDate = date,
                ValeurBooleen = booleen,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existant.ValeurTexte = texte;
            existant.ValeurNombre = nombre;
            existant.ValeurDate = date;
            existant.ValeurBooleen = booleen;
            existant.UpdatedAtUtc = DateTime.UtcNow;
        }

        _db.SaveChanges();
    }

    private static string? FormaterValeur(DefinitionChampDynamique def, ValeurChampDynamique? val)
    {
        if (val == null) return null;
        return def.TypeDonnee switch
        {
            DefinitionChampDynamique.TypeNombre => val.ValeurNombre?.ToString(CultureInfo.InvariantCulture),
            DefinitionChampDynamique.TypeDate => val.ValeurDate?.ToString("yyyy-MM-dd"),
            DefinitionChampDynamique.TypeBooleen => val.ValeurBooleen?.ToString(),
            _ => val.ValeurTexte
        };
    }
}
