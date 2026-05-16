using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Persistance des valeurs de champs dynamiques (table FormFieldValues).
/// </summary>
public sealed class FormValueService
{
    private readonly PaieDbContext _db;

    public FormValueService(PaieDbContext db) => _db = db;

    public Dictionary<string, string?> ChargerValeurs(string formId, string entityType, int entityId)
    {
        return _db.FormFieldValues
            .AsNoTracking()
            .Where(v => v.FormId == formId && v.EntityType == entityType && v.EntityId == entityId)
            .ToDictionary(v => v.FieldKey, v => v.Value);
    }

    public void EnregistrerValeurs(string formId, string entityType, int entityId, IReadOnlyDictionary<string, string?> valeurs)
    {
        var existants = _db.FormFieldValues
            .Where(v => v.FormId == formId && v.EntityType == entityType && v.EntityId == entityId)
            .ToList();

        var parCle = existants.ToDictionary(v => v.FieldKey);
        var now = DateTime.UtcNow;

        foreach (var (cle, valeur) in valeurs)
        {
            if (parCle.TryGetValue(cle, out var row))
            {
                row.Value = valeur;
                row.DateModification = now;
            }
            else
            {
                _db.FormFieldValues.Add(new FormFieldValue
                {
                    FormId = formId,
                    EntityType = entityType,
                    EntityId = entityId,
                    FieldKey = cle,
                    Value = valeur,
                    DateModification = now
                });
            }
        }

        _db.SaveChanges();
    }

    public bool EntiteExiste(string entityType, int entityId)
    {
        return entityType.ToUpperInvariant() switch
        {
            "EMPLOYE" => _db.Employes.Any(e => e.Id == entityId),
            "ENTREPRISE" => _db.Entreprises.Any(e => e.Id == entityId),
            "CONTRAT" => _db.Contrats.Any(c => c.Id == entityId),
            _ => entityId > 0
        };
    }
}
