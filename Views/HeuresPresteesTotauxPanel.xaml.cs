using System.Windows.Controls;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class HeuresPresteesTotauxPanel : UserControl
{
    public HeuresPresteesTotauxPanel()
    {
        InitializeComponent();
        DataContext = new HeuresPresteesTotauxViewModel(new PaieDbContext());
    }

    public HeuresPresteesTotauxViewModel? TotauxViewModel => DataContext as HeuresPresteesTotauxViewModel;
}
