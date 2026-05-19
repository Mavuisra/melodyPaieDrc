using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

public sealed class ControleClotureLigne
{
    public string Code { get; init; } = "";
    public string Libelle { get; init; } = "";
    public string Message { get; init; } = "";
    public string Severite { get; init; } = "";
    public bool EstErreur { get; init; }
}

public class CloturePeriodeViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly int _periodeId;
    private string _libellePeriode = "";
    private bool _dejaCloturee;
    private bool _peutCloturer;
    private string _resume = "";

    public CloturePeriodeViewModel(PaieDbContext db, int periodeId)
    {
        _db = db;
        _periodeId = periodeId;
        Controles = new ObservableCollection<ControleClotureLigne>();
        AnalyserCommand = new RelayCommand(_ => Analyser());
        CloturerCommand = new RelayCommand(_ => Cloturer(false), _ => DroitsUi.PeutModifier && PeutCloturer && !DejaCloturee);
        CloturerForcerCommand = new RelayCommand(_ => Cloturer(true), _ => DroitsUi.PeutModifier && !DejaCloturee);
        RouvrirCommand = new RelayCommand(_ => Rouvrir(), _ => DroitsUi.PeutModifier && DejaCloturee);
        ChargerPeriode();
        Analyser();
    }

    public ObservableCollection<ControleClotureLigne> Controles { get; }

    public string LibellePeriode
    {
        get => _libellePeriode;
        private set { _libellePeriode = value; OnPropertyChanged(); }
    }

    public bool DejaCloturee
    {
        get => _dejaCloturee;
        private set { _dejaCloturee = value; OnPropertyChanged(); (CloturerCommand as RelayCommand)?.RaiseCanExecuteChanged(); (RouvrirCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public bool PeutCloturer
    {
        get => _peutCloturer;
        private set { _peutCloturer = value; OnPropertyChanged(); (CloturerCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public string Resume
    {
        get => _resume;
        private set { _resume = value; OnPropertyChanged(); }
    }

    public ICommand AnalyserCommand { get; }
    public ICommand CloturerCommand { get; }
    public ICommand CloturerForcerCommand { get; }
    public ICommand RouvrirCommand { get; }

    public bool PeutModifier => DroitsUi.PeutModifier;

    public Action? OnClotureEffectuee { get; set; }
    public Action<string>? OnErreur { get; set; }
    public Action<string>? OnSucces { get; set; }

    private void ChargerPeriode()
    {
        var p = _db.PeriodesPaie.Find(_periodeId);
        if (p is null) return;
        LibellePeriode = $"Période {p.Mois:D2}/{p.Annee}";
        DejaCloturee = p.Cloturee;
        if (p.Cloturee && p.DateClotureUtc is { } dt)
            Resume = $"Clôturée le {DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime():g} par {p.CloturePar ?? "—"}";
    }

    private void Analyser()
    {
        try
        {
            var svc = new PeriodeClotureService(_db);
            var rapport = svc.Analyser(_periodeId);
            Controles.Clear();
            foreach (var c in rapport.Controles)
            {
                Controles.Add(new ControleClotureLigne
                {
                    Code = c.Code,
                    Libelle = c.Libelle,
                    Message = c.Message,
                    Severite = c.Severite,
                    EstErreur = c.EstErreur
                });
            }
            PeutCloturer = rapport.PeutCloturerSansForcer;
            DejaCloturee = rapport.PeriodeDejaCloturee;
            Resume = rapport.Controles.Count == 0
                ? "Aucun problème détecté. Vous pouvez clôturer la période."
                : $"{rapport.Controles.Count(c => c.EstErreur)} erreur(s), {rapport.Controles.Count(c => !c.EstErreur)} avertissement(s).";
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    private void Cloturer(bool forcer)
    {
        try
        {
            var svc = new PeriodeClotureService(_db);
            svc.Cloturer(_periodeId, Environment.UserName, forcer);
            OnSucces?.Invoke("Période clôturée avec succès.");
            ChargerPeriode();
            Analyser();
            OnClotureEffectuee?.Invoke();
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    private void Rouvrir()
    {
        try
        {
            new PeriodeClotureService(_db).Rouvrir(_periodeId);
            OnSucces?.Invoke("Période rouverte.");
            ChargerPeriode();
            Analyser();
            OnClotureEffectuee?.Invoke();
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
