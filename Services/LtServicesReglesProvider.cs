using MelodyPaieRDC.Data;

namespace MelodyPaieRDC.Services;

public static class LtServicesReglesProvider
{
    /// <summary>Charge les règles de l'entreprise courante (pointage + horaires).</summary>
    public static LtServicesRegles ChargerDepuisDb(PaieDbContext db) =>
        LtServicesRegles.DepuisParametres(ParametresApplicationHelper.GetParametresEntrepriseCourante(db));
}
