using System.Windows;

namespace MagickCrop.Models.MeasurementControls;

/// <summary>
/// Data transfer object for angle measurement controls
/// </summary>
public class AngleMeasurementControlDto : MeasurementControlDto
{
    public AngleMeasurementControlDto()
    {
        Type = "Angle";
    }

    /// <summary>
    /// First point position of the angle measurement
    /// </summary>
    public Point Point1Position { get; set; }

    /// <summary>
    /// Vertex (center) point position of the angle measurement
    /// </summary>
    public Point VertexPosition { get; set; }

    /// <summary>
    /// Third point position of the angle measurement
    /// </summary>
    public Point Point3Position { get; set; }

    /// <summary>
    /// Color of the angle's legs, arc, and point handles
    /// </summary>
    public string StrokeColor { get; set; } = "#0066FF";
}
