using System.Collections.Generic;
using System.Windows;

namespace MagickCrop.Models.MeasurementControls;

/// <summary>
/// Data transfer object for polygon measurement controls
/// </summary>
public class PolygonMeasurementControlDto : MeasurementControlDto
{
    public PolygonMeasurementControlDto()
    {
        Type = "Polygon";
    }

    /// <summary>
    /// List of vertices that define the polygon shape
    /// </summary>
    public List<Point> Vertices { get; set; } = new();

    /// <summary>
    /// Scale factor for converting pixel measurements to real-world units
    /// </summary>
    public double ScaleFactor { get; set; } = 1.0;

    /// <summary>
    /// Units of measurement (e.g., "pixels", "mm", "in")
    /// </summary>
    public string Units { get; set; } = "pixels";

    /// <summary>
    /// Whether the polygon is closed (completed)
    /// </summary>
    public bool IsClosed { get; set; } = false;
}