using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Une période affichée sur le tableau de bord (évolution masse salariale / IPR).
/// </summary>
public class DashboardMoisItem : INotifyPropertyChanged
{
    private string _libelle = "";
    private decimal _masseSalariale;
    private decimal _iprTotal;
    private double _barHeightMasse = 50;
    private double _barHeightIpr = 50;

    public string Libelle { get => _libelle; set { _libelle = value ?? ""; OnPropertyChanged(); } }
    public decimal MasseSalariale { get => _masseSalariale; set { _masseSalariale = value; OnPropertyChanged(); } }
    public decimal IprTotal { get => _iprTotal; set { _iprTotal = value; OnPropertyChanged(); } }
    /// <summary>Hauteur de la barre masse salariale (0-100 px) pour le graphique.</summary>
    public double BarHeightMasse { get => _barHeightMasse; set { _barHeightMasse = value; OnPropertyChanged(); } }
    /// <summary>Hauteur de la barre IPR (0-100 px) pour le graphique.</summary>
    public double BarHeightIpr { get => _barHeightIpr; set { _barHeightIpr = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
