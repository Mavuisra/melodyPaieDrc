using System.Windows.Media;

namespace MelodyPaieRDC.Helpers;

/// <summary>Manipulation de couleurs pour la palette entreprise.</summary>
public static class ColorBlendHelper
{
    public static Color Assombrir(Color couleur, double facteur = 0.72)
    {
        facteur = Math.Clamp(facteur, 0, 1);
        return Color.FromRgb(
            (byte)(couleur.R * facteur),
            (byte)(couleur.G * facteur),
            (byte)(couleur.B * facteur));
    }

    public static Color Eclaircir(Color couleur, double facteur = 0.28)
    {
        facteur = Math.Clamp(facteur, 0, 1);
        return Color.FromRgb(
            (byte)(couleur.R + (255 - couleur.R) * facteur),
            (byte)(couleur.G + (255 - couleur.G) * facteur),
            (byte)(couleur.B + (255 - couleur.B) * facteur));
    }

    public static Color Melanger(Color a, Color b, double proportionB = 0.5)
    {
        proportionB = Math.Clamp(proportionB, 0, 1);
        var proportionA = 1 - proportionB;
        return Color.FromRgb(
            (byte)(a.R * proportionA + b.R * proportionB),
            (byte)(a.G * proportionA + b.G * proportionB),
            (byte)(a.B * proportionA + b.B * proportionB));
    }
}
