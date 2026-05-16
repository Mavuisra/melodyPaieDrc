using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

public class DefinitionChampsViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly int _entrepriseId;
    private DefinitionChampDynamique? _selection;

    public DefinitionChampsViewModel(PaieDbContext db)
    {
        _db = db;
        _entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(db);
        Definitions = new ObservableCollection<DefinitionChampDynamique>();
        EntiteCible = "Employe";
        TypeDonnee = DefinitionChampDynamique.TypeTexte;
        AjouterCommand = new RelayCommand(_ => Ajouter());
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => Selection != null);
        EnregistrerCommand = new RelayCommand(_ => Enregistrer(), _ => Selection != null);
        Charger();
    }

    public ObservableCollection<DefinitionChampDynamique> Definitions { get; }

    public DefinitionChampDynamique? Selection
    {
        get => _selection;
        set
        {
            _selection = value;
            OnPropertyChanged();
            if (value != null)
            {
                Code = value.Code;
                Libelle = value.Libelle;
                EntiteCible = value.EntiteCible;
                TypeDonnee = value.TypeDonnee;
                Obligatoire = value.Obligatoire;
                Ordre = value.Ordre;
            }
            (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (EnregistrerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public string EntiteCible { get; set; } = "Employe";
    public string TypeDonnee { get; set; } = DefinitionChampDynamique.TypeTexte;
    public bool Obligatoire { get; set; }
    public int Ordre { get; set; } = 10;

    public ICommand AjouterCommand { get; }
    public ICommand SupprimerCommand { get; }
    public ICommand EnregistrerCommand { get; }
    public Action<string>? OnErreur { get; set; }

    private void Charger()
    {
        Definitions.Clear();
        foreach (var d in _db.DefinitionsChampsDynamiques.AsNoTracking()
                     .Where(x => x.EntrepriseId == _entrepriseId)
                     .OrderBy(x => x.Ordre))
            Definitions.Add(d);
    }

    private void Ajouter()
    {
        if (string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Libelle))
        {
            OnErreur?.Invoke("Code et libellé obligatoires.");
            return;
        }

        _db.DefinitionsChampsDynamiques.Add(new DefinitionChampDynamique
        {
            EntrepriseId = _entrepriseId,
            Code = Code.Trim(),
            Libelle = Libelle.Trim(),
            EntiteCible = EntiteCible.Trim(),
            TypeDonnee = TypeDonnee,
            Obligatoire = Obligatoire,
            Ordre = Ordre
        });
        _db.SaveChanges();
        Charger();
    }

    private void Enregistrer()
    {
        if (Selection == null) return;
        var e = _db.DefinitionsChampsDynamiques.First(x => x.Id == Selection.Id);
        e.Code = Code.Trim();
        e.Libelle = Libelle.Trim();
        e.EntiteCible = EntiteCible.Trim();
        e.TypeDonnee = TypeDonnee;
        e.Obligatoire = Obligatoire;
        e.Ordre = Ordre;
        _db.SaveChanges();
        Charger();
    }

    private void Supprimer()
    {
        if (Selection == null) return;
        var e = _db.DefinitionsChampsDynamiques.First(x => x.Id == Selection.Id);
        _db.DefinitionsChampsDynamiques.Remove(e);
        _db.SaveChanges();
        Charger();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
