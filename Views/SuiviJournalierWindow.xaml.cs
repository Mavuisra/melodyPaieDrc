using System.Windows;

namespace MelodyPaieRDC.Views;

public partial class SuiviJournalierWindow : Window
{
    public SuiviJournalierWindow()
    {
        InitializeComponent();
        SuiviRoot.ConfigurerModeFenetreModale(this);
    }
}
