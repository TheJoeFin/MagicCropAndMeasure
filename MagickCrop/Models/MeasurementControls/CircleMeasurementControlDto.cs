using System.Windows;

namespace MagickCrop.Models.MeasurementControls;

public class CircleMeasurementControlDto : MeasurementControlDto
{
    public CircleMeasurementControlDto()
    {
        Type = "Circle";
    }

    /// <summary>
    /// Center point of the circle
    /// </summary>
    public Point Center { get; set; }

    /// <summary>
    /// Point on the edge of the circle (used to calculate radius)
    /// </summary>
    public Point EdgePoint { get; set; }

    /// <summary>
    /// Scale factor for converting pixel measurements to real-world units
    /// </summary>
    public double ScaleFactor { get; set; } = 1.0;

    /// <summary>
    /// Units of measurement (e.g., "pixels", "mm", "in")
    /// </summary>
    public string Units { get; set; } = "pixels";
}
