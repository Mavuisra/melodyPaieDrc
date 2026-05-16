using System.Globalization;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

public sealed class PointageImportResult
{
    public int LignesLues { get; set; }
    public int PointagesValides { get; set; }
    public int LignesIgnorees { get; set; }
    public int EmployesTrouves { get; set; }
    public int JoursCalcules { get; set; }
    public int SuivisCrees { get; set; }
    public int SuivisMisAJour { get; set; }
}

public sealed class PointageImportService
{
    private readonly PaieDbContext _db;

    public PointageImportService(PaieDbContext db) => _db = db;

    /// <summary>
    /// Fusionne les pointages lus sur un terminal ZKTeco avec le suivi existant (ignore les jours en heures manuelles).
    /// La correspondance priorise Employe.ZkUserId puis le matricule.
    /// </summary>
    public PointageImportResult FusionnerPoinconsDepuisTerminal(IReadOnlyList<(string CodePin, DateTime DateHeure)> bruts)
    {
        var result = new PointageImportResult();
        if (bruts == null || bruts.Count == 0)
            return result;

        var employes = _db.Employes.ToList();
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);
        var mapEmployes = ConstruireMapEmployes(employes);
        var parEmployeJour = new Dictionary<(int EmployeId, DateTime Jour), List<DateTime>>();
        var employesIds = new HashSet<int>();

        foreach (var (codePin, dt) in bruts)
        {
            result.LignesLues++;
            if (!TrouverEmployeId(mapEmployes, codePin, out var employeId))
            {
                result.LignesIgnorees++;
                continue;
            }

            employesIds.Add(employeId);
            result.PointagesValides++;
            var jour = dt.Date;
            var key = (employeId, jour);
            if (!parEmployeJour.TryGetValue(key, out var liste))
            {
                liste = new List<DateTime>();
                parEmployeJour[key] = liste;
            }
            liste.Add(dt);
        }

        result.EmployesTrouves = employesIds.Count;

        foreach (var kvp in parEmployeJour)
        {
            var employeId = kvp.Key.EmployeId;
            var jour = kvp.Key.Jour;
            var nouveaux = kvp.Value;

            var jourDebut = jour.Date;
            var jourFinExcl = jourDebut.AddDays(1);
            var existant = _db.SuivisJournaliers.FirstOrDefault(s =>
                s.EmployeId == employeId &&
                s.Date >= jourDebut && s.Date < jourFinExcl);

            if (existant?.HeuresManuelles == true)
                continue;

            var anciens = PointagesJournalierSerializer.Deserialiser(existant?.PointagesJson, jour);
            var fusion = anciens
                .Concat(nouveaux)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            var heures = LtServicesPointageCalcul.CalculerHeuresPrestees(fusion, jour, reglesLt);
            var json = PointagesJournalierSerializer.Serialiser(fusion);

            if (existant == null)
            {
                _db.SuivisJournaliers.Add(new SuiviJournalier
                {
                    EmployeId = employeId,
                    Date = jour,
                    HeuresPrestees = heures,
                    TypeJour = SuiviJournalier.TypeNormal,
                    PointagesJson = json,
                    HeuresManuelles = false
                });
                result.SuivisCrees++;
            }
            else
            {
                existant.HeuresPrestees = heures;
                existant.TypeJour = SuiviJournalier.TypeNormal;
                existant.PointagesJson = json;
                existant.HeuresManuelles = false;
                result.SuivisMisAJour++;
            }

            result.JoursCalcules++;
        }

        _db.SaveChanges();
        return result;
    }

    private static bool TrouverEmployeId(Dictionary<string, int> map, string codeBrut, out int employeId)
    {
        employeId = 0;
        var code = NormaliserCle(codeBrut);
        if (!string.IsNullOrWhiteSpace(code) && map.TryGetValue(code, out employeId))
            return true;

        var chiffres = NormaliserChiffres(codeBrut);
        if (!string.IsNullOrWhiteSpace(chiffres) && map.TryGetValue(chiffres, out employeId))
            return true;

        return false;
    }

    private static Dictionary<string, int> ConstruireMapEmployes(IEnumerable<Employe> employes)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in employes)
        {
            var zkId = e.ZkUserId?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(zkId))
            {
                var zkCle = NormaliserCle(zkId);
                if (!map.ContainsKey(zkCle)) map.Add(zkCle, e.Id);

                var zkDigits = NormaliserChiffres(zkId);
                if (!string.IsNullOrWhiteSpace(zkDigits) && !map.ContainsKey(zkDigits))
                    map.Add(zkDigits, e.Id);
            }

            var mat = e.Matricule?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(mat)) continue;

            var cle = NormaliserCle(mat);
            if (!map.ContainsKey(cle)) map.Add(cle, e.Id);

            var chiffres = NormaliserChiffres(mat);
            if (!string.IsNullOrWhiteSpace(chiffres) && !map.ContainsKey(chiffres))
                map.Add(chiffres, e.Id);
        }
        return map;
    }

    private static string NormaliserCle(string valeur)
        => (valeur ?? "").Trim().Replace(" ", "").ToUpperInvariant();

    private static string NormaliserChiffres(string valeur)
    {
        var digits = new string((valeur ?? "").Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits)) return "";
        var sansZeros = digits.TrimStart('0');
        return string.IsNullOrEmpty(sansZeros) ? "0" : sansZeros;
    }
}
