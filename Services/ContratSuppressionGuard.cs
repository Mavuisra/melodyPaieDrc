using MelodyPaieRDC.Data;

namespace MelodyPaieRDC.Services;

public sealed class ContratSuppressionDiagnostic
{
    public bool PeutSupprimer { get; init; }
    public bool DemanderConfirmationPrimes { get; init; }
    public string Message { get; init; } = "";
}

public static class ContratSuppressionGuard
{
    public static ContratSuppressionDiagnostic Analyser(PaieDbContext db, int employeId)
    {
        if (db.BulletinsPaie.Any(b => b.EmployeId == employeId))
        {
            return new ContratSuppressionDiagnostic
            {
                PeutSupprimer = false,
                Message = "Impossible de supprimer le contrat : des bulletins de paie existent déjà pour cet employé."
            };
        }

        var nbPrets = db.PretsAvances.Count(p => p.EmployeId == employeId && p.SoldeRestant > 0);
        if (nbPrets > 0)
        {
            return new ContratSuppressionDiagnostic
            {
                PeutSupprimer = false,
                Message = $"Impossible de supprimer le contrat : {nbPrets} prêt(s) / avance(s) encore en cours. Soldez ou supprimez-les d’abord pour éviter des données orphelines."
            };
        }

        var nbPrimes = db.AffectationsPrimesIndemnites.Count(a => a.EmployeId == employeId);
        if (nbPrimes > 0)
        {
            return new ContratSuppressionDiagnostic
            {
                PeutSupprimer = true,
                DemanderConfirmationPrimes = true,
                Message = $"{nbPrimes} prime(s) / indemnité(s) resteront attachées à l’employé (pas au contrat). Continuer la suppression du contrat ?"
            };
        }

        return new ContratSuppressionDiagnostic { PeutSupprimer = true };
    }
}
