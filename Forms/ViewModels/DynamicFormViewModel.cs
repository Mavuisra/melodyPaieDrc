using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Forms.Engine;
using MelodyPaieRDC.Forms.Metadata;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Forms.ViewModels;

public sealed class DynamicFormViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly FormValueService _valueService;
    private readonly FormLookupResolver _lookupResolver;
    private readonly FormFieldHandlerRegistry _registry = new();

    public DynamicFormViewModel(
        PaieDbContext db,
        FormDefinition definition,
        int entityId,
        string? sousTitre = null)
    {
        _db = db;
        Definition = definition;
        EntityId = entityId;
        SousTitre = sousTitre;
        _valueService = new FormValueService(db);
        _lookupResolver = new FormLookupResolver(db);

        TitreFenetre = definition.Title;
        if (!string.IsNullOrWhiteSpace(sousTitre))
            TitreFenetre += $" — {sousTitre}";

        InitialiserChamps();
        ChargerValeursExistantes();

        EnregistrerCommand = new RelayCommand(_ => Enregistrer());
        AnnulerCommand = new RelayCommand(_ => OnAnnuler?.Invoke());
        RechargerMetadonneesCommand = new RelayCommand(_ => RechargerMetadonnees());
    }

    public FormDefinition Definition { get; private set; }
    public int EntityId { get; }
    public string? SousTitre { get; }
    public string TitreFenetre { get; private set; }

    public ObservableCollection<DynamicFieldViewModel> Champs { get; } = new();

    public ICommand EnregistrerCommand { get; }
    public ICommand AnnulerCommand { get; }
    public ICommand RechargerMetadonneesCommand { get; }

    public Action? OnEnregistreReussi { get; set; }
    public Action? OnAnnuler { get; set; }
    public Action<string>? OnErreurValidation { get; set; }
    public Action<string>? OnInfo { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void InitialiserChamps()
    {
        Champs.Clear();
        foreach (var section in Definition.Sections)
        {
            foreach (var field in section.Fields)
            {
                var options = field.Type.Equals("lookup", StringComparison.OrdinalIgnoreCase)
                    ? _lookupResolver.Resoudre(field.Lookup)
                    : Array.Empty<LookupOption>();

                var vm = new DynamicFieldViewModel(section.Id, field, options);
                vm.ValueChanged += (_, _) => FormVisibilityEvaluator.MettreAJourVisibilite(Champs);
                Champs.Add(vm);
            }
        }

        FormVisibilityEvaluator.MettreAJourVisibilite(Champs);
    }

    private void ChargerValeursExistantes()
    {
        if (EntityId <= 0) return;

        var valeurs = _valueService.ChargerValeurs(Definition.FormId, Definition.EntityType, EntityId);
        foreach (var champ in Champs)
        {
            if (valeurs.TryGetValue(champ.Key, out var v))
                champ.Value = v;
        }

        FormVisibilityEvaluator.MettreAJourVisibilite(Champs);
    }

    private void Enregistrer()
    {
        if (EntityId <= 0)
        {
            OnErreurValidation?.Invoke("Enregistrez d'abord l'entité principale avant les champs complémentaires.");
            return;
        }

        if (!_valueService.EntiteExiste(Definition.EntityType, EntityId))
        {
            OnErreurValidation?.Invoke($"L'entité {Definition.EntityType} #{EntityId} est introuvable.");
            return;
        }

        if (!FormValidator.Valider(Definition, Champs.ToList(), _registry, out var erreur))
        {
            OnErreurValidation?.Invoke(erreur);
            return;
        }

        var dict = new Dictionary<string, string?>();
        foreach (var champ in Champs.Where(c => c.EstVisible))
        {
            var handler = _registry.Obtenir(champ.Definition.Type);
            dict[champ.Key] = handler.NormaliserValeur(champ.Value, champ.Definition);
        }

        _valueService.EnregistrerValeurs(Definition.FormId, Definition.EntityType, EntityId, dict);
        OnEnregistreReussi?.Invoke();
    }

    private void RechargerMetadonnees()
    {
        FormDefinitionLoader.RechargerFormulaire(Definition.FormId, out var nouvelleDef, out var erreur);
        if (nouvelleDef == null)
        {
            OnErreurValidation?.Invoke(erreur ?? "Impossible de recharger le formulaire.");
            return;
        }

        var sauvegarde = Champs.ToDictionary(c => c.Key, c => c.Value);
        Definition = nouvelleDef;
        TitreFenetre = Definition.Title;
        if (!string.IsNullOrWhiteSpace(SousTitre))
            TitreFenetre += $" — {SousTitre}";
        OnPropertyChanged(nameof(TitreFenetre));

        InitialiserChamps();
        foreach (var champ in Champs)
        {
            if (sauvegarde.TryGetValue(champ.Key, out var v))
                champ.Value = v;
        }

        OnInfo?.Invoke("Définition du formulaire rechargée depuis le fichier JSON.");
        OnMetadonneesRechargees?.Invoke();
    }

    /// <summary>Signale à la vue de reconstruire les contrôles WPF.</summary>
    public event Action? OnMetadonneesRechargees;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
