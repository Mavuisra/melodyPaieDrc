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
        get => _politique?.Libelle ?? "";
        set { if (_politique != null) { _politique.Libelle = value ?? ""; OnPropertyChanged(); } }
    }

    public string JoursReferencePaie { get; set; } = "26";
    public string HeuresParJour { get; set; } = "8";
    public bool SalaireContratEnNet { get; set; }
    public bool UtiliserBaremeIpr { get; set; } = true;
    public bool UtiliserTauxSociauxDb { get; set; } = true;
    public string ModeCalculPresence { get; set; } = ParametrePolitiquePaie.ModePresencePointages;

    public ICommand EnregistrerCommand { get; }
    public Action<string>? OnSucces { get; set; }
    public Action<string>? OnErreur { get; set; }

    private void Charger()
    {
        var ctx = new PolitiquePaieService(_db).Charger(_entrepriseId);
        _politique = ctx.Politique;
        OnPropertyChanged(nameof(LibellePolitique));

        JoursReferencePaie = ctx.JoursReferencePaie.ToString("0.##");
        HeuresParJour = ctx.HeuresParJour.ToString("0.##");
        SalaireContratEnNet = ctx.SalaireContratEnNet;
        UtiliserBaremeIpr = ctx.UtiliserBaremeIpr;
        UtiliserTauxSociauxDb = ctx.UtiliserTauxSociauxDb;
        ModeCalculPresence = ctx.ModeCalculPresence;
        OnPropertyChanged(nameof(JoursReferencePaie));
        OnPropertyChanged(nameof(HeuresParJour));
        OnPropertyChanged(nameof(SalaireContratEnNet));
        OnPropertyChanged(nameof(UtiliserBaremeIpr));
        OnPropertyChanged(nameof(UtiliserTauxSociauxDb));
        OnPropertyChanged(nameof(ModeCalculPresence));

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

            foreach (var r in Rubriques)
            {
                var rub = _db.RubriquesBulletin.First(x => x.Id == r.Id);
                rub.Libelle = r.Libelle;
                rub.OrdreAffichage = r.OrdreAffichage;
                rub.AfficherSurBulletin = r.AfficherSurBulletin;
            }

            _db.SaveChanges();
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
