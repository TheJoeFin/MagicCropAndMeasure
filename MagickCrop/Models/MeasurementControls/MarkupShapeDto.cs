using MagickCrop.Models;
using System.Windows;

namespace MagickCrop.Models.MeasurementControls;

public class MarkupShapeDto : MeasurementControlDto
{
    public MarkupShapeDto()
    {
        Type = "MarkupShape";
    }

    public MarkupShapeType ShapeType { get; set; }
    public Point Point1 { get; set; }
    public Point Point2 { get; set; }
    public string StrokeColor { get; set; } = "#FFFF0000";
    public double StrokeThickness { get; set; } = 3.0;
}
