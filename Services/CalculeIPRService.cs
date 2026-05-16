using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Moteur de calcul IPR (Impôt sur les Rémunérations) pour la RDC.
/// Les tranches et paramètres sont entièrement configurables via la base de données
/// (tables GrilleIPR et ParametreIPR).
/// </summary>
public class CalculeIPRService
{
    private readonly PaieDbContext _db;

    public CalculeIPRService(PaieDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Calcule l'IPR mensuelle et retourne le détail (brut, réduction famille, net).
    /// </summary>
    public IprResultat CalculerDetailsIprMensuelle(decimal baseImposableMensuelle, int nombreEnfantsACharge)
    {
        if (baseImposableMensuelle <= 0)
            return new IprResultat();

        // SQLite ne supporte pas ORDER BY sur decimal : chargement puis tri en mémoire
        var tranches = _db.GrillesIpr.ToList().OrderBy(t => t.BorneInf).ToList();

        if (tranches.Count == 0)
        {
            // Aucun barème configuré : on ne retient pas d'IPR.
            return new IprResultat();
        }

        var param = _db.ParametresIpr.FirstOrDefault()
                    ?? new ParametreIPR(); // valeurs par défaut (ex : plafond 30%, aucune réduction)

        decimal iprBrut = 0m;

        foreach (var tranche in tranches)
        {
            if (baseImposableMensuelle <= tranche.BorneInf)
                continue;

            // Si BorneSup <= 0, on considère qu'il n'y a pas de plafond pour la tranche.
            var upperLimit = tranche.BorneSup <= 0 ? baseImposableMensuelle : Math.Min(baseImposableMensuelle, tranche.BorneSup);

            var montantDansTranche = Math.Max(0, upperLimit - tranche.BorneInf);
            if (montantDansTranche <= 0)
                continue;

            iprBrut += montantDansTranche * (tranche.Taux / 100m);
        }

        // Réduction familiale par enfant (facultative / configurable)
        decimal reductionFamille = 0m;
        if (param.ReductionParEnfant > 0 && nombreEnfantsACharge > 0)
        {
            reductionFamille = param.ReductionParEnfant * nombreEnfantsACharge;
        }

        var iprNet = Math.Max(0m, iprBrut - reductionFamille);

        // Application du plafond de taux effectif (ex : 30% du salaire imposable)
        if (param.TauxEffectifMaximum > 0)
        {
            var iprMaximum = baseImposableMensuelle * param.TauxEffectifMaximum;
            if (iprNet > iprMaximum)
                iprNet = iprMaximum;
        }

        var arrondiNet = decimal.Round(iprNet, 2, MidpointRounding.AwayFromZero);

        return new IprResultat
        {
            BaseImposable = baseImposableMensuelle,
            IprBrut = decimal.Round(iprBrut, 2, MidpointRounding.AwayFromZero),
            ReductionFamille = decimal.Round(reductionFamille, 2, MidpointRounding.AwayFromZero),
            IprNet = arrondiNet
        };
    }

    /// <summary>
    /// Version simplifiée : ne retourne que le montant net mensuel.
    /// </summary>
    public decimal CalculerIprMensuelle(decimal baseImposableMensuelle, int nombreEnfantsACharge)
        => CalculerDetailsIprMensuelle(baseImposableMensuelle, nombreEnfantsACharge).IprNet;

    /// <summary>
    /// Calcule l'IPR annuelle en partant d'une base imposable mensuelle.
    /// </summary>
    public decimal CalculerIprAnnuelle(decimal baseImposableMensuelle, int nombreEnfantsACharge)
        => CalculerIprMensuelle(baseImposableMensuelle, nombreEnfantsACharge) * 12m;
}

/// <summary>
/// Détail du calcul IPR pour une période mensuelle.
/// </summary>
public class IprResultat
{
    public decimal BaseImposable { get; set; }
    public decimal IprBrut { get; set; }
    public decimal ReductionFamille { get; set; }
    public decimal IprNet { get; set; }
}
