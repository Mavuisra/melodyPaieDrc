using System.Windows.Input;

namespace MelodyPaieRDC.ViewModels;

/// <summary>Étape du parcours mensuel affiché sur le tableau de bord.</summary>
public sealed class MoisPaieChecklistItem
{
    public int Numero { get; init; }
    public string Libelle { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool EstTermine { get; init; }
    /// <summary>Première étape non terminée (mise en avant sur le tableau de bord).</summary>
    public bool EstProchaineEtape { get; init; }
    public int MenuCible { get; init; }
    public ICommand? OuvrirCommand { get; init; }
}
