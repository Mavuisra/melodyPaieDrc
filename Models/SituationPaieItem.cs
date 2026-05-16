using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Situation de la paie (mois ou cumulée) pour le tableau de bord.
/// </summary>
public class SituationPaieItem : INotifyPropertyChanged
{
    private string _libelle = "";
    private decimal _totalSalairesNets;
    private decimal _totalIndemnitesTransport;
    private decimal _totalIndemnitesLogement;
    private decimal _totalIprRetenus;
    private decimal _totalCnssRetenues;
    private decimal _totalAllocationsFamiliales;

    public string Libelle { get => _libelle; set { _libelle = value ?? ""; OnPropertyChanged(); } }
    public decimal TotalSalairesNets { get => _totalSalairesNets; set { _totalSalairesNets = value; OnPropertyChanged(); } }
    public decimal TotalIndemnitesTransport { get => _totalIndemnitesTransport; set { _totalIndemnitesTransport = value; OnPropertyChanged(); } }
    public decimal TotalIndemnitesLogement { get => _totalIndemnitesLogement; set { _totalIndemnitesLogement = value; OnPropertyChanged(); } }
    public decimal TotalIprRetenus { get => _totalIprRetenus; set { _totalIprRetenus = value; OnPropertyChanged(); } }
    public decimal TotalCnssRetenues { get => _totalCnssRetenues; set { _totalCnssRetenues = value; OnPropertyChanged(); } }
    public decimal TotalAllocationsFamiliales { get => _totalAllocationsFamiliales; set { _totalAllocationsFamiliales = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
