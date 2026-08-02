using MagickCrop.Controls;
using MagickCrop.Helpers;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MagickCrop;

/// <summary>
/// Parametric construction geometry: place points along an object's edges, connect them
/// into edge lines, and let the corners fall out of where those lines cross. Moving a
/// point re-solves the shape.
/// </summary>
public partial class MainWindow
{
    /// <summary>Identifies which construction tool the user has active, across both tabs.</summary>
    public enum ConstructionTool
    {
        None,
        Edge,
        Point,
        Line,
        Boundary
    }

    private const string ConstructionEdgeTag = "ConstructionEdge";
    private const string ConstructionPointTag = "ConstructionPoint";
    private const string ConstructionLineTag = "ConstructionLine";
    private const string ConstructionBoundaryTag = "ConstructionBoundary";

    /// <summary>Minimum drag distance before an edge drag counts as more than a click.</summary>
    private const double ConstructionDragThreshold = 5.0;

    private readonly ObservableCollection<ConstructionOverlayControl> constructionControls = [];

    private ConstructionOverlayControl? activeConstructionControl;

    /// <summary>The overlay currently accepting new points and lines.</summary>
    private ConstructionOverlayControl? constructionOverlay;

    /// <summary>The end point being dragged out while the Edge tool creates a line.</summary>
    private Guid? constructionDragEndPointId;
    private Guid? constructionDragLineId;

    /// <summary>Where the in-progress boundary probe was pressed, in canvas coordinates.</summary>
    private Point? boundaryProbeStart;

    // In-memory copy of the current image, kept so a probe can sample on every mouse move
    // without re-decoding the file. Keyed by the path it was built from, which is how it
    // invalidates itself: every edit writes a new temp file.
    private ImageSampleBuffer? probeBuffer;
    private string? probeBufferPath;
    private bool isWarmingProbeBuffer;

    #region Tool state

    private ConstructionTool ActiveConstructionTool
    {
        get
        {
            foreach (ToggleButton toggle in AllToolToggles())
            {
                if (toggle.IsChecked != true || toggle.Tag is not string tag) continue;

                switch (tag)
                {
                    case ConstructionEdgeTag: return ConstructionTool.Edge;
                    case ConstructionPointTag: return ConstructionTool.Point;
                    case ConstructionLineTag: return ConstructionTool.Line;
                    case ConstructionBoundaryTag: return ConstructionTool.Boundary;
                }
            }

            return ConstructionTool.None;
        }
    }

    private bool IsConstructionToolActive => ActiveConstructionTool != ConstructionTool.None;

    /// <summary>
    /// Grab radius in canvas units. Divided by the zoom so it stays constant on screen —
    /// at 5x zoom a 15px screen radius is only 3 canvas units.
    /// </summary>
    private double ConstructionHitTolerance =>
        Defaults.VertexCloseTolerance / Math.Max(MinZoom, canvasScale.ScaleX);

    #endregion

    #region Overlay lifecycle

    /// <summary>
    /// Returns the overlay to build into, creating and wiring it on first use.
    /// </summary>
    private ConstructionOverlayControl EnsureConstructionOverlay()
    {
        if (constructionOverlay is not null)
            return constructionOverlay;

        ConstructionOverlayControl overlay = new()
        {
            ScaleFactor = ScaleInput?.Value ?? 1.0,
            Units = MeasurementUnits?.Text ?? "pixels"
        };

        WireConstructionOverlay(overlay);

        constructionControls.Add(overlay);
        ShapeCanvas.Children.Add(overlay);
        constructionOverlay = overlay;

        UpdateConstructionImageBounds();
        UpdateConstructionVisualScale();
        SyncConstructionToolState();

        return overlay;
    }

    private void WireConstructionOverlay(ConstructionOverlayControl overlay)
    {
        overlay.MeasurementPointMouseDown += ConstructionPoint_MouseDown;
        overlay.RemoveControlRequested += ConstructionControl_RemoveControlRequested;
        overlay.ConstructionChanged += ConstructionOverlay_Changed;
        overlay.GeometryEdited += ConstructionOverlay_GeometryEdited;
    }

    private void UnwireConstructionOverlay(ConstructionOverlayControl overlay)
    {
        overlay.MeasurementPointMouseDown -= ConstructionPoint_MouseDown;
        overlay.RemoveControlRequested -= ConstructionControl_RemoveControlRequested;
        overlay.ConstructionChanged -= ConstructionOverlay_Changed;
        overlay.GeometryEdited -= ConstructionOverlay_GeometryEdited;
    }

    /// <summary>
    /// Every committed point/line edit lands here and becomes one undo step. The overlay
    /// hands over before/after snapshots; it does not know about the undo stack itself.
    /// </summary>
    private void ConstructionOverlay_GeometryEdited(object? sender, ConstructionGeometryEditedEventArgs e)
    {
        if (sender is not ConstructionOverlayControl overlay) return;

        UndoRedo.AddUndo(new ConstructionGeometryEditedItem(overlay, e.Before, e.After));
    }

    private void ConstructionControl_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is not ConstructionOverlayControl overlay) return;

        RemoveConstructionOverlays([overlay], recordUndo: true);
    }

    private void ConstructionOverlay_Changed(object? sender, EventArgs e) =>
        UpdateConstructionApplyButtons();

    /// <summary>
    /// Removes every construction overlay without an undo step. Called from the shared
    /// measurement teardown, where the whole undo stack is going away anyway.
    /// </summary>
    private void RemoveConstructionControls()
    {
        RemoveConstructionOverlays([.. constructionControls], recordUndo: false);
        ReleaseProbeBuffer();
    }

    /// <summary>
    /// Takes overlays off the canvas, optionally as an undoable step. The controls
    /// themselves are kept alive so an undo can put back the very same instances that
    /// earlier geometry undo items still reference.
    /// </summary>
    private void RemoveConstructionOverlays(List<ConstructionOverlayControl> overlays, bool recordUndo)
    {
        if (overlays.Count == 0) return;

        foreach (ConstructionOverlayControl overlay in overlays)
        {
            UnwireConstructionOverlay(overlay);
            constructionControls.Remove(overlay);
            ShapeCanvas.Children.Remove(overlay);
        }

        if (recordUndo)
        {
            UndoRedo.AddUndo(new ConstructionOverlaysRemovedItem(
                overlays,
                constructionControls,
                ShapeCanvas,
                WireConstructionOverlay,
                UnwireConstructionOverlay,
                AfterConstructionOverlaysRestored));
        }

        AfterConstructionOverlaysRestored();
    }

    /// <summary>
    /// Re-establishes the window's view of the overlays after they are added or removed,
    /// including which one new points and lines get built into.
    /// </summary>
    private void AfterConstructionOverlaysRestored()
    {
        constructionOverlay = constructionControls.FirstOrDefault();
        activeConstructionControl = null;
        constructionDragEndPointId = null;
        constructionDragLineId = null;

        UpdateConstructionImageBounds();
        UpdateConstructionVisualScale();
        SyncConstructionToolState();
        UpdateConstructionApplyButtons();
    }

    private void UpdateConstructionImageBounds()
    {
        if (MainImage is null) return;

        Rect bounds = new(0, 0,
            Math.Max(1, MainImage.ActualWidth),
            Math.Max(1, MainImage.ActualHeight));

        foreach (ConstructionOverlayControl overlay in constructionControls)
            overlay.ImageBounds = bounds;
    }

    /// <summary>
    /// Counter-scales construction gizmos against the canvas zoom, alongside the
    /// transform handles.
    /// </summary>
    private void UpdateConstructionVisualScale()
    {
        double inverseScale = 1.0 / Math.Max(MinZoom, canvasScale.ScaleX);

        foreach (ConstructionOverlayControl overlay in constructionControls)
            overlay.UpdateVisualScale(inverseScale);
    }

    #endregion

    #region Tool input

    /// <summary>
    /// Handles a canvas click for whichever construction tool is active.
    /// Returns true when the click was consumed.
    /// </summary>
    private bool HandleConstructionMouseDown(Point canvasPoint, MouseButtonEventArgs e)
    {
        switch (ActiveConstructionTool)
        {
            case ConstructionTool.Edge:
                StartConstructionEdgeDrag(canvasPoint);
                e.Handled = true;
                return true;

            case ConstructionTool.Point:
                PlaceConstructionPoint(canvasPoint);
                e.Handled = true;
                return true;

            case ConstructionTool.Line:
                HandleConstructionLineClick(canvasPoint);
                e.Handled = true;
                return true;

            case ConstructionTool.Boundary:
                StartBoundaryProbe(canvasPoint);
                e.Handled = true;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Begins a drag that lays down a whole edge: a start point (reusing an existing one
    /// if the press landed on it) and an end point that follows the cursor.
    /// </summary>
    private void StartConstructionEdgeDrag(Point canvasPoint)
    {
        ConstructionOverlayControl overlay = EnsureConstructionOverlay();
        double tolerance = ConstructionHitTolerance;

        // Starting a new edge abandons whatever was picked before it.
        overlay.ClearSelection();

        // Points, the line, and every position the loose end passes through are all one
        // undo step — the user laid down one edge.
        overlay.BeginDrag();

        Guid startId = overlay.FindPointNear(canvasPoint, tolerance) ?? overlay.AddPoint(canvasPoint);
        Guid endId = overlay.AddPoint(canvasPoint);

        constructionDragEndPointId = endId;
        constructionDragLineId = overlay.AddLine(startId, endId);

        draggingMode = DraggingMode.ConstructionEdgeCreate;
        isCreatingMeasurement = true;
        ShapeCanvas.CaptureMouse();
        ShowPixelZoom(canvasPoint);
    }

    private void PlaceConstructionPoint(Point canvasPoint)
    {
        ConstructionOverlayControl overlay = EnsureConstructionOverlay();

        // Landing on an existing point selects it rather than doing nothing — a click
        // aimed at a point should always mean "this one", however close it lands. This
        // is checked first so picking a second point to connect still works.
        if (overlay.TrySelectPointNear(canvasPoint, ConstructionHitTolerance))
            return;

        // Genuinely empty space: the click means "done with that selection".
        overlay.ClearSelection();
        overlay.AddPoint(canvasPoint);
        ShowPixelZoom(canvasPoint);
    }

    /// <summary>
    /// Two-click line creation. Each click picks (or creates) a point and hands it to the
    /// overlay's selection; because the tool sets ConnectOnSecondSelection, the second
    /// pick connects immediately and stays selected so the next click chains on.
    /// </summary>
    private void HandleConstructionLineClick(Point canvasPoint)
    {
        ConstructionOverlayControl overlay = EnsureConstructionOverlay();

        Guid pointId = overlay.FindPointNear(canvasPoint, ConstructionHitTolerance)
            ?? overlay.AddPoint(canvasPoint);

        overlay.SelectPoint(pointId);
        ShowPixelZoom(canvasPoint);
    }

    /// <summary>
    /// Drives the in-progress gesture on mouse move. Returns true when consumed.
    /// </summary>
    private bool HandleConstructionMouseMove(Point canvasPoint)
    {
        if (draggingMode == DraggingMode.ConstructionBoundaryProbe)
            return UpdateBoundaryProbe(canvasPoint);

        if (draggingMode == DraggingMode.ConstructionEdgeCreate &&
            constructionOverlay is not null &&
            constructionDragEndPointId is Guid dragEndId)
        {
            constructionOverlay.MoveConstructionPoint(dragEndId, canvasPoint);
            return true;
        }

        // Rubber band for the Line tool, driven off the selection rather than a parallel
        // copy of it, so it always points at the point that is actually highlighted.
        if (ActiveConstructionTool == ConstructionTool.Line &&
            constructionOverlay?.SingleSelectedPointPosition is Point start)
        {
            constructionOverlay.ShowPreviewLine(start, canvasPoint);
            return true;
        }

        constructionOverlay?.HidePreviewLine();
        return false;
    }

    /// <summary>
    /// Completes an edge drag. A drag that never really moved is treated as a click that
    /// just placed a point; otherwise the loose end snaps onto an existing point when it
    /// lands near one, which is what joins edges into a shape.
    /// </summary>
    private bool HandleConstructionMouseUp(Point canvasPoint)
    {
        if (draggingMode == DraggingMode.ConstructionBoundaryProbe)
            return FinishBoundaryProbe(canvasPoint);

        if (draggingMode != DraggingMode.ConstructionEdgeCreate)
            return false;

        ConstructionOverlayControl? overlay = constructionOverlay;
        Guid? endId = constructionDragEndPointId;
        Guid? lineId = constructionDragLineId;

        constructionDragEndPointId = null;
        constructionDragLineId = null;
        isCreatingMeasurement = false;
        draggingMode = DraggingMode.None;
        ShapeCanvas.ReleaseMouseCapture();

        if (overlay is null || endId is not Guid dragEndId || lineId is not Guid dragLineId)
        {
            overlay?.EndDrag();
            return true;
        }

        try
        {
            bool movedFarEnough =
                Math.Abs(canvasPoint.X - clickedPoint.X) > ConstructionDragThreshold ||
                Math.Abs(canvasPoint.Y - clickedPoint.Y) > ConstructionDragThreshold;

            if (!movedFarEnough)
            {
                // Not a drag — drop the degenerate line and its loose end, keeping the
                // point the press created.
                overlay.RemoveConstructionLine(dragLineId);
                overlay.RemoveConstructionPoint(dragEndId);
                return true;
            }

            overlay.MoveConstructionPoint(dragEndId, canvasPoint);

            if (overlay.FindPointNear(canvasPoint, ConstructionHitTolerance, exclude: dragEndId) is Guid reuseId)
            {
                overlay.SetLineEnd(dragLineId, reuseId);
                overlay.RemoveConstructionPoint(dragEndId);
            }

            return true;
        }
        finally
        {
            overlay.EndDrag();
        }
    }

    #region Boundary probe

    /// <summary>
    /// Begins a probe: the user drags a short line across a boundary and the point lands
    /// on the transition rather than wherever the cursor happened to stop.
    /// </summary>
    /// <remarks>
    /// Unlike the Edge tool this creates nothing up front. There is no point to drag until
    /// the analysis has somewhere to put one, so the whole gesture is a preview and the
    /// geometry is only touched on release.
    /// </remarks>
    private void StartBoundaryProbe(Point canvasPoint)
    {
        ConstructionOverlayControl overlay = EnsureConstructionOverlay();

        // Starting a probe abandons whatever was picked before it, as every other tool does.
        overlay.ClearSelection();

        boundaryProbeStart = canvasPoint;
        draggingMode = DraggingMode.ConstructionBoundaryProbe;
        isCreatingMeasurement = true;
        ShapeCanvas.CaptureMouse();
        ShowPixelZoom(canvasPoint);

        // Normally already warm from when the tool was picked. Doing it here rather than
        // on the first mouse move keeps any decode cost out of the middle of the drag.
        GetProbeBuffer();
    }

    /// <summary>
    /// Tracks the probe as it is dragged, showing both the probe line and where the point
    /// would land if it were released now.
    /// </summary>
    private bool UpdateBoundaryProbe(Point canvasPoint)
    {
        if (boundaryProbeStart is not Point start || constructionOverlay is null)
            return false;

        constructionOverlay.ShowPreviewLine(start, canvasPoint);

        if (TryProbeBoundary(start, canvasPoint, out Point found, out bool isWeak))
        {
            constructionOverlay.ShowBoundaryCandidate(found, isWeak);

            // The crosshairs belong on the edge that was found, not on the cursor — the
            // whole point of the gesture is that those are different places, and seeing
            // the transition magnified is how the user judges whether it picked right.
            UpdatePixelZoom(canvasPoint, found);
        }
        else
        {
            constructionOverlay.HideBoundaryCandidate();
            UpdatePixelZoom(canvasPoint);
        }

        return true;
    }

    /// <summary>
    /// Commits the probe as a single free point. A probe too short to say which way the
    /// boundary runs places nothing rather than guessing at the press position.
    /// </summary>
    private bool FinishBoundaryProbe(Point canvasPoint)
    {
        ConstructionOverlayControl? overlay = constructionOverlay;
        Point? probeStart = boundaryProbeStart;

        boundaryProbeStart = null;
        isCreatingMeasurement = false;
        draggingMode = DraggingMode.None;
        ShapeCanvas.ReleaseMouseCapture();

        overlay?.HidePreviewLine();
        overlay?.HideBoundaryCandidate();

        if (overlay is null || probeStart is not Point start)
            return true;

        bool movedFarEnough =
            Math.Abs(canvasPoint.X - start.X) > ConstructionDragThreshold ||
            Math.Abs(canvasPoint.Y - start.Y) > ConstructionDragThreshold;

        if (!movedFarEnough)
            return true;

        if (!TryProbeBoundary(start, canvasPoint, out Point found, out bool isWeak))
            return true;

        // AddPoint is one Edit, so the whole gesture lands on the undo stack as one step.
        overlay.AddPoint(found);

        // Said rather than enforced: a weak reading is still usually close, and the point
        // is an ordinary one the user can drag.
        overlay.TransientHint = isWeak
            ? "Weak boundary — check the point and nudge it if needed"
            : null;

        return true;
    }

    /// <summary>
    /// Runs one probe. Canvas coordinates in and out; the analysis itself happens in image
    /// pixel space, at the resolution the pixels actually have rather than the one they
    /// are displayed at.
    /// </summary>
    private bool TryProbeBoundary(Point canvasStart, Point canvasEnd, out Point found, out bool isWeak)
    {
        found = default;
        isWeak = true;

        Point startPixel = ConvertCanvasToImageCoordinates(canvasStart);
        Point endPixel = ConvertCanvasToImageCoordinates(canvasEnd);

        if (!BoundaryProbeAnalyzer.TryFindBoundary(
                GetProbeBuffer(), startPixel, endPixel,
                out BoundaryProbeAnalyzer.BoundaryProbeResult result))
            return false;

        found = ConvertImageToCanvasCoordinates(result.Position);
        isWeak = result.IsWeak;
        return true;
    }

    /// <summary>
    /// The sample buffer for the image on screen right now, building it if the warm-up
    /// has not finished or the image has changed since.
    /// </summary>
    private ImageSampleBuffer? GetProbeBuffer()
    {
        string? path = ViewModel.ImagePath;
        if (string.IsNullOrEmpty(path))
            return null;

        if (probeBuffer is not null && probeBufferPath == path)
            return probeBuffer;

        // Sampling the previous image's pixels would put the point in the wrong place
        // silently, which is worse than a brief pause here.
        probeBuffer = ImageSampleBuffer.FromFile(path);
        probeBufferPath = probeBuffer is null ? null : path;
        return probeBuffer;
    }

    /// <summary>
    /// Builds the sample buffer off the UI thread, so picking the tool absorbs the decode
    /// instead of the first press. Shows a ring next to the tool button while it runs.
    /// </summary>
    private async void WarmProbeBuffer()
    {
        string? path = ViewModel.ImagePath;

        if (string.IsNullOrEmpty(path) || path == probeBufferPath)
        {
            SetProbeWarmingIndicator(false);
            return;
        }

        // Reselecting the tool while a build is still running: the in-flight one finishes
        // the job, but the indicator still has to be put back up for it.
        SetProbeWarmingIndicator(true);

        if (isWarmingProbeBuffer)
            return;

        isWarmingProbeBuffer = true;

        try
        {
            ImageSampleBuffer? buffer = await Task.Run(() => ImageSampleBuffer.FromFile(path));

            // The tool may have been put down, or the image edited again, while this was
            // decoding. Either way the result is stale; GetProbeBuffer rebuilds on demand.
            if (buffer is not null &&
                path == ViewModel.ImagePath &&
                ActiveConstructionTool == ConstructionTool.Boundary)
            {
                probeBuffer = buffer;
                probeBufferPath = path;
            }
        }
        finally
        {
            isWarmingProbeBuffer = false;
            SetProbeWarmingIndicator(false);
        }
    }

    /// <summary>
    /// Drops the sample buffer. Called when the tool is put down: it is the largest thing
    /// this window holds that nothing else needs, and it is cheap enough to rebuild.
    /// </summary>
    private void ReleaseProbeBuffer()
    {
        probeBuffer = null;
        probeBufferPath = null;
        SetProbeWarmingIndicator(false);
    }

    private void SetProbeWarmingIndicator(bool isWarming)
    {
        Visibility visibility = isWarming ? Visibility.Visible : Visibility.Collapsed;

        // Mirrored on both tabs, like the tool button itself.
        if (ConstructionBoundaryProgressRing is not null)
            ConstructionBoundaryProgressRing.Visibility = visibility;

        if (ConstructionBoundaryProgressRingTransform is not null)
            ConstructionBoundaryProgressRingTransform.Visibility = visibility;
    }

    #endregion

    /// <summary>Clears any half-finished construction gesture.</summary>
    private void CancelConstructionGesture()
    {
        if (constructionOverlay is not null)
        {
            if (constructionDragLineId is Guid lineId)
                constructionOverlay.RemoveConstructionLine(lineId);

            if (constructionDragEndPointId is Guid endId)
                constructionOverlay.RemoveConstructionPoint(endId);

            constructionOverlay.HidePreviewLine();
        }

        foreach (ConstructionOverlayControl overlay in constructionControls)
        {
            // Closes any drag abandoned by the cancel, so what survives it is still one
            // undo step.
            overlay.EndDrag();
            overlay.ClearSelection();
            overlay.HideBoundaryCandidate();
        }

        constructionDragEndPointId = null;
        constructionDragLineId = null;
        boundaryProbeStart = null;
    }

    /// <summary>
    /// Drops the selection on every overlay. This is what a click on bare canvas means:
    /// the click reached the canvas only because no point, line, or faint line claimed
    /// it first, so the user was pointing at nothing.
    /// </summary>
    private void ClearConstructionSelection()
    {
        foreach (ConstructionOverlayControl overlay in constructionControls)
            overlay.ClearSelection();
    }

    /// <summary>
    /// Pushes the active tool down to the overlays. Only the Line tool wants a second
    /// pick to connect on its own; every other tool leaves the faint line to be clicked.
    /// </summary>
    private void SyncConstructionToolState()
    {
        ConstructionTool tool = ActiveConstructionTool;
        bool connectOnSecondSelection = tool == ConstructionTool.Line;

        foreach (ConstructionOverlayControl overlay in constructionControls)
            overlay.ConnectOnSecondSelection = connectOnSecondSelection;

        // Reading the image for probing takes a moment, so it happens when the tool is
        // picked rather than partway through the first drag — and the copy is dropped
        // again the moment the tool is put down, since nothing else uses it.
        if (tool == ConstructionTool.Boundary)
            WarmProbeBuffer();
        else
            ReleaseProbeBuffer();
    }

    /// <summary>
    /// True when any overlay has a point or line picked, so the window can tell whether
    /// Delete belongs to the construction or to some other selection.
    /// </summary>
    private bool HasConstructionSelection =>
        constructionControls.Any(overlay => overlay.HasSelection);

    /// <summary>
    /// Deletes the selected line — or, when no line is selected, the selected points.
    /// Removing a connection deliberately leaves its two points in place.
    /// </summary>
    private bool DeleteSelectedConstruction()
    {
        bool deleted = false;

        foreach (ConstructionOverlayControl overlay in constructionControls)
            deleted |= overlay.DeleteSelection();

        return deleted;
    }

    private void ConstructionPoint_MouseDown(object sender, MouseButtonEventArgs? e)
    {
        if (!HandleMeasurementMouseDown<ConstructionOverlayControl>(
                sender, e, DraggingMode.ConstructionPoint, control => activeConstructionControl = control))
            return;

        // The whole drag is one undo step, not one per mouse move.
        activeConstructionControl?.BeginDrag();
    }

    #endregion

    #region Applying the constructed shape

    /// <summary>
    /// The constructed quadrilateral in canvas coordinates, or null when the construction
    /// is not exactly four solved corners.
    /// </summary>
    private QuadrilateralDetector.DetectedQuadrilateral? GetConstructedQuadrilateral()
    {
        foreach (ConstructionOverlayControl overlay in constructionControls)
        {
            if (overlay.TryGetQuadrilateral(out QuadrilateralDetector.DetectedQuadrilateral quad))
                return quad;
        }

        return null;
    }

    /// <summary>
    /// Prepends the constructed shape to a detection list so "Detect Shape" surfaces it
    /// alongside the automatically found quadrilaterals.
    /// </summary>
    private void PrependConstructedQuadrilateral(List<QuadrilateralDetector.DetectedQuadrilateral> quads)
    {
        if (GetConstructedQuadrilateral() is not QuadrilateralDetector.DetectedQuadrilateral quad)
            return;

        quad.Label = "Constructed shape";
        quads.Insert(0, quad);
    }

    private bool HasConstructedQuadrilateral => GetConstructedQuadrilateral() is not null;

    private void UpdateConstructionApplyButtons()
    {
        bool enabled = HasConstructedQuadrilateral;

        foreach (Button? button in new[]
        {
            ApplyConstructionToTransformButton,
            ApplyConstructionToCropButton,
            ApplyConstructionToUnWarpButton,
            ApplyConstructionToTransformButtonTransformTab
        })
        {
            if (button is not null)
                button.IsEnabled = enabled;
        }
    }

    private void ApplyConstructionToTransform_Click(object sender, RoutedEventArgs e)
    {
        if (GetConstructedQuadrilateral() is not QuadrilateralDetector.DetectedQuadrilateral quad)
            return;

        ShowTransformControls();
        PositionCornerMarkers(quad);
    }

    private void ApplyConstructionToCrop_Click(object sender, RoutedEventArgs e)
    {
        if (GetConstructedQuadrilateral() is not QuadrilateralDetector.DetectedQuadrilateral quad)
            return;

        ShowCroppingControls();
        PositionCroppingRectangle(quad);
    }

    private void ApplyConstructionToUnWarp_Click(object sender, RoutedEventArgs e)
    {
        if (GetConstructedQuadrilateral() is not QuadrilateralDetector.DetectedQuadrilateral quad)
            return;

        ShowUnWarpControls();
        PositionUnWarpMarkers(quad);
    }

    private void ClearConstruction_Click(object sender, RoutedEventArgs e)
    {
        RemoveConstructionOverlays([.. constructionControls], recordUndo: true);
        UncheckAllBut();
    }

    #endregion
}
