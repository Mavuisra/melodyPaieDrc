using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Calcul des cotisations sociales (CNSS, INPP, ONEM, ...) à partir des taux en base.
/// Les taux sont configurables via la table TauxSociaux (codes : CNSS_Ouvrier, CNSS_Patronal, INPP, ONEM, ...).
/// </summary>
public class CotisationsSocialesService
{
    private readonly PaieDbContext _db;

    public CotisationsSocialesService(PaieDbContext db)
    {
        _db = db;
    }

    public CotisationsResultat Calculer(decimal salaireBrut, int? entrepriseId = null)
    {
        if (salaireBrut <= 0)
            return new CotisationsResultat();

        decimal Taux(string code)
            => _db.TauxSociaux
                .Where(t => t.Code == code && (t.EntrepriseId == null || t.EntrepriseId == entrepriseId))
                .Select(t => t.Pourcentage)
                .FirstOrDefault();

        var tauxCnssOuvrier = Taux("CNSS_Ouvrier");
        var tauxCnssPatronal = Taux("CNSS_Patronal");
        var tauxInpp = Taux("INPP");
        var tauxOnem = Taux("ONEM");

        var cotisations = new CotisationsResultat
        {
            TauxCnssOuvrier = tauxCnssOuvrier,
            TauxCnssPatronal = tauxCnssPatronal,
            TauxInpp = tauxInpp,
            TauxOnem = tauxOnem,
            CnssOuvrier = Round(salaireBrut * tauxCnssOuvrier / 100m),
            CnssPatronal = Round(salaireBrut * tauxCnssPatronal / 100m),
            Inpp = Round(salaireBrut * tauxInpp / 100m),
            Onem = Round(salaireBrut * tauxOnem / 100m)
        };

        return cotisations;
    }

    private static decimal Round(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Résultat détaillé des cotisations sociales.
/// </summary>
public class CotisationsResultat
{
    public decimal TauxCnssOuvrier { get; set; }
    public decimal TauxCnssPatronal { get; set; }
    public decimal TauxInpp { get; set; }
    public decimal TauxOnem { get; set; }

    /// <summary>
    /// Part ouvrière CNSS (retenue sur salaire).
    /// </summary>
    public decimal CnssOuvrier { get; set; }

    /// <summary>
    /// Part patronale CNSS (charge employeur).
    /// </summary>
    public decimal CnssPatronal { get; set; }

    /// <summary>
    /// Cotisation INPP (employeur).
    /// </summary>
    public decimal Inpp { get; set; }

    /// <summary>
    /// Cotisation ONEM (employeur).
    /// </summary>
    public decimal Onem { get; set; }
}

