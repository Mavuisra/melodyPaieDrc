using System.Globalization;
using System.Linq;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Charge la politique de paie active et ses paramètres configurables.
/// </summary>
public class PolitiquePaieService
{
    private readonly PaieDbContext _db;

    public PolitiquePaieService(PaieDbContext db) => _db = db;

    public PolitiquePaieContext Charger(int entrepriseId)
    {
        var politique = _db.PolitiquesPaie
            .Include(p => p.Parametres)
            .Include(p => p.Rubriques)
            .Where(p => p.EntrepriseId == entrepriseId && p.Actif)
            .OrderByDescending(p => p.DateEffet)
            .ThenByDescending(p => p.Id)
            .FirstOrDefault();

        if (politique == null)
        {
            politique = CreerPolitiqueParDefaut(entrepriseId);
        }

        var parametres = politique.Parametres.ToDictionary(p => p.Cle, p => p.Valeur, StringComparer.OrdinalIgnoreCase);
        var rubriques = politique.Rubriques
            .Where(r => r.AfficherSurBulletin)
            .OrderBy(r => r.OrdreAffichage)
            .ThenBy(r => r.Code)
            .ToList();

        return new PolitiquePaieContext(politique, parametres, rubriques);
    }

    private PolitiquePaie CreerPolitiqueParDefaut(int entrepriseId)
    {
        var politique = DonneesPaieReferenceSeed.CreerPolitiqueParDefaut(entrepriseId);
        _db.PolitiquesPaie.Add(politique);
        _db.SaveChanges();
        return politique;
    }
}

public sealed class PolitiquePaieContext
{
    public PolitiquePaie Politique { get; }
    private readonly IReadOnlyDictionary<string, string> _parametres;
    public IReadOnlyList<RubriqueBulletin> Rubriques { get; }

    public PolitiquePaieContext(
        PolitiquePaie politique,
        IReadOnlyDictionary<string, string> parametres,
        IReadOnlyList<RubriqueBulletin> rubriques)
    {
        Politique = politique;
        _parametres = parametres;
        Rubriques = rubriques;
    }

    public decimal JoursReferencePaie => GetDecimal(ParametrePolitiquePaie.Cles.JoursReferencePaie, 26m);
    public decimal HeuresParJour => GetDecimal(ParametrePolitiquePaie.Cles.HeuresParJour, 8m);
    public bool SalaireContratEnNet => GetBool(ParametrePolitiquePaie.Cles.SalaireContratEnNet, false);
    public bool UtiliserBaremeIpr => GetBool(ParametrePolitiquePaie.Cles.UtiliserBaremeIpr, true);
    public bool UtiliserTauxSociauxDb => GetBool(ParametrePolitiquePaie.Cles.UtiliserTauxSociauxDb, true);
    public string ModeCalculPresence => GetString(ParametrePolitiquePaie.Cles.ModeCalculPresence, ParametrePolitiquePaie.ModePresencePointages);

    public string? LibelleRubrique(string code)
        => Rubriques.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase))?.Libelle;

    public decimal GetDecimal(string cle, decimal defaut)
    {
        if (!_parametres.TryGetValue(cle, out var v)) return defaut;
        return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : defaut;
    }

    public bool GetBool(string cle, bool defaut)
    {
        if (!_parametres.TryGetValue(cle, out var v)) return defaut;
        return v is "1" or "true" or "True" or "oui" or "Oui";
    }

    public string GetString(string cle, string defaut)
        => _parametres.TryGetValue(cle, out var v) && !string.IsNullOrWhiteSpace(v) ? v : defaut;
}
