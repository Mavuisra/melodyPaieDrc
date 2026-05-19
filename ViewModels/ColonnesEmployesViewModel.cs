using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

public sealed class ColonneEmployeItem : INotifyPropertyChanged
{
    public string Code { get; init; } = "";
    public string Libelle { get; init; } = "";
    private bool _visible = true;
    public bool Visible
    {
        get => _visible;
        set { _visible = value; OnPropertyChanged(); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class ColonnesEmployesViewModel
{
    private readonly PaieDbContext _db;
    private ConfigurationPlateforme _config = new();

    public ColonnesEmployesViewModel(PaieDbContext db)
    {
        _db = db;
        Colonnes = new ObservableCollection<ColonneEmployeItem>();
        EnregistrerCommand = new RelayCommand(_ => Enregistrer(), _ => DroitsUi.PeutModifier);
        Charger();
    }

    public ObservableCollection<ColonneEmployeItem> Colonnes { get; }
    public ICommand EnregistrerCommand { get; }
    public bool PeutModifier => DroitsUi.PeutModifier;
    public Action? OnEnregistre { get; set; }

    private static readonly (string Code, string Libelle)[] Definitions =
    {
        ("Matricule", "Matricule"),
        ("Nom", "Nom"),
        ("Postnom", "Postnom"),
        ("Prenom", "Prénom"),
        ("Sexe", "Sexe"),
        ("Telephone", "Téléphone"),
        ("Departement", "Département"),
        ("SalaireMensuelUsd", "Salaire base (USD)"),
        ("SalaireMensuelCdf", "Salaire base (FC)"),
        ("SalaireJourUsd", "Salaire / jour (USD)"),
        ("SalaireJourCdf", "Salaire / jour (FC)"),
        ("SalaireHeureUsd", "Salaire / h (USD)"),
        ("SalaireHeureCdf", "Salaire / h (FC)")
    };

    private void Charger()
    {
        _config = ConfigurationPlateformeService.Charger(_db);
        Colonnes.Clear();
        foreach (var (code, libelle) in Definitions)
        {
            var visible = !_config.ColonnesListeEmployes.TryGetValue(code, out var v) || v;
            Colonnes.Add(new ColonneEmployeItem { Code = code, Libelle = libelle, Visible = visible });
        }
    }

    private void Enregistrer()
    {
        foreach (var c in Colonnes)
            _config.ColonnesListeEmployes[c.Code] = c.Visible;
        ConfigurationPlateformeService.Enregistrer(_db, _config);
        OnEnregistre?.Invoke();
    }
}
