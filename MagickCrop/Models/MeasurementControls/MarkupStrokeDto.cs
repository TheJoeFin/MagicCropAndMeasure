using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace MagickCrop.Models.MeasurementControls;

public class MarkupStrokeDto
{
    public List<Point> Points { get; set; } = [];
    public Color Color { get; set; }
    public double Thickness { get; set; }
    public bool IsHighlighter { get; set; }

    public static MarkupStrokeDto FromStroke(Stroke stroke)
    {
        return new MarkupStrokeDto
        {
            Points = [.. stroke.StylusPoints.Select(sp => new Point(sp.X, sp.Y))],
            Color = stroke.DrawingAttributes.Color,
            Thickness = stroke.DrawingAttributes.Width,
            IsHighlighter = stroke.DrawingAttributes.IsHighlighter
        };
    }

    public Stroke ToStroke()
    {
        StylusPointCollection stylusPoints = [.. Points.Select(p => new StylusPoint(p.X, p.Y))];
        DrawingAttributes attrs = new()
        {
            Color = Color,
            Width = Thickness,
            Height = Thickness,
            IsHighlighter = IsHighlighter
        };
        return new Stroke(stylusPoints, attrs);
    }
}
