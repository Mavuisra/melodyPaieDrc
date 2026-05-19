using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

/// <summary>Détail jour + édition des moments de pointage (entrée / pause / sortie).</summary>
public sealed class DetailJourEmployeVm : INotifyPropertyChanged
{
    private static readonly CultureInfo Fr = new("fr-FR");
    private string _typeJourSelectionne = SuiviJournalier.TypeNormal;

    private decimal _heuresPrestees;
    private string _entree = "";
    private string _debutPause = "";
    private string _finPause = "";
    private string _sortie = "";
    private string _messageStatut = "";

    /// <summary>Pointages au-delà des 4 premiers (lun–ven) ou entre entrée/sortie (samedi), conservés à l’enregistrement.</summary>
    private readonly List<DateTime> _extrasOriginaux;

    private DetailJourEmployeVm(
        DateTime jour,
        string titreDate,
        string sousTitreJour,
        string typeJour,
        string modeCalcul,
        decimal heuresPrestees,
        bool estLayoutSemaine,
        bool estLayoutSamedi,
        bool estLayoutEntreeSortie,
        bool afficherChampsPause,
        bool estLayoutTroisPointages,
        bool peutEditerMoments,
        IReadOnlyList<DateTime> extrasOriginaux)
    {
        Jour = jour.Date;
        TitreDate = titreDate;
        SousTitreJour = sousTitreJour;
        TypeJour = typeJour;
        ModeCalcul = modeCalcul;
        _heuresPrestees = heuresPrestees;
        EstLayoutSemaine = estLayoutSemaine;
        EstLayoutSamedi = estLayoutSamedi;
        EstLayoutEntreeSortie = estLayoutEntreeSortie;
        AfficherChampsPause = afficherChampsPause;
        EstLayoutTroisPointages = estLayoutTroisPointages;
        AfficherGrilleJourOuvrable = estLayoutSemaine || estLayoutEntreeSortie;
        AfficherFinPause = estLayoutSemaine && !estLayoutTroisPointages;
        PeutEditerMoments = peutEditerMoments;
        _extrasOriginaux = extrasOriginaux.Count > 0 ? new List<DateTime>(extrasOriginaux) : new List<DateTime>();
        PointagesSupplementaires = new ObservableCollection<string>();
        foreach (var x in _extrasOriginaux)
            PointagesSupplementaires.Add(PointagesMomentsHelper.FormaterHhMm(x));
    }

    public DateTime Jour { get; }

    public string TitreDate { get; }
    public string SousTitreJour { get; }
    public string TypeJour { get; }
    public ObservableCollection<string> TypesJourDisponibles { get; } = new(new[]
    {
        SuiviJournalier.TypeNormal,
        SuiviJournalier.TypeAbsence,
        SuiviJournalier.TypeCongeCirconstance,
        SuiviJournalier.TypeMaladie
    });

    public string TypeJourSelectionne
    {
        get => _typeJourSelectionne;
        set
        {
            var nouveau = value ?? SuiviJournalier.TypeNormal;
            if (_typeJourSelectionne == nouveau) return;
            _typeJourSelectionne = nouveau;
            OnPropertyChanged();
        }
    }
    public string ModeCalcul { get; }

    public decimal HeuresPrestees
    {
        get => _heuresPrestees;
        private set
        {
            if (_heuresPrestees == value) return;
            _heuresPrestees = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HeuresLibelle));
        }
    }

    public string HeuresLibelle =>
        HeuresPrestees.ToString("N2", CultureInfo.CurrentCulture) + " h";

    public bool EstLayoutSemaine { get; }
    public bool EstLayoutSamedi { get; }

    /// <summary>Lun–ven en mode 2 pointages : entrée et sortie seulement.</summary>
    public bool EstLayoutEntreeSortie { get; }

    /// <summary>Affiche début/fin pause (mode 4) ou pause unique (mode 3).</summary>
    public bool AfficherChampsPause { get; }

    /// <summary>Mode 3 : une seule case « pause » (début pause).</summary>
    public bool EstLayoutTroisPointages { get; }

    /// <summary>Affiche la grille d’édition lun–ven (2, 3 ou 4 pointages).</summary>
    public bool AfficherGrilleJourOuvrable { get; }

    /// <summary>Affiche le champ « fin de pause » (mode 4 uniquement).</summary>
    public bool AfficherFinPause { get; }

    /// <summary>Édition réservée aux jours normaux non « heures manuelles ».</summary>
    public bool PeutEditerMoments { get; }

    public string EntreeHhMm
    {
        get => _entree;
        set { _entree = value ?? ""; OnPropertyChanged(); }
    }

    public string DebutPauseHhMm
    {
        get => _debutPause;
        set { _debutPause = value ?? ""; OnPropertyChanged(); }
    }

    public string FinPauseHhMm
    {
        get => _finPause;
        set { _finPause = value ?? ""; OnPropertyChanged(); }
    }

    public string SortieHhMm
    {
        get => _sortie;
        set { _sortie = value ?? ""; OnPropertyChanged(); }
    }

    public ObservableCollection<string> PointagesSupplementaires { get; }

    public bool AfficherPointagesSupplementaires => PointagesSupplementaires.Count > 0;

    public string MessageStatut
    {
        get => _messageStatut;
        private set
        {
            _messageStatut = value ?? "";
            OnPropertyChanged();
        }
    }

    public void DefinirMessageStatut(string message) => MessageStatut = message ?? "";

    public static DetailJourEmployeVm Creer(
        DateTime jour,
        SuiviJournalierPdfLigne lignePdf,
        SuiviJournalier? suivi,
        IReadOnlyList<DateTime> pointagesTriés,
        LtServicesRegles? regles = null)
    {
        var titre = jour.ToString("dddd d MMMM yyyy", Fr);
        var sous = $"{lignePdf.JourSemaine} · {lignePdf.TypeJour}";

        var typeOk = lignePdf.TypeJour == SuiviJournalier.TypeNormal;
        var manuel = suivi?.HeuresManuelles == true;

        var r = regles ?? LtServicesRegles.Defaut;
        var decoupe = PointagesMomentsHelper.Decouper(pointagesTriés, jour, r);
        var dow = jour.DayOfWeek;
        var estSemaine = dow is >= DayOfWeek.Monday and <= DayOfWeek.Friday;
        var estSamedi = dow == DayOfWeek.Saturday;
        var layoutDeux = estSemaine && r.UtiliseDeuxPointages;
        var layoutTrois = estSemaine && r.UtiliseTroisPointages;
        var layoutQuatre = estSemaine && r.UtiliseQuatrePointages;
        var peutEditerMoments = DroitsUi.PeutModifier && typeOk && !manuel && (estSemaine || estSamedi);

        var vm = new DetailJourEmployeVm(
            jour,
            titre,
            sous,
            lignePdf.TypeJour,
            lignePdf.ModeCalcul,
            lignePdf.HeuresPrestees,
            layoutQuatre || layoutTrois,
            estSamedi,
            layoutDeux,
            layoutTrois || layoutQuatre,
            layoutTrois,
            peutEditerMoments,
            decoupe.PointagesSupplementaires);
        vm.TypeJourSelectionne = lignePdf.TypeJour;

        if (layoutQuatre)
        {
            vm.EntreeHhMm = PointagesMomentsHelper.FormaterHhMm(decoupe.Entree);
            vm.DebutPauseHhMm = PointagesMomentsHelper.FormaterHhMm(decoupe.DebutPause);
            vm.FinPauseHhMm = PointagesMomentsHelper.FormaterHhMm(decoupe.FinPause);
            vm.SortieHhMm = PointagesMomentsHelper.FormaterHhMm(decoupe.Sortie);
        }
        else if (layoutTrois)
        {
            vm.EntreeHhMm = PointagesMomentsHelper.FormaterHhMm(decoupe.Entree);
            vm.DebutPauseHhMm = PointagesMomentsHelper.FormaterHhMm(decoupe.DebutPause);
            vm.SortieHhMm = PointagesMomentsHelper.FormaterHhMm(decoupe.Sortie);
        }
        else if (layoutDeux || estSamedi)
        {
            vm.EntreeHhMm = PointagesMomentsHelper.FormaterHhMm(decoupe.Entree);
            vm.SortieHhMm = PointagesMomentsHelper.FormaterHhMm(decoupe.Sortie);
        }

        return vm;
    }

    /// <summary>Construit la liste à persister (ordre chronologique final).</summary>
    public bool TryConstruireListePourEnregistrement(out List<DateTime> liste, out string erreur)
    {
        liste = new List<DateTime>();
        erreur = "";

        if (EstLayoutEntreeSortie && !EstLayoutSamedi)
        {
            return ConstruireEntreeSortie(out liste, out erreur);
        }

        if (EstLayoutTroisPointages)
        {
            return ConstruireTroisPointages(out liste, out erreur);
        }

        if (EstLayoutSemaine)
        {
            foreach (var txt in new[] { EntreeHhMm, DebutPauseHhMm, FinPauseHhMm, SortieHhMm })
            {
                if (string.IsNullOrWhiteSpace(txt))
                    continue;
                if (!PointagesMomentsHelper.TryParseHeureDuJour(txt, Jour, out var dt))
                {
                    erreur = $"Heure invalide : « {txt.Trim()} ». Utilisez le format HH:mm (ex. 14:25).";
                    return false;
                }

                liste.Add(dt);
            }

            var chaine = liste.ToList();
            for (var i = 1; i < chaine.Count; i++)
            {
                if (chaine[i] <= chaine[i - 1])
                {
                    erreur = "Les horaires des moments doivent être strictement croissants (entrée → pause début → fin pause → sortie).";
                    return false;
                }
            }

            liste.AddRange(_extrasOriginaux);
            liste = liste.OrderBy(x => x).ToList();
            return true;
        }

        if (EstLayoutSamedi)
            return ConstruireEntreeSortie(out liste, out erreur);

        erreur = "Ce jour ne prend pas en charge l’édition des moments.";
        return false;
    }

    private bool ConstruireEntreeSortie(out List<DateTime> liste, out string erreur)
    {
        liste = new List<DateTime>();
        erreur = "";
        DateTime? e = null;
        DateTime? s = null;
        if (!string.IsNullOrWhiteSpace(EntreeHhMm))
        {
            if (!PointagesMomentsHelper.TryParseHeureDuJour(EntreeHhMm, Jour, out var dt))
            {
                erreur = $"Entrée invalide : « {EntreeHhMm.Trim()} ».";
                return false;
            }

            e = dt;
        }

        if (!string.IsNullOrWhiteSpace(SortieHhMm))
        {
            if (!PointagesMomentsHelper.TryParseHeureDuJour(SortieHhMm, Jour, out var dt))
            {
                erreur = $"Sortie invalide : « {SortieHhMm.Trim()} ».";
                return false;
            }

            s = dt;
        }

        if (e.HasValue)
            liste.Add(e.Value);
        liste.AddRange(_extrasOriginaux);
        if (s.HasValue)
            liste.Add(s.Value);
        liste = liste.OrderBy(x => x).ToList();

        if (liste.Count >= 2 && liste[0] >= liste[^1])
        {
            erreur = "L’entrée doit être avant la sortie.";
            return false;
        }

        return true;
    }

    private bool ConstruireTroisPointages(out List<DateTime> liste, out string erreur)
    {
        liste = new List<DateTime>();
        erreur = "";
        foreach (var txt in new[] { EntreeHhMm, DebutPauseHhMm, SortieHhMm })
        {
            if (string.IsNullOrWhiteSpace(txt))
                continue;
            if (!PointagesMomentsHelper.TryParseHeureDuJour(txt, Jour, out var dt))
            {
                erreur = $"Heure invalide : « {txt.Trim()} ». Utilisez le format HH:mm (ex. 14:25).";
                return false;
            }

            liste.Add(dt);
        }

        var chaine = liste.ToList();
        for (var i = 1; i < chaine.Count; i++)
        {
            if (chaine[i] <= chaine[i - 1])
            {
                erreur = "Les horaires doivent être strictement croissants (entrée → pause → sortie).";
                return false;
            }
        }

        liste.AddRange(_extrasOriginaux);
        liste = liste.OrderBy(x => x).ToList();
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
