using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

public class SaisiePaieLigne : INotifyPropertyChanged
{
    public int EmployeId { get; set; }
    public string Matricule { get; set; } = "";
    public string NomComplet { get; set; } = "";

    private int _joursPrestes;
    private decimal _autresGainsImposables;
    private decimal _autresGainsNonImposables;
    private decimal _autresRetenues;
    private decimal _acomptesSalaire;
    private decimal _sanctionsDisciplinaires;
    private string? _commentaire;

    public int JoursPrestes { get => _joursPrestes; set { _joursPrestes = value; OnPropertyChanged(); } }
    public decimal AutresGainsImposables { get => _autresGainsImposables; set { _autresGainsImposables = value; OnPropertyChanged(); } }
    public decimal AutresGainsNonImposables { get => _autresGainsNonImposables; set { _autresGainsNonImposables = value; OnPropertyChanged(); } }
    public decimal AutresRetenues { get => _autresRetenues; set { _autresRetenues = value; OnPropertyChanged(); } }
    public decimal AcomptesSalaire { get => _acomptesSalaire; set { _acomptesSalaire = value; OnPropertyChanged(); } }
    public decimal SanctionsDisciplinaires { get => _sanctionsDisciplinaires; set { _sanctionsDisciplinaires = value; OnPropertyChanged(); } }
    public string? Commentaire { get => _commentaire; set { _commentaire = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class SaisiePaieMoisViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly int _periodePaieId;

    private bool _periodeVerrouillee;

    public SaisiePaieMoisViewModel(PaieDbContext db, int periodePaieId)
    {
        _db = db;
        _periodePaieId = periodePaieId;
        Lignes = new ObservableCollection<SaisiePaieLigne>();
        EnregistrerCommand = new RelayCommand(_ => Enregistrer(), _ => !PeriodeVerrouillee);
    }

    public bool PeriodeVerrouillee
    {
        get => _periodeVerrouillee;
        private set { _periodeVerrouillee = value; OnPropertyChanged(); (EnregistrerCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public ObservableCollection<SaisiePaieLigne> Lignes { get; }

    public ICommand EnregistrerCommand { get; }

    public Action? OnFermer { get; set; }
    public Action<string>? OnErreur { get; set; }

    public void Charger()
    {
        Lignes.Clear();
        var periode = _db.PeriodesPaie.FirstOrDefault(p => p.Id == _periodePaieId);
        var cfg = ConfigurationExportsPaieService.Obtenir(_db);
        PeriodeVerrouillee = cfg.Cloture.BloquerSaisiePaieSiCloturee && (periode?.Cloturee ?? false);
        var employes = ContexteEntrepriseService.EmployesEntrepriseCourante(_db)
            .AsNoTracking()
            .OrderBy(e => e.Nom)
            .ThenBy(e => e.Prenom)
            .ToList();
        var saisiesExistantes = _db.SaisiesPaie.Where(s => s.PeriodePaieId == _periodePaieId).ToList();

        foreach (var e in employes)
        {
            var saisie = saisiesExistantes.FirstOrDefault(s => s.EmployeId == e.Id);
            Lignes.Add(new SaisiePaieLigne
            {
                EmployeId = e.Id,
                Matricule = e.Matricule,
                NomComplet = $"{e.Nom} {e.Postnom} {e.Prenom}".Trim(),
                JoursPrestes = saisie?.JoursPrestes ?? 0,
                AutresGainsImposables = saisie?.AutresGainsImposables ?? 0,
                AutresGainsNonImposables = saisie?.AutresGainsNonImposables ?? 0,
                AutresRetenues = saisie?.AutresRetenues ?? 0,
                AcomptesSalaire = saisie?.AcomptesSalaire ?? 0,
                SanctionsDisciplinaires = saisie?.SanctionsDisciplinaires ?? 0,
                Commentaire = saisie?.Commentaire
            });
        }
    }

    private void Enregistrer()
    {
        if (PeriodeVerrouillee)
        {
            OnErreur?.Invoke("La période est clôturée : la saisie de paie est verrouillée.");
            return;
        }
        try
        {
            var existantes = _db.SaisiesPaie.Where(s => s.PeriodePaieId == _periodePaieId).ToList();
            foreach (var ligne in Lignes)
            {
                var s = existantes.FirstOrDefault(x => x.EmployeId == ligne.EmployeId);
                var isEmpty = ligne.JoursPrestes == 0
                              && ligne.AutresGainsImposables == 0
                              && ligne.AutresGainsNonImposables == 0
                              && ligne.AutresRetenues == 0
                              && ligne.AcomptesSalaire == 0
                              && ligne.SanctionsDisciplinaires == 0
                              && string.IsNullOrWhiteSpace(ligne.Commentaire);

                if (s == null)
                {
                    if (isEmpty) continue;
                    s = new SaisiePaie
                    {
                        EmployeId = ligne.EmployeId,
                        PeriodePaieId = _periodePaieId
                    };
                    _db.SaisiesPaie.Add(s);
                    existantes.Add(s);
                }

                if (isEmpty)
                {
                    _db.SaisiesPaie.Remove(s);
                    continue;
                }

                s.JoursPrestes = ligne.JoursPrestes;
                s.AutresGainsImposables = ligne.AutresGainsImposables;
                s.AutresGainsNonImposables = ligne.AutresGainsNonImposables;
                s.AutresRetenues = ligne.AutresRetenues;
                s.AcomptesSalaire = ligne.AcomptesSalaire;
                s.SanctionsDisciplinaires = ligne.SanctionsDisciplinaires;
                s.Commentaire = ligne.Commentaire;
            }

            _db.SaveChanges();
            OnFermer?.Invoke();
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

