using MagickCrop.Helpers;
using MagickCrop.Models.Construction;
using MagickCrop.Models.MeasurementControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MagickCrop.Controls;

/// <summary>
/// Hosts a whole parametric construction — points, the lines through them, and the shape
/// derived from where those lines cross.
///
/// Unlike the other measurement controls this is one control for the entire graph rather
/// than one per entity, because a corner is a function of <em>all</em> the lines and so the
/// solve needs a single owner.
/// </summary>
public partial class ConstructionOverlayControl : UserControl
{
    private const double BasePointSize = 12;
    private const double BaseSmallPointSize = 6;
    private const double BaseStrokeThickness = 2;
    private const double HitStrokeThickness = 12;

    /// <summary>How far past the construction extended lines are allowed to run.</summary>
    private const double BoundsInflation = 0.2;

    /// <summary>How much bigger a selected point handle draws than an idle one.</summary>
    private const double SelectedPointScale = 1.5;

    // Mutable per-instance so each construction overlay can have its own color. Selection
    // and face-state brushes below stay fixed/static — they're state indicators, not the
    // shape's identity color.
    private Brush PointBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0066FF"));
    private Brush LineBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0066FF"));
    private static readonly Brush SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6600"));

    // Nearly transparent rather than fully so WPF still hit-tests the fill — an idle face
    // needs to be clickable before it has ever been hovered.
    private static readonly Brush FaceIdleBrush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
    private static readonly Brush FaceHoverBrush = new SolidColorBrush(Color.FromArgb(70, 0, 150, 255));
    private static readonly Brush FaceSelectedBrush = new SolidColorBrush(Color.FromArgb(110, 0, 200, 90));

    // Layering inside the shared canvas. Explicit z-indices avoid having to remove and
    // re-add elements to keep points clickable above the lines.
    private const int FaceZIndex = -1;
    private const int ShapeZIndex = 0;
    private const int LineZIndex = 1;
    private const int HitZIndex = 2;

    /// <summary>Above the line hit paths so it is clickable, below real points.</summary>
    private const int CandidateZIndex = 3;

    private const int PointZIndex = 4;
    private const int TextZIndex = 5;

    private readonly ConstructionGeometry geometry = new();
    private readonly List<Ellipse> pointHandles = [];
    private readonly List<Path> linePaths = [];
    private readonly List<Path> hitPaths = [];

    /// <summary>
    /// Points the user has picked, oldest first. Capped at three: two define a line,
    /// three define a circle, and nothing needs more, so the cap is what keeps "select
    /// two, then a third" a self-explaining gesture.
    /// </summary>
    private readonly List<Guid> selectedPointIds = [];

    private const int MaxSelectedPoints = 3;

    private Guid? selectedLineId;
    private Guid? selectedCircleId;

    private readonly List<Path> circlePaths = [];
    private readonly List<Path> circleHitPaths = [];

    /// <summary>
    /// Labels for individual lines and circles the user has asked to see measured.
    /// Rebuilt on every refresh, because what they read depends on positions that move.
    /// </summary>
    private readonly List<Border> measurementLabels = [];

    // Crossings and centres the construction implies. Recomputed every refresh and never
    // stored — clicking one is what turns it into a point the construction owns.
    private readonly List<Ellipse> candidateHandles = [];
    private List<DerivedPointCandidate> derivedCandidates = [];

    // The "you could build this" shape offered by the current selection: a faint dashed
    // visual plus a fat transparent twin that is comfortable to click. Two points offer
    // a line, three offer a circle.
    private Path? ghostLinePath;
    private Path? ghostHitPath;

    // Live preview of where a boundary probe is reading the edge. Created on first use and
    // then reused, because it is repositioned on every mouse move of the gesture.
    private Ellipse? boundaryCandidate;
    private Point? boundaryCandidatePosition;
    private bool boundaryCandidateIsWeak;

    private string? transientHint;

    private bool showShapeMeasurement = true;

    private int pointDraggingIndex = -1;
    private bool areDragGizmosVisible = true;
    private bool areEndpointCapsVisible;
    private double visualScale = 1.0;

    private IReadOnlyList<Point> solvedRing = [];
    private ConstructionSolver.SolveStatus solveStatus = ConstructionSolver.SolveStatus.NotEnoughLines;

    // Every bounded cell the current lines carve out, not just the single outer shape
    // above. Rebuilt alongside it on every refresh; the user clicks these to build an
    // arbitrary polygon out of adjacent cells.
    private List<ConstructionFace> faces = [];
    private readonly List<Path> facePaths = [];
    private readonly HashSet<int> selectedFaceIndices = [];
    private int? hoveredFaceIndex;
    private bool isFaceSelectionModeActive;

    public ConstructionOverlayControl()
    {
        InitializeComponent();

        Panel.SetZIndex(ShapePath, ShapeZIndex);
        Panel.SetZIndex(PreviewLine, HitZIndex);
        Panel.SetZIndex(MeasurementText, TextZIndex);

        Refresh();
    }

    #region Measurement contract

    private double scaleFactor = 1.0;
    public double ScaleFactor
    {
        get => scaleFactor;
        set
        {
            scaleFactor = value;
            UpdateDisplay();
        }
    }

    private string units = "pixels";
    public string Units
    {
        get => units;
        set
        {
            units = value;
            UpdateDisplay();
        }
    }

    private Color strokeColor = (Color)ColorConverter.ConvertFromString("#0066FF");

    /// <summary>
    /// The construction's identity color — applied to its points, lines, circles, and the
    /// derived shape's outline/fill. One color for the whole construction, since it is one
    /// context menu and one "thing" to the user, even though it is many visual elements.
    /// </summary>
    public Color StrokeColor
    {
        get => strokeColor;
        set
        {
            strokeColor = value;
            PointBrush = new SolidColorBrush(strokeColor);
            LineBrush = new SolidColorBrush(strokeColor);
            ShapePath.Stroke = new SolidColorBrush(strokeColor);
            ShapePath.Fill = new SolidColorBrush(Color.FromArgb(0x26, strokeColor.R, strokeColor.G, strokeColor.B));
            Refresh();
        }
    }

    public bool IsDragGizmoVisible
    {
        get => areDragGizmosVisible;
        set
        {
            areDragGizmosVisible = value;
            Visibility visibility = value ? Visibility.Visible : Visibility.Collapsed;
            foreach (Ellipse handle in pointHandles)
                handle.Visibility = visibility;

            // The build affordances are gizmos too — none may survive into an export.
            if (ghostLinePath is not null) ghostLinePath.Visibility = visibility;
            if (ghostHitPath is not null) ghostHitPath.Visibility = visibility;
            ApplyBoundaryCandidateAppearance();

            // Candidates are dropped entirely rather than hidden, so they cannot be
            // clicked while invisible.
            RenderDerivedCandidates();
        }
    }

    public bool IsEndpointCapVisible
    {
        set
        {
            areEndpointCapsVisible = value;
            ApplyPointSizes();
        }
    }

    public event MouseButtonEventHandler? MeasurementPointMouseDown;
    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    /// <summary>Raised when the construction changes so the host can refresh dependent UI.</summary>
    public event EventHandler? ConstructionChanged;

    /// <summary>
    /// Raised once per completed edit, carrying before/after snapshots for the undo
    /// stack. A drag raises this on release, not on every mouse move.
    /// </summary>
    public event EventHandler<ConstructionGeometryEditedEventArgs>? GeometryEdited;

    public void MovePoint(int pointIndex, Point newPosition)
    {
        if (pointIndex < 0 || pointIndex >= geometry.Points.Count) return;

        // A derived point is wherever its parents put it; a drag cannot override that.
        if (geometry.Points[pointIndex].IsDerived) return;

        Edit(() => geometry.MovePoint(geometry.Points[pointIndex].Id, newPosition));
    }

    public int GetActivePointIndex() => pointDraggingIndex;

    public void ResetActivePoint() => pointDraggingIndex = -1;

    #endregion

    #region Construction API

    /// <summary>
    /// Bounds of the image in canvas coordinates. Extended lines are clipped to this
    /// unioned with the construction's own extent, so a corner outside the image still
    /// gets drawn.
    /// </summary>
    public Rect ImageBounds { get; set; } = new(0, 0, 1000, 1000);

    public int PointCount => geometry.Points.Count;
    public int LineCount => geometry.Lines.Count;
    public int CircleCount => geometry.Circles.Count;

    public bool IsEmpty =>
        geometry.Points.Count == 0 && geometry.Lines.Count == 0 && geometry.Circles.Count == 0;

    public Guid AddPoint(Point position)
    {
        Guid id = Guid.Empty;
        Edit(() => id = geometry.AddPoint(position));
        return id;
    }

    public Guid AddLine(Guid startPointId, Guid endPointId)
    {
        Guid id = Guid.Empty;
        Edit(() => id = geometry.AddLine(startPointId, endPointId));
        return id;
    }

    public void MoveConstructionPoint(Guid pointId, Point position)
    {
        if (geometry.FindPoint(pointId)?.IsDerived != false) return;

        Edit(() => geometry.MovePoint(pointId, position));
    }

    public void RemoveConstructionPoint(Guid pointId) =>
        Edit(() =>
        {
            geometry.RemovePoint(pointId);
            PruneSelection();
        });

    public void RemoveConstructionLine(Guid lineId) =>
        Edit(() =>
        {
            geometry.RemoveLine(lineId);
            PruneSelection();
        });

    public void RemoveConstructionCircle(Guid circleId) =>
        Edit(() =>
        {
            geometry.RemoveCircle(circleId);
            PruneSelection();
        });

    /// <summary>
    /// Repoints a line's end, used when a drag-created edge is released on an existing
    /// point — that reuse is what connects edges into a shape.
    /// </summary>
    public void SetLineEnd(Guid lineId, Guid endPointId) =>
        Edit(() =>
        {
            if (geometry.FindLine(lineId) is ConstructionLine line)
                line.EndPointId = endPointId;
        });

    public void SetLineExtended(Guid lineId, bool isExtended) =>
        Edit(() =>
        {
            if (geometry.FindLine(lineId) is ConstructionLine line)
                line.IsExtended = isExtended;
        });

    public bool IsLineExtended(Guid lineId) => geometry.FindLine(lineId)?.IsExtended ?? true;

    /// <summary>
    /// Shows or hides the length label beside one line. Routed through <see cref="Edit"/>
    /// so the toggle joins the undo stack like any other change to the construction.
    /// </summary>
    public void SetLineMeasurementVisible(Guid lineId, bool isVisible) =>
        Edit(() =>
        {
            if (geometry.FindLine(lineId) is ConstructionLine line)
                line.ShowMeasurement = isVisible;
        });

    public bool IsLineMeasurementVisible(Guid lineId) =>
        geometry.FindLine(lineId)?.ShowMeasurement ?? false;

    /// <summary>Shows or hides the radius/circumference/area label at one circle's centre.</summary>
    public void SetCircleMeasurementVisible(Guid circleId, bool isVisible) =>
        Edit(() =>
        {
            if (geometry.FindCircle(circleId) is ConstructionCircle circle)
                circle.ShowMeasurement = isVisible;
        });

    public bool IsCircleMeasurementVisible(Guid circleId) =>
        geometry.FindCircle(circleId)?.ShowMeasurement ?? false;

    /// <summary>
    /// Whether the derived shape's perimeter and area readout is shown. Unlike the
    /// per-line and per-circle flags this is not part of the geometry, so it is not
    /// undoable — it is a view setting on the construction as a whole.
    /// </summary>
    public bool ShowShapeMeasurement
    {
        get => showShapeMeasurement;
        set
        {
            if (showShapeMeasurement == value) return;

            showShapeMeasurement = value;
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Finds a point within <paramref name="tolerance"/> of <paramref name="position"/>.
    /// The caller must divide the tolerance by the canvas zoom so the grab radius is
    /// constant on screen.
    /// </summary>
    public Guid? FindPointNear(Point position, double tolerance, Guid? exclude = null) =>
        geometry.FindPointNear(position, tolerance, exclude)?.Id;

    public Point? GetPointPosition(Guid pointId) => geometry.FindPoint(pointId)?.Position;

    public void ShowPreviewLine(Point from, Point to)
    {
        PreviewLine.X1 = from.X;
        PreviewLine.Y1 = from.Y;
        PreviewLine.X2 = to.X;
        PreviewLine.Y2 = to.Y;
        PreviewLine.Visibility = Visibility.Visible;
    }

    public void HidePreviewLine() => PreviewLine.Visibility = Visibility.Collapsed;

    /// <summary>
    /// Shows where a boundary probe is currently reading the edge, so the user can see the
    /// point track the transition while they are still shaping the probe. Purely a
    /// preview — nothing is added to the geometry until the gesture is released.
    /// </summary>
    /// <param name="isWeak">
    /// Draws hollow and dashed instead of solid, so a guess never looks as certain as a
    /// reading.
    /// </param>
    public void ShowBoundaryCandidate(Point position, bool isWeak)
    {
        boundaryCandidatePosition = position;
        boundaryCandidateIsWeak = isWeak;

        boundaryCandidate ??= CreateBoundaryCandidate();
        ApplyBoundaryCandidateAppearance();
    }

    public void HideBoundaryCandidate()
    {
        boundaryCandidatePosition = null;

        if (boundaryCandidate is not null)
            boundaryCandidate.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// A note about the last gesture, shown under the measurement until the next edit
    /// replaces it. Used to say a probe found only a weak boundary without interrupting
    /// the user with a dialog for something they can simply nudge.
    /// </summary>
    public string? TransientHint
    {
        get => transientHint;
        set
        {
            transientHint = value;
            UpdateDisplay();
        }
    }

    /// <summary>Ring of derived corners, empty when the construction cannot be solved.</summary>
    public IReadOnlyList<Point> SolvedRing => solvedRing;

    public bool TryGetRing(out IReadOnlyList<Point> ring)
    {
        ring = solvedRing;
        return solvedRing.Count >= 3;
    }

    /// <summary>
    /// Produces a quadrilateral for the transform / crop / un-warp consumers. Returns
    /// false whenever the construction is not exactly four solved corners.
    /// </summary>
    public bool TryGetQuadrilateral(out QuadrilateralDetector.DetectedQuadrilateral quadrilateral)
    {
        quadrilateral = null!;

        if (solvedRing.Count != 4) return false;

        // DetectedQuadrilateral labels corners by x+y / x-y extremes, which mislabels a
        // strongly rotated quad. Detection output is near axis-aligned so it never trips
        // on this, but a hand construction can sit at 40 degrees — normalizing the winding
        // first keeps the TL/TR/BR/BL labels honest.
        List<Point> ordered = ConstructionSolver.NormalizeWinding(solvedRing);
        double area = GeometryMathHelper.PolygonArea(ordered);

        if (area <= 0 || double.IsNaN(area) || double.IsInfinity(area)) return false;

        foreach (Point corner in ordered)
        {
            if (double.IsNaN(corner.X) || double.IsNaN(corner.Y) ||
                double.IsInfinity(corner.X) || double.IsInfinity(corner.Y))
                return false;
        }

        quadrilateral = new QuadrilateralDetector.DetectedQuadrilateral([.. ordered], area, 1.0);
        return true;
    }

    #endregion

    #region Face selection

    /// <summary>
    /// Gates whether the bounded cells the arrangement carves out can be hovered and
    /// clicked. Off by default so face paths never steal clicks meant for the point, line,
    /// and boundary tools sharing this canvas.
    /// </summary>
    public bool IsFaceSelectionModeActive
    {
        get => isFaceSelectionModeActive;
        set
        {
            isFaceSelectionModeActive = value;

            foreach (Path path in facePaths)
                path.IsHitTestVisible = value;

            if (!value)
            {
                hoveredFaceIndex = null;
                UpdateFaceVisuals();
            }
        }
    }

    /// <summary>Raised whenever a face is clicked, so the host can enable/disable its "Make Polygon" button.</summary>
    public event EventHandler? FaceSelectionChanged;

    public bool HasSelectedFaces => selectedFaceIndices.Count > 0;

    public void ClearFaceSelection()
    {
        if (selectedFaceIndices.Count == 0) return;

        selectedFaceIndices.Clear();
        UpdateFaceVisuals();
        FaceSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The selected faces merged into one or more outer boundaries — more than one only
    /// when the selection is split into separate clumps. Empty rather than a single empty
    /// ring when nothing is selected.
    /// </summary>
    public bool TryGetSelectedFacesUnion(out List<List<Point>> rings)
    {
        rings = selectedFaceIndices.Count > 0
            ? ConstructionFaceSolver.UnionFaces(faces, selectedFaceIndices)
            : [];

        return rings.Count > 0;
    }

    #endregion

    #region Undo transactions

    /// <summary>Geometry as it stood when the current drag began.</summary>
    private ConstructionGeometryDto? dragSnapshot;

    /// <summary>Geometry as it stood when the current single mutation began.</summary>
    private ConstructionGeometryDto? editSnapshot;

    /// <summary>
    /// Opens a drag: every mouse move writes a new position, but only the gesture as a
    /// whole is worth undoing, so the individual writes stop publishing until
    /// <see cref="EndDrag"/>.
    /// </summary>
    public void BeginDrag()
    {
        // A drag whose release was missed is published here rather than silently lost,
        // which keeps a dropped mouse-up from wedging the stack.
        EndDrag();
        dragSnapshot = CaptureGeometry();
    }

    /// <summary>Closes a drag and publishes it as one undo step. Safe to call twice.</summary>
    public void EndDrag()
    {
        ConstructionGeometryDto? before = dragSnapshot;
        dragSnapshot = null;

        if (before is not null)
            PublishEdit(before);
    }

    /// <summary>Runs a single mutation, publishing it unless a wider edit is in flight.</summary>
    private void Edit(Action change)
    {
        // Only the outermost scope captures and publishes: inside a drag, or inside
        // another Edit, the mutation is part of a bigger step.
        bool isOutermost = dragSnapshot is null && editSnapshot is null;
        if (isOutermost)
            editSnapshot = CaptureGeometry();

        // Any change to the geometry makes a note about the previous gesture stale. The
        // probe tool sets its note back after the point it adds lands here.
        transientHint = null;

        try
        {
            change();
            Refresh();
        }
        finally
        {
            if (isOutermost)
            {
                ConstructionGeometryDto before = editSnapshot!;
                editSnapshot = null;
                PublishEdit(before);
            }
        }
    }

    /// <summary>
    /// Raises the edit, but only when the geometry actually differs — a click that
    /// changed nothing, or a drag that never moved, leaves the undo stack alone.
    /// </summary>
    private void PublishEdit(ConstructionGeometryDto before)
    {
        ConstructionGeometryDto after = CaptureGeometry();
        if (GeometryEquals(before, after)) return;

        GeometryEdited?.Invoke(this, new ConstructionGeometryEditedEventArgs(before, after));
    }

    /// <summary>
    /// Snapshot of the point/line graph alone. Scale and units are deliberately left at
    /// their defaults: they are window-level settings and must not ride along on an undo.
    /// </summary>
    public ConstructionGeometryDto CaptureGeometry()
    {
        ConstructionGeometryDto snapshot = new();

        foreach (ConstructionPoint point in geometry.Points)
        {
            snapshot.Points.Add(new ConstructionPointDto
            {
                Id = point.Id,
                Position = point.Position,
                Source = point.Source,
                ParentAId = point.ParentAId,
                ParentBId = point.ParentBId
            });
        }

        foreach (ConstructionLine line in geometry.Lines)
        {
            snapshot.Lines.Add(new ConstructionLineDto
            {
                Id = line.Id,
                StartPointId = line.StartPointId,
                EndPointId = line.EndPointId,
                IsExtended = line.IsExtended,
                ShowMeasurement = line.ShowMeasurement
            });
        }

        foreach (ConstructionCircle circle in geometry.Circles)
        {
            snapshot.Circles.Add(new ConstructionCircleDto
            {
                Id = circle.Id,
                PointAId = circle.PointAId,
                PointBId = circle.PointBId,
                PointCId = circle.PointCId,
                ShowMeasurement = circle.ShowMeasurement
            });
        }

        return snapshot;
    }

    /// <summary>
    /// Replaces the graph with a snapshot, without touching scale or units. Selection is
    /// dropped because the points it referred to may not exist in the restored state.
    /// Does not itself raise <see cref="GeometryEdited"/> — undo is not a new edit.
    /// </summary>
    public void RestoreGeometry(ConstructionGeometryDto snapshot)
    {
        foreach (Ellipse handle in pointHandles)
            MeasurementCanvas.Children.Remove(handle);
        pointHandles.Clear();

        selectedPointIds.Clear();
        selectedLineId = null;
        selectedCircleId = null;

        // Whatever was in flight refers to a state that no longer exists.
        dragSnapshot = null;
        editSnapshot = null;
        transientHint = null;
        HideBoundaryCandidate();

        geometry.Clear();

        foreach (ConstructionPointDto point in snapshot.Points)
            geometry.AddPoint(point.Id, point.Position, point.Source, point.ParentAId, point.ParentBId);

        foreach (ConstructionLineDto line in snapshot.Lines)
            geometry.AddLine(line.Id, line.StartPointId, line.EndPointId, line.IsExtended, line.ShowMeasurement);

        foreach (ConstructionCircleDto circle in snapshot.Circles)
            geometry.AddCircle(circle.Id, circle.PointAId, circle.PointBId, circle.PointCId, circle.ShowMeasurement);

        Refresh();
    }

    private static bool GeometryEquals(ConstructionGeometryDto a, ConstructionGeometryDto b)
    {
        if (a.Points.Count != b.Points.Count ||
            a.Lines.Count != b.Lines.Count ||
            a.Circles.Count != b.Circles.Count)
            return false;

        for (int i = 0; i < a.Points.Count; i++)
        {
            if (a.Points[i].Id != b.Points[i].Id ||
                a.Points[i].Position != b.Points[i].Position ||
                a.Points[i].Source != b.Points[i].Source ||
                a.Points[i].ParentAId != b.Points[i].ParentAId ||
                a.Points[i].ParentBId != b.Points[i].ParentBId)
                return false;
        }

        for (int i = 0; i < a.Lines.Count; i++)
        {
            if (a.Lines[i].Id != b.Lines[i].Id ||
                a.Lines[i].StartPointId != b.Lines[i].StartPointId ||
                a.Lines[i].EndPointId != b.Lines[i].EndPointId ||
                a.Lines[i].IsExtended != b.Lines[i].IsExtended ||
                a.Lines[i].ShowMeasurement != b.Lines[i].ShowMeasurement)
                return false;
        }

        for (int i = 0; i < a.Circles.Count; i++)
        {
            if (a.Circles[i].Id != b.Circles[i].Id ||
                a.Circles[i].PointAId != b.Circles[i].PointAId ||
                a.Circles[i].PointBId != b.Circles[i].PointBId ||
                a.Circles[i].PointCId != b.Circles[i].PointCId ||
                a.Circles[i].ShowMeasurement != b.Circles[i].ShowMeasurement)
                return false;
        }

        return true;
    }

    #endregion

    #region Selection

    /// <summary>
    /// True when anything is picked, so the host can tell whether Delete belongs to this
    /// control or to some other selection elsewhere in the window.
    /// </summary>
    public bool HasSelection =>
        selectedPointIds.Count > 0 || selectedLineId is not null || selectedCircleId is not null;

    public int SelectedPointCount => selectedPointIds.Count;

    /// <summary>Position of the lone selected point, or null unless exactly one is picked.</summary>
    public Point? SingleSelectedPointPosition =>
        selectedPointIds.Count == 1 ? geometry.FindPoint(selectedPointIds[0])?.Position : null;

    /// <summary>
    /// When true, picking a second point connects it straight away instead of offering
    /// the faint line. The Connect Points tool sets this, so its click-click flow and a
    /// plain click on a point handle can never disagree about what happens next.
    /// </summary>
    public bool ConnectOnSecondSelection { get; set; }

    /// <summary>
    /// The line the user is acting on: one they clicked, or — when two connected points
    /// are picked — the line already joining them. That second case is what lets a
    /// connection be broken by selecting its two ends.
    /// </summary>
    private Guid? EffectiveSelectedLineId
    {
        get
        {
            if (selectedLineId is Guid explicitId && geometry.FindLine(explicitId) is not null)
                return explicitId;

            if (selectedPointIds.Count != 2)
                return null;

            return geometry.FindLineBetween(selectedPointIds[0], selectedPointIds[1])?.Id;
        }
    }

    /// <summary>
    /// The circle the user is acting on: one they clicked, or — when three points that
    /// already define a circle are picked — that circle. Mirrors the line rule, so a
    /// circle is removed by reselecting the three points that made it.
    /// </summary>
    private Guid? EffectiveSelectedCircleId
    {
        get
        {
            if (selectedCircleId is Guid explicitId && geometry.FindCircle(explicitId) is not null)
                return explicitId;

            if (selectedPointIds.Count != 3)
                return null;

            return geometry.FindCircleThrough(
                selectedPointIds[0], selectedPointIds[1], selectedPointIds[2])?.Id;
        }
    }

    public void ClearSelection()
    {
        if (!HasSelection) return;

        selectedPointIds.Clear();
        selectedLineId = null;
        selectedCircleId = null;
        Refresh();
    }

    /// <summary>
    /// Deletes whatever is selected, preferring the line or circle: breaking one of those
    /// is the common case and it must not take the points with it. Returns true when
    /// something was removed.
    /// </summary>
    public bool DeleteSelection()
    {
        if (EffectiveSelectedLineId is Guid lineId)
        {
            Edit(() =>
            {
                geometry.RemoveLine(lineId);
                selectedLineId = null;
                PruneSelection();
            });
            return true;
        }

        if (EffectiveSelectedCircleId is Guid circleId)
        {
            Edit(() =>
            {
                geometry.RemoveCircle(circleId);
                selectedCircleId = null;
                PruneSelection();
            });
            return true;
        }

        if (selectedPointIds.Count == 0)
            return false;

        Edit(() =>
        {
            foreach (Guid pointId in selectedPointIds.ToList())
                geometry.RemovePoint(pointId);

            selectedPointIds.Clear();
            PruneSelection();
        });
        return true;
    }

    /// <summary>Picks a point, replacing the oldest once all three slots are full.</summary>
    public void SelectPoint(Guid pointId)
    {
        if (geometry.FindPoint(pointId) is null) return;

        selectedLineId = null;
        selectedCircleId = null;

        if (!selectedPointIds.Contains(pointId))
        {
            selectedPointIds.Add(pointId);

            // Three points define the largest thing on offer, so beyond that the oldest
            // drops out and the selection walks forward rather than starting over.
            while (selectedPointIds.Count > MaxSelectedPoints)
                selectedPointIds.RemoveAt(0);
        }

        if (ConnectOnSecondSelection && TryConnectSelectedPoints())
            return;

        Refresh();
    }

    /// <summary>
    /// Selects an existing point near <paramref name="position"/>, if there is one.
    /// Lets a click that lands beside a point still count as picking it.
    /// </summary>
    public bool TrySelectPointNear(Point position, double tolerance)
    {
        if (geometry.FindPointNear(position, tolerance)?.Id is not Guid pointId)
            return false;

        SelectPoint(pointId);
        return true;
    }

    /// <summary>
    /// Turns the two selected points into a real line. Returns false when there are not
    /// two of them, or when they are already joined.
    /// </summary>
    public bool TryConnectSelectedPoints()
    {
        if (selectedPointIds.Count != 2) return false;

        Guid startId = selectedPointIds[0];
        Guid endId = selectedPointIds[1];
        if (geometry.FindLineBetween(startId, endId) is not null) return false;

        Edit(() =>
        {
            geometry.AddLine(startId, endId);

            // Leave the far end selected so the next point click chains straight into
            // the following edge, the way walking a polygon actually goes.
            selectedPointIds.Clear();
            selectedPointIds.Add(endId);
            selectedLineId = null;
        });

        return true;
    }

    /// <summary>
    /// Turns the three selected points into a real circle. Returns false unless there
    /// are three of them, they are not already circled, and they are not collinear —
    /// three points in a straight line have no finite circle through them.
    /// </summary>
    public bool TryCircleSelectedPoints()
    {
        if (selectedPointIds.Count != 3) return false;

        Guid aId = selectedPointIds[0];
        Guid bId = selectedPointIds[1];
        Guid cId = selectedPointIds[2];

        if (geometry.FindCircleThrough(aId, bId, cId) is not null) return false;
        if (!TryGetSelectionCircle(out _, out _)) return false;

        Edit(() =>
        {
            geometry.AddCircle(aId, bId, cId);

            // Nothing chains off a circle, so the selection is spent.
            selectedPointIds.Clear();
            selectedLineId = null;
            selectedCircleId = null;
        });

        return true;
    }

    /// <summary>
    /// The circle the three selected points would make. False when there are not three,
    /// or when they are collinear.
    /// </summary>
    private bool TryGetSelectionCircle(out Point center, out double radius)
    {
        center = default;
        radius = 0;

        if (selectedPointIds.Count != 3) return false;

        ConstructionPoint? a = geometry.FindPoint(selectedPointIds[0]);
        ConstructionPoint? b = geometry.FindPoint(selectedPointIds[1]);
        ConstructionPoint? c = geometry.FindPoint(selectedPointIds[2]);
        if (a is null || b is null || c is null) return false;

        return GeometryMathHelper.TryGetCircumcircle(
            a.Position, b.Position, c.Position, out center, out radius);
    }

    /// <summary>Drops selection entries whose point, line, or circle has since been deleted.</summary>
    private void PruneSelection()
    {
        selectedPointIds.RemoveAll(id => geometry.FindPoint(id) is null);

        if (selectedLineId is Guid lineId && geometry.FindLine(lineId) is null)
            selectedLineId = null;

        if (selectedCircleId is Guid circleId && geometry.FindCircle(circleId) is null)
            selectedCircleId = null;
    }

    #endregion

    #region Rendering

    /// <summary>Re-solves and redraws everything. Cheap enough to run on every mouse move.</summary>
    public void Refresh()
    {
        // Kept derived points track their parents, so they must be re-fitted before
        // anything reads a position.
        geometry.RefreshDerivedPoints();

        Solve();
        SolveFaces();
        RenderLines();
        RenderCircles();
        RenderPoints();
        RenderDerivedCandidates();
        RenderGhostLine();
        RenderShape();
        RenderFaces();
        RenderMeasurementLabels();
        UpdateDisplay();
        ConstructionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Solve()
    {
        List<(Guid Id, Point Start, Point End)> lines = geometry.GetResolvedLines();
        List<Point> positions = [.. geometry.Points.Select(p => p.Position)];

        ConstructionSolver.SolveResult result = ConstructionSolver.Solve(lines, positions);
        solveStatus = result.Status;
        solvedRing = result.Ring;
    }

    /// <summary>
    /// Re-derives every bounded cell of the arrangement. Selection is kept across the
    /// re-solve by matching on each face's centroid rather than its list index, since nothing
    /// guarantees a face keeps the same index once the geometry it comes from changes.
    /// </summary>
    private void SolveFaces()
    {
        List<(Guid Id, Point Start, Point End)> lines = geometry.GetResolvedLines();
        List<ConstructionFace> newFaces = lines.Count >= 3
            ? ConstructionFaceSolver.SolveFaces(lines, GetConstructionBounds())
            : [];

        if (selectedFaceIndices.Count > 0)
        {
            HashSet<(long X, long Y)> selectedCentroids = [];
            foreach (int index in selectedFaceIndices)
            {
                if (index < 0 || index >= faces.Count) continue;
                selectedCentroids.Add(CentroidKey(faces[index].Ring));
            }

            selectedFaceIndices.Clear();
            for (int i = 0; i < newFaces.Count; i++)
            {
                if (selectedCentroids.Contains(CentroidKey(newFaces[i].Ring)))
                    selectedFaceIndices.Add(i);
            }
        }

        faces = newFaces;
    }

    private static (long X, long Y) CentroidKey(IReadOnlyList<Point> ring)
    {
        Point centroid = ConstructionSolver.Centroid(ring);
        return ((long)Math.Round(centroid.X), (long)Math.Round(centroid.Y));
    }

    /// <summary>
    /// Region extended lines may run through: the image, the placed points, and any solved
    /// corners, inflated a little so a corner just outside is still comfortably visible.
    /// </summary>
    private Rect GetConstructionBounds()
    {
        Rect bounds = ImageBounds;

        foreach (ConstructionPoint point in geometry.Points)
            bounds.Union(point.Position);

        foreach (Point corner in solvedRing)
        {
            if (double.IsNaN(corner.X) || double.IsNaN(corner.Y)) continue;
            bounds.Union(corner);
        }

        if (bounds.Width <= 0 || bounds.Height <= 0)
            bounds = new Rect(0, 0, 1000, 1000);

        bounds.Inflate(bounds.Width * BoundsInflation, bounds.Height * BoundsInflation);
        return bounds;
    }

    private void RenderLines()
    {
        foreach (Path path in linePaths)
            MeasurementCanvas.Children.Remove(path);
        linePaths.Clear();

        foreach (Path path in hitPaths)
            MeasurementCanvas.Children.Remove(path);
        hitPaths.Clear();

        Rect bounds = GetConstructionBounds();
        Guid? highlightedLineId = EffectiveSelectedLineId;

        foreach (ConstructionLine line in geometry.Lines)
        {
            ConstructionPoint? start = geometry.FindPoint(line.StartPointId);
            ConstructionPoint? end = geometry.FindPoint(line.EndPointId);
            if (start is null || end is null) continue;

            bool isSelected = line.Id == highlightedLineId;

            if (line.IsExtended &&
                TryClipToBounds(start.Position, end.Position, bounds, out Point clipStart, out Point clipEnd))
            {
                // Dashed past the two owned points reads as "inferred"; the corner it
                // makes with a neighbouring edge is what the user is actually aiming at.
                AddLinePath(BuildLinePath(clipStart, clipEnd, dashed: true, isSelected), line.Id);
            }

            AddLinePath(BuildLinePath(start.Position, end.Position, dashed: false, isSelected), line.Id);

            Path hitPath = BuildHitPath(start.Position, end.Position, bounds, line);
            Panel.SetZIndex(hitPath, HitZIndex);
            hitPaths.Add(hitPath);
            MeasurementCanvas.Children.Add(hitPath);
        }
    }

    private Path BuildLinePath(Point from, Point to, bool dashed, bool isSelected)
    {
        Path path = new()
        {
            Stroke = isSelected ? SelectionBrush : LineBrush,
            StrokeThickness = BaseStrokeThickness * (isSelected ? 2 : 1) * visualScale,
            Opacity = dashed ? 0.55 : 0.9,
            IsHitTestVisible = false,
            Data = new LineGeometry(from, to)
        };

        if (dashed)
            path.StrokeDashArray = [6, 4];

        return path;
    }

    /// <summary>
    /// An invisible thick path so a 2px line is comfortable to right-click.
    /// </summary>
    private Path BuildHitPath(Point from, Point to, Rect bounds, ConstructionLine line)
    {
        Point hitFrom = from;
        Point hitTo = to;

        if (line.IsExtended && TryClipToBounds(from, to, bounds, out Point clipStart, out Point clipEnd))
        {
            hitFrom = clipStart;
            hitTo = clipEnd;
        }

        Path path = new()
        {
            Stroke = Brushes.Transparent,
            StrokeThickness = HitStrokeThickness * visualScale,
            Cursor = Cursors.Hand,
            Tag = line.Id,
            Data = new LineGeometry(hitFrom, hitTo),
            ToolTip = "Click to select this line, then press Delete to remove it"
        };

        path.MouseDown += LineHitPath_MouseDown;
        path.ContextMenu = BuildLineContextMenu(line);
        return path;
    }

    /// <summary>
    /// The faint shape the current selection is offering: a line between two selected
    /// points, or the circle through three. Clicking it is what turns the offer into a
    /// real one, so building never competes with dragging.
    /// </summary>
    private void RenderGhostLine()
    {
        if (ghostLinePath is not null) MeasurementCanvas.Children.Remove(ghostLinePath);
        if (ghostHitPath is not null) MeasurementCanvas.Children.Remove(ghostHitPath);
        ghostLinePath = null;
        ghostHitPath = null;

        if (TryBuildGhostGeometry() is not (Geometry shape, string tooltip))
            return;

        Visibility visibility = areDragGizmosVisible ? Visibility.Visible : Visibility.Collapsed;

        ghostLinePath = new Path
        {
            Stroke = SelectionBrush,
            StrokeThickness = BaseStrokeThickness * visualScale,
            StrokeDashArray = [4, 3],
            Opacity = 0.45,
            IsHitTestVisible = false,
            Data = shape,
            Visibility = visibility
        };
        Panel.SetZIndex(ghostLinePath, LineZIndex);
        MeasurementCanvas.Children.Add(ghostLinePath);

        ghostHitPath = new Path
        {
            Stroke = Brushes.Transparent,
            StrokeThickness = HitStrokeThickness * visualScale,
            Cursor = Cursors.Hand,
            Data = shape,
            ToolTip = tooltip,
            Visibility = visibility
        };
        ghostHitPath.MouseDown += GhostLine_MouseDown;

        // Below the point handles so the defining points stay grabbable.
        Panel.SetZIndex(ghostHitPath, HitZIndex);
        MeasurementCanvas.Children.Add(ghostHitPath);
    }

    /// <summary>
    /// What the selection is currently offering to build, or null when it is offering
    /// nothing — too few points, a pair or triple that already exists, or three points
    /// in a straight line.
    /// </summary>
    private (Geometry Shape, string ToolTip)? TryBuildGhostGeometry()
    {
        if (selectedPointIds.Count == 2)
        {
            ConstructionPoint? start = geometry.FindPoint(selectedPointIds[0]);
            ConstructionPoint? end = geometry.FindPoint(selectedPointIds[1]);
            if (start is null || end is null) return null;

            // Already joined — the existing line is highlighted instead, and a second
            // line between the same two points would only duplicate it.
            if (geometry.FindLineBetween(start.Id, end.Id) is not null) return null;

            return (new LineGeometry(start.Position, end.Position),
                "Click to connect these two points");
        }

        if (selectedPointIds.Count == 3)
        {
            if (geometry.FindCircleThrough(
                    selectedPointIds[0], selectedPointIds[1], selectedPointIds[2]) is not null)
                return null;

            if (!TryGetSelectionCircle(out Point center, out double radius))
                return null;

            return (new EllipseGeometry(center, radius, radius),
                "Click to draw the circle through these three points");
        }

        return null;
    }

    /// <summary>
    /// Draws each circle from the centre and radius its three points imply. A circle
    /// whose points have drifted into a straight line simply drops out until they are
    /// moved apart again — it is re-fitted from the points on every refresh.
    /// </summary>
    private void RenderCircles()
    {
        foreach (Path path in circlePaths)
            MeasurementCanvas.Children.Remove(path);
        circlePaths.Clear();

        foreach (Path path in circleHitPaths)
            MeasurementCanvas.Children.Remove(path);
        circleHitPaths.Clear();

        Guid? highlightedCircleId = EffectiveSelectedCircleId;

        foreach ((Guid id, Point center, double radius) in geometry.GetResolvedCircles())
        {
            bool isSelected = id == highlightedCircleId;

            Path path = new()
            {
                Stroke = isSelected ? SelectionBrush : LineBrush,
                StrokeThickness = BaseStrokeThickness * (isSelected ? 2 : 1) * visualScale,
                Opacity = 0.9,
                IsHitTestVisible = false,
                Data = new EllipseGeometry(center, radius, radius)
            };
            Panel.SetZIndex(path, LineZIndex);
            circlePaths.Add(path);
            MeasurementCanvas.Children.Add(path);

            Path hitPath = new()
            {
                Stroke = Brushes.Transparent,
                StrokeThickness = HitStrokeThickness * visualScale,
                Cursor = Cursors.Hand,
                Tag = id,
                Data = new EllipseGeometry(center, radius, radius),
                ToolTip = "Click to select this circle, then press Delete to remove it",
                ContextMenu = BuildCircleContextMenu(id)
            };
            hitPath.MouseDown += CircleHitPath_MouseDown;
            Panel.SetZIndex(hitPath, HitZIndex);
            circleHitPaths.Add(hitPath);
            MeasurementCanvas.Children.Add(hitPath);
        }
    }

    private ContextMenu BuildCircleContextMenu(Guid circleId)
    {
        ContextMenu menu = new();

        MenuItem showMeasurement = new()
        {
            Header = "Show Measurement",
            IsCheckable = true,
            IsChecked = IsCircleMeasurementVisible(circleId),
            Tag = circleId,
            ToolTip = "Label this circle with its radius, circumference, and area"
        };
        showMeasurement.Click += CircleMeasurementMenuItem_Click;

        MenuItem delete = new()
        {
            Header = "Delete Circle",
            Tag = circleId,
            ToolTip = "Delete this circle (its three points are kept)"
        };
        delete.Click += CircleDeleteMenuItem_Click;

        menu.Items.Add(showMeasurement);
        menu.Items.Add(delete);
        return menu;
    }

    private ContextMenu BuildLineContextMenu(ConstructionLine line)
    {
        ContextMenu menu = new();

        MenuItem extend = new()
        {
            Header = "Extend to construction edges",
            IsCheckable = true,
            IsChecked = line.IsExtended,
            Tag = line.Id,
            ToolTip = "Draw this line past its points so the corners it forms are visible"
        };
        extend.Click += LineExtendMenuItem_Click;

        MenuItem showMeasurement = new()
        {
            Header = "Show Measurement",
            IsCheckable = true,
            IsChecked = line.ShowMeasurement,
            Tag = line.Id,
            ToolTip = "Label this line with the distance between its two points"
        };
        showMeasurement.Click += LineMeasurementMenuItem_Click;

        MenuItem delete = new()
        {
            Header = "Delete Line",
            Tag = line.Id,
            ToolTip = "Delete this line (its points are kept)"
        };
        delete.Click += LineDeleteMenuItem_Click;

        menu.Items.Add(extend);
        menu.Items.Add(showMeasurement);
        menu.Items.Add(delete);
        return menu;
    }

    private void AddLinePath(Path path, Guid lineId)
    {
        path.Tag = lineId;
        Panel.SetZIndex(path, LineZIndex);
        linePaths.Add(path);
        MeasurementCanvas.Children.Add(path);
    }

    /// <summary>
    /// Liang-Barsky clip of the infinite line through two points against a rectangle.
    /// </summary>
    private static bool TryClipToBounds(Point a, Point b, Rect bounds, out Point start, out Point end)
    {
        start = a;
        end = b;

        double dx = b.X - a.X;
        double dy = b.Y - a.Y;

        if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9) return false;

        double tMin = double.NegativeInfinity;
        double tMax = double.PositiveInfinity;

        (double p, double q)[] edges =
        [
            (-dx, a.X - bounds.Left),
            (dx, bounds.Right - a.X),
            (-dy, a.Y - bounds.Top),
            (dy, bounds.Bottom - a.Y)
        ];

        foreach ((double p, double q) in edges)
        {
            if (Math.Abs(p) < 1e-9)
            {
                if (q < 0) return false; // Parallel to this edge and outside it.
                continue;
            }

            double t = q / p;
            if (p < 0) tMin = Math.Max(tMin, t);
            else tMax = Math.Min(tMax, t);
        }

        if (tMin >= tMax) return false;

        start = new Point(a.X + (tMin * dx), a.Y + (tMin * dy));
        end = new Point(a.X + (tMax * dx), a.Y + (tMax * dy));
        return true;
    }

    private void RenderPoints()
    {
        // Rebuild handles only when the count changed; otherwise just reposition, so a
        // drag does not churn the visual tree on every mouse move.
        if (pointHandles.Count != geometry.Points.Count)
        {
            foreach (Ellipse handle in pointHandles)
                MeasurementCanvas.Children.Remove(handle);
            pointHandles.Clear();

            for (int i = 0; i < geometry.Points.Count; i++)
                CreatePointHandle(i);
        }

        for (int i = 0; i < pointHandles.Count; i++)
        {
            // Tags carry the list index for the host's drag pipeline, so they must be
            // rewritten after any removal shifts the list.
            pointHandles[i].Tag = i.ToString();
            ApplyPointAppearance(i);
        }
    }

    /// <summary>
    /// Sizes and styles one handle for its current selection state, and positions it.
    /// A selected point reads as a distinctly different object — bigger, orange, and
    /// carrying the move cursor — because only a selected point can be dragged.
    /// </summary>
    private void ApplyPointAppearance(int index)
    {
        if (index < 0 || index >= pointHandles.Count || index >= geometry.Points.Count) return;

        ConstructionPoint point = geometry.Points[index];
        Ellipse handle = pointHandles[index];

        bool isSelected = selectedPointIds.Contains(point.Id);
        double size = CurrentPointSize() * (isSelected ? SelectedPointScale : 1.0);

        handle.Width = size;
        handle.Height = size;
        handle.StrokeThickness = (isSelected ? 2 : 1) * visualScale;
        handle.Opacity = isSelected ? 1.0 : 0.85;

        if (point.IsDerived)
        {
            // Hollow, so a computed point never looks like one you can drag.
            handle.Fill = Brushes.White;
            handle.Stroke = isSelected ? SelectionBrush : PointBrush;
            handle.StrokeThickness = (isSelected ? 3 : 2) * visualScale;
            handle.Cursor = Cursors.Hand;
            handle.ToolTip = point.Source == ConstructionPointSource.CircleCenter
                ? "Circle centre. Follows its circle; cannot be dragged."
                : "Line crossing. Follows its lines; cannot be dragged.";
        }
        else
        {
            handle.Fill = isSelected ? SelectionBrush : PointBrush;
            handle.Stroke = Brushes.White;
            handle.Cursor = isSelected ? Cursors.SizeAll : Cursors.Hand;
            handle.ToolTip = isSelected
                ? "Drag to move. Select another point to connect them, or two more for a circle."
                : "Click to select. A point only moves once selected.";
        }

        PositionHandle(handle, point.Position);
    }

    private void CreatePointHandle(int index)
    {
        double size = CurrentPointSize();

        Ellipse handle = new()
        {
            Width = size,
            Height = size,
            Fill = PointBrush,
            Stroke = Brushes.White,
            StrokeThickness = 1 * visualScale,
            Opacity = 0.85,
            Cursor = Cursors.Hand,
            Tag = index.ToString(),
            ToolTip = "Click to select. A point only moves once selected.",
            Visibility = areDragGizmosVisible ? Visibility.Visible : Visibility.Collapsed
        };

        handle.MouseDown += PointHandle_MouseDown;
        handle.ContextMenu = BuildPointContextMenu();
        Panel.SetZIndex(handle, PointZIndex);

        pointHandles.Add(handle);
        MeasurementCanvas.Children.Add(handle);
    }

    /// <summary>
    /// Draws the crossings and centres the construction implies as faint hollow rings.
    /// They are offers, not geometry: they follow the lines and circles that produce
    /// them, and vanish with them unless the user clicks one to keep it.
    /// </summary>
    private void RenderDerivedCandidates()
    {
        foreach (Ellipse handle in candidateHandles)
            MeasurementCanvas.Children.Remove(handle);
        candidateHandles.Clear();

        derivedCandidates = [];

        // They are gizmos, so an export must not show them.
        if (!areDragGizmosVisible) return;

        Rect bounds = GetConstructionBounds();

        foreach (DerivedPointCandidate candidate in
                 geometry.GetDerivedCandidates(CandidateMergeTolerance))
        {
            // A pair of near-parallel lines crosses somewhere far away that the user is
            // not looking at and cannot reach.
            if (!bounds.Contains(candidate.Position)) continue;

            derivedCandidates.Add(candidate);
            CreateCandidateHandle(derivedCandidates.Count - 1, candidate);
        }
    }

    /// <summary>
    /// How close a candidate has to be to a real point to count as the same place.
    /// Divided by the zoom so it means a constant distance on screen.
    /// </summary>
    private double CandidateMergeTolerance => 8 * visualScale;

    private void CreateCandidateHandle(int index, DerivedPointCandidate candidate)
    {
        double size = BasePointSize * 0.85 * visualScale;

        Ellipse handle = new()
        {
            Width = size,
            Height = size,

            // Transparent rather than null: it still takes the click, so the whole disc
            // is a target and not just the ring.
            Fill = Brushes.Transparent,
            Stroke = LineBrush,
            StrokeThickness = 1.5 * visualScale,
            StrokeDashArray = [2, 2],
            Opacity = 0.6,
            Cursor = Cursors.Hand,
            Tag = index,
            ToolTip = candidate.Source == ConstructionPointSource.CircleCenter
                ? "Circle centre — click to keep it as a point"
                : "Where two lines cross — click to keep it as a point"
        };

        handle.MouseDown += CandidateHandle_MouseDown;
        Panel.SetZIndex(handle, CandidateZIndex);

        candidateHandles.Add(handle);
        MeasurementCanvas.Children.Add(handle);

        Canvas.SetLeft(handle, candidate.Position.X - (size / 2));
        Canvas.SetTop(handle, candidate.Position.Y - (size / 2));
    }

    private ContextMenu BuildPointContextMenu()
    {
        ContextMenu menu = new();

        MenuItem deselect = new()
        {
            Header = "Clear Selection",
            ToolTip = "Deselect all points and lines"
        };
        deselect.Click += ClearSelectionMenuItem_Click;

        MenuItem delete = new()
        {
            Header = "Delete Point",
            ToolTip = "Delete this point and any lines that use it"
        };
        delete.Click += PointDeleteMenuItem_Click;

        menu.Items.Add(deselect);
        menu.Items.Add(delete);
        return menu;
    }

    private Ellipse CreateBoundaryCandidate()
    {
        Ellipse marker = new()
        {
            // A preview of where the click will land, so it must never eat the click.
            IsHitTestVisible = false
        };

        Panel.SetZIndex(marker, PointZIndex);
        MeasurementCanvas.Children.Add(marker);
        return marker;
    }

    private void ApplyBoundaryCandidateAppearance()
    {
        if (boundaryCandidate is null) return;

        if (boundaryCandidatePosition is not Point position || !areDragGizmosVisible)
        {
            boundaryCandidate.Visibility = Visibility.Collapsed;
            return;
        }

        double size = CurrentPointSize() * SelectedPointScale;

        boundaryCandidate.Width = size;
        boundaryCandidate.Height = size;
        boundaryCandidate.Stroke = SelectionBrush;
        boundaryCandidate.Visibility = Visibility.Visible;

        if (boundaryCandidateIsWeak)
        {
            boundaryCandidate.Fill = null;
            boundaryCandidate.StrokeThickness = 2 * visualScale;
            boundaryCandidate.StrokeDashArray = [2, 2];
        }
        else
        {
            boundaryCandidate.Fill = SelectionBrush;
            boundaryCandidate.Stroke = Brushes.White;
            boundaryCandidate.StrokeThickness = 2 * visualScale;
            boundaryCandidate.StrokeDashArray = null;
        }

        PositionHandle(boundaryCandidate, position);
    }

    private double CurrentPointSize() =>
        (areEndpointCapsVisible ? BaseSmallPointSize : BasePointSize) * visualScale;

    private static void PositionHandle(Ellipse handle, Point position)
    {
        Canvas.SetLeft(handle, position.X - (handle.Width / 2));
        Canvas.SetTop(handle, position.Y - (handle.Height / 2));
    }

    private void ApplyPointSizes()
    {
        for (int i = 0; i < pointHandles.Count && i < geometry.Points.Count; i++)
            ApplyPointAppearance(i);
    }

    /// <summary>
    /// Counter-scales handles and strokes against the canvas zoom so gizmos keep a
    /// constant size on screen. Mirrors MainWindow.UpdateTransformVisualScale.
    /// </summary>
    public void UpdateVisualScale(double inverseScale)
    {
        visualScale = inverseScale <= 0 ? 1.0 : inverseScale;

        ApplyPointSizes();
        ShapePath.StrokeThickness = BaseStrokeThickness * visualScale;
        PreviewLine.StrokeThickness = BaseStrokeThickness * visualScale;

        // Rebuilt rather than patched in place: a selected line or circle carries a
        // different thickness, so there is no single multiplier to reapply here.
        RenderLines();
        RenderCircles();
        RenderDerivedCandidates();
        RenderGhostLine();
        RenderFaces();
        RenderMeasurementLabels();
        ApplyBoundaryCandidateAppearance();

        MeasurementText.RenderTransformOrigin = new Point(0.5, 0.5);
        MeasurementText.RenderTransform = new ScaleTransform(visualScale, visualScale);
    }

    private void RenderShape()
    {
        if (solvedRing.Count < 3)
        {
            ShapePath.Data = null;
            return;
        }

        PathFigure figure = new() { StartPoint = solvedRing[0], IsClosed = true, IsFilled = true };
        for (int i = 1; i < solvedRing.Count; i++)
            figure.Segments.Add(new LineSegment(solvedRing[i], true));

        PathGeometry pathGeometry = new();
        pathGeometry.Figures.Add(figure);
        ShapePath.Data = pathGeometry;
    }

    /// <summary>
    /// Draws a hit-testable, mostly-invisible fill over every bounded cell so it can be
    /// hovered and clicked. Rebuilt wholesale each refresh, same as the lines above — the
    /// cells themselves can appear, disappear, split, or merge as the geometry changes, so
    /// there is no single element per cell to patch in place.
    /// </summary>
    private void RenderFaces()
    {
        foreach (Path path in facePaths)
            MeasurementCanvas.Children.Remove(path);
        facePaths.Clear();

        for (int i = 0; i < faces.Count; i++)
        {
            ConstructionFace face = faces[i];

            PathFigure figure = new() { StartPoint = face.Ring[0], IsClosed = true, IsFilled = true };
            for (int v = 1; v < face.Ring.Count; v++)
                figure.Segments.Add(new LineSegment(face.Ring[v], true));

            PathGeometry pathGeometry = new();
            pathGeometry.Figures.Add(figure);

            Path path = new()
            {
                Data = pathGeometry,
                StrokeThickness = 1.5 * visualScale,
                Cursor = Cursors.Hand,
                Tag = i,
                IsHitTestVisible = isFaceSelectionModeActive,
                ToolTip = "Click to select this shape. Selecting shapes that border each other merges them into one polygon."
            };

            Panel.SetZIndex(path, FaceZIndex);
            path.MouseEnter += FacePath_MouseEnter;
            path.MouseLeave += FacePath_MouseLeave;
            path.MouseDown += FacePath_MouseDown;

            facePaths.Add(path);
            MeasurementCanvas.Children.Add(path);
        }

        UpdateFaceVisuals();
    }

    private void FacePath_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!isFaceSelectionModeActive || sender is not Path { Tag: int index }) return;

        hoveredFaceIndex = index;
        UpdateFaceVisuals();
    }

    private void FacePath_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Path { Tag: int index }) return;

        if (hoveredFaceIndex == index)
            hoveredFaceIndex = null;

        UpdateFaceVisuals();
    }

    private void FacePath_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!isFaceSelectionModeActive || sender is not Path { Tag: int index }) return;

        if (!selectedFaceIndices.Remove(index))
            selectedFaceIndices.Add(index);

        UpdateFaceVisuals();
        FaceSelectionChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void UpdateFaceVisuals()
    {
        for (int i = 0; i < facePaths.Count; i++)
        {
            bool isSelected = selectedFaceIndices.Contains(i);
            bool isHovered = hoveredFaceIndex == i;

            facePaths[i].Fill = isSelected ? FaceSelectedBrush : isHovered ? FaceHoverBrush : FaceIdleBrush;
            facePaths[i].Stroke = isSelected ? SelectionBrush : Brushes.Transparent;
        }
    }

    /// <summary>
    /// Draws a readout beside every line and circle the user has opted into. Rebuilt
    /// wholesale each refresh: the text depends on positions that move on every drag,
    /// so patching individual labels would only duplicate the work of recreating them.
    /// </summary>
    private void RenderMeasurementLabels()
    {
        foreach (Border label in measurementLabels)
            MeasurementCanvas.Children.Remove(label);
        measurementLabels.Clear();

        foreach (ConstructionLine line in geometry.Lines)
        {
            if (!line.ShowMeasurement) continue;

            ConstructionPoint? start = geometry.FindPoint(line.StartPointId);
            ConstructionPoint? end = geometry.FindPoint(line.EndPointId);
            if (start is null || end is null) continue;

            double length = GeometryMathHelper.Distance(start.Position, end.Position) * ScaleFactor;
            Point midpoint = new(
                (start.Position.X + end.Position.X) / 2,
                (start.Position.Y + end.Position.Y) / 2);

            AddMeasurementLabel($"{length:N2} {Units}", midpoint);
        }

        foreach ((Guid id, Point center, double radius) in geometry.GetResolvedCircles())
        {
            if (geometry.FindCircle(id)?.ShowMeasurement != true) continue;

            AddMeasurementLabel(BuildCircleText(radius), center);
        }
    }

    /// <summary>
    /// Places a readout centred just above <paramref name="anchor"/>. The label is
    /// measured up front because it is created fresh each refresh and so has no
    /// ActualWidth to centre on yet.
    /// </summary>
    private void AddMeasurementLabel(string text, Point anchor)
    {
        Border label = new()
        {
            Padding = new Thickness(4, 1, 4, 1),
            Background = new SolidColorBrush(Color.FromArgb(0x7F, 0, 0, 0)),
            CornerRadius = new CornerRadius(3),

            // Purely a readout: it must never intercept a click aimed at the geometry.
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            }
        };

        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size size = label.DesiredSize;

        label.RenderTransformOrigin = new Point(0.5, 0.5);
        label.RenderTransform = new ScaleTransform(visualScale, visualScale);

        Canvas.SetLeft(label, anchor.X - (size.Width / 2));
        Canvas.SetTop(label, anchor.Y - (size.Height / 2));

        Panel.SetZIndex(label, TextZIndex);
        measurementLabels.Add(label);
        MeasurementCanvas.Children.Add(label);
    }

    #endregion

    #region Readout

    private void UpdateDisplay()
    {
        ConstructionTextBlock.Text = BuildMeasurementText();
        PositionMeasurementText();
    }

    private string BuildMeasurementText()
    {
        string primary = BuildPrimaryText();
        string? hint = BuildSelectionHint();

        return hint is null ? primary : $"{primary}\n{hint}";
    }

    private string BuildPrimaryText()
    {
        // A picked circle is what the user is looking at, so it wins the readout.
        if (EffectiveSelectedCircleId is Guid selectedId &&
            TryGetResolvedCircleRadius(selectedId, out double selectedRadius))
            return BuildCircleText(selectedRadius);

        if (solvedRing.Count >= 3)
        {
            // The readout is hidden but the label is not: it stays as a small anchor so
            // its context menu is still reachable to switch the measurement back on.
            if (!showShapeMeasurement)
                return "Construction";

            double perimeter = GeometryMathHelper.PolygonPerimeter(solvedRing, isClosed: true) * ScaleFactor;
            double area = GeometryMathHelper.PolygonArea(solvedRing) * ScaleFactor * ScaleFactor;
            return MeasurementFormattingHelper.FormatPerimeterArea(perimeter, area, Units);
        }

        // No derived shape to report on, but a circle is a measurement in its own right.
        if (geometry.Lines.Count == 0 &&
            geometry.GetResolvedCircles() is { Count: > 0 } circles)
            return BuildCircleText(circles[0].Radius);

        return solveStatus switch
        {
            ConstructionSolver.SolveStatus.NotEnoughLines when geometry.Lines.Count == 0 && geometry.Points.Count > 0 =>
                "Click a point to select it",
            ConstructionSolver.SolveStatus.NotEnoughLines when geometry.Lines.Count == 0 =>
                "Drag along an edge to add a line",
            ConstructionSolver.SolveStatus.NotEnoughLines =>
                $"Add {3 - geometry.Lines.Count} more line(s) to form a shape",
            ConstructionSolver.SolveStatus.NoUsableCorners =>
                "Lines are parallel — no corner formed",
            ConstructionSolver.SolveStatus.SelfIntersecting =>
                "Lines cross — check the construction",
            ConstructionSolver.SolveStatus.Degenerate =>
                "Shape has collapsed — move a point",
            _ => "No shape yet"
        };
    }

    /// <summary>
    /// Radius of one circle as currently fitted, or false when its points have gone
    /// collinear and it has no finite circle right now.
    /// </summary>
    private bool TryGetResolvedCircleRadius(Guid circleId, out double radius)
    {
        foreach ((Guid id, Point _, double resolvedRadius) in geometry.GetResolvedCircles())
        {
            if (id != circleId) continue;

            radius = resolvedRadius;
            return true;
        }

        radius = 0;
        return false;
    }

    /// <summary>Matches the standalone circle measurement tool's readout.</summary>
    private string BuildCircleText(double radius)
    {
        double scaledRadius = radius * ScaleFactor;
        double circumference = 2 * Math.PI * scaledRadius;
        double area = Math.PI * scaledRadius * scaledRadius;

        return $"r: {scaledRadius:N2} {Units}, C: {circumference:N2} {Units}, A: {area:N2} {Units}²";
    }

    /// <summary>
    /// Narrates the build gesture. The faint shape is only discoverable if something
    /// says it is there, so the readout doubles as the prompt for the next step.
    /// </summary>
    private string? BuildSelectionHint()
    {
        // A note about the gesture that just happened outranks a prompt for the next one.
        if (transientHint is not null)
            return transientHint;

        if (selectedLineId is not null)
            return "Line selected — press Delete to remove it";

        if (selectedCircleId is not null)
            return "Circle selected — press Delete to remove it";

        return selectedPointIds.Count switch
        {
            1 => "Select a second point to connect them",
            2 when EffectiveSelectedLineId is not null =>
                "Already connected — press Delete to disconnect, or select a third point for a circle",
            2 => "Click the faint line to connect them, or select a third point for a circle",
            3 when EffectiveSelectedCircleId is not null =>
                "Already circled — press Delete to remove the circle",
            3 when !TryGetSelectionCircle(out _, out _) =>
                "These three points are in a straight line — no circle through them",
            3 => "Click the faint circle to draw it",
            _ => null
        };
    }

    private void PositionMeasurementText()
    {
        Point centre = solvedRing.Count >= 3
            ? ConstructionSolver.Centroid(solvedRing)
            : ConstructionSolver.Centroid([.. geometry.Points.Select(p => p.Position)]);

        if (geometry.Points.Count == 0 && solvedRing.Count == 0)
        {
            MeasurementText.Visibility = Visibility.Collapsed;
            return;
        }

        MeasurementText.Visibility = Visibility.Visible;
        Canvas.SetLeft(MeasurementText, centre.X - (MeasurementText.ActualWidth / 2));
        Canvas.SetTop(MeasurementText, centre.Y - MeasurementText.ActualHeight - (10 * visualScale));
    }

    #endregion

    #region Input

    private void PointHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse handle || handle.Tag is not string indexString) return;

        // Right-click belongs to the context menu.
        if (e.ChangedButton != MouseButton.Left) return;

        if (!int.TryParse(indexString, out int index)) return;
        if (index < 0 || index >= geometry.Points.Count) return;

        Guid pointId = geometry.Points[index].Id;

        // Selection has to come first: without it, aiming at a point to pick it nudges
        // the point instead, which makes connecting two points nearly impossible.
        if (!selectedPointIds.Contains(pointId))
        {
            SelectPoint(pointId);
            e.Handled = true;
            return;
        }

        // A derived point's position is computed from its parents, so there is nothing
        // to drag — move the geometry that defines it instead.
        if (geometry.Points[index].IsDerived)
        {
            e.Handled = true;
            return;
        }

        pointDraggingIndex = index;
        MeasurementPointMouseDown?.Invoke(sender, e);
        e.Handled = true;
    }

    private void CandidateHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (sender is not Ellipse handle || handle.Tag is not int index) return;
        if (index < 0 || index >= derivedCandidates.Count) return;

        // Captured before the edit, which rebuilds the candidate list underneath us.
        DerivedPointCandidate candidate = derivedCandidates[index];

        e.Handled = true;

        Guid keptId = Guid.Empty;
        Edit(() => keptId = geometry.KeepDerivedPoint(candidate));

        // Keeping it is also picking it, so it can go straight into a line or a circle.
        if (keptId != Guid.Empty)
            SelectPoint(keptId);
    }

    private void GhostLine_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        e.Handled = true;

        // Whichever of the two the selection is offering; only one can apply, since they
        // need a different number of points.
        if (!TryConnectSelectedPoints())
            TryCircleSelectedPoints();
    }

    private void LineHitPath_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (sender is not Path path || path.Tag is not Guid lineId) return;

        // Toggle, so a mis-click on a line is undone by clicking it again.
        selectedLineId = selectedLineId == lineId ? null : lineId;
        selectedCircleId = null;
        selectedPointIds.Clear();

        e.Handled = true;
        Refresh();
    }

    private void CircleHitPath_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (sender is not Path path || path.Tag is not Guid circleId) return;

        selectedCircleId = selectedCircleId == circleId ? null : circleId;
        selectedLineId = null;
        selectedPointIds.Clear();

        e.Handled = true;
        Refresh();
    }

    private void CircleDeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not Guid circleId) return;

        RemoveConstructionCircle(circleId);
    }

    private void ClearSelectionMenuItem_Click(object sender, RoutedEventArgs e) => ClearSelection();

    private void LineExtendMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not Guid lineId) return;

        SetLineExtended(lineId, item.IsChecked);
    }

    private void LineMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not Guid lineId) return;

        SetLineMeasurementVisible(lineId, item.IsChecked);
    }

    private void CircleMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not Guid circleId) return;

        SetCircleMeasurementVisible(circleId, item.IsChecked);
    }

    private void LineDeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not Guid lineId) return;

        RemoveConstructionLine(lineId);
    }

    private void PointDeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;

        // The menu's placement target is the handle it was opened from.
        if (item.Parent is not ContextMenu menu || menu.PlacementTarget is not Ellipse handle) return;
        if (handle.Tag is not string indexString || !int.TryParse(indexString, out int index)) return;
        if (index < 0 || index >= geometry.Points.Count) return;

        RemoveConstructionPoint(geometry.Points[index].Id);
    }

    private void CopyMeasurementMenuItem_Click(object sender, RoutedEventArgs e) =>
        Clipboard.SetText(ConstructionTextBlock.Text);

    private void ShapeMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;

        ShowShapeMeasurement = item.IsChecked;
    }

    private void MeasurementContextMenu_Opened(object sender, RoutedEventArgs e) =>
        ShowShapeMeasurementMenuItem.IsChecked = showShapeMeasurement;

    private void MeasurementButton_Click(object sender, RoutedEventArgs e)
    {
        ContextMenu? contextMenu = MeasurementText.ContextMenu;
        if (contextMenu is null) return;

        contextMenu.PlacementTarget = MeasurementText;
        contextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void RemoveMeasurementMenuItem_Click(object sender, RoutedEventArgs e) =>
        RemoveControlRequested?.Invoke(this, EventArgs.Empty);

    private async void ChangeColorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        Color? picked = await ColorPickerDialog.PickColorAsync(mainWindow, strokeColor, "Change Construction Color");
        if (picked is Color color)
            StrokeColor = color;
    }

    #endregion

    #region Persistence

    public ConstructionGeometryDto ToDto()
    {
        // The saved form is a geometry snapshot plus the readout settings the snapshot
        // deliberately leaves out.
        ConstructionGeometryDto dto = CaptureGeometry();
        dto.ScaleFactor = ScaleFactor;
        dto.Units = Units;
        dto.ShowShapeMeasurement = showShapeMeasurement;
        dto.StrokeColor = strokeColor.ToString();
        return dto;
    }

    public void FromDto(ConstructionGeometryDto dto)
    {
        // Loading a project is not an edit, so this deliberately bypasses the undo
        // transaction machinery.
        scaleFactor = dto.ScaleFactor;
        units = dto.Units;
        showShapeMeasurement = dto.ShowShapeMeasurement;

        // Absent from projects saved before this existed — leave the construction in its
        // just-constructed appearance (blue points/lines, orange shape) rather than
        // forcing every old project's shape from orange to blue.
        if (!string.IsNullOrEmpty(dto.StrokeColor))
        {
            try { StrokeColor = (Color)ColorConverter.ConvertFromString(dto.StrokeColor); }
            catch { /* Keep the just-constructed default on a corrupt value. */ }
        }

        RestoreGeometry(dto);
    }

    #endregion
}

/// <summary>
/// A completed construction edit, as the before/after snapshots the undo stack needs.
/// </summary>
public class ConstructionGeometryEditedEventArgs(
    ConstructionGeometryDto before,
    ConstructionGeometryDto after) : EventArgs
{
    public ConstructionGeometryDto Before { get; } = before;
    public ConstructionGeometryDto After { get; } = after;
}
