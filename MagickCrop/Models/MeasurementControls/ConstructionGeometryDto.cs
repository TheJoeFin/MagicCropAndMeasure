using MagickCrop.Models.Construction;
using System.Windows;

namespace MagickCrop.Models.MeasurementControls;

/// <summary>
/// Data transfer object for a parametric construction: the points, the lines that
/// reference them, and the measurement settings for the derived shape's readout.
/// </summary>
public class ConstructionGeometryDto : MeasurementControlDto
{
    public ConstructionGeometryDto()
    {
        Type = "Construction";
    }

    public List<ConstructionPointDto> Points { get; set; } = [];

    public List<ConstructionLineDto> Lines { get; set; } = [];

    public List<ConstructionCircleDto> Circles { get; set; } = [];

    /// <summary>
    /// Scale factor for converting pixel measurements to real-world units
    /// </summary>
    public double ScaleFactor { get; set; } = 1.0;

    /// <summary>
    /// Units of measurement (e.g., "pixels", "mm", "in")
    /// </summary>
    public string Units { get; set; } = "pixels";

    /// <summary>
    /// Whether the derived shape's perimeter and area readout is shown. Defaults to true
    /// so projects saved before the toggle existed keep the readout they had.
    /// </summary>
    public bool ShowShapeMeasurement { get; set; } = true;

    /// <summary>
    /// Color of the construction's points, lines, and derived shape. Null for projects
    /// saved before this existed, which keep their original blue/orange appearance rather
    /// than being forced onto a single color.
    /// </summary>
    public string? StrokeColor { get; set; }
}

/// <summary>
/// A construction point. The id is persisted because lines reference points by id.
/// </summary>
public class ConstructionPointDto
{
    public Guid Id { get; set; }
    public Point Position { get; set; }

    /// <summary>
    /// How the position is produced. Absent from projects saved before derived points
    /// existed, which correctly default to a free point.
    /// </summary>
    public ConstructionPointSource Source { get; set; } = ConstructionPointSource.Free;

    /// <summary>Line or circle the point is derived from; empty for a free point.</summary>
    public Guid ParentAId { get; set; }

    /// <summary>Second line of a derived intersection; empty otherwise.</summary>
    public Guid ParentBId { get; set; }
}

/// <summary>
/// A construction line, referencing its two endpoints by id.
/// </summary>
public class ConstructionLineDto
{
    public Guid Id { get; set; }
    public Guid StartPointId { get; set; }
    public Guid EndPointId { get; set; }

    /// <summary>
    /// Whether the line draws past its points to reveal the corners it forms.
    /// </summary>
    public bool IsExtended { get; set; } = true;

    /// <summary>
    /// Whether the line's length is labelled. Absent from projects saved before
    /// per-line readouts existed, which correctly default to unlabelled.
    /// </summary>
    public bool ShowMeasurement { get; set; }
}

/// <summary>
/// A construction circle, referencing the three points it passes through by id.
/// Centre and radius are derived on load, never persisted.
/// </summary>
public class ConstructionCircleDto
{
    public Guid Id { get; set; }
    public Guid PointAId { get; set; }
    public Guid PointBId { get; set; }
    public Guid PointCId { get; set; }

    /// <summary>
    /// Whether the circle's radius, circumference, and area are labelled at its centre.
    /// </summary>
    public bool ShowMeasurement { get; set; }
}
