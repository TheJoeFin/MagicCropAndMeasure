namespace MagickCrop;

public enum DraggingMode
{
    None,
    MoveElement,
    Panning,
    Resizing,
    MeasureDistance,
    MeasureAngle,
    MeasureRectangle,
    MeasureCircle,
    MeasurePolygon,
    CreatingMeasurement,
    WhitePointPicker,
    BlackPointPicker,
    EdgeCorrectionDragging,
    GridStraightenDragging,
    MarkupShape,
    MarkupText,
    MarkupGroupSelect,
    MarkupGroupMove,

    /// <summary>Dragging an existing construction point.</summary>
    ConstructionPoint,

    /// <summary>Dragging out a new construction edge and its two points.</summary>
    ConstructionEdgeCreate,

    /// <summary>Dragging a probe line across a boundary to find where the edge falls on it.</summary>
    ConstructionBoundaryProbe
}
