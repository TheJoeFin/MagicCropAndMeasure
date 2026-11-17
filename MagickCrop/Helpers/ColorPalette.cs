using System.Windows.Media;

namespace MagickCrop.Helpers;

/// <summary>
/// The named swatch colors offered wherever the user can pick a color, matching the set
/// already used by the Markup tool's color palette in MainWindow.xaml.
/// </summary>
public static class ColorPalette
{
    public static readonly (string Name, Color Color)[] Swatches =
    [
        ("Red", Colors.Red),
        ("OrangeRed", Colors.OrangeRed),
        ("Yellow", Colors.Yellow),
        ("LimeGreen", Colors.LimeGreen),
        ("Cyan", Colors.Cyan),
        ("DodgerBlue", Colors.DodgerBlue),
        ("MediumPurple", Colors.MediumPurple),
        ("DeepPink", Colors.DeepPink),
        ("White", Colors.White),
        ("Black", Colors.Black),
    ];
}
