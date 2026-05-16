using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services.Export;

namespace MelodyPaieRDC.Services;

public abstract class PaieExportServiceBase
{
    protected readonly PaieDbContext Db;
    protected readonly PaieExportContextFactory Factory;

    protected PaieExportServiceBase(PaieDbContext db)
    {
        Db = db;
        Factory = new PaieExportContextFactory(db);
    }

    protected void VerifierExportAutorise(PeriodePaie? periode)
    {
        var cfg = ConfigurationExportsPaieService.Obtenir(Db);
        if (!cfg.Cloture.ExportsOfficielsExigentPeriodeCloturee) return;
        if (periode is { Cloturee: false })
            throw new InvalidOperationException(
                "La configuration exige une période clôturée avant cet export officiel. Clôturez la période dans « Périodes de paie ».");
    }

    protected static IEnumerable<IReadOnlyList<string>> ConstruireDonnees(
        PaieExportLot lot,
        ProfilExportConfig profil)
    {
        foreach (var ctx in lot.Lignes)
            yield return ExportTabulaireWriter.ConstruireLigne(ctx, profil);
    }
}
