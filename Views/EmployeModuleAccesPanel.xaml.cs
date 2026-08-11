using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using materialDesign = MaterialDesignThemes.Wpf;

namespace MelodyPaieRDC.Views;

public partial class EmployeModuleAccesPanel : UserControl
{
    public static readonly DependencyProperty TitreProperty =
        DependencyProperty.Register(nameof(Titre), typeof(string), typeof(EmployeModuleAccesPanel), new PropertyMetadata(""));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(EmployeModuleAccesPanel), new PropertyMetadata(""));

    public static readonly DependencyProperty ActionLibelleProperty =
        DependencyProperty.Register(nameof(ActionLibelle), typeof(string), typeof(EmployeModuleAccesPanel), new PropertyMetadata("Ouvrir"));

    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.Register(nameof(IconKind), typeof(materialDesign.PackIconKind), typeof(EmployeModuleAccesPanel),
            new PropertyMetadata(materialDesign.PackIconKind.FolderOpenOutline));

    public static readonly DependencyProperty OuvrirCommandProperty =
        DependencyProperty.Register(nameof(OuvrirCommand), typeof(ICommand), typeof(EmployeModuleAccesPanel));

    public EmployeModuleAccesPanel() => InitializeComponent();

    public void ActualiserEtat(Models.Employe? employe)
    {
        if (employe == null)
        {
            EtatVide.Visibility = Visibility.Visible;
            ContenuModule.Visibility = Visibility.Collapsed;
            return;
        }

        EtatVide.Visibility = Visibility.Collapsed;
        ContenuModule.Visibility = Visibility.Visible;
        EmployeLibelle.Text = $"Employé : {employe.NomComplet} · {employe.Matricule}";
    }

    public string Titre
    {
        get => (string)GetValue(TitreProperty);
        set => SetValue(TitreProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string ActionLibelle
    {
        get => (string)GetValue(ActionLibelleProperty);
        set => SetValue(ActionLibelleProperty, value);
    }

    public materialDesign.PackIconKind IconKind
    {
        get => (materialDesign.PackIconKind)GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    public ICommand? OuvrirCommand
    {
        get => (ICommand?)GetValue(OuvrirCommandProperty);
        set => SetValue(OuvrirCommandProperty, value);
    }
}
