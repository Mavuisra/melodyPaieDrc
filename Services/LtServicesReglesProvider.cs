using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

public static class LtServicesReglesProvider
{
    public static LtServicesRegles ChargerDepuisDb(PaieDbContext db)
    {
        ParametresApplicationHelper.EnsureRow(db);
        var p = db.ParametresApplication.FirstOrDefault(x => x.Id == ParametresApplication.SingletonId);
        return LtServicesRegles.DepuisParametres(p);
    }
}
