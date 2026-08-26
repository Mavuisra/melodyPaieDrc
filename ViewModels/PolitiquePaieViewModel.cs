using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

public class PolitiquePaieViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly int _entrepriseId;
    private PolitiquePaie? _politique;
    private string _libellePolitique = "";
    private string _joursReferencePaie = "26";
    private string _heuresParJour = "8";
    private bool _salaireContratEnNet;
    private bool _utiliserBaremeIpr = true;
    private bool _utiliserTauxSociauxDb = true;
    private string _modeCalculPresence = ParametrePolitiquePaie.ModePresencePointages;
    private bool _periodeDecalee;
    private string _jourDebutPeriode = "26";
    private string _jourFinPeriode = "25";
    private bool _forcerSamediOuvre;
    private bool _completerJoursSansSaisie;
    private bool _retardSanctionActive;
    private string _retardSeuilMinutes = "1";
    private string _retardModeSanction = ParametrePolitiquePaie.RetardModeAucun;

    public PolitiquePaieViewModel(PaieDbContext db)
    {
        _db = db;
        _entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(db);
        Rubriques = new ObservableCollection<RubriqueBulletin>();
        EnregistrerCommand = new RelayCommand(_ => Enregistrer(), _ => DroitsUi.PeutModifier);
        Charger();
    }

    public bool PeutModifier => DroitsUi.PeutModifier;

    public ObservableCollection<RubriqueBulletin> Rubriques { get; }

    public string LibellePolitique
    {
        get => _libellePolitique;
        set { _libellePolitique = value ?? ""; OnPropertyChanged(); }
    }

    public string JoursReferencePaie
    {
        get => _joursReferencePaie;
        set { _joursReferencePaie = value ?? ""; OnPropertyChanged(); }
    }

    public string HeuresParJour
    {
        get => _heuresParJour;
        set { _heuresParJour = value ?? ""; OnPropertyChanged(); }
    }

    public bool SalaireContratEnNet
    {
        get => _salaireContratEnNet;
        set { _salaireContratEnNet = value; OnPropertyChanged(); }
    }

    public bool UtiliserBaremeIpr
    {
        get => _utiliserBaremeIpr;
        set { _utiliserBaremeIpr = value; OnPropertyChanged(); }
    }

    public bool UtiliserTauxSociauxDb
    {
        get => _utiliserTauxSociauxDb;
        set { _utiliserTauxSociauxDb = value; OnPropertyChanged(); }
    }

    public string ModeCalculPresence
    {
        get => _modeCalculPresence;
        set { _modeCalculPresence = value ?? ParametrePolitiquePaie.ModePresencePointages; OnPropertyChanged(); }
    }

    public bool PeriodeDecalee
    {
        get => _periodeDecalee;
        set { _periodeDecalee = value; OnPropertyChanged(); }
    }

    public string JourDebutPeriode
    {
        get => _jourDebutPeriode;
        set { _jourDebutPeriode = value ?? ""; OnPropertyChanged(); }
    }

    public string JourFinPeriode
    {
        get => _jourFinPeriode;
        set { _jourFinPeriode = value ?? ""; OnPropertyChanged(); }
    }

    public bool ForcerSamediOuvre
    {
        get => _forcerSamediOuvre;
        set { _forcerSamediOuvre = value; OnPropertyChanged(); }
    }

    public bool CompleterJoursSansSaisie
    {
        get => _completerJoursSansSaisie;
        set { _completerJoursSansSaisie = value; OnPropertyChanged(); }
    }

    public bool RetardSanctionActive
    {
        get => _retardSanctionActive;
        set { _retardSanctionActive = value; OnPropertyChanged(); }
    }

    public string RetardSeuilMinutes
    {
        get => _retardSeuilMinutes;
        set { _retardSeuilMinutes = value ?? ""; OnPropertyChanged(); }
    }

    public string RetardModeSanction
    {
        get => _retardModeSanction;
        set { _retardModeSanction = value ?? ParametrePolitiquePaie.RetardModeAucun; OnPropertyChanged(); }
    }

    public ICommand EnregistrerCommand { get; }
    public Action<string>? OnSucces { get; set; }
    public Action<string>? OnErreur { get; set; }

    private void Charger()
    {
        var ctx = new PolitiquePaieService(_db).Charger(_entrepriseId);
        _politique = ctx.Politique;

        LibellePolitique = _politique.Libelle ?? "";
        JoursReferencePaie = ctx.JoursReferencePaie.ToString("0.##");
        HeuresParJour = ctx.HeuresParJour.ToString("0.##");
        SalaireContratEnNet = ctx.SalaireContratEnNet;
        UtiliserBaremeIpr = ctx.UtiliserBaremeIpr;
        UtiliserTauxSociauxDb = ctx.UtiliserTauxSociauxDb;
        ModeCalculPresence = ctx.ModeCalculPresence;
        PeriodeDecalee = ctx.PeriodeDecalee;
        JourDebutPeriode = ctx.JourDebutPeriodeDecalee.ToString("0");
        JourFinPeriode = ctx.JourFinPeriodeDecalee.ToString("0");
        ForcerSamediOuvre = ctx.ForcerSamediOuvre;
        CompleterJoursSansSaisie = ctx.CompleterJoursSansSaisie;
        RetardSanctionActive = ctx.RetardSanctionActive;
        RetardSeuilMinutes = ctx.RetardSeuilMinutes.ToString();
        RetardModeSanction = ctx.RetardModeSanction;

        Rubriques.Clear();
        foreach (var r in _politique.Rubriques.OrderBy(x => x.OrdreAffichage))
            Rubriques.Add(r);
    }

    private void Enregistrer()
    {
        if (_politique == null) return;
        try
        {
            var entite = _db.PolitiquesPaie
                .Include(p => p.Parametres)
                .First(p => p.Id == _politique.Id);

            entite.Libelle = LibellePolitique.Trim();
            entite.UpdatedAtUtc = DateTime.UtcNow;

            DefinirParam(entite, ParametrePolitiquePaie.Cles.JoursReferencePaie, JoursReferencePaie);
            DefinirParam(entite, ParametrePolitiquePaie.Cles.HeuresParJour, HeuresParJour);
            DefinirParam(entite, ParametrePolitiquePaie.Cles.SalaireContratEnNet, SalaireContratEnNet ? "true" : "false");
            DefinirParam(entite, ParametrePolitiquePaie.Cles.UtiliserBaremeIpr, UtiliserBaremeIpr ? "true" : "false");
            DefinirParam(entite, ParametrePolitiquePaie.Cles.UtiliserTauxSociauxDb, UtiliserTauxSociauxDb ? "true" : "false");
            DefinirParam(entite, ParametrePolitiquePaie.Cles.ModeCalculPresence, ModeCalculPresence);
            DefinirParam(entite, ParametrePolitiquePaie.Cles.TypePeriodePaie,
                PeriodeDecalee ? ParametrePolitiquePaie.TypePeriodeDecalee : ParametrePolitiquePaie.TypePeriodeCalendaire);
            DefinirParam(entite, ParametrePolitiquePaie.Cles.JourDebutPeriodeDecalee, JourDebutPeriode);
            DefinirParam(entite, ParametrePolitiquePaie.Cles.JourFinPeriodeDecalee, JourFinPeriode);
            DefinirParam(entite, ParametrePolitiquePaie.Cles.ForcerSamediOuvre, ForcerSamediOuvre ? "true" : "false");
            DefinirParam(entite, ParametrePolitiquePaie.Cles.CompleterJoursSansSaisie, CompleterJoursSansSaisie ? "true" : "false");
            DefinirParam(entite, ParametrePolitiquePaie.Cles.RetardSanctionActive, RetardSanctionActive ? "true" : "false");
            DefinirParam(entite, ParametrePolitiquePaie.Cles.RetardSeuilMinutes, RetardSeuilMinutes);
            DefinirParam(entite, ParametrePolitiquePaie.Cles.RetardModeSanction,
                RetardSanctionActive ? RetardModeSanction : ParametrePolitiquePaie.RetardModeAucun);

            foreach (var r in Rubriques)
            {
                var rub = _db.RubriquesBulletin.First(x => x.Id == r.Id);
                rub.Libelle = r.Libelle;
                rub.OrdreAffichage = r.OrdreAffichage;
                rub.AfficherSurBulletin = r.AfficherSurBulletin;
            }

            _db.SaveChanges();
            UiFeedback.Succes("Politique de paie enregistrée. Les prochains calculs utiliseront ces règles.");
            OnSucces?.Invoke("Politique de paie enregistrée. Les prochains calculs utiliseront ces règles.");
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    private static void DefinirParam(PolitiquePaie politique, string cle, string valeur)
    {
        var p = politique.Parametres.FirstOrDefault(x => string.Equals(x.Cle, cle, StringComparison.OrdinalIgnoreCase));
        if (p == null)
        {
            politique.Parametres.Add(new ParametrePolitiquePaie { Cle = cle, Valeur = valeur });
            return;
        }
        p.Valeur = valeur;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
