using System.ComponentModel;
using System.Runtime.CompilerServices;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.ViewModels;

public class FinContratViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly int _employeId;
    private Contrat? _contratActif;

    public FinContratViewModel(PaieDbContext db, int employeId)
    {
        _db = db;
        _employeId = employeId;
    }

    public string NomEmploye { get; private set; } = string.Empty;

    public Contrat? ContratActif
    {
        get => _contratActif;
        private set { _contratActif = value; OnPropertyChanged(); OnPropertyChanged(nameof(SalaireDeBase)); OnPropertyChanged(nameof(PreavisMontant)); OnPropertyChanged(nameof(IndemniteLicenciementMontant)); }
    }

    public decimal SalaireDeBase => ContratActif?.SalaireBase ?? 0m;

    /// <summary>Montant théorique du préavis = SalaireBase * PreavisMoisBase.</summary>
    public decimal PreavisMontant => ContratActif is null ? 0m : decimal.Round(ContratActif.SalaireBase * ContratActif.PreavisMoisBase, 2);

    /// <summary>Montant théorique de l'indemnité de licenciement = SalaireBase * IndemniteLicenciementMoisBase.</summary>
    public decimal IndemniteLicenciementMontant => ContratActif is null ? 0m : decimal.Round(ContratActif.SalaireBase * ContratActif.IndemniteLicenciementMoisBase, 2);

    public void Charger()
    {
        var emp = _db.Employes.Find(_employeId);
        NomEmploye = emp != null ? $"{emp.Nom} {emp.Prenom}".Trim() : string.Empty;
        OnPropertyChanged(nameof(NomEmploye));

        // Dernier contrat par date début (considéré comme actif / de référence)
        ContratActif = _db.Contrats
            .Where(c => c.EmployeId == _employeId)
            .OrderByDescending(c => c.DateDebut)
            .FirstOrDefault();
    }

public event PropertyChangedEventHandler? PropertyChanged;
protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
