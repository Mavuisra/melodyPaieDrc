using MelodyPaieRDC.Data;
using MelodyPaieRDC.Forms.Metadata;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Résout les listes déroulantes définies dans les métadonnées (source DB ou statique).
/// </summary>
public sealed class FormLookupResolver
{
    private readonly PaieDbContext _db;

    public FormLookupResolver(PaieDbContext db) => _db = db;

    public IReadOnlyList<LookupOption> Resoudre(LookupDefinition? lookup)
    {
        if (lookup == null) return Array.Empty<LookupOption>();

        var source = lookup.Source.Trim().ToLowerInvariant();
        return source switch
        {
            "static" => (lookup.Items ?? new List<LookupItem>())
                .Select(i => new LookupOption(i.Value, i.Label))
                .ToList(),
            "departements" => _db.Departements
                .AsNoTracking()
                .OrderBy(d => d.NomDepartement)
                .Select(d => new LookupOption(d.Id.ToString(), d.NomDepartement))
                .ToList(),
            "categoriesprofessionnelles" => _db.CategoriesProfessionnelles
                .AsNoTracking()
                .OrderBy(c => c.Libelle)
                .Select(c => new LookupOption(c.Id.ToString(), c.Libelle))
                .ToList(),
            "periodespaie" => _db.PeriodesPaie
                .AsNoTracking()
                .OrderByDescending(p => p.Annee)
                .ThenByDescending(p => p.Mois)
                .Select(p => new LookupOption(p.Id.ToString(), $"{p.Mois:D2}/{p.Annee}"))
                .ToList(),
            _ => (lookup.Items ?? new List<LookupItem>())
                .Select(i => new LookupOption(i.Value, i.Label))
                .ToList()
        };
    }
}

public sealed class LookupOption
{
    public LookupOption(string value, string label)
    {
        Value = value;
        Label = label;
    }

    public string Value { get; }
    public string Label { get; }
}
