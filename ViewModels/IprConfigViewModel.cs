using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

/// <summary>
/// ViewModel pour l'écran de configuration de l'IPR (barème + paramètres généraux).
/// </summary>
public class IprConfigViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private ParametreIPR _parametre = new();
    private GrilleIPR? _trancheSelectionnee;

    public IprConfigViewModel(PaieDbContext db)
    {
        _db = db;
        Tranches = new ObservableCollection<GrilleIPR>();
        EnregistrerCommand = new RelayCommand(_ => Enregistrer(), _ => DroitsUi.PeutModifier);
        AjouterTrancheCommand = new RelayCommand(_ => AjouterTranche(), _ => DroitsUi.PeutModifier);
        SupprimerTrancheCommand = new RelayCommand(_ => SupprimerTranche(), _ => DroitsUi.PeutModifier && TrancheSelectionnee != null);
    }

    public bool PeutModifier => DroitsUi.PeutModifier;

    public ObservableCollection<GrilleIPR> Tranches { get; }

    public ParametreIPR Parametre
    {
        get => _parametre;
        set { _parametre = value; OnPropertyChanged(); }
    }

    public GrilleIPR? TrancheSelectionnee
    {
        get => _trancheSelectionnee;
        set
        {
            _trancheSelectionnee = value;
            OnPropertyChanged();
        }
    }

    public ICommand EnregistrerCommand { get; }
    public ICommand AjouterTrancheCommand { get; }
    public ICommand SupprimerTrancheCommand { get; }

    public Action? OnEnregistrementReussi { get; set; }
    public Action<string>? OnErreur { get; set; }

    public void Charger()
    {
        Tranches.Clear();
        // SQLite ne supporte pas ORDER BY sur decimal : tri en mémoire
        var tranches = _db.GrillesIpr.ToList().OrderBy(t => t.BorneInf);
        foreach (var t in tranches)
            Tranches.Add(new GrilleIPR
            {
                Id = t.Id,
                BorneInf = t.BorneInf,
                BorneSup = t.BorneSup,
                Taux = t.Taux
            });

        Parametre = _db.ParametresIpr.FirstOrDefault() ?? new ParametreIPR();
    }

    private void AjouterTranche()
    {
        var nouvelle = new GrilleIPR
        {
            BorneInf = 0,
            BorneSup = 0,
            Taux = 0
        };
        Tranches.Add(nouvelle);
        TrancheSelectionnee = nouvelle;
    }

    private void SupprimerTranche()
    {
        if (TrancheSelectionnee is null)
            return;
        Tranches.Remove(TrancheSelectionnee);
        TrancheSelectionnee = null;
    }

    private void Enregistrer()
    {
        try
        {
            // Validation rapide : borne sup >= borne inf et taux >= 0
            foreach (var t in Tranches)
            {
                if (t.BorneSup > 0 && t.BorneSup < t.BorneInf)
                {
                    OnErreur?.Invoke("Une tranche a une borne supérieure inférieure à la borne inférieure.");
                    return;
                }
                if (t.Taux < 0)
                {
                    OnErreur?.Invoke("Le taux d'une tranche ne peut pas être négatif.");
                    return;
                }
            }

            // Sauvegarde des paramètres généraux
            var existingParam = _db.ParametresIpr.FirstOrDefault();
            if (existingParam is null)
            {
                _db.ParametresIpr.Add(Parametre);
            }
            else
            {
                existingParam.TauxEffectifMaximum = Parametre.TauxEffectifMaximum;
                existingParam.ReductionParEnfant = Parametre.ReductionParEnfant;
            }

            // Stratégie simple : on remplace complètement la grille
            var toutesTranches = _db.GrillesIpr.ToList();
            _db.GrillesIpr.RemoveRange(toutesTranches);

            foreach (var t in Tranches.OrderBy(t => t.BorneInf))
            {
                t.Id = 0; // laisser EF générer les identifiants
                _db.GrillesIpr.Add(t);
            }

            _db.SaveChanges();
            OnEnregistrementReussi?.Invoke();
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

