using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services.Export;

public sealed class ExportDonneesPaieContext
{
    public required BulletinPaie Bulletin { get; init; }
    public Employe? Employe => Bulletin.Employe;
    public PeriodePaie? Periode => Bulletin.PeriodePaie;
    public Contrat? Contrat { get; init; }
    public SaisiePaie? Saisie { get; init; }
    public Entreprise? Entreprise { get; init; }
    public CotisationsCalculees? Cotisations { get; init; }
    public int NumeroOrdre { get; init; }
    public int NbEnfants { get; init; }
    public string? ReferenceVirement { get; init; }
    /// <summary>Heures prestées sur la période (pointage journalier).</summary>
    public decimal HeuresTravailPeriode { get; init; }
    /// <summary>Commune / territoire pour l'export CNSS.</summary>
    public string? CommuneAffectation { get; init; }
}

public sealed class CotisationsCalculees
{
    public decimal BaseCnss { get; set; }
    public decimal CnssOuvrier { get; set; }
    public decimal CnssPatronal { get; set; }
    public decimal Inpp { get; set; }
    public decimal Onem { get; set; }
}
