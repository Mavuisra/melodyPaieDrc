using System.Windows;
using System.Windows.Controls;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Views;

public partial class BulletinView : UserControl
{
    public BulletinView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ActualiserSynthese();
    }

    private void ActualiserSynthese()
    {
        if (DataContext is BulletinPaie bulletin)
            SynthesePanel.DataContext = BulletinSyntheseHelper.Construire(bulletin);
        else
            SynthesePanel.DataContext = null;
    }
}
