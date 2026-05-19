using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Forms.Metadata;
using MelodyPaieRDC.Forms.ViewModels;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Views;

public partial class DynamicFormWindow : Window
{
    private readonly PaieDbContext _db;

    public DynamicFormWindow(FormDefinition definition, int entityId, string? sousTitre = null)
    {
        InitializeComponent();
        _db = new PaieDbContext();

        Width = definition.Width > 0 ? definition.Width : 480;
        Height = definition.Height > 0 ? definition.Height : 520;

        DescriptionBlock.Text = definition.Description ?? "";
        DescriptionBlock.Visibility = string.IsNullOrWhiteSpace(definition.Description)
            ? Visibility.Collapsed
            : Visibility.Visible;

        var vm = new DynamicFormViewModel(_db, definition, entityId, sousTitre);
        DataContext = vm;

        vm.OnEnregistreReussi = () =>
        {
            UiFeedback.Succes("Formulaire enregistré.");
            DialogResult = true;
            Close();
        };
        vm.OnAnnuler = () =>
        {
            DialogResult = false;
            Close();
        };
        vm.OnErreurValidation = msg => MessageBox.Show(msg, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
        vm.OnInfo = msg => UiFeedback.Info(msg);

        FormControl.Initialiser(vm);
    }
}
