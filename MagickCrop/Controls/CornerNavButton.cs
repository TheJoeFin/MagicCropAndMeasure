using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MagickCrop.Controls;

/// <summary>
/// A small circular button placed beside a transform handle that jumps the viewport to the
/// neighbouring handle in the polygon. Derives from <see cref="Button"/> so it can be styled
/// implicitly and identified during hit testing.
/// </summary>
public class CornerNavButton : Button
{
    private readonly RotateTransform arrowRotation = new();

    public CornerNavButton()
    {
        Path arrow = new()
        {
            Data = Geometry.Parse("M 0,0 L 6,4.5 L 0,9 Z"),
            Fill = Brushes.White,
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = arrowRotation,
            IsHitTestVisible = false,
        };

        Content = arrow;
    }

    /// <summary>
    /// The handle this button navigates to.
    /// </summary>
    public Ellipse? Target { get; set; }

    /// <summary>
    /// The handle this button is anchored beside.
    /// </summary>
    public Ellipse? Anchor { get; set; }

    /// <summary>
    /// Rotates the arrow glyph so it points at the target handle.
    /// </summary>
    public double ArrowAngle
    {
        get => arrowRotation.Angle;
        set => arrowRotation.Angle = value;
    }
}
