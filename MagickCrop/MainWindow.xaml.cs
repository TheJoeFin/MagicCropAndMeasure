using ImageMagick;
using MagickCrop.Controls;
using MagickCrop.Helpers;
using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.Services;
using MagickCrop.ViewModels;
using Microsoft.Win32;
using Microsoft.Windows.Media.Capture;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Windows.ApplicationModel;
using Windows.Storage;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace MagickCrop;

public partial class MainWindow : FluentWindow, IMainWindowView
{
    public MainWindowViewModel ViewModel { get; private set; } = null!;

    // ── IMainWindowView implementation ──
    BitmapSource? IMainWindowView.ImageSource
    {
        get => MainImage.Source as BitmapSource;
        set => MainImage.Source = value;
    }

    System.Windows.Controls.Image IMainWindowView.MainImageControl => MainImage;
    double IMainWindowView.ImageActualWidth => MainImage.ActualWidth;
    double IMainWindowView.ImageActualHeight => MainImage.ActualHeight;

    bool IMainWindowView.IsLocalAdjustment => LocalAdjustmentCheckBox.IsChecked == true;

    bool IMainWindowView.HasMeasurements =>
        measurementTools.Count > 0
        || angleMeasurementTools.Count > 0
        || rectangleMeasurementTools.Count > 0
        || polygonMeasurementTools.Count > 0
        || circleMeasurementTools.Count > 0;

    MagickGeometry IMainWindowView.GetLocalAdjustmentRegion() => LocalAdjustmentRectangle.CropShape;

    void IMainWindowView.SetBusy(bool busy)
    {
        if (busy)
            SetUiForLongTask();
        else
            SetUiForCompletedTask();
    }

    Window IMainWindowView.OwnerWindow => this;

    private Point clickedPoint = new();
    private Vector handleGrabOffset = new();
    private Size oldGridSize = new();
    private FrameworkElement? clickedElement;
    private bool allowHandlesOutsideImage = true;
    private bool isUpdatingCanvasNavigation;
    private bool showMiniMap = true;
    private const double CanvasOriginOffset = 50;
    private const double DefaultSidebarWidth = 240;

    // Size input properties
    private bool isUpdatingFromCode = false;
    private bool isPixelMode = true;
    private bool isAspectRatioLocked = true;
    private bool isDraggingResizeGrip = false;
    private double aspectRatio = 1.0;
    private int pointDraggingIndex = -1;
    private Polygon? lines;
    private readonly int ImageWidthConst = 700;

    // Quadrilateral detection parameters
    private const double QuadDetectionMinArea = 0.02;
    private const int QuadDetectionMaxResults = 5;

    private DraggingMode draggingMode = DraggingMode.None;

    private readonly List<UIElement> _polygonElements;

    public UndoRedo UndoRedo => ViewModel.UndoRedo;
    private AspectRatioItem? selectedAspectRatio;
    private readonly ObservableCollection<DistanceMeasurementControl> measurementTools = [];
    private DistanceMeasurementControl? activeMeasureControl;
    private readonly ObservableCollection<AngleMeasurementControl> angleMeasurementTools = [];
    private AngleMeasurementControl? activeAngleMeasureControl;
    private readonly ObservableCollection<RectangleMeasurementControl> rectangleMeasurementTools = [];
    private RectangleMeasurementControl? activeRectangleMeasureControl;
    private readonly ObservableCollection<PolygonMeasurementControl> polygonMeasurementTools = [];
    private PolygonMeasurementControl? activePolygonMeasureControl;
    private readonly ObservableCollection<CircleMeasurementControl> circleMeasurementTools = [];
    private CircleMeasurementControl? activeCircleMeasureControl;

    private readonly ObservableCollection<VerticalLineControl> verticalLineControls = [];
    private readonly ObservableCollection<HorizontalLineControl> horizontalLineControls = [];

    // --- Markup state ---
    private readonly ObservableCollection<MarkupShapeControl> markupShapeControls = [];
    private MarkupShapeControl? activeMarkupShapeControl;
    private readonly ObservableCollection<MarkupTextControl> markupTextControls = [];
    private System.Windows.Media.Color markupColor = System.Windows.Media.Colors.Red;
    private double markupSize = 3.0;
    private bool isMarkupPenMode = false;
    private bool isMarkupHighlighterMode = false;
    private bool isMarkupSelectMode = false;
    private bool isMarkupShapeMode = false;
    private bool isMarkupTextMode = false;
    private Rect? _selectionBoundsBeforeMove = null;
    private StrokeCollection? _strokesBeforeMove = null;
    private Rect? _selectionBoundsBeforeResize = null;
    private StrokeCollection? _strokesBeforeResize = null;
    private MagickCrop.Models.MarkupShapeType activeMarkupShapeType = MagickCrop.Models.MarkupShapeType.Rectangle;
    private bool isMarkupShapeDragCreation = false;
    private Point markupShapeBeforePoint1;
    private Point markupShapeBeforePoint2;
    private int markupShapeBeforeDragIndex = -1;

    // --- Markup group selection (Select tool: ink + shapes + text together) ---
    private readonly HashSet<MarkupShapeControl> selectedMarkupShapes = [];
    private readonly HashSet<MarkupTextControl> selectedMarkupTexts = [];
    private readonly List<System.Windows.Shapes.Rectangle> markupSelectionHighlights = [];
    private Point? markupMarqueeStartPoint;
    private Point markupGroupDragLastPoint;
    private double markupGroupDragTotalDeltaX;
    private double markupGroupDragTotalDeltaY;
    private StrokeCollection? markupGroupMoveStrokes;
    private List<MarkupShapeControl>? markupGroupMoveShapes;
    private List<MarkupTextControl>? markupGroupMoveTexts;

    private Services.RecentProjectsManager? recentProjectsManager;
    private System.Timers.Timer? autoSaveTimer;
    private readonly int AutoSaveIntervalMs = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;

    private readonly ObservableCollection<Line> verticalLines = [];
    private readonly ObservableCollection<Line> horizontalLines = [];

    private bool isDrawingMode = false;
    private Dictionary<Stroke, StrokeInfo> strokeMeasurements = [];

    private bool isCreatingMeasurement = false;

    // --- White point picker state ---
    private bool isWhitePointPickerMode = false;

    // --- Black point picker state ---
    private bool isBlackPointPickerMode = false;

    // --- Angle measurement placement state ---
    private bool isPlacingAngleMeasurement = false;
    private AnglePlacementStep anglePlacementStep = AnglePlacementStep.None;
    private AngleMeasurementControl? activeAnglePlacementControl = null;

    // --- Rectangle measurement placement state ---
    private bool isPlacingRectangleMeasurement = false;
    private RectangleMeasurementControl? activeRectanglePlacementControl = null;    // --- Polygon measurement placement state ---
    private bool isPlacingPolygonMeasurement = false;
    private PolygonMeasurementControl? activePolygonPlacementControl = null;

    // --- Circle measurement placement state ---
    private bool isPlacingCircleMeasurement = false;
    private CircleMeasurementControl? activeCirclePlacementControl = null;

    // Precise rotation state
    private bool isRotateMode = false;
    private double currentPreviewRotation = 0.0; // degrees
    private bool suppressRotateEvents = false; // prevent feedback loops
    private Point freeRotateLastPoint;
    private RotateTransform? previewRotateTransform; // applied only during preview
    private const double FreeRotateSensitivity = 0.3; // degrees per pixel dragged

    // new flag
    private bool isFreeRotatingDrag = false;

    // runtime reference to angle overlay
    private readonly WpfTextBlock? rotationOverlayLabel;
    private long lastRotateUpdateTicks = 0;
    private double lastAppliedAdornerAngle = 0.0;
    private const int RotateUpdateMinIntervalMs = 12; // throttle to reduce UI thrash
    private const double RotateMinDelta = 0.1; // degrees

    private RotateAdorner? rotateAdorner;
    private AdornerLayer? rotateAdornerLayer;
    private bool isAdornerRotatingDrag = false; // true while adorner has the mouse captured

    // Hover highlight polygon for quadrilateral selector
    private Polygon? hoverHighlightPolygon;

    // --- Tri-fold correction state ---
    private bool isTriFoldMode = false;
    private Polygon? triFoldPolygon;
    private readonly List<UIElement> _triFoldElements = [];

    // --- Un-warp state ---
    private bool isUnWarpMode = false;
    private System.Windows.Shapes.Path? unWarpPath;
    private readonly List<UIElement> _unWarpElements = [];

    // --- Edge correction state ---
    private bool isEdgeCorrectionMode = false;
    private readonly List<Point> edgeCorrectionPoints = [];
    private readonly List<Ellipse> edgeCorrectionMarkers = [];
    private readonly List<Line> edgeCorrectionSnapLines = [];
    private int edgeCorrectionDragIndex = -1;
    private bool isEdgeCorrectionSpacePanning = false;

    // --- Grid straighten state ---
    private bool isGridStraightenMode = false;
    private List<Point> gridStraightenPoints = [];
    private readonly List<Ellipse> gridStraightenMarkers = [];
    private readonly List<Line> gridStraightenLines = [];
    private int gridStraightenDragIndex = -1;
    private int gridStraightenRows = 4;
    private int gridStraightenCols = 4;
    private bool isGridStraightenSpacePanning = false;

    public MainWindow()
    {
        ViewModel = new MainWindowViewModel();
        ViewModel.SetView(this);
        DataContext = ViewModel;

        ThemeService themeService = new();
        themeService.SetTheme(ApplicationTheme.Dark);

        Color teal = (Color)ColorConverter.ConvertFromString("#0066FF");
        ApplicationAccentColorManager.Apply(teal);

        InitializeComponent();
        canvasScale.Changed += CanvasScale_Changed;
        canvasTranslate.Changed += CanvasTranslate_Changed;
        CanvasMiniMap.ViewportCenterRequested += CanvasMiniMap_ViewportCenterRequested;
        MainGrid.SizeChanged += (_, _) => UpdateMiniMap();
        MainImage.SizeChanged += (_, _) => UpdateMiniMap();
        DependencyPropertyDescriptor
            .FromProperty(System.Windows.Controls.Image.SourceProperty, typeof(System.Windows.Controls.Image))
            ?.AddValueChanged(MainImage, (_, _) => UpdateMiniMap());
        // Ensure zoom still works if mouse wheel fires at window level (after a pan or when mouse over other element)
        PreviewMouseWheel += ShapeCanvas_PreviewMouseWheel;

        DrawPolyLine();

        lines ??= new();

        _polygonElements = [lines, TopLeft, TopRight, BottomRight, BottomLeft];

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Collapsed;

        // Tri-fold elements: all 8 markers + fold guide lines (built later)
        _triFoldElements.AddRange([TopLeft, TopRight, BottomRight, BottomLeft,
            UpperFoldLeft, UpperFoldRight, LowerFoldLeft, LowerFoldRight]);

        // Un-warp elements: 4 corners + 4 midpoint handles
        _unWarpElements.AddRange([TopLeft, TopRight, BottomRight, BottomLeft,
            UnWarpMidTop, UnWarpMidRight, UnWarpMidBottom, UnWarpMidLeft]);

        // Wire QuadrilateralSelector events explicitly in code-behind
        CropQuadrilateralSelectorControl.QuadrilateralSelected += CropQuadrilateralSelector_Selected;
        CropQuadrilateralSelectorControl.ManualSelection += CropQuadrilateralSelector_ManualSelection;
        CropQuadrilateralSelectorControl.Cancelled += CropQuadrilateralSelector_Cancelled;

        QuadrilateralSelectorControl.QuadrilateralSelected += QuadrilateralSelector_Selected;
        QuadrilateralSelectorControl.ManualSelection += QuadrilateralSelector_ManualSelection;
        QuadrilateralSelectorControl.Cancelled += QuadrilateralSelector_Cancelled;

        UnWarpQuadrilateralSelectorControl.QuadrilateralSelected += UnWarpQuadrilateralSelector_Selected;
        UnWarpQuadrilateralSelectorControl.ManualSelection += UnWarpQuadrilateralSelector_ManualSelection;
        UnWarpQuadrilateralSelectorControl.Cancelled += UnWarpQuadrilateralSelector_Cancelled;

        try
        {
            PackageVersion version = Package.Current.Id.Version;
            ViewModel.WindowTitle += $" v{version.Major}.{version.Minor}.{version.Build}";
        }
        catch (Exception)
        {
            // do nothing this is just running unpackaged.
        }

        AspectRatioComboBox.ItemsSource = AspectRatioItem.GetStandardAspectRatios();
        AspectRatioComboBox.SelectedIndex = 0;
        selectedAspectRatio = AspectRatioComboBox.SelectedItem as AspectRatioItem;
        AspectRatioTransformPreview.RatioItem = selectedAspectRatio;

        InitializeProjectManager();

        ShapeCanvas.MouseUp += ShapeCanvas_MouseUp;
        ShapeCanvas.LostMouseCapture += ShapeCanvas_LostMouseCapture; // safety to ensure capture released
        MainGrid.LostMouseCapture += MainGrid_LostMouseCapture;
        rotationOverlayLabel = FindName("RotationOverlayLabel") as WpfTextBlock; // cache
        UpdateCanvasNavigationUi();
        UpdateTransformVisualScale();

        CheckObjectEraseAvailability();
    }

    private async void CheckObjectEraseAvailability()
    {
        try
        {
            bool supported = await Task.Run(() => ObjectEraseHelper.IsSupported());
            if (supported)
            {
                ObjectEraseSeparator.Visibility = Visibility.Visible;
                ObjectEraseMenuItem.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            // AI feature not available on this device — keep menu item hidden
        }
    }

    private void ShapeCanvas_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (draggingMode == DraggingMode.Panning)
        {
            draggingMode = DraggingMode.None;
            Cursor = null;
        }
    }

    private void MainGrid_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (draggingMode == DraggingMode.Panning)
        {
            draggingMode = DraggingMode.None;
            Cursor = null;
        }
    }

    private void DrawPolyLine()
    {
        Color color = (Color)ColorConverter.ConvertFromString("#0066FF");

        if (lines is not null)
            ShapeCanvas.Children.Remove(lines);

        lines = new()
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            IsHitTestVisible = false,
            StrokeLineJoin = PenLineJoin.Round,
            Opacity = 0.8,
        };

        // Only include the 4 corner markers (Tags 0-3) in the polyline
        Ellipse[] cornerEllipses = [TopLeft, TopRight, BottomRight, BottomLeft];

        foreach (Ellipse ellipse in cornerEllipses)
        {
            lines.Points.Add(
                new Point(Canvas.GetLeft(ellipse) + (ellipse.Width / 2),
                                Canvas.GetTop(ellipse) + (ellipse.Height / 2)));
        }

        ShapeCanvas.Children.Add(lines);

        // Keep _polygonElements in sync with the new lines reference
        if (_polygonElements is not null && _polygonElements.Count > 0)
            _polygonElements[0] = lines;

        UpdateTransformVisualScale();
    }

    private void TopLeft_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (isAdornerRotatingDrag)
        {
            e.Handled = true;
            return;
        }
        if (sender is not Ellipse ellipse || ellipse.Tag is not string intAsString)
            return;

        pointDraggingIndex = int.Parse(intAsString);
        clickedElement = ellipse;
        draggingMode = DraggingMode.MoveElement;
        clickedPoint = e.GetPosition(ShapeCanvas);
        Point handleCenter = new(
            Canvas.GetLeft(ellipse) + (ellipse.Width / 2),
            Canvas.GetTop(ellipse) + (ellipse.Height / 2));
        handleGrabOffset = clickedPoint - handleCenter;
        lastActiveTransformHandle = ellipse;
        lastActiveTransformIndex = pointDraggingIndex;
        CaptureMouse();

        BeginTransformHandleDrag(ellipse, handleCenter);

        // Magnify the handle centre — not the cursor — so an off-centre grab still shows the
        // pixel the corner will land on.
        ShowPixelZoom(handleCenter, clickedPoint);
    }

    private void TopLeft_MouseMove(object sender, MouseEventArgs e)
    {
        if (isAdornerRotatingDrag)
        {
            e.Handled = true;
            return;
        }

        if (isFreeRotatingDrag)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                HandleFreeRotateDrag(e);
                e.Handled = true;
                return; // skip other drag behaviors while rotating
            }
            else
            {
                // mouse released
                isFreeRotatingDrag = false;
                HideRotationOverlay();
            }
        }

        // Update pixel zoom if it should be shown (including before first measurement placement)
        Point mousePos = e.GetPosition(ShapeCanvas);
        if (ShouldShowPixelZoom())
        {
            // While dragging a transform handle the loupe follows the handle centre (grab offset
            // removed and bounds applied), so the crosshair marks where the point really lands.
            Point loupeTarget = draggingMode == DraggingMode.MoveElement && clickedElement is not null
                ? ConstrainHandlePosition(mousePos - handleGrabOffset)
                : mousePos;

            // Show the pixel zoom if not already visible
            if (PixelZoomControl.Visibility != Visibility.Visible)
            {
                ShowPixelZoom(loupeTarget, mousePos);
            }
            else
            {
                UpdatePixelZoom(loupeTarget, mousePos);
            }
        }
        else
        {
            // Hide zoom when conditions are no longer met
            HidePixelZoom();
        }

        // --- ANGLE MEASUREMENT PLACEMENT LOGIC ---
        if (isPlacingAngleMeasurement && activeAnglePlacementControl != null)
        {
            if (anglePlacementStep == AnglePlacementStep.DraggingFirstLeg)
            {
                activeAnglePlacementControl.MovePoint(1, mousePos); // Move point1 to follow mouse
                e.Handled = true;
                return;
            }
            else if (anglePlacementStep == AnglePlacementStep.PlacingThirdPoint)
            {
                activeAnglePlacementControl.MovePoint(2, mousePos);
                e.Handled = true;
                return;
            }
        }

        // --- RECTANGLE MEASUREMENT PLACEMENT LOGIC ---
        if (isPlacingRectangleMeasurement && activeRectanglePlacementControl != null && draggingMode == DraggingMode.CreatingMeasurement)
        {
            activeRectanglePlacementControl.MovePoint(1, mousePos); // Update bottom-right point as mouse moves
            e.Handled = true;
            return;
        }

        // --- POLYGON MEASUREMENT PLACEMENT LOGIC ---
        if (isPlacingPolygonMeasurement && activePolygonPlacementControl != null && !activePolygonPlacementControl.IsClosed)
        {
            activePolygonPlacementControl.UpdatePreviewLine(mousePos);
            e.Handled = true;
            return;
        }        // --- CIRCLE MEASUREMENT PLACEMENT LOGIC ---
        if (isPlacingCircleMeasurement && activeCirclePlacementControl != null && draggingMode == DraggingMode.CreatingMeasurement)
        {
            activeCirclePlacementControl.MovePoint(1, mousePos); // Update edge point as mouse moves
            e.Handled = true;
            return;
        }

        if (Mouse.MiddleButton == MouseButtonState.Released && Mouse.LeftButton == MouseButtonState.Released)
        {
            if (draggingMode == DraggingMode.Panning)
            {
                // panning release handled in MouseUp, nothing else here
            }

            if (draggingMode == DraggingMode.Resizing)
            {
                isDraggingResizeGrip = false;
            }

            if (draggingMode == DraggingMode.MeasureDistance && activeMeasureControl is not null)
            {
                activeMeasureControl.ResetActivePoint();
                activeMeasureControl = null;
            }

            if (draggingMode == DraggingMode.MeasureAngle && activeAngleMeasureControl is not null)
            {
                activeAngleMeasureControl.ResetActivePoint();
                activeAngleMeasureControl = null;
            }

            if (draggingMode == DraggingMode.MeasureRectangle && activeRectangleMeasureControl is not null)
            {
                activeRectangleMeasureControl.ResetActivePoint();
                activeRectangleMeasureControl = null;
            }

            if (draggingMode == DraggingMode.MeasurePolygon && activePolygonMeasureControl is not null)
            {
                activePolygonMeasureControl.ResetActivePoint();
                activePolygonMeasureControl = null;
            }

            if (draggingMode == DraggingMode.MeasureCircle && activeCircleMeasureControl is not null)
            {
                activeCircleMeasureControl.ResetActivePoint();
                activeCircleMeasureControl = null;
            }

            if (draggingMode == DraggingMode.EdgeCorrectionDragging)
            {
                edgeCorrectionDragIndex = -1;
            }

            if (draggingMode == DraggingMode.GridStraightenDragging)
            {
                gridStraightenDragIndex = -1;
            }

            if (draggingMode == DraggingMode.MarkupShape && activeMarkupShapeControl is not null)
            {
                if (isMarkupShapeDragCreation)
                {
                    // New shape added — record undo for the whole addition
                    MarkupShapeControl ctrl = activeMarkupShapeControl;
                    UndoRedo.AddUndo(new MarkupShapeAddedItem(
                        ctrl, markupShapeControls, ShapeCanvas,
                        wireEvents: () =>
                        {
                            ctrl.MeasurementPointMouseDown += MarkupShapePoint_MouseDown;
                            ctrl.RemoveControlRequested += MarkupShapeControl_RemoveControlRequested;
                        },
                        unwireEvents: () =>
                        {
                            ctrl.MeasurementPointMouseDown -= MarkupShapePoint_MouseDown;
                            ctrl.RemoveControlRequested -= MarkupShapeControl_RemoveControlRequested;
                        }));
                }
                else if (markupShapeBeforeDragIndex >= 0)
                {
                    // Existing handle dragged — record undo for the point move
                    (Point afterP1, Point afterP2) = activeMarkupShapeControl.GetPoints();
                    Point before = markupShapeBeforeDragIndex == 0 ? markupShapeBeforePoint1 : markupShapeBeforePoint2;
                    Point after = markupShapeBeforeDragIndex == 0 ? afterP1 : afterP2;
                    if (before != after)
                    {
                        MarkupShapeControl ctrl = activeMarkupShapeControl;
                        UndoRedo.AddUndo(new MarkupShapePointMovedItem(ctrl, markupShapeBeforeDragIndex, before, after));
                    }
                }

                activeMarkupShapeControl.ResetActivePoint();
                activeMarkupShapeControl = null;
                isMarkupShapeDragCreation = false;
            }

            if (draggingMode == DraggingMode.MarkupGroupSelect)
            {
                FinishMarkupMarquee(e.GetPosition(ShapeCanvas));
            }

            if (draggingMode == DraggingMode.MarkupGroupMove)
            {
                FinishMarkupGroupMove();
            }

            EndTransformHandleDrag();

            clickedElement = null;
            pointDraggingIndex = -1;
            ReleaseMouseCapture();
            draggingMode = DraggingMode.None;

            return;
        }

        if (draggingMode == DraggingMode.Panning)
        {
            PanCanvas(e);
            return;
        }

        if (draggingMode == DraggingMode.Resizing)
        {
            ResizeImage(e);
            return;
        }

        Point movingPoint = e.GetPosition(ShapeCanvas);

        if (draggingMode == DraggingMode.MarkupGroupSelect)
        {
            UpdateMarkupMarqueeVisual(movingPoint);
            e.Handled = true;
            return;
        }

        if (draggingMode == DraggingMode.MarkupGroupMove)
        {
            double groupDeltaX = movingPoint.X - markupGroupDragLastPoint.X;
            double groupDeltaY = movingPoint.Y - markupGroupDragLastPoint.Y;
            if (groupDeltaX != 0 || groupDeltaY != 0)
            {
                ApplyMarkupGroupDelta(groupDeltaX, groupDeltaY);
                markupGroupDragTotalDeltaX += groupDeltaX;
                markupGroupDragTotalDeltaY += groupDeltaY;
                markupGroupDragLastPoint = movingPoint;
            }
            e.Handled = true;
            return;
        }

        if (draggingMode == DraggingMode.MeasureDistance && activeMeasureControl is not null)
        {
            int pointIndex = activeMeasureControl.GetActivePointIndex();
            if (pointIndex >= 0)
            {
                activeMeasureControl.MovePoint(pointIndex, movingPoint);
            }
            e.Handled = true;
            return;
        }

        if (draggingMode == DraggingMode.MeasureAngle && activeAngleMeasureControl is not null)
        {
            int pointIndex = activeAngleMeasureControl.GetActivePointIndex();
            if (pointIndex >= 0)
            {
                activeAngleMeasureControl.MovePoint(pointIndex, movingPoint);
            }
            e.Handled = true;
            return;
        }

        if (draggingMode == DraggingMode.MeasureRectangle && activeRectangleMeasureControl is not null)
        {
            int pointIndex = activeRectangleMeasureControl.GetActivePointIndex();
            if (pointIndex >= 0)
            {
                activeRectangleMeasureControl.MovePoint(pointIndex, movingPoint);
            }
            e.Handled = true;
            return;
        }

        if (draggingMode == DraggingMode.MeasurePolygon && activePolygonMeasureControl is not null)
        {
            int pointIndex = activePolygonMeasureControl.GetActivePointIndex();
            if (pointIndex >= 0)
            {
                activePolygonMeasureControl.MovePoint(pointIndex, movingPoint);
            }
            e.Handled = true;
            return;
        }

        if (draggingMode == DraggingMode.MeasureCircle && activeCircleMeasureControl is not null)
        {
            int pointIndex = activeCircleMeasureControl.GetActivePointIndex();
            if (pointIndex >= 0)
            {
                activeCircleMeasureControl.MovePoint(pointIndex, movingPoint);
            }
            e.Handled = true;
            return;
        }

        if (draggingMode == DraggingMode.EdgeCorrectionDragging && edgeCorrectionDragIndex >= 0)
        {
            Point imagePoint = e.GetPosition(MainImage);
            MoveEdgeCorrectionPoint(edgeCorrectionDragIndex, imagePoint);
            e.Handled = true;
            return;
        }

        if (draggingMode == DraggingMode.GridStraightenDragging && gridStraightenDragIndex >= 0)
        {
            Point imagePoint = e.GetPosition(MainImage);
            MoveGridStraightenPoint(gridStraightenDragIndex, imagePoint);
            e.Handled = true;
            return;
        }

        if (draggingMode == DraggingMode.MarkupShape && activeMarkupShapeControl is not null)
        {
            int idx = activeMarkupShapeControl.GetActivePointIndex();
            if (idx >= 0)
                activeMarkupShapeControl.MovePoint(idx, movingPoint);
            e.Handled = true;
            return;
        }

        if (draggingMode != DraggingMode.MoveElement || clickedElement is null)
            return;

        MoveTransformHandleTo(
            clickedElement,
            pointDraggingIndex,
            new Point(
                movingPoint.X - handleGrabOffset.X,
                movingPoint.Y - handleGrabOffset.Y));

        if (draggingMode == DraggingMode.CreatingMeasurement && isCreatingMeasurement)
        {
            e.Handled = true;
        }
    }

    private void ResizeImage(MouseEventArgs e)
    {
        MainImage.Stretch = Stretch.Fill;
        Point currentPoint = e.GetPosition(ShapeCanvas);
        double deltaX = currentPoint.X - clickedPoint.X;
        double deltaY = currentPoint.Y - clickedPoint.Y;

        // Calculate new dimensions
        double newWidth = oldGridSize.Width + deltaX;
        double newHeight = oldGridSize.Height + deltaY;

        // Enforce minimum dimensions of 50px
        ImageGrid.Width = Math.Max(50, newWidth);
        ImageGrid.Height = Math.Max(50, newHeight);

        // Update text boxes based on the current display size and full resolution
        UpdateResizeTextBoxesFromDrag();

        e.Handled = true;
    }

    /// <summary>
    /// Updates the width/height text boxes based on the current ImageGrid dimensions during resize grip drag.
    /// Projects the current display aspect ratio back to full resolution to show the final image dimensions.
    /// </summary>
    private void UpdateResizeTextBoxesFromDrag()
    {
        if (ViewModel.ActualImageSize.Width <= 0 || ViewModel.ActualImageSize.Height <= 0)
            return;

        // Get current display dimensions
        double displayWidth = ImageGrid.Width;
        double displayHeight = ImageGrid.Height;

        // Calculate the new aspect ratio from the current drag
        double newAspectRatio = displayWidth / displayHeight;

        // Determine final dimensions by projecting the new aspect ratio onto the full resolution
        // Use the dimension that results in the largest image without exceeding original bounds
        double finalWidth, finalHeight;

        if (newAspectRatio > ViewModel.ActualImageSize.Width / ViewModel.ActualImageSize.Height)
        {
            // New aspect ratio is wider - use original width as base
            finalWidth = ViewModel.ActualImageSize.Width;
            finalHeight = ViewModel.ActualImageSize.Width / newAspectRatio;
        }
        else
        {
            // New aspect ratio is taller or same - use original height as base
            finalHeight = ViewModel.ActualImageSize.Height;
            finalWidth = ViewModel.ActualImageSize.Height * newAspectRatio;
        }

        isUpdatingFromCode = true;

        if (isPixelMode)
        {
            WidthTextBox.Text = ((int)Math.Round(finalWidth)).ToString();
            HeightTextBox.Text = ((int)Math.Round(finalHeight)).ToString();
        }
        else
        {
            // For percentage mode, calculate percentage relative to original actual size
            double widthPercent = (finalWidth / ViewModel.ActualImageSize.Width) * 100.0;
            double heightPercent = (finalHeight / ViewModel.ActualImageSize.Height) * 100.0;
            WidthTextBox.Text = ((int)Math.Round(widthPercent)).ToString();
            HeightTextBox.Text = ((int)Math.Round(heightPercent)).ToString();
        }

        isUpdatingFromCode = false;
    }

    private void PanCanvas(MouseEventArgs e)
    {
        StopCanvasTranslateAnimation();

        Point currentPosition = e.GetPosition(this);
        Vector delta = currentPosition - clickedPoint;

        // Update the translation
        canvasTranslate.X += delta.X;
        canvasTranslate.Y += delta.Y;

        clickedPoint = currentPosition;
    }

    private Point ConstrainHandlePosition(Point position)
    {
        if (allowHandlesOutsideImage || MainImage.ActualWidth <= 0 || MainImage.ActualHeight <= 0)
            return position;

        return new Point(
            Math.Clamp(position.X, 0, MainImage.ActualWidth),
            Math.Clamp(position.Y, 0, MainImage.ActualHeight));
    }

    private IEnumerable<Ellipse> GetTransformHandles()
    {
        yield return TopLeft;
        yield return TopRight;
        yield return BottomRight;
        yield return BottomLeft;
        yield return UpperFoldLeft;
        yield return UpperFoldRight;
        yield return LowerFoldLeft;
        yield return LowerFoldRight;
        yield return UnWarpMidTop;
        yield return UnWarpMidRight;
        yield return UnWarpMidBottom;
        yield return UnWarpMidLeft;
    }

    private void UpdateTransformVisualScale()
    {
        double scale = Math.Max(MinZoom, canvasScale.ScaleX);
        double inverseScale = 1.0 / scale;

        foreach (Ellipse handle in GetTransformHandles())
        {
            handle.RenderTransformOrigin = new Point(0.5, 0.5);
            handle.RenderTransform = new ScaleTransform(inverseScale, inverseScale);
        }

        lines?.StrokeThickness = 2 * inverseScale;

        UpdateActiveHandleCrosshairScale();
        UpdateCornerNavButtons();
    }

    private void MovePolyline(int handleIndex, Point newPoint)
    {
        if (handleIndex < 0)
            return;

        // Update standard 4-corner polyline when dragging corner markers (index 0-3)
        if (handleIndex < 4 && lines is not null)
        {
            lines.Points[handleIndex] = newPoint;
            AspectRatioTransformPreview.SetAndScalePoints(lines.Points);
        }

        // Update tri-fold guide lines when in tri-fold mode
        if (isTriFoldMode)
            UpdateTriFoldGuideLines();

        // Update un-warp guide curves when in un-warp mode
        if (isUnWarpMode)
            UpdateUnWarpGuideCurves();
    }

    private async Task<(MagickImage Image, int TargetWidth, int TargetHeight)?> CorrectDistortion(string pathOfImage)
    {
        if (lines is null || selectedAspectRatio is null)
            return null;

        MagickImage image = new(pathOfImage);
        double scaleFactor = image.Width / MainImage.ActualWidth;

        //  #   X     Y
        //  1   798   304
        //  2   2410  236
        //  3   2753  1405
        //  4   704   1556
        //  3264 x 1836

        // Ratio defined by Height / Width
        double aspectRatio = selectedAspectRatio.RatioValue;

        if (selectedAspectRatio.AspectRatioEnum == AspectRatio.Original)
        {
            if (ViewModel.OriginalImageSize.Width > 0 && ViewModel.OriginalImageSize.Height > 0)
                aspectRatio = ViewModel.OriginalImageSize.Height / ViewModel.OriginalImageSize.Width;
            else
                return null;
        }
        else if (selectedAspectRatio.AspectRatioEnum == AspectRatio.Custom)
        {
            if (CustomHeight.Value is double height
                && CustomWidth.Value is double width
                && height != 0
                && width != 0)
                aspectRatio = height / width;
            else
                return null;
        }

        Rect? visualContentBounds = ReflectionHelper.GetPrivatePropertyValue(lines, "VisualContentBounds") as Rect?;
        Rect finalSize = new(0, 0, MainImage.ActualWidth, MainImage.ActualHeight);

        if (visualContentBounds is not null)
        {
            int width = (int)(visualContentBounds.Value.Width * scaleFactor);
            int height = (int)(width * aspectRatio);
            finalSize = new(0, 0, width, height);
        }

        double[] arguments =
        [
            // top left
            lines.Points[0].X * scaleFactor, lines.Points[0].Y * scaleFactor,
            0,0,

            // bottom left
            lines.Points[3].X * scaleFactor, lines.Points[3].Y * scaleFactor,
            0, finalSize.Height,

            // bottom right
            lines.Points[2].X * scaleFactor, lines.Points[2].Y * scaleFactor,
            finalSize.Width, finalSize.Height,

            // top right
            lines.Points[1].X * scaleFactor, lines.Points[1].Y * scaleFactor,
            finalSize.Width, 0,
        ];

        DistortSettings distortSettings = new(DistortMethod.Perspective)
        {
            Bestfit = true,
        };

        try
        {
            await Task.Run(() => image.Distort(distortSettings, arguments));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        return (image, (int)finalSize.Width, (int)finalSize.Height);
    }

    /// <summary>
    /// Applies the perspective distortion correction to the currently loaded image.
    /// This method also adjusts the position and size of any visible cropping rectangle
    /// to account for changes in image dimensions after distortion correction.
    /// </summary>
    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
            return;

        SetUiForLongTask();

        // Capture original image dimensions and crop rectangle position before distortion
        Size originalDisplaySize = new(MainImage.ActualWidth, MainImage.ActualHeight);
        bool cropRectangleVisible = CroppingRectangle.Visibility == Visibility.Visible;
        double originalCropLeft = 0, originalCropTop = 0, originalCropWidth = 0, originalCropHeight = 0;

        if (cropRectangleVisible)
        {
            originalCropLeft = Canvas.GetLeft(CroppingRectangle);
            originalCropTop = Canvas.GetTop(CroppingRectangle);
            originalCropWidth = CroppingRectangle.ActualWidth;
            originalCropHeight = CroppingRectangle.ActualHeight;
        }

        (MagickImage Image, int TargetWidth, int TargetHeight)? result = await CorrectDistortion(ViewModel.ImagePath);

        if (result is null)
        {
            SetUiForCompletedTask();
            return;
        }

        (MagickImage? image, int targetWidth, int targetHeight) = result.Value;

        bool alsoCropAndScale = AlsoCropAndScaleCheckBox.IsChecked == true;

        // Crop the distorted image to the exact target dimensions
        if (alsoCropAndScale && targetWidth > 0 && targetHeight > 0)
        {
            await Task.Run(() =>
            {
                image.Crop(new MagickGeometry((uint)targetWidth, (uint)targetHeight)
                {
                    IgnoreAspectRatio = true,
                });
                image.Page = new MagickGeometry(0, 0, (uint)targetWidth, (uint)targetHeight);
            });
        }

        string tempFileName = System.IO.Path.GetTempFileName();
        await image.WriteAsync(tempFileName);
        ViewModel.ImagePath = tempFileName;

        // Reset ImageGrid so it auto-sizes to the new image's aspect ratio.
        // This prevents letterboxing when the output has a different aspect ratio from
        // the previous ImageGrid size, which would cause a vertical/horizontal offset
        // when mapping subsequent detection coordinates.
        ImageGrid.Width = ImageWidthConst;
        ImageGrid.Height = double.NaN;

        MainImage.Source = image.ToBitmapSource();

        // Adjust cropping rectangle position if it was visible before distortion correction
        if (cropRectangleVisible)
        {
            // Force layout update to ensure MainImage has updated its ActualWidth/Height
            UpdateLayout();

            Size newDisplaySize = new(MainImage.ActualWidth, MainImage.ActualHeight);

            if (newDisplaySize.Width > 0 && newDisplaySize.Height > 0 &&
                originalDisplaySize.Width > 0 && originalDisplaySize.Height > 0)
            {
                // Calculate scale factors for the display size change
                double widthScale = newDisplaySize.Width / originalDisplaySize.Width;
                double heightScale = newDisplaySize.Height / originalDisplaySize.Height;

                // Transform the crop rectangle position and size
                double newCropLeft = originalCropLeft * widthScale;
                double newCropTop = originalCropTop * heightScale;
                double newCropWidth = originalCropWidth * widthScale;
                double newCropHeight = originalCropHeight * heightScale;

                // Ensure the adjusted rectangle stays within bounds
                newCropLeft = Math.Max(0, Math.Min(newCropLeft, newDisplaySize.Width - newCropWidth));
                newCropTop = Math.Max(0, Math.Min(newCropTop, newDisplaySize.Height - newCropHeight));
                newCropWidth = Math.Min(newCropWidth, newDisplaySize.Width - newCropLeft);
                newCropHeight = Math.Min(newCropHeight, newDisplaySize.Height - newCropTop);

                // Apply the adjusted position and size
                Canvas.SetLeft(CroppingRectangle, newCropLeft);
                Canvas.SetTop(CroppingRectangle, newCropTop);
                CroppingRectangle.Width = newCropWidth;
                CroppingRectangle.Height = newCropHeight;
            }
        }

        // Set measurement scale based on the selected aspect ratio's real-world dimensions
        if (alsoCropAndScale && selectedAspectRatio is not null
            && selectedAspectRatio.RealWorldWidth is double realWidth
            && selectedAspectRatio.RealWorldHeight is double realHeight
            && !string.IsNullOrEmpty(selectedAspectRatio.RealWorldUnits))
        {
            UpdateLayout();
            double displayWidth = MainImage.ActualWidth;
            if (displayWidth > 0)
            {
                ScaleInput.Value = realWidth / displayWidth;
                MeasurementUnits.Text = selectedAspectRatio.RealWorldUnits;
            }
        }

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Collapsed;

        SetUiForCompletedTask();
        HideTransformControls();
    }


    private async void ApplySaveSplitButton_Click(object sender, RoutedEventArgs e)
    {
        SetUiForLongTask();

        SaveFileDialog saveFileDialog = new()
        {
            Filter = "Image Files|*.jpg;",
            RestoreDirectory = true,
            FileName = $"{ViewModel.OpenedFileName}_corrected.jpg",
            InitialDirectory = !string.IsNullOrEmpty(ViewModel.OriginalFilePath) ? System.IO.Path.GetDirectoryName(ViewModel.OriginalFilePath) : null,
        };

        if (saveFileDialog.ShowDialog() is not true || lines is null)
        {
            BottomPane.IsEnabled = true;
            Cursor = null;
            SetUiForCompletedTask();
            return;
        }

        string correctedImageFileName = saveFileDialog.FileName;

        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath) || string.IsNullOrWhiteSpace(correctedImageFileName))
        {
            SetUiForCompletedTask();
            return;
        }

        (MagickImage Image, int TargetWidth, int TargetHeight)? result = await CorrectDistortion(ViewModel.ImagePath);


        if (result is null)
        {
            SetUiForCompletedTask();
            return;
        }

        (MagickImage? image, int _, int _) = result.Value;

        try
        {
            await image.WriteAsync(correctedImageFileName);

            OpenFolderButton.IsEnabled = true;
            SaveWindow saveWindow = new(correctedImageFileName);
            saveWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            ViewModel.SavedPath = correctedImageFileName;

            SetUiForCompletedTask();
            image.Dispose();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ViewModel.ImagePath))
            return;

        SetUiForLongTask();

        try
        {
            // Get current image dimensions
            MagickImage magickImage = new(ViewModel.ImagePath);
            double width = magickImage.Width;
            double height = magickImage.Height;
            magickImage.Dispose();

            // Create and show save options dialog in a window
            SaveOptionsDialog saveOptionsDialog = new(
                width,
                height,
                (options, cancellationToken) => EstimateSavedImageSizeAsync(
                    options,
                    (int)width,
                    (int)height,
                    cancellationToken));
            Window dialogWindow = new()
            {
                Title = "Save Options",
                Content = saveOptionsDialog,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize
            };

            // Show the dialog window
            bool? dialogResult = dialogWindow.ShowDialog();

            // If dialog was cancelled or closed
            if (dialogResult != true)
            {
                SetUiForCompletedTask();
                return;
            }

            SaveOptions options = saveOptionsDialog.Options;

            // Configure save file dialog based on selected format
            SaveFileDialog saveFileDialog = new()
            {
                Filter = SaveOptionsDialog.GetFileFilter(
                            MainWindowViewModel.Formats.FirstOrDefault(f => f.Format == options.Format)
                            ?? MainWindowViewModel.Formats[0]),
                DefaultExt = options.Extension,
                RestoreDirectory = true,
                FileName = $"{ViewModel.OpenedFileName}_edited{options.Extension}",
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                SetUiForCompletedTask();
                return;
            }

            string correctedImageFileName = saveFileDialog.FileName;

            using MagickImage image = CreateImageForSave(options, (int)width, (int)height);
            ApplySaveOptions(image, options);

            // Save with the selected format
            await image.WriteAsync(correctedImageFileName, options.Format);

            // Show preview and enable open folder button
            OpenFolderButton.IsEnabled = true;
            SaveWindow saveWindow = new(correctedImageFileName);
            saveWindow.Show();

            // Store the saved path for the open folder button
            ViewModel.SavedPath = correctedImageFileName;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetUiForCompletedTask();
        }
    }

    private MagickImage CreateImageForSave(SaveOptions options, int imageWidth, int imageHeight)
    {
        if (!options.IncludeMarkup && !options.IncludeMeasurements)
            return new MagickImage(ViewModel.ImagePath);

        BitmapSource renderedImage = RenderImageWithSelectedOverlays(
            imageWidth,
            imageHeight,
            options.IncludeMarkup,
            options.IncludeMeasurements);

        return new MagickImage(EncodeBitmapAsPng(renderedImage));
    }

    private async Task<long> EstimateSavedImageSizeAsync(
        SaveOptions options,
        int imageWidth,
        int imageHeight,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!options.IncludeMarkup && !options.IncludeMeasurements)
        {
            string imagePath = ViewModel.ImagePath;
            return await Task.Run(
                () => EncodeImageForSave(new MagickImage(imagePath), options, cancellationToken),
                cancellationToken);
        }

        BitmapSource renderedImage = RenderImageWithSelectedOverlays(
            imageWidth,
            imageHeight,
            options.IncludeMarkup,
            options.IncludeMeasurements);
        byte[] renderedImageBytes = EncodeBitmapAsPng(renderedImage);

        return await Task.Run(
            () => EncodeImageForSave(new MagickImage(renderedImageBytes), options, cancellationToken),
            cancellationToken);
    }

    private static long EncodeImageForSave(
        MagickImage image,
        SaveOptions options,
        CancellationToken cancellationToken)
    {
        using (image)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplySaveOptions(image, options);
            cancellationToken.ThrowIfCancellationRequested();
            return image.ToByteArray(options.Format).LongLength;
        }
    }

    private static byte[] EncodeBitmapAsPng(BitmapSource image)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using MemoryStream stream = new();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static void ApplySaveOptions(MagickImage image, SaveOptions options)
    {
        if (options.Resize)
        {
            MagickGeometry resizeGeometry = new((uint)options.Width, (uint)options.Height)
            {
                IgnoreAspectRatio = !options.MaintainAspectRatio
            };
            image.Resize(resizeGeometry);
        }

        if (options.Format is MagickFormat.Jpg or MagickFormat.WebP)
            image.Quality = (uint)options.Quality;
    }

    private BitmapSource RenderImageWithSelectedOverlays(
        int imageWidth,
        int imageHeight,
        bool includeMarkup,
        bool includeMeasurements)
    {
        if (MainImage.ActualWidth <= 0 || MainImage.ActualHeight <= 0)
            throw new InvalidOperationException("The image must be loaded before annotations can be saved.");

        HashSet<UIElement> includedElements = [ImageGrid];

        if (includeMeasurements)
        {
            includedElements.UnionWith(measurementTools);
            includedElements.UnionWith(angleMeasurementTools);
            includedElements.UnionWith(rectangleMeasurementTools);
            includedElements.UnionWith(polygonMeasurementTools);
            includedElements.UnionWith(circleMeasurementTools);
            includedElements.UnionWith(verticalLineControls);
            includedElements.UnionWith(horizontalLineControls);
            includedElements.UnionWith(ShapeCanvas.Children.OfType<StrokeLengthDisplay>());
        }

        if (includeMarkup)
        {
            includedElements.UnionWith(markupShapeControls);
            includedElements.UnionWith(markupTextControls);
        }

        Dictionary<UIElement, Visibility> visibilityBeforeRender = [];
        void SetVisibilityForRender(UIElement element, Visibility visibility)
        {
            visibilityBeforeRender.TryAdd(element, element.Visibility);
            element.Visibility = visibility;
        }

        Dictionary<MarkupShapeControl, bool> markupGizmoVisibility = [];
        StopCanvasTranslateAnimation();
        double originalScaleX = canvasScale.ScaleX;
        double originalScaleY = canvasScale.ScaleY;
        double originalTranslateX = canvasTranslate.X;
        double originalTranslateY = canvasTranslate.Y;

        try
        {
            foreach (UIElement element in ShapeCanvas.Children)
            {
                SetVisibilityForRender(
                    element,
                    includedElements.Contains(element) ? Visibility.Visible : Visibility.Collapsed);
            }

            SetVisibilityForRender(DrawingCanvas, includeMeasurements ? Visibility.Visible : Visibility.Collapsed);
            SetVisibilityForRender(MarkupCanvas, includeMarkup ? Visibility.Visible : Visibility.Collapsed);
            SetVisibilityForRender(EraseMaskCanvas, Visibility.Collapsed);
            SetVisibilityForRender(ImageResizeGrip, Visibility.Collapsed);

            foreach (MarkupShapeControl control in markupShapeControls)
            {
                markupGizmoVisibility[control] = control.IsDragGizmoVisible;
                control.IsDragGizmoVisible = false;
            }

            canvasScale.ScaleX = 1;
            canvasScale.ScaleY = 1;
            canvasTranslate.X = 0;
            canvasTranslate.Y = 0;

            ShapeCanvas.UpdateLayout();

            DrawingVisual visual = new();
            using (DrawingContext context = visual.RenderOpen())
            {
                VisualBrush imageBrush = new(ShapeCanvas)
                {
                    Stretch = Stretch.Fill,
                    Viewbox = new Rect(0, 0, MainImage.ActualWidth, MainImage.ActualHeight),
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewport = new Rect(0, 0, imageWidth, imageHeight),
                    ViewportUnits = BrushMappingMode.Absolute
                };
                context.DrawRectangle(imageBrush, null, new Rect(0, 0, imageWidth, imageHeight));
            }

            RenderTargetBitmap renderedImage = new(
                imageWidth,
                imageHeight,
                96,
                96,
                PixelFormats.Pbgra32);
            renderedImage.Render(visual);
            renderedImage.Freeze();
            return renderedImage;
        }
        finally
        {
            foreach ((UIElement element, Visibility visibility) in visibilityBeforeRender)
                element.Visibility = visibility;

            foreach ((MarkupShapeControl control, bool visible) in markupGizmoVisibility)
                control.IsDragGizmoVisible = visible;

            canvasScale.ScaleX = originalScaleX;
            canvasScale.ScaleY = originalScaleY;
            canvasTranslate.X = originalTranslateX;
            canvasTranslate.Y = originalTranslateY;
        }
    }

    private void SetUiForLongTask()
    {
        BottomPane.IsEnabled = false;
        Cursor = Cursors.Wait;
        ViewModel.IsBusy = true;
        autoSaveTimer?.Stop();
    }

    private void SetUiForCompletedTask()
    {
        ViewModel.IsBusy = false;
        Cursor = null;
        BottomPane.IsEnabled = true;

        autoSaveTimer?.Stop();
        autoSaveTimer?.Start();
    }

    private void LocalAdjustmentCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        LocalAdjustmentRectangle.SetAppearance(
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x00)),
            showFill: false);
        LocalAdjustmentRectangle.Visibility = Visibility.Visible;
    }

    private void LocalAdjustmentCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        LocalAdjustmentRectangle.Visibility = Visibility.Collapsed;
    }

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        SetUiForLongTask();

        OpenFileDialog openFileDialog = new()
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.heic;*.heif;*.bmp;*.gif;*.tif;*.tiff;*.webp|All files (*.*)|*.*",
            RestoreDirectory = true,
        };

        if (openFileDialog.ShowDialog() != true)
        {
            SetUiForCompletedTask();
            WelcomeMessageModal.Visibility = Visibility.Visible;
            return;
        }

        RemoveMeasurementControls();
        ViewModel.WindowTitle = $"Magick Crop & Measure: {System.IO.Path.GetFileName(openFileDialog.FileName)}";
        await OpenImagePath(openFileDialog.FileName);
    }

    private async void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        // Check if clipboard contains image data using robust detection
        if (!ClipboardHelper.ContainsImageData())
        {
            string availableFormats = ClipboardHelper.GetClipboardFormatsInfo();
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Paste Error",
                Content = $"No image found in clipboard. Copy an image first.\n\nAvailable clipboard formats: {availableFormats}",
            };
            await uiMessageBox.ShowDialogAsync();
            SetUiForCompletedTask();
            WelcomeMessageModal.Visibility = Visibility.Visible;
            return;
        }

        SetUiForLongTask();
        try
        {
            WelcomeMessageModal.Visibility = Visibility.Collapsed;

            // Use robust clipboard image retrieval
            BitmapSource? clipboardImage = ClipboardHelper.GetImageFromClipboard();

            if (clipboardImage is null)
            {
                string availableFormats = ClipboardHelper.GetClipboardFormatsInfo();
                Wpf.Ui.Controls.MessageBox uiMessageBox = new()
                {
                    Title = "Paste Error",
                    Content = $"Could not retrieve a valid image from the clipboard.\n\nDetected formats: {availableFormats}\n\nTry copying the image again or using a different source.",
                };
                await uiMessageBox.ShowDialogAsync();
                WelcomeMessageModal.Visibility = Visibility.Visible;
                return;
            }

            // Save the clipboard image to a temporary file using optimal format
            string tempFileName = ClipboardHelper.SaveImageToTempFile(clipboardImage);

            // Reset any current measurements
            RemoveMeasurementControls();
            ViewModel.OpenedFileName = "Pasted_Image_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // Open the image in the application
            await OpenImagePath(tempFileName);

            // Update UI
            ShowSidebar();
        }
        catch (Exception ex)
        {
            WelcomeMessageModal.Visibility = Visibility.Visible;
            string availableFormats = ClipboardHelper.GetClipboardFormatsInfo();
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Paste Error",
                Content = $"Error pasting image: {ex.Message}\n\nClipboard formats: {availableFormats}\n\nPlease try copying the image again.",
            };
            await uiMessageBox.ShowDialogAsync();
        }
        finally
        {
            SetUiForCompletedTask();
        }
    }

    private async void CameraButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetUiForLongTask();
            WelcomeMessageModal.Visibility = Visibility.Collapsed;

            nint hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);

            CameraCaptureUI cameraCaptureUI = new(windowId);
            cameraCaptureUI.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Png;

            StorageFile file = await cameraCaptureUI.CaptureFileAsync(CameraCaptureUIMode.Photo);

            if (file != null)
            {
                RemoveMeasurementControls();
                await OpenImagePath(file.Path);
                ViewModel.OpenedFileName = "CameraCapture-" + DateTime.Now.ToString("HH-mm-MMM-dd-yyyy");
                ShowSidebar();
            }
            else
            {
                WelcomeMessageModal.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            WelcomeMessageModal.Visibility = Visibility.Visible;
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Camera Error",
                Content = $"Error capturing image from camera: {ex.Message}",
            };
            await uiMessageBox.ShowDialogAsync();
        }
        finally
        {
            SetUiForCompletedTask();
        }
    }

    private void OverlayButton_Click(object sender, RoutedEventArgs e)
    {
        WelcomeMessageModal.Visibility = Visibility.Collapsed;
        ShowSidebar();
        MainGrid.Background = new SolidColorBrush(Colors.Transparent);
        Background = new SolidColorBrush(Colors.Transparent);
        ShapeCanvas.Background = new SolidColorBrush(Color.FromArgb(10, 255, 255, 255));
        Topmost = true;

        MeasureTabItem.IsSelected = true;
        TransformTabItem.IsEnabled = false;
        EditImageTabItem.IsEnabled = false;

        CropButtonPanel.Visibility = Visibility.Collapsed;
        TransformButtonPanel.Visibility = Visibility.Collapsed;
        ResizeButtonsPanel.Visibility = Visibility.Collapsed;
        SaveAndOpenDestFolderPanel.Visibility = Visibility.Collapsed;
        UndoRedoPanel.Visibility = Visibility.Collapsed;

        autoSaveTimer?.Stop();

        ImageIconOpenedName.Symbol = SymbolRegular.Ruler24;
        ViewModel.OpenedFileName = "Overlay Mode";
    }

    //protected override void OnExtendsContentIntoTitleBarChanged(bool oldValue, bool newValue)
    //{
    //    SetCurrentValue(WindowStyleProperty, WindowStyle);

    //    WindowChrome.SetWindowChrome(
    //        this,
    //        new WindowChrome
    //        {
    //            CaptionHeight = 0,
    //            CornerRadius = default,
    //            GlassFrameThickness = new Thickness(-1),
    //            ResizeBorderThickness = ResizeMode == ResizeMode.NoResize ? default : new Thickness(4),
    //            UseAeroCaptionButtons = false,
    //        }
    //    );

    //    _ = UnsafeNativeMethods.RemoveWindowTitlebarContents(this);
    //}

    private async Task OpenImagePath(string imageFilePath)
    {
        // Reset all transient state from any previous image / project
        ResetTransientState();
        RemoveMeasurementControls();
        HideTransformControls();
        HideCroppingControls();
        HideResizeControls();

        Save.IsEnabled = true;
        ImageGrid.Width = ImageWidthConst;
        ImageGrid.Height = double.NaN;
        MainImage.Stretch = Stretch.Uniform;

        WelcomeMessageModal.Visibility = Visibility.Collapsed;
        string tempFileName = System.IO.Path.GetTempFileName();
        tempFileName = System.IO.Path.ChangeExtension(tempFileName, ".jpg");
        await Task.Run(async () =>
        {
            MagickImage bitmap = new(imageFilePath);
            bitmap.AutoOrient();

            await bitmap.WriteAsync(tempFileName, MagickFormat.Jpeg);
        });

        MagickImage bitmapImage = new(tempFileName);

        ViewModel.ImagePath = tempFileName;
        ViewModel.OriginalFilePath = imageFilePath;
        ViewModel.OpenedFileName = System.IO.Path.GetFileNameWithoutExtension(imageFilePath);
        MainImage.Source = bitmapImage.ToBitmapSource();

        // Update original size after image is loaded (will be the default ImageWidthConst height calculated from aspect ratio)
        ViewModel.OriginalImageSize = new Size(bitmapImage.Width, bitmapImage.Height);

        // Update the aspect ratio preview if "Original" is currently selected
        if (selectedAspectRatio?.AspectRatioEnum == AspectRatio.Original)
            UpdateOriginalAspectRatioPreview();

        ShowSidebar();
        SetUiForCompletedTask();

        // Create a new project ID for this image
        ViewModel.CurrentProjectId = Guid.NewGuid().ToString();

        // Center and zoom to fit the image in the viewport
        CenterAndZoomToFit();
    }

    private async void ResetToOriginalMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string? originalPath = ViewModel.OriginalFilePath;
        if (string.IsNullOrWhiteSpace(originalPath) || !File.Exists(originalPath))
        {
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Reset to Original",
                Content = "The original image file could no longer be found, so the image cannot be reset.",
            };
            await uiMessageBox.ShowDialogAsync();
            return;
        }

        Wpf.Ui.Controls.MessageBox confirmBox = new()
        {
            Title = "Reset to Original",
            Content = "This will discard all edits and measurements and reload the original image. Continue?",
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
        };

        if (await confirmBox.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        await OpenImagePath(originalPath);
    }

    private const double ZoomFactor = 0.1;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;

    private void ShapeCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Only zoom when the mouse is over the canvas area so ScrollViewers elsewhere still work
        if (!MainGrid.IsMouseOver || IsOverMiniMap(e))
            return;

        // Get the current mouse position relative to the canvas
        Point mousePosition = e.GetPosition(ShapeCanvas);

        double zoomChange = e.Delta > 0 ? ZoomFactor : -ZoomFactor;
        ZoomAtCanvasPoint(canvasScale.ScaleX + (canvasScale.ScaleX * zoomChange), mousePosition);

        e.Handled = true;
    }

    private void ShapeCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (isAdornerRotatingDrag)
        {
            e.Handled = true;
            return;
        }
        Debug.WriteLine($"ShapeCanvas_MouseDown: Button={e.ChangedButton}, isCreatingMeasurement={isCreatingMeasurement}, isPlacingPolygon={isPlacingPolygonMeasurement}, ToolSelected={IsAnyToolSelected()}");

        // If in rotate mode with free rotate enabled and click on image start exclusive rotation drag
        // Only use legacy drag path when the RotateAdorner is not active
        if (isRotateMode && FreeRotateToggle != null && FreeRotateToggle.IsChecked == true && rotateAdorner == null && e.LeftButton == MouseButtonState.Pressed)
        {
            Point p = e.GetPosition(MainImage);
            // ensure point lies within image bounds to avoid starting when clicking UI overlays
            if (p.X >= 0 && p.Y >= 0 && p.X <= MainImage.ActualWidth && p.Y <= MainImage.ActualHeight)
            {
                isFreeRotatingDrag = true;
                freeRotateLastPoint = p;
                ShowRotationOverlay();
                UpdateRotationOverlay();
                e.Handled = true; // prevent panning setup
                return; // skip rest so other modes don't activate
            }
        }

        // White point picker mode - pick color from image
        if (isWhitePointPickerMode && e.LeftButton == MouseButtonState.Pressed)
        {
            Point imagePoint = e.GetPosition(MainImage);
            // Ensure click is within image bounds
            if (imagePoint.X >= 0 && imagePoint.Y >= 0 && imagePoint.X < MainImage.ActualWidth && imagePoint.Y < MainImage.ActualHeight)
            {
                _ = PickWhitePointColorAsync(imagePoint);
                e.Handled = true;
                return;
            }
        }

        // Black point picker mode - pick color from image
        if (isBlackPointPickerMode && e.LeftButton == MouseButtonState.Pressed)
        {
            Point imagePoint = e.GetPosition(MainImage);
            // Ensure click is within image bounds
            if (imagePoint.X >= 0 && imagePoint.Y >= 0 && imagePoint.X < MainImage.ActualWidth && imagePoint.Y < MainImage.ActualHeight)
            {
                _ = PickBlackPointColorAsync(imagePoint);
                e.Handled = true;
                return;
            }
        }

        // Edge correction mode - place a point on click (not while space-panning)
        if (isEdgeCorrectionMode && e.LeftButton == MouseButtonState.Pressed && !isEdgeCorrectionSpacePanning)
        {
            Point imagePoint = e.GetPosition(MainImage);
            if (imagePoint.X >= 0 && imagePoint.Y >= 0
                && imagePoint.X <= MainImage.ActualWidth
                && imagePoint.Y <= MainImage.ActualHeight)
            {
                AddEdgeCorrectionPoint(imagePoint);
                e.Handled = true;
                return;
            }
        }

        // Edge correction space-bar panning
        if (isEdgeCorrectionMode && isEdgeCorrectionSpacePanning && e.LeftButton == MouseButtonState.Pressed)
        {
            draggingMode = DraggingMode.Panning;
            clickedPoint = e.GetPosition(this);
            ShapeCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        // Grid straighten space-bar panning
        if (isGridStraightenMode && isGridStraightenSpacePanning && e.LeftButton == MouseButtonState.Pressed)
        {
            draggingMode = DraggingMode.Panning;
            clickedPoint = e.GetPosition(this);
            ShapeCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        // In grid straighten mode, don't fall through to other tools
        if (isGridStraightenMode && e.LeftButton == MouseButtonState.Pressed)
        {
            e.Handled = true;
            return;
        }

        // Check if we're in the measure tab and starting a measurement
        if (Mouse.LeftButton != MouseButtonState.Pressed)
        {
            Debug.WriteLine($"Left button not pressed, returning. Button state: {Mouse.LeftButton}");
            return;
        }

        clickedPoint = e.GetPosition(ShapeCanvas);

        // A markup text edit in progress absorbs this click: commit it instead of
        // starting a new tool action (otherwise clicking away to accept the text
        // would immediately place another text box)
        if (CommitPendingMarkupTextEdit())
        {
            e.Handled = true;
            return;
        }

        // --- ANGLE MEASUREMENT PLACEMENT LOGIC ---
        if (isPlacingAngleMeasurement && anglePlacementStep == AnglePlacementStep.PlacingThirdPoint && activeAnglePlacementControl != null)
        {
            // Finalize third point
            activeAnglePlacementControl.MovePoint(2, clickedPoint);
            // Enable hit testing for Point3 now that placement is complete
            activeAnglePlacementControl.SetPoint3HitTestable(true);
            angleMeasurementTools.Add(activeAnglePlacementControl);
            activeAnglePlacementControl.MeasurementPointMouseDown += AngleMeasurementPoint_MouseDown;
            activeAnglePlacementControl.RemoveControlRequested += AngleMeasurementControl_RemoveControlRequested;
            isPlacingAngleMeasurement = false;
            anglePlacementStep = AnglePlacementStep.None;
            activeAnglePlacementControl = null;
            isCreatingMeasurement = false;
            draggingMode = DraggingMode.None;
            ShapeCanvas.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        // --- MARKUP SHAPE PLACEMENT ---
        if (isMarkupShapeMode && e.LeftButton == MouseButtonState.Pressed)
        {
            MarkupShapeControl shapeControl = new()
            {
                ShapeType = activeMarkupShapeType,
                StrokeColor = markupColor,
                StrokeThickness = markupSize,
                IsDragGizmoVisible = MarkupTabItem?.IsSelected == true,
                IsHitTestVisible = MarkupTabItem?.IsSelected == true
            };
            shapeControl.MeasurementPointMouseDown += MarkupShapePoint_MouseDown;
            shapeControl.RemoveControlRequested += MarkupShapeControl_RemoveControlRequested;
            markupShapeControls.Add(shapeControl);
            ShapeCanvas.Children.Add(shapeControl);
            shapeControl.MovePoint(0, clickedPoint);
            shapeControl.StartDraggingPoint(1); // fires MarkupShapePoint_MouseDown → CaptureMouse
            isMarkupShapeDragCreation = true;   // distinguishes creation from handle move for undo
            draggingMode = DraggingMode.MarkupShape;
            e.Handled = true;
            return;
        }

        // --- MARKUP TEXT PLACEMENT ---
        if (isMarkupTextMode && e.LeftButton == MouseButtonState.Pressed)
        {
            MarkupTextControl textControl = new()
            {
                TextColor = markupColor,
                MarkupFontSize = markupSize * 4,
                IsHitTestVisible = MarkupTabItem?.IsSelected == true
            };
            textControl.RemoveControlRequested += MarkupTextControl_RemoveControlRequested;
            Canvas.SetLeft(textControl, clickedPoint.X);
            Canvas.SetTop(textControl, clickedPoint.Y);
            markupTextControls.Add(textControl);
            ShapeCanvas.Children.Add(textControl);

            // Push the undo item only once the initial edit is committed; cancelling
            // (Escape, or committing empty text) discards the control entirely
            MarkupTextControl ctrl = textControl;
            void OnFirstCommit(object? s, EventArgs args)
            {
                ctrl.EditCommitted -= OnFirstCommit;
                ctrl.EditCancelled -= OnFirstCancel;
                UndoRedo.AddUndo(new MarkupTextAddedItem(
                    ctrl, markupTextControls, ShapeCanvas,
                    wireEvents: () => ctrl.RemoveControlRequested += MarkupTextControl_RemoveControlRequested,
                    unwireEvents: () => ctrl.RemoveControlRequested -= MarkupTextControl_RemoveControlRequested));

                // From now on, edits and drags of this label get their own undo items
                ctrl.EditCommitted += MarkupTextControl_EditCommitted;
                ctrl.TextMoved += MarkupTextControl_TextMoved;
            }
            void OnFirstCancel(object? s, EventArgs args)
            {
                ctrl.EditCommitted -= OnFirstCommit;
                ctrl.EditCancelled -= OnFirstCancel;
                ctrl.RemoveControlRequested -= MarkupTextControl_RemoveControlRequested;
                markupTextControls.Remove(ctrl);
                ShapeCanvas.Children.Remove(ctrl);
            }
            ctrl.EditCommitted += OnFirstCommit;
            ctrl.EditCancelled += OnFirstCancel;

            textControl.EnterEditMode();
            e.Handled = true;
            return;
        }

        if (MeasureDistanceToggle.IsChecked is true)
        {
            double scale = ScaleInput.Value ?? 1.0;
            DistanceMeasurementControl measurementControl = new()
            {
                ScaleFactor = scale,
                Units = MeasurementUnits.Text
            };
            measurementControl.MeasurementPointMouseDown += MeasurementPoint_MouseDown;
            measurementControl.SetRealWorldLengthRequested += MeasurementControl_SetRealWorldLengthRequested;
            measurementControl.RemoveControlRequested += DistanceMeasurementControl_RemoveControlRequested;
            measurementTools.Add(measurementControl);
            ShapeCanvas.Children.Add(measurementControl);

            // Set the start and end positions of the measurement
            measurementControl.MovePoint(0, clickedPoint);
            measurementControl.StartDraggingPoint(1);
            isCreatingMeasurement = true;

            // Show pixel zoom for precise measurement placement
            ShowPixelZoom(clickedPoint);
        }
        else if (MeasureAngleToggle.IsChecked is true)
        {
            // Start angle measurement placement
            isPlacingAngleMeasurement = true;
            anglePlacementStep = AnglePlacementStep.DraggingFirstLeg;
            isCreatingMeasurement = false;
            draggingMode = DraggingMode.None;

            // Create the control 
            activeAnglePlacementControl = new AngleMeasurementControl();

            // Disable hit testing for Point3 during placement
            activeAnglePlacementControl.SetPoint3HitTestable(false);

            // Set initial positions - vertex at clicked point, others will be moved
            activeAnglePlacementControl.MovePoint(1, clickedPoint); // vertex at click point
            activeAnglePlacementControl.MovePoint(0, clickedPoint); // point1 starts at vertex
            activeAnglePlacementControl.MovePoint(2, clickedPoint); // point3 starts at vertex

            ShapeCanvas.Children.Add(activeAnglePlacementControl);
            ShapeCanvas.CaptureMouse();

            // Show pixel zoom for precise angle placement
            ShowPixelZoom(clickedPoint);

            e.Handled = true;
            return;
        }
        else if (RectangleMeasureToggle.IsChecked is true)
        {
            isPlacingRectangleMeasurement = true;
            draggingMode = DraggingMode.CreatingMeasurement; // Use CreatingMeasurement to signify drag
            isCreatingMeasurement = true; // Ensure this is set for MouseUp cleanup

            // Create new rectangle control with current scale factor and units
            activeRectanglePlacementControl = new RectangleMeasurementControl
            {
                ScaleFactor = ScaleInput.Value ?? 1.0,
                Units = MeasurementUnits.Text
            };

            // Show pixel zoom for precise rectangle placement
            ShowPixelZoom(clickedPoint);

            activeRectanglePlacementControl.MovePoint(0, clickedPoint); // Set top-left to initial click
            activeRectanglePlacementControl.MovePoint(1, clickedPoint); // Set bottom-right to initial click, will be updated on mouse move/up
            ShapeCanvas.Children.Add(activeRectanglePlacementControl);
            ShapeCanvas.CaptureMouse();
            e.Handled = true;
        }
        else if (PolygonMeasureToggle.IsChecked is true)
        {
            Debug.WriteLine($"Polygon tool clicked at: ({clickedPoint.X:F1}, {clickedPoint.Y:F1})");

            if (!isPlacingPolygonMeasurement)
            {
                // Start new polygon
                Debug.WriteLine("Starting new polygon");
                isPlacingPolygonMeasurement = true;
                isCreatingMeasurement = true; // This prevents panning interference
                activePolygonPlacementControl = new PolygonMeasurementControl
                {
                    ScaleFactor = ScaleInput.Value ?? 1.0,
                    Units = MeasurementUnits.Text
                };
                ShapeCanvas.Children.Add(activePolygonPlacementControl);
                // Don't capture mouse for polygon - we need to allow multiple clicks
            }

            if (activePolygonPlacementControl != null)
            {
                Debug.WriteLine($"Adding vertex to existing polygon. Current count: {activePolygonPlacementControl.VertexCount}");
                activePolygonPlacementControl.AddVertex(clickedPoint);

                // If polygon was closed, finalize it
                if (activePolygonPlacementControl.IsClosed)
                {
                    Debug.WriteLine("Polygon closed, finalizing");
                    polygonMeasurementTools.Add(activePolygonPlacementControl);
                    Debug.WriteLine($"Added polygon to collection. Total polygons: {polygonMeasurementTools.Count}");
                    activePolygonPlacementControl.MeasurementPointMouseDown += PolygonMeasurementPoint_MouseDown;
                    activePolygonPlacementControl.RemoveControlRequested += PolygonMeasurementControl_RemoveControlRequested;
                    isPlacingPolygonMeasurement = false;
                    isCreatingMeasurement = false; // Reset this when polygon is complete
                    activePolygonPlacementControl = null;
                    Debug.WriteLine("Polygon finalization complete");
                }
            }
            e.Handled = true;
        }
        else if (CircleMeasureToggle.IsChecked is true)
        {
            isPlacingCircleMeasurement = true;
            draggingMode = DraggingMode.CreatingMeasurement; // Use CreatingMeasurement to signify drag
            isCreatingMeasurement = true; // Ensure this is set for MouseUp cleanup

            // Create new circle control with current scale factor and units
            activeCirclePlacementControl = new CircleMeasurementControl
            {
                ScaleFactor = ScaleInput.Value ?? 1.0,
                Units = MeasurementUnits.Text
            };

            // Show pixel zoom for precise circle placement
            ShowPixelZoom(clickedPoint);

            activeCirclePlacementControl.MovePoint(0, clickedPoint); // Set center to initial click
            activeCirclePlacementControl.MovePoint(1, clickedPoint); // Set edge to initial click, will be updated on mouse move/up
            ShapeCanvas.Children.Add(activeCirclePlacementControl);
            ShapeCanvas.CaptureMouse();
            e.Handled = true;
        }
        else if (isPlacingAngleMeasurement)
        {
            // Should not happen, but safety
            e.Handled = true;
            return;
        }
        else if (DrawingLinesToggle.IsChecked is true)
        {
            isCreatingMeasurement = true;
            draggingMode = DraggingMode.CreatingMeasurement;
            ShapeCanvas.CaptureMouse();
            e.Handled = true;
        }
        else if (HorizontalLineRadio.IsChecked is true)
        {
            AddHorizontalLineAtPosition(clickedPoint.Y);
        }
        else if (VerticalLineToggle.IsChecked is true)
        {
            AddVerticalLineAtPosition(clickedPoint.X);
        }
        else
        {
            // No tools active -> begin panning
            if (!IsAnyToolSelected() && !isRotateMode)
            {
                draggingMode = DraggingMode.Panning;
                clickedPoint = e.GetPosition(this);
                ShapeCanvas.CaptureMouse();
                e.Handled = true;
            }
        }
    }

    private void ShapeCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        // Hide pixel zoom only if we're not in a precision mode anymore
        if (!ShouldShowPixelZoom())
        {
            HidePixelZoom();
        }

        // If we were panning, release immediately so wheel events work even without a post-release move
        if (draggingMode == DraggingMode.Panning)
        {
            draggingMode = DraggingMode.None;
            ShapeCanvas.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        // --- ANGLE MEASUREMENT PLACEMENT LOGIC ---
        if (isPlacingAngleMeasurement && anglePlacementStep == AnglePlacementStep.DraggingFirstLeg && activeAnglePlacementControl != null)
        {
            anglePlacementStep = AnglePlacementStep.PlacingThirdPoint;
            ShapeCanvas.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (isCreatingMeasurement && draggingMode == DraggingMode.CreatingMeasurement)
        {
            Point endPoint = e.GetPosition(ShapeCanvas);
            if (Math.Abs(endPoint.X - clickedPoint.X) > 5 || Math.Abs(endPoint.Y - clickedPoint.Y) > 5)
            {
                if (isPlacingRectangleMeasurement && activeRectanglePlacementControl != null)
                {
                    activeRectanglePlacementControl.ScaleFactor = ScaleInput.Value ?? 1.0;
                    activeRectanglePlacementControl.Units = MeasurementUnits.Text;
                    activeRectanglePlacementControl.MovePoint(1, endPoint);
                    rectangleMeasurementTools.Add(activeRectanglePlacementControl);
                    activeRectanglePlacementControl.MeasurementPointMouseDown += RectangleMeasurementPoint_MouseDown;
                    activeRectanglePlacementControl.RemoveControlRequested += RectangleMeasurementControl_RemoveControlRequested;
                    activeRectanglePlacementControl = null;
                    isPlacingRectangleMeasurement = false;
                }
                else if (isPlacingCircleMeasurement && activeCirclePlacementControl != null)
                {
                    activeCirclePlacementControl.ScaleFactor = ScaleInput.Value ?? 1.0;
                    activeCirclePlacementControl.Units = MeasurementUnits.Text;
                    activeCirclePlacementControl.MovePoint(1, endPoint);
                    circleMeasurementTools.Add(activeCirclePlacementControl);
                    activeCirclePlacementControl.MeasurementPointMouseDown += CircleMeasurementPoint_MouseDown;
                    activeCirclePlacementControl.RemoveControlRequested += CircleMeasurementControl_RemoveControlRequested;
                    activeCirclePlacementControl = null;
                    isPlacingCircleMeasurement = false;
                }
                else
                {
                    CreateMeasurementFromDrag(clickedPoint, endPoint);
                }
            }
            else if (isPlacingRectangleMeasurement && activeRectanglePlacementControl != null)
            {
                ShapeCanvas.Children.Remove(activeRectanglePlacementControl);
                activeRectanglePlacementControl = null;
                isPlacingRectangleMeasurement = false;
            }
            else if (isPlacingCircleMeasurement && activeCirclePlacementControl != null)
            {
                ShapeCanvas.Children.Remove(activeCirclePlacementControl);
                activeCirclePlacementControl = null;
                isPlacingCircleMeasurement = false;
            }

            isCreatingMeasurement = false;
            draggingMode = DraggingMode.None;
            ShapeCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void AspectRatioComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not AspectRatioItem item || !IsLoaded)
            return;

        selectedAspectRatio = item;

        if (item.AspectRatioEnum == AspectRatio.Custom)
        {
            CustomButtonGrid.Visibility = Visibility.Visible;
            UpdateCustomAspectRatioPreview();
            return;
        }

        CustomButtonGrid.Visibility = Visibility.Collapsed;

        if (item.AspectRatioEnum == AspectRatio.Original)
        {
            UpdateOriginalAspectRatioPreview();
            return;
        }

        AspectRatioTransformPreview.RatioItem = item;
    }

    private void CustomWidth_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        double aspectRatio = double.NaN;

        if (CustomHeight.Value is double height && CustomWidth.Value is double width && height != 0 && width != 0)
            aspectRatio = height / width;

        double trimmedValue = Math.Round(aspectRatio, 2);
        AspectRatioTextBox.Text = $"Ratio: {trimmedValue}";

        UpdateCustomAspectRatioPreview();
    }

    /// <summary>
    /// Updates the aspect ratio preview shape to match the currently loaded image's original dimensions.
    /// Does not mutate items in the ComboBox to avoid breaking selection tracking.
    /// </summary>
    private void UpdateOriginalAspectRatioPreview()
    {
        if (ViewModel.OriginalImageSize.Width <= 0 || ViewModel.OriginalImageSize.Height <= 0)
            return;

        double ratio = ViewModel.OriginalImageSize.Height / ViewModel.OriginalImageSize.Width;
        AspectRatioItem previewItem = new()
        {
            RatioValue = ratio,
            AspectRatioEnum = AspectRatio.Original
        };
        AspectRatioTransformPreview.RatioItem = previewItem;
    }

    /// <summary>
    /// Updates the aspect ratio preview shape to match the current custom width/height values.
    /// </summary>
    private void UpdateCustomAspectRatioPreview()
    {
        if (CustomHeight.Value is double h && CustomWidth.Value is double w && h != 0 && w != 0)
        {
            double ratio = h / w;
            AspectRatioItem customPreview = new()
            {
                RatioValue = ratio,
                AspectRatioEnum = AspectRatio.Custom
            };
            AspectRatioTransformPreview.RatioItem = customPreview;
        }
    }

    private void FluentWindow_PreviewDragOver(object sender, DragEventArgs e)
    {
        bool isText = e.Data.GetDataPresent("Text");
        e.Handled = true;

        if (isText)
        {
            string textData = (string)e.Data.GetData("Text");
            if (!File.Exists(textData))
            {
                e.Effects = DragDropEffects.None;
                return;
            }
        }

        // After here we will now allow the dropping of "non-text" content
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void FluentWindow_PreviewDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetDataPresent("Text"))
        {
            if (e.Data.GetData("Text") is string filePath && File.Exists(filePath))
            {
                await OpenImagePath(filePath);
            }
            return;
        }


        if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
        {
            if (e.Data.GetData(DataFormats.FileDrop, true) is not string[] fileNames || fileNames.Length == 0)
                return;

            if (File.Exists(fileNames[0]))
                await OpenImagePath(fileNames[0]);
        }
    }

    private void ResetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ResetCanvasNavigation();
    }

    private void ResetCanvasNavigationButton_Click(object sender, RoutedEventArgs e)
    {
        ResetCanvasNavigation();
    }

    private void ResetCanvasNavigation()
    {
        StopCanvasTranslateAnimation();

        canvasScale.ScaleX = 1;
        canvasScale.ScaleY = 1;

        canvasScale.CenterX = 0;
        canvasScale.CenterY = 0;

        canvasTranslate.X = 0;
        canvasTranslate.Y = 0;
        UpdateCanvasNavigationUi();
    }

    private void FitImageButton_Click(object sender, RoutedEventArgs e)
    {
        CenterAndZoomToFit();
    }

    private void FitTransformButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetActiveTransformBounds(out Rect bounds))
            ZoomToFitBounds(bounds);
        else
            CenterAndZoomToFit();
    }

    private void AllowOutsideImageToggle_Changed(object sender, RoutedEventArgs e)
    {
        allowHandlesOutsideImage = AllowOutsideImageToggle.IsChecked == true;
    }

    private void CanvasZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || isUpdatingCanvasNavigation)
            return;

        double scale = Math.Clamp(e.NewValue, MinZoom, MaxZoom);
        if (Math.Abs(scale - canvasScale.ScaleX) < double.Epsilon)
            return;

        Point viewportCenter = new(MainGrid.ActualWidth / 2, MainGrid.ActualHeight / 2);
        ZoomAtViewportPoint(scale, viewportCenter);
    }

    private void CanvasScale_Changed(object? sender, EventArgs e)
    {
        if (!IsInitialized)
            return;

        UpdateTransformVisualScale();
        UpdateCanvasNavigationUi();
        UpdateMiniMap();
    }

    private void CanvasTranslate_Changed(object? sender, EventArgs e)
    {
        if (!IsInitialized)
            return;

        UpdateMiniMap();
    }

    /// <summary>
    /// Recomputes the visible canvas region and pushes it to the mini map overlay.
    /// </summary>
    private void UpdateMiniMap()
    {
        if (!IsInitialized)
            return;

        bool hasContent = false;
        double scale = canvasScale.ScaleX;

        if (showMiniMap
            && scale > 0
            && MainImage.Source is not null
            && MainImage.ActualWidth > 0
            && MainImage.ActualHeight > 0
            && MainGrid.ActualWidth > 0
            && MainGrid.ActualHeight > 0)
        {
            Rect viewportInCanvas = new(
                (-CanvasOriginOffset - canvasTranslate.X) / scale,
                (-CanvasOriginOffset - canvasTranslate.Y) / scale,
                MainGrid.ActualWidth / scale,
                MainGrid.ActualHeight / scale);

            hasContent = CanvasMiniMap.UpdateMap(
                MainImage.Source,
                new Size(MainImage.ActualWidth, MainImage.ActualHeight),
                viewportInCanvas);
        }

        CanvasMiniMap.Visibility = hasContent ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CanvasMiniMap_ViewportCenterRequested(object? sender, Point canvasPoint)
    {
        CenterViewportOnCanvasPoint(canvasPoint, animate: false);
    }

    private void UpdateCanvasNavigationUi()
    {
        if (!IsInitialized)
            return;

        isUpdatingCanvasNavigation = true;
        try
        {
            double scale = Math.Clamp(canvasScale.ScaleX, MinZoom, MaxZoom);
            CanvasZoomSlider.Value = scale;
            CanvasZoomText.Text = $"{scale:P0}";
        }
        finally
        {
            isUpdatingCanvasNavigation = false;
        }
    }

    private bool TryGetActiveTransformBounds(out Rect bounds)
    {
        Ellipse[] corners = [TopLeft, TopRight, BottomRight, BottomLeft];
        if (corners.All(handle => handle.Visibility == Visibility.Visible))
        {
            double minX = corners.Min(handle => Canvas.GetLeft(handle) + (handle.Width / 2));
            double minY = corners.Min(handle => Canvas.GetTop(handle) + (handle.Height / 2));
            double maxX = corners.Max(handle => Canvas.GetLeft(handle) + (handle.Width / 2));
            double maxY = corners.Max(handle => Canvas.GetTop(handle) + (handle.Height / 2));
            bounds = new Rect(new Point(minX, minY), new Point(maxX, maxY));
            return bounds.Width > 0 && bounds.Height > 0;
        }

        if (CroppingRectangle.Visibility == Visibility.Visible
            && CroppingRectangle.ActualWidth > 0
            && CroppingRectangle.ActualHeight > 0)
        {
            bounds = new Rect(
                Canvas.GetLeft(CroppingRectangle),
                Canvas.GetTop(CroppingRectangle),
                CroppingRectangle.ActualWidth,
                CroppingRectangle.ActualHeight);
            return true;
        }

        bounds = Rect.Empty;
        return false;
    }

    private void ZoomToFitBounds(Rect bounds)
    {
        if (MainGrid.ActualWidth <= 0 || MainGrid.ActualHeight <= 0 || bounds.IsEmpty)
            return;

        const double paddingFactor = 0.85;
        double availableWidth = MainGrid.ActualWidth * paddingFactor;
        double availableHeight = MainGrid.ActualHeight * paddingFactor;
        double scale = Math.Clamp(
            Math.Min(availableWidth / bounds.Width, availableHeight / bounds.Height),
            MinZoom,
            MaxZoom);

        canvasScale.ScaleX = scale;
        canvasScale.ScaleY = scale;
        StopCanvasTranslateAnimation();
        canvasTranslate.X = (MainGrid.ActualWidth / 2) - ((50 + bounds.X + (bounds.Width / 2)) * scale);
        canvasTranslate.Y = (MainGrid.ActualHeight / 2) - ((50 + bounds.Y + (bounds.Height / 2)) * scale);
        UpdateCanvasNavigationUi();
    }

    /// <summary>
    /// Centers and zooms the canvas to fit the image in the viewport with padding.
    /// </summary>
    private void CenterAndZoomToFit()
    {
        if (MainImage.Source == null || MainGrid.ActualWidth == 0 || MainGrid.ActualHeight == 0)
            return;

        // Force layout update to ensure ImageGrid has rendered
        UpdateLayout();

        // Get the viewport size (the visible area in MainGrid)
        double viewportWidth = MainGrid.ActualWidth;
        double viewportHeight = MainGrid.ActualHeight;

        // Get the image size (ImageGrid size which contains the image)
        double imageWidth = ImageGrid.ActualWidth;
        double imageHeight = ImageGrid.ActualHeight;

        if (imageWidth == 0 || imageHeight == 0)
            return;

        ZoomToFitBounds(new Rect(0, 0, imageWidth, imageHeight));
    }

    /// <summary>
    /// Menu item handler to center and zoom to fit the image on demand.
    /// </summary>
    private void CenterAndZoomToFitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CenterAndZoomToFit();
    }

    private void MainGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsOverMiniMap(e) || IsOverCornerNavButton(e))
            return;

        if (e.ChangedButton == MouseButton.Left && isMarkupSelectMode
            && TryStartMarkupSelectGesture(e))
        {
            return;
        }

        if (e.ChangedButton != MouseButton.Middle)
            return;

        draggingMode = DraggingMode.Panning;
        clickedPoint = e.GetPosition(this);
        MainGrid.CaptureMouse();
        Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    /// <summary>
    /// Handles a left-button press while the markup "Select" tool is active. Clicking an
    /// already-selected shape/text (as part of a multi-item selection) drags the whole
    /// group together; clicking empty canvas starts a rubber-band marquee that can enclose
    /// ink strokes, shapes, and text as one group; clicking a stroke is left untouched so the
    /// native InkCanvas selection/resize behavior keeps working for ink-only selections.
    /// </summary>
    /// <returns>True if the gesture was handled here and should not fall through.</returns>
    private bool TryStartMarkupSelectGesture(MouseButtonEventArgs e)
    {
        Point clickPoint = e.GetPosition(ShapeCanvas);

        if (e.OriginalSource is DependencyObject originalSource)
        {
            MarkupShapeControl? hitShape = FindAncestor<MarkupShapeControl>(originalSource);
            MarkupTextControl? hitText = FindAncestor<MarkupTextControl>(originalSource);

            if (hitShape is not null || hitText is not null)
            {
                bool alreadySelected = (hitShape is not null && selectedMarkupShapes.Contains(hitShape))
                    || (hitText is not null && selectedMarkupTexts.Contains(hitText));
                int totalSelected = selectedMarkupShapes.Count + selectedMarkupTexts.Count
                    + MarkupCanvas.GetSelectedStrokes().Count;

                if (alreadySelected && totalSelected > 1)
                {
                    BeginMarkupGroupMove(clickPoint);
                    e.Handled = true;
                    return true;
                }

                // Single click on a (possibly unselected) item: reset any existing group
                // selection but let the item's own click/drag/edit behavior proceed untouched.
                ClearMarkupGroupSelection();
                return false;
            }
        }

        Stroke? hitStroke = null;
        foreach (Stroke stroke in MarkupCanvas.Strokes)
        {
            if (stroke.HitTest(clickPoint))
            {
                hitStroke = stroke;
                break;
            }
        }

        if (hitStroke is not null)
        {
            // Preserve an existing mixed group only if this stroke is already part of it;
            // otherwise a fresh ink-only interaction is starting, so drop the stale selection.
            bool partOfCurrentGroup = MarkupCanvas.GetSelectedStrokes().Contains(hitStroke)
                && (selectedMarkupShapes.Count > 0 || selectedMarkupTexts.Count > 0);
            if (!partOfCurrentGroup)
                ClearMarkupGroupSelection();
            return false; // let the native InkCanvas Select behavior handle strokes as before
        }

        BeginMarkupMarquee(clickPoint);
        e.Handled = true;
        return true;
    }

    private static bool IsOverMiniMap(RoutedEventArgs e)
        => e.OriginalSource is DependencyObject source && FindAncestor<MiniMap>(source) is not null;

    private static bool IsOverCornerNavButton(RoutedEventArgs e)
        => e.OriginalSource is DependencyObject source && FindAncestor<CornerNavButton>(source) is not null;

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = current is Visual ? VisualTreeHelper.GetParent(current) : null;
        }
        return null;
    }

    private void CanvasContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        bool isBarVisible = CanvasNavigationBar.Visibility == Visibility.Visible;

        ToggleCanvasNavigationMenuItem.IsEnabled = ViewModel.HasImage;
        ToggleCanvasNavigationMenuItem.IsChecked = isBarVisible;

        ToggleCanvasNavigationBarMenuItem.IsEnabled = ViewModel.HasImage;
        ToggleCanvasNavigationBarMenuItem.IsChecked = isBarVisible;

        ToggleMiniMapMenuItem.IsEnabled = ViewModel.HasImage;
        ToggleMiniMapMenuItem.IsChecked = showMiniMap;

        ToggleMiniMapBarMenuItem.IsEnabled = ViewModel.HasImage;
        ToggleMiniMapBarMenuItem.IsChecked = showMiniMap;
    }

    private void ToggleMiniMapMenuItem_Click(object sender, RoutedEventArgs e)
    {
        showMiniMap = sender is System.Windows.Controls.MenuItem { IsCheckable: true } menuItem
            ? menuItem.IsChecked
            : !showMiniMap;

        ToggleMiniMapMenuItem.IsChecked = showMiniMap;
        ToggleMiniMapBarMenuItem.IsChecked = showMiniMap;

        UpdateMiniMap();
    }

    private void ToggleCanvasNavigationMenuItem_Click(object sender, RoutedEventArgs e)
    {
        bool showBar = sender is System.Windows.Controls.MenuItem { IsCheckable: true } menuItem
            ? menuItem.IsChecked
            : CanvasNavigationBar.Visibility != Visibility.Visible;

        if (showBar)
            CanvasNavigationBar.ClearValue(UIElement.VisibilityProperty);
        else
            CanvasNavigationBar.Visibility = Visibility.Collapsed;

        // Keep both menu items (canvas background + canvas bar) in sync.
        ToggleCanvasNavigationMenuItem.IsChecked = showBar;
        ToggleCanvasNavigationBarMenuItem.IsChecked = showBar;
    }

    private void ShowSidebar()
    {
        SidebarToggleButton.Visibility = Visibility.Visible;
        SidebarToggleButton.IsChecked = true;
        BottomBorder.Visibility = Visibility.Visible;
        SidebarColumn.Width = new GridLength(DefaultSidebarWidth);
        SidebarToggleText.Text = "Hide tools";
        SidebarToggleButton.ToolTip = "Collapse tools sidebar";
    }

    private void HideSidebar()
    {
        BottomBorder.Visibility = Visibility.Collapsed;
        SidebarColumn.Width = new GridLength(0);
        SidebarToggleButton.IsChecked = false;
        SidebarToggleButton.Visibility = Visibility.Collapsed;
    }

    private void SidebarToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized)
            return;

        BottomBorder.Visibility = Visibility.Visible;
        SidebarColumn.Width = new GridLength(DefaultSidebarWidth);
        SidebarToggleText.Text = "Hide tools";
        SidebarToggleButton.ToolTip = "Collapse tools sidebar";
    }

    private void SidebarToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized)
            return;

        BottomBorder.Visibility = Visibility.Collapsed;
        SidebarColumn.Width = new GridLength(0);
        SidebarToggleText.Text = "Show tools";
        SidebarToggleButton.ToolTip = "Show tools sidebar";
    }

    private void MainGrid_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || draggingMode != DraggingMode.Panning)
            return;

        draggingMode = DraggingMode.None;
        MainGrid.ReleaseMouseCapture();
        Cursor = null;
        e.Handled = true;
    }

    private void ZoomAtCanvasPoint(double scale, Point canvasPoint)
    {
        StopCanvasTranslateAnimation();

        double originalScale = canvasScale.ScaleX;
        if (originalScale <= 0)
            return;

        double targetScale = Math.Clamp(scale, MinZoom, MaxZoom);
        double absoluteX = (canvasPoint.X * originalScale) + canvasTranslate.X;
        double absoluteY = (canvasPoint.Y * originalScale) + canvasTranslate.Y;
        canvasTranslate.X = absoluteX - (canvasPoint.X * targetScale);
        canvasTranslate.Y = absoluteY - (canvasPoint.Y * targetScale);
        canvasScale.ScaleX = targetScale;
        canvasScale.ScaleY = targetScale;
        UpdateCanvasNavigationUi();
    }

    private void ZoomAtViewportPoint(double scale, Point viewportPoint)
    {
        double currentScale = canvasScale.ScaleX;
        if (currentScale <= 0)
            return;

        Point canvasPoint = new(
            (viewportPoint.X - 50 - canvasTranslate.X) / currentScale,
            (viewportPoint.Y - 50 - canvasTranslate.Y) / currentScale);
        ZoomAtCanvasPoint(scale, canvasPoint);
    }

    private void WhitePointPickerToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
        {
            WhitePointPickerToggle.IsChecked = false;
            return;
        }

        // Enable white point picker mode
        isWhitePointPickerMode = true;
        draggingMode = DraggingMode.WhitePointPicker;

        // Change cursor to indicate picking mode
        Cursor = Cursors.Cross;
    }

    private void WhitePointPickerToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        // Disable white point picker mode
        isWhitePointPickerMode = false;
        draggingMode = DraggingMode.None;
        Cursor = null;
    }

    private void BlackPointPickerToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
        {
            BlackPointPickerToggle.IsChecked = false;
            return;
        }

        // Enable black point picker mode
        isBlackPointPickerMode = true;
        draggingMode = DraggingMode.BlackPointPicker;

        // Change cursor to indicate picking mode
        Cursor = Cursors.Cross;
    }

    private void BlackPointPickerToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        // Disable black point picker mode
        isBlackPointPickerMode = false;
        draggingMode = DraggingMode.None;
        Cursor = null;
    }

    private async Task PickWhitePointColorAsync(Point imagePoint) =>
        await PickColorPointAsync(imagePoint, isWhitePoint: true);

    private async Task PickBlackPointColorAsync(Point imagePoint) =>
        await PickColorPointAsync(imagePoint, isWhitePoint: false);

    private async Task PickColorPointAsync(Point imagePoint, bool isWhitePoint)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
            return;

        try
        {
            // Reset picker mode and cursor
            if (isWhitePoint)
            {
                isWhitePointPickerMode = false;
                WhitePointPickerToggle.IsChecked = false;
            }
            else
            {
                isBlackPointPickerMode = false;
                BlackPointPickerToggle.IsChecked = false;
            }
            draggingMode = DraggingMode.None;
            Cursor = null;

            using MagickImage magickImage = new(ViewModel.ImagePath);

            // Convert display coordinates to actual image pixel coordinates
            double scaleX = magickImage.Width / MainImage.ActualWidth;
            double scaleY = magickImage.Height / MainImage.ActualHeight;
            int pixelX = Math.Clamp((int)(imagePoint.X * scaleX), 0, (int)magickImage.Width - 1);
            int pixelY = Math.Clamp((int)(imagePoint.Y * scaleY), 0, (int)magickImage.Height - 1);

            // Get the color at the clicked pixel
            IMagickColor<ushort> pixelColor = magickImage.GetPixels().GetPixel(pixelX, pixelY).ToColor()
                ?? throw new InvalidOperationException("Could not get pixel color");

            byte r = (byte)(pixelColor.R / 257);
            byte g = (byte)(pixelColor.G / 257);
            byte b = (byte)(pixelColor.B / 257);

            // Show the picked color in the preview
            WhitePointColorRectangle.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            WhitePointColorPreview.Visibility = Visibility.Visible;

            await Task.Delay(800);
            SetUiForLongTask();

            // Build the per-channel Level adjustment
            void ApplyColorPoint(MagickImage target)
            {
                if (isWhitePoint)
                {
                    byte lr = r == 0 ? (byte)1 : r;
                    byte lg = g == 0 ? (byte)1 : g;
                    byte lb = b == 0 ? (byte)1 : b;
                    target.Level(new Percentage(0), new Percentage((lr / 255.0) * 100), 1.0, Channels.Red);
                    target.Level(new Percentage(0), new Percentage((lg / 255.0) * 100), 1.0, Channels.Green);
                    target.Level(new Percentage(0), new Percentage((lb / 255.0) * 100), 1.0, Channels.Blue);
                }
                else
                {
                    byte lr = r == 255 ? (byte)254 : r;
                    byte lg = g == 255 ? (byte)254 : g;
                    byte lb = b == 255 ? (byte)254 : b;
                    target.Level(new Percentage((lr / 255.0) * 100), new Percentage(100), 1.0, Channels.Red);
                    target.Level(new Percentage((lg / 255.0) * 100), new Percentage(100), 1.0, Channels.Green);
                    target.Level(new Percentage((lb / 255.0) * 100), new Percentage(100), 1.0, Channels.Blue);
                }
            }

            if (LocalAdjustmentCheckBox.IsChecked == true)
            {
                MagickGeometry region = LocalAdjustmentRectangle.CropShape;

                double displayWidth = MainImage.ActualWidth;
                double displayHeight = MainImage.ActualHeight;
                if (displayWidth == 0 || displayHeight == 0)
                {
                    SetUiForCompletedTask();
                    return;
                }

                double factor = magickImage.Height / displayHeight;
                region.ScaleAll(factor);

                if (region.X < 0) region.X = 0;
                if (region.Y < 0) region.Y = 0;
                if (region.X + region.Width > magickImage.Width)
                    region.Width = (uint)(magickImage.Width - region.X);
                if (region.Y + region.Height > magickImage.Height)
                    region.Height = (uint)(magickImage.Height - region.Y);

                int regionX = region.X;
                int regionY = region.Y;

                await Task.Run(() =>
                {
                    using MagickImage cropped = (MagickImage)magickImage.Clone();
                    cropped.Crop(region);
                    cropped.Page = new MagickGeometry(0, 0, cropped.Width, cropped.Height);
                    ApplyColorPoint(cropped);
                    magickImage.Composite(cropped, regionX, regionY, CompositeOperator.Over);
                });
            }
            else
            {
                await Task.Run(() => ApplyColorPoint(magickImage));
            }

            string tempFileName = System.IO.Path.GetTempFileName();
            await magickImage.WriteAsync(tempFileName);

            MagickImageUndoRedoItem undoRedoItem = new(MainImage, ViewModel.ImagePath, tempFileName);
            UndoRedo.AddUndo(undoRedoItem);

            ViewModel.ImagePath = tempFileName;
            MainImage.Source = magickImage.ToBitmapSource();
            ViewModel.ActualImageSize = new Size(magickImage.Width, magickImage.Height);

            await Task.Delay(1500);
            WhitePointColorPreview.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            WhitePointColorPreview.Visibility = Visibility.Collapsed;
            string pointType = isWhitePoint ? "white" : "black";
            Wpf.Ui.Controls.MessageBox errorBox = new()
            {
                Title = "Error",
                Content = $"Failed to apply {pointType} point: {ex.Message}",
            };
            await errorBox.ShowDialogAsync();
        }
        finally
        {
            SetUiForCompletedTask();
        }
    }

    private void StretchMenuItem_Click(object sender, RoutedEventArgs e)
    {
        oldGridSize = new(ImageGrid.ActualWidth, ImageGrid.ActualHeight);
        ShowResizeControls();
    }

    private async void CropImage_Click(object sender, RoutedEventArgs e)
    {
        ShowCroppingControls();
        await RunCropDetectionAsync();
    }

    private void ShowCroppingControls()
    {
        HideResizeControls();
        HideTransformControls();
        HideTriFoldControls();
        HideUnWarpControls();
        HideObjectEraseControls();
        HideEdgeCorrectionControls();
        HideGridStraightenControls();

        CropButtonPanel.Visibility = Visibility.Visible;
        CroppingRectangle.Visibility = Visibility.Visible;
    }

    private async void ApplyCropButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ViewModel.ImagePath))
            return;

        MagickGeometry cropGeometry = CroppingRectangle.CropShape;
        using MagickImage magickImage = new(ViewModel.ImagePath);

        // Calculate scale factor based on actual image dimensions vs display dimensions
        // Use the MainImage.Source dimensions, which reflect the actual loaded image
        double displayWidth = MainImage.ActualWidth;
        double displayHeight = MainImage.ActualHeight;

        if (displayWidth == 0 || displayHeight == 0)
        {
            SetUiForCompletedTask();
            return;
        }

        // Scale factor to convert from display coordinates to actual image coordinates
        double factor = magickImage.Height / displayHeight;
        cropGeometry.ScaleAll(factor);

        SetUiForLongTask();

        magickImage.Crop(cropGeometry);

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, ViewModel.ImagePath, tempFileName);
        UndoRedo.AddUndo(undoRedoItem);

        ViewModel.ImagePath = tempFileName;

        // Reset ImageGrid dimensions so it auto-sizes to fit the newly cropped image.
        // Without this, the pre-crop ImageGrid height stays in place; with Stretch="Uniform"
        // the image renders centred inside the oversized grid, introducing a vertical (and
        // possibly horizontal) offset that makes subsequent crop/edge-detection coordinates wrong.
        ImageGrid.Width = ImageWidthConst;
        ImageGrid.Height = double.NaN;

        MainImage.Source = magickImage.ToBitmapSource();

        // Update ViewModel.ActualImageSize to reflect the new cropped dimensions
        ViewModel.ActualImageSize = new Size(magickImage.Width, magickImage.Height);

        SetUiForCompletedTask();

        HideCroppingControls();
    }

    private void CancelCrop_Click(object sender, RoutedEventArgs e)
    {
        HideCroppingControls();
    }

    private void HideCroppingControls()
    {
        CropButtonPanel.Visibility = Visibility.Collapsed;
        CroppingRectangle.Visibility = Visibility.Collapsed;
        HideCropQuadrilateralSelector();
    }

    private async void DetectCropShapeButton_Click(object sender, RoutedEventArgs e)
    {
        await RunCropDetectionAsync();
    }

    private async Task RunCropDetectionAsync()
    {
        CropDetectInfoText.Visibility = Visibility.Collapsed;

        if (string.IsNullOrEmpty(ViewModel.ImagePath) || !File.Exists(ViewModel.ImagePath))
            return;

        ViewModel.IsBusy = true;

        try
        {
            QuadrilateralDetector.DetectionResult detectionResult = await Task.Run(() =>
                QuadrilateralDetector.DetectQuadrilateralsWithDimensions(ViewModel.ImagePath, minArea: QuadDetectionMinArea, maxResults: QuadDetectionMaxResults));

            if (detectionResult.Quadrilaterals.Count == 0)
            {
                CropDetectInfoText.Text = "No shapes detected. Position the crop rectangle manually.";
                CropDetectInfoText.Visibility = Visibility.Visible;
            }
            else
            {
                List<QuadrilateralDetector.DetectedQuadrilateral> scaledQuads = [.. detectionResult.Quadrilaterals.Select(q =>
                    QuadrilateralDetector.ScaleToDisplay(
                        q,
                        detectionResult.ImageWidth,
                        detectionResult.ImageHeight,
                        MainImage.ActualWidth,
                        MainImage.ActualHeight))];

                CropQuadrilateralSelectorControl.SetQuadrilaterals(scaledQuads);
                CropQuadrilateralSelectorControl.QuadrilateralHoverEnter -= QuadrilateralSelector_HoverEnter;
                CropQuadrilateralSelectorControl.QuadrilateralHoverExit -= QuadrilateralSelector_HoverExit;
                CropQuadrilateralSelectorControl.QuadrilateralHoverEnter += QuadrilateralSelector_HoverEnter;
                CropQuadrilateralSelectorControl.QuadrilateralHoverExit += QuadrilateralSelector_HoverExit;
                CropQuadrilateralSelectorControl.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            CropDetectInfoText.Text = $"Detection failed: {ex.Message}";
            CropDetectInfoText.Visibility = Visibility.Visible;
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private void CropQuadrilateralSelector_Selected(object? sender, QuadrilateralDetector.DetectedQuadrilateral quad)
    {
        PositionCroppingRectangle(quad);
        HideCropQuadrilateralSelector();

        // Defensive: ensure transform elements stay hidden while in crop mode
        HideTransformControls();
        CropButtonPanel.Visibility = Visibility.Visible;
        CroppingRectangle.Visibility = Visibility.Visible;
    }

    private void CropQuadrilateralSelector_ManualSelection(object? sender, EventArgs e)
    {
        HideCropQuadrilateralSelector();
    }

    private void CropQuadrilateralSelector_Cancelled(object? sender, EventArgs e)
    {
        HideCropQuadrilateralSelector();
    }

    private void HideCropQuadrilateralSelector()
    {
        CropQuadrilateralSelectorControl.Visibility = Visibility.Collapsed;
        CropQuadrilateralSelectorControl.QuadrilateralHoverEnter -= QuadrilateralSelector_HoverEnter;
        CropQuadrilateralSelectorControl.QuadrilateralHoverExit -= QuadrilateralSelector_HoverExit;
        RemoveHoverHighlight();
    }

    private void PositionCroppingRectangle(QuadrilateralDetector.DetectedQuadrilateral quad)
    {
        // Compute the axis-aligned bounding box of the quadrilateral
        double minX = Math.Min(Math.Min(quad.TopLeft.X, quad.TopRight.X), Math.Min(quad.BottomLeft.X, quad.BottomRight.X));
        double minY = Math.Min(Math.Min(quad.TopLeft.Y, quad.TopRight.Y), Math.Min(quad.BottomLeft.Y, quad.BottomRight.Y));
        double maxX = Math.Max(Math.Max(quad.TopLeft.X, quad.TopRight.X), Math.Max(quad.BottomLeft.X, quad.BottomRight.X));
        double maxY = Math.Max(Math.Max(quad.TopLeft.Y, quad.TopRight.Y), Math.Max(quad.BottomLeft.Y, quad.BottomRight.Y));

        double width = maxX - minX;
        double height = maxY - minY;

        if (width <= 0 || height <= 0)
            return;

        Canvas.SetLeft(CroppingRectangle, minX);
        Canvas.SetTop(CroppingRectangle, minY);
        CroppingRectangle.Width = width;
        CroppingRectangle.Height = height;
    }

    private async void PerspectiveCorrectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowTransformControls();
        await RunTransformDetectionAsync();
    }

    private void CancelTransformButton_Click(object sender, RoutedEventArgs e)
    {
        HideTransformControls();
    }

    private void ShowTransformControls()
    {
        HideCroppingControls();
        HideResizeControls();
        HideTriFoldControls();
        HideUnWarpControls();
        HideObjectEraseControls();
        HideEdgeCorrectionControls();
        HideGridStraightenControls();

        TransformButtonPanel.Visibility = Visibility.Visible;

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Visible;

        RefreshCornerNavButtons();
    }

    private void HideTransformControls()
    {
        TransformButtonPanel.Visibility = Visibility.Collapsed;
        HideQuadrilateralSelector();

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Collapsed;

        lines?.Visibility = Visibility.Collapsed;

        RefreshCornerNavButtons();
    }

    #region Tri-Fold Correction

    private void TriFoldCorrectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowTriFoldControls();
    }

    private void CancelTriFoldButton_Click(object sender, RoutedEventArgs e)
    {
        HideTriFoldControls();
    }

    private void ShowTriFoldControls()
    {
        Debug.WriteLine($"[TriFold] ShowTriFoldControls called. Stack: {Environment.StackTrace}");
        HideCroppingControls();
        HideTransformControls();
        HideResizeControls();
        HideUnWarpControls();
        HideObjectEraseControls();
        HideEdgeCorrectionControls();
        HideGridStraightenControls();

        isTriFoldMode = true;
        TriFoldButtonPanel.Visibility = Visibility.Visible;

        // Position markers at default locations based on image size
        ResetTriFoldMarkers();

        // Show all 8 markers
        foreach (UIElement element in _triFoldElements)
            element.Visibility = Visibility.Visible;

        // Hide the 4-corner polyline; the tri-fold polygon replaces it
        lines?.Visibility = Visibility.Collapsed;

        // Build the unified tri-fold polygon
        DrawTriFoldGuideLines();

        RefreshCornerNavButtons();
    }

    private void HideTriFoldControls()
    {
        isTriFoldMode = false;
        TriFoldButtonPanel.Visibility = Visibility.Collapsed;

        // Hide fold markers (corners are shared with _polygonElements, hide those too)
        UpperFoldLeft.Visibility = Visibility.Collapsed;
        UpperFoldRight.Visibility = Visibility.Collapsed;
        LowerFoldLeft.Visibility = Visibility.Collapsed;
        LowerFoldRight.Visibility = Visibility.Collapsed;

        // Hide corner markers
        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Collapsed;

        RemoveTriFoldGuideLines();

        RefreshCornerNavButtons();
    }

    private void ResetTriFoldMarkers()
    {
        double imgW = MainImage.ActualWidth > 0 ? MainImage.ActualWidth : 600;
        double imgH = MainImage.ActualHeight > 0 ? MainImage.ActualHeight : 425;

        double margin = 20;
        double left = margin;
        double right = imgW - margin;
        double top = margin;
        double bottom = imgH - margin;
        double oneThird = top + ((bottom - top) / 3.0);
        double twoThirds = top + (2.0 * (bottom - top) / 3.0);

        double halfEllipse = TopLeft.Width / 2;

        Canvas.SetLeft(TopLeft, left - halfEllipse);
        Canvas.SetTop(TopLeft, top - halfEllipse);
        Canvas.SetLeft(TopRight, right - halfEllipse);
        Canvas.SetTop(TopRight, top - halfEllipse);
        Canvas.SetLeft(BottomRight, right - halfEllipse);
        Canvas.SetTop(BottomRight, bottom - halfEllipse);
        Canvas.SetLeft(BottomLeft, left - halfEllipse);
        Canvas.SetTop(BottomLeft, bottom - halfEllipse);

        Canvas.SetLeft(UpperFoldLeft, left - halfEllipse);
        Canvas.SetTop(UpperFoldLeft, oneThird - halfEllipse);
        Canvas.SetLeft(UpperFoldRight, right - halfEllipse);
        Canvas.SetTop(UpperFoldRight, oneThird - halfEllipse);
        Canvas.SetLeft(LowerFoldLeft, left - halfEllipse);
        Canvas.SetTop(LowerFoldLeft, twoThirds - halfEllipse);
        Canvas.SetLeft(LowerFoldRight, right - halfEllipse);
        Canvas.SetTop(LowerFoldRight, twoThirds - halfEllipse);

        DrawPolyLine();
    }

    private void DrawTriFoldGuideLines()
    {
        RemoveTriFoldGuideLines();

        Color color = (Color)ColorConverter.ConvertFromString("#0066FF");
        SolidColorBrush brush = new(color);

        // Build a single polygon that traces the full tri-fold outline:
        //   TL → TR → UFR → UFL → LFL → LFR → BR → BL → LFL → LFR → UFR → UFL
        // This draws the outer rectangle and both internal fold lines in one stroke.
        triFoldPolygon = new Polygon
        {
            Stroke = brush,
            StrokeThickness = 2,
            IsHitTestVisible = false,
            StrokeLineJoin = PenLineJoin.Round,
            Opacity = 0.8,
            Points = GetTriFoldPolygonPoints()
        };

        ShapeCanvas.Children.Add(triFoldPolygon);
    }

    private PointCollection GetTriFoldPolygonPoints()
    {
        Point tl = GeometryMathHelper.GetEllipseCenter(TopLeft);
        Point tr = GeometryMathHelper.GetEllipseCenter(TopRight);
        Point ufl = GeometryMathHelper.GetEllipseCenter(UpperFoldLeft);
        Point ufr = GeometryMathHelper.GetEllipseCenter(UpperFoldRight);
        Point lfl = GeometryMathHelper.GetEllipseCenter(LowerFoldLeft);
        Point lfr = GeometryMathHelper.GetEllipseCenter(LowerFoldRight);
        Point bl = GeometryMathHelper.GetEllipseCenter(BottomLeft);
        Point br = GeometryMathHelper.GetEllipseCenter(BottomRight);

        // Trace: outer top → right side down to upper fold → cross left → down left side
        // to lower fold → cross right → down right side to bottom → bottom back to left
        // → up left side to lower fold → cross right → up right side to upper fold → cross left → close
        return [tl, tr, ufr, ufl, lfl, lfr, br, bl, lfl, lfr, ufr, ufl];
    }

    private void UpdateTriFoldGuideLines()
    {
        triFoldPolygon?.Points = GetTriFoldPolygonPoints();
    }

    private void RemoveTriFoldGuideLines()
    {
        if (triFoldPolygon is not null)
        {
            ShapeCanvas.Children.Remove(triFoldPolygon);
            triFoldPolygon = null;
        }
    }

    private async void ApplyTriFoldButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
            return;

        SetUiForLongTask();

        try
        {
            using MagickImage sizeCheck = new(ViewModel.ImagePath);
            double scaleFactor = sizeCheck.Width / MainImage.ActualWidth;

            Point tl = GeometryMathHelper.GetEllipseCenter(TopLeft);
            Point tr = GeometryMathHelper.GetEllipseCenter(TopRight);
            Point ufl = GeometryMathHelper.GetEllipseCenter(UpperFoldLeft);
            Point ufr = GeometryMathHelper.GetEllipseCenter(UpperFoldRight);
            Point lfl = GeometryMathHelper.GetEllipseCenter(LowerFoldLeft);
            Point lfr = GeometryMathHelper.GetEllipseCenter(LowerFoldRight);
            Point bl = GeometryMathHelper.GetEllipseCenter(BottomLeft);
            Point br = GeometryMathHelper.GetEllipseCenter(BottomRight);

            MagickImage? result = await TriFoldCorrector.CorrectTriFoldAsync(
                ViewModel.ImagePath, tl, tr, ufl, ufr, lfl, lfr, bl, br, scaleFactor);

            if (result is null)
            {
                SetUiForCompletedTask();
                return;
            }

            string tempFileName = System.IO.Path.GetTempFileName();
            await result.WriteAsync(tempFileName);

            MagickImageUndoRedoItem undoRedoItem = new(MainImage, ViewModel.ImagePath, tempFileName);
            UndoRedo.AddUndo(undoRedoItem);

            ViewModel.ImagePath = tempFileName;
            // Reset ImageGrid so it auto-sizes to the new image aspect ratio
            ImageGrid.Width = ImageWidthConst;
            ImageGrid.Height = double.NaN;
            MainImage.Source = result.ToBitmapSource();

            ViewModel.ActualImageSize = new Size(result.Width, result.Height);
            result.Dispose();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Tri-Fold Correction Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            HideTriFoldControls();
            SetUiForCompletedTask();
        }
    }

    #endregion Tri-Fold Correction

    #region Un-Warp Correction

    private async void UnWarpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowUnWarpControls();
        await RunUnWarpDetectionAsync();
    }

    private void CancelUnWarpButton_Click(object sender, RoutedEventArgs e)
    {
        HideUnWarpControls();
    }

    private async void DetectUnWarpShapeButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUnWarpDetectionAsync();
    }

    private async Task RunUnWarpDetectionAsync()
    {
        UnWarpDetectInfoText.Visibility = Visibility.Collapsed;

        if (string.IsNullOrEmpty(ViewModel.ImagePath) || !File.Exists(ViewModel.ImagePath))
            return;

        ViewModel.IsBusy = true;

        try
        {
            QuadrilateralDetector.DetectionResult detectionResult = await Task.Run(() =>
                QuadrilateralDetector.DetectQuadrilateralsWithDimensions(
                    ViewModel.ImagePath, minArea: QuadDetectionMinArea, maxResults: QuadDetectionMaxResults));

            if (detectionResult.Quadrilaterals.Count == 0)
            {
                UnWarpDetectInfoText.Text = "No shapes detected. Position the corner markers manually.";
                UnWarpDetectInfoText.Visibility = Visibility.Visible;
            }
            else
            {
                List<QuadrilateralDetector.DetectedQuadrilateral> scaledQuads =
                [.. detectionResult.Quadrilaterals.Select(q =>
                    QuadrilateralDetector.ScaleToDisplay(
                        q,
                        detectionResult.ImageWidth,
                        detectionResult.ImageHeight,
                        MainImage.ActualWidth,
                        MainImage.ActualHeight))];

                UnWarpQuadrilateralSelectorControl.SetQuadrilaterals(scaledQuads);
                UnWarpQuadrilateralSelectorControl.QuadrilateralHoverEnter -= QuadrilateralSelector_HoverEnter;
                UnWarpQuadrilateralSelectorControl.QuadrilateralHoverExit -= QuadrilateralSelector_HoverExit;
                UnWarpQuadrilateralSelectorControl.QuadrilateralHoverEnter += QuadrilateralSelector_HoverEnter;
                UnWarpQuadrilateralSelectorControl.QuadrilateralHoverExit += QuadrilateralSelector_HoverExit;
                UnWarpQuadrilateralSelectorControl.Visibility = Visibility.Visible;
                UnWarpActionButtons.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            UnWarpDetectInfoText.Text = $"Detection failed: {ex.Message}";
            UnWarpDetectInfoText.Visibility = Visibility.Visible;
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private void UnWarpQuadrilateralSelector_Selected(object? sender, QuadrilateralDetector.DetectedQuadrilateral quad)
    {
        Debug.WriteLine($"[UnWarp] QuadrilateralSelector_Selected: TL=({quad.TopLeft.X:F0},{quad.TopLeft.Y:F0})");
        PositionUnWarpMarkers(quad);
        HideUnWarpQuadrilateralSelector();
        Debug.WriteLine($"[UnWarp] Selection complete. isUnWarpMode={isUnWarpMode}, UnWarpPanel={UnWarpButtonPanel.Visibility}, TriFoldPanel={TriFoldButtonPanel.Visibility}");
    }

    private void UnWarpQuadrilateralSelector_ManualSelection(object? sender, EventArgs e)
    {
        Debug.WriteLine("[UnWarp] ManualSelection clicked");
        HideUnWarpQuadrilateralSelector();
    }

    private void UnWarpQuadrilateralSelector_Cancelled(object? sender, EventArgs e)
    {
        Debug.WriteLine("[UnWarp] Cancelled clicked");
        HideUnWarpQuadrilateralSelector();
    }

    private void HideUnWarpQuadrilateralSelector()
    {
        UnWarpQuadrilateralSelectorControl.Visibility = Visibility.Collapsed;
        UnWarpQuadrilateralSelectorControl.QuadrilateralHoverEnter -= QuadrilateralSelector_HoverEnter;
        UnWarpQuadrilateralSelectorControl.QuadrilateralHoverExit -= QuadrilateralSelector_HoverExit;
        RemoveHoverHighlight();
        UnWarpActionButtons.Visibility = Visibility.Visible;
    }

    private void PositionUnWarpMarkers(QuadrilateralDetector.DetectedQuadrilateral quad)
    {
        double half = TopLeft.Width / 2;

        // Position corner markers
        Canvas.SetLeft(TopLeft, quad.TopLeft.X - half);
        Canvas.SetTop(TopLeft, quad.TopLeft.Y - half);
        Canvas.SetLeft(TopRight, quad.TopRight.X - half);
        Canvas.SetTop(TopRight, quad.TopRight.Y - half);
        Canvas.SetLeft(BottomRight, quad.BottomRight.X - half);
        Canvas.SetTop(BottomRight, quad.BottomRight.Y - half);
        Canvas.SetLeft(BottomLeft, quad.BottomLeft.X - half);
        Canvas.SetTop(BottomLeft, quad.BottomLeft.Y - half);

        // Position midpoint markers at the midpoint of each edge
        Point midTop = GeometryMathHelper.MidPoint(quad.TopLeft, quad.TopRight);
        Point midRight = GeometryMathHelper.MidPoint(quad.TopRight, quad.BottomRight);
        Point midBottom = GeometryMathHelper.MidPoint(quad.BottomLeft, quad.BottomRight);
        Point midLeft = GeometryMathHelper.MidPoint(quad.TopLeft, quad.BottomLeft);

        Canvas.SetLeft(UnWarpMidTop, midTop.X - half);
        Canvas.SetTop(UnWarpMidTop, midTop.Y - half);
        Canvas.SetLeft(UnWarpMidRight, midRight.X - half);
        Canvas.SetTop(UnWarpMidRight, midRight.Y - half);
        Canvas.SetLeft(UnWarpMidBottom, midBottom.X - half);
        Canvas.SetTop(UnWarpMidBottom, midBottom.Y - half);
        Canvas.SetLeft(UnWarpMidLeft, midLeft.X - half);
        Canvas.SetTop(UnWarpMidLeft, midLeft.Y - half);

        DrawPolyLine();

        // In un-warp mode the Bézier guide curves replace the polyline
        lines?.Visibility = Visibility.Collapsed;

        UpdateUnWarpGuideCurves();

        UpdateCornerNavButtons();
    }

    private void ShowUnWarpControls()
    {
        HideCroppingControls();
        HideTransformControls();
        HideResizeControls();
        HideTriFoldControls();
        HideObjectEraseControls();
        HideEdgeCorrectionControls();
        HideGridStraightenControls();

        isUnWarpMode = true;
        UnWarpButtonPanel.Visibility = Visibility.Visible;

        ResetUnWarpMarkers();

        // Show all 8 markers (4 corners + 4 midpoints)
        foreach (UIElement element in _unWarpElements)
            element.Visibility = Visibility.Visible;

        // Hide the 4-corner polyline; the un-warp curves replace it
        lines?.Visibility = Visibility.Collapsed;

        DrawUnWarpGuideCurves();

        RefreshCornerNavButtons();
    }

    private void HideUnWarpControls()
    {
        Debug.WriteLine($"[UnWarp] HideUnWarpControls called. isUnWarpMode was {isUnWarpMode}");
        isUnWarpMode = false;
        UnWarpButtonPanel.Visibility = Visibility.Collapsed;

        // Hide quadrilateral selector if open
        HideUnWarpQuadrilateralSelector();

        // Hide midpoint markers
        UnWarpMidTop.Visibility = Visibility.Collapsed;
        UnWarpMidRight.Visibility = Visibility.Collapsed;
        UnWarpMidBottom.Visibility = Visibility.Collapsed;
        UnWarpMidLeft.Visibility = Visibility.Collapsed;

        // Hide corner markers
        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Collapsed;

        RemoveUnWarpGuideCurves();

        RefreshCornerNavButtons();
    }

    private void ResetUnWarpMarkers()
    {
        double imgW = MainImage.ActualWidth > 0 ? MainImage.ActualWidth : 600;
        double imgH = MainImage.ActualHeight > 0 ? MainImage.ActualHeight : 425;

        double margin = 20;
        double left = margin;
        double right = imgW - margin;
        double top = margin;
        double bottom = imgH - margin;
        double midX = (left + right) / 2.0;
        double midY = (top + bottom) / 2.0;

        double halfEllipse = TopLeft.Width / 2;

        // Corners
        Canvas.SetLeft(TopLeft, left - halfEllipse);
        Canvas.SetTop(TopLeft, top - halfEllipse);
        Canvas.SetLeft(TopRight, right - halfEllipse);
        Canvas.SetTop(TopRight, top - halfEllipse);
        Canvas.SetLeft(BottomRight, right - halfEllipse);
        Canvas.SetTop(BottomRight, bottom - halfEllipse);
        Canvas.SetLeft(BottomLeft, left - halfEllipse);
        Canvas.SetTop(BottomLeft, bottom - halfEllipse);

        // Midpoints (initially on the straight edges)
        Canvas.SetLeft(UnWarpMidTop, midX - halfEllipse);
        Canvas.SetTop(UnWarpMidTop, top - halfEllipse);
        Canvas.SetLeft(UnWarpMidRight, right - halfEllipse);
        Canvas.SetTop(UnWarpMidRight, midY - halfEllipse);
        Canvas.SetLeft(UnWarpMidBottom, midX - halfEllipse);
        Canvas.SetTop(UnWarpMidBottom, bottom - halfEllipse);
        Canvas.SetLeft(UnWarpMidLeft, left - halfEllipse);
        Canvas.SetTop(UnWarpMidLeft, midY - halfEllipse);

        DrawPolyLine();
    }

    private void DrawUnWarpGuideCurves()
    {
        RemoveUnWarpGuideCurves();

        Color color = (Color)ColorConverter.ConvertFromString("#00CC88");
        SolidColorBrush brush = new(color);

        Point tl = GeometryMathHelper.GetEllipseCenter(TopLeft);
        Point tr = GeometryMathHelper.GetEllipseCenter(TopRight);
        Point bl = GeometryMathHelper.GetEllipseCenter(BottomLeft);
        Point br = GeometryMathHelper.GetEllipseCenter(BottomRight);
        Point mt = GeometryMathHelper.GetEllipseCenter(UnWarpMidTop);
        Point mr = GeometryMathHelper.GetEllipseCenter(UnWarpMidRight);
        Point mb = GeometryMathHelper.GetEllipseCenter(UnWarpMidBottom);
        Point ml = GeometryMathHelper.GetEllipseCenter(UnWarpMidLeft);

        PathGeometry geometry = GeometryMathHelper.BuildUnWarpPathGeometry(tl, tr, bl, br, mt, mr, mb, ml);

        unWarpPath = new System.Windows.Shapes.Path
        {
            Stroke = brush,
            StrokeThickness = 2,
            IsHitTestVisible = false,
            StrokeLineJoin = PenLineJoin.Round,
            Opacity = 0.8,
            Data = geometry
        };

        ShapeCanvas.Children.Add(unWarpPath);
    }

    private void UpdateUnWarpGuideCurves()
    {
        if (unWarpPath is null)
            return;

        Point tl = GeometryMathHelper.GetEllipseCenter(TopLeft);
        Point tr = GeometryMathHelper.GetEllipseCenter(TopRight);
        Point bl = GeometryMathHelper.GetEllipseCenter(BottomLeft);
        Point br = GeometryMathHelper.GetEllipseCenter(BottomRight);
        Point mt = GeometryMathHelper.GetEllipseCenter(UnWarpMidTop);
        Point mr = GeometryMathHelper.GetEllipseCenter(UnWarpMidRight);
        Point mb = GeometryMathHelper.GetEllipseCenter(UnWarpMidBottom);
        Point ml = GeometryMathHelper.GetEllipseCenter(UnWarpMidLeft);

        unWarpPath.Data = GeometryMathHelper.BuildUnWarpPathGeometry(tl, tr, bl, br, mt, mr, mb, ml);
    }

    private void RemoveUnWarpGuideCurves()
    {
        if (unWarpPath is not null)
        {
            ShapeCanvas.Children.Remove(unWarpPath);
            unWarpPath = null;
        }
    }

    private async void ApplyUnWarpButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
            return;

        SetUiForLongTask();

        try
        {
            using MagickImage sizeCheck = new(ViewModel.ImagePath);
            double scaleFactor = sizeCheck.Width / MainImage.ActualWidth;

            Point tl = GeometryMathHelper.GetEllipseCenter(TopLeft);
            Point tr = GeometryMathHelper.GetEllipseCenter(TopRight);
            Point bl = GeometryMathHelper.GetEllipseCenter(BottomLeft);
            Point br = GeometryMathHelper.GetEllipseCenter(BottomRight);
            Point mt = GeometryMathHelper.GetEllipseCenter(UnWarpMidTop);
            Point mr = GeometryMathHelper.GetEllipseCenter(UnWarpMidRight);
            Point mb = GeometryMathHelper.GetEllipseCenter(UnWarpMidBottom);
            Point ml = GeometryMathHelper.GetEllipseCenter(UnWarpMidLeft);

            MagickImage? result;
            if (LocalUnWarpCheckBox.IsChecked == true)
            {
                result = await UnWarpCorrector.CorrectUnWarpLocalAsync(
                    ViewModel.ImagePath, tl, tr, bl, br, mt, mr, mb, ml, scaleFactor);
            }
            else
            {
                result = await UnWarpCorrector.CorrectUnWarpAsync(
                    ViewModel.ImagePath, tl, tr, bl, br, mt, mr, mb, ml, scaleFactor);
            }

            if (result is null)
            {
                SetUiForCompletedTask();
                return;
            }

            string tempFileName = System.IO.Path.GetTempFileName();
            await result.WriteAsync(tempFileName);

            MagickImageUndoRedoItem undoRedoItem = new(MainImage, ViewModel.ImagePath, tempFileName);
            UndoRedo.AddUndo(undoRedoItem);

            ViewModel.ImagePath = tempFileName;
            // Reset ImageGrid so it auto-sizes to the new image aspect ratio
            ImageGrid.Width = ImageWidthConst;
            ImageGrid.Height = double.NaN;
            MainImage.Source = result.ToBitmapSource();

            ViewModel.ActualImageSize = new Size(result.Width, result.Height);
            result.Dispose();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Un-Warp Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            HideUnWarpControls();
            SetUiForCompletedTask();
        }
    }

    #endregion Un-Warp Correction

    #region Edge Correction

    private void EdgeCorrectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowEdgeCorrectionControls();
    }

    private void CancelEdgeCorrectionButton_Click(object sender, RoutedEventArgs e)
    {
        HideEdgeCorrectionControls();
    }

    private void ShowEdgeCorrectionControls()
    {
        HideCroppingControls();
        HideTransformControls();
        HideResizeControls();
        HideTriFoldControls();
        HideUnWarpControls();
        HideObjectEraseControls();
        HideGridStraightenControls();

        isEdgeCorrectionMode = true;
        EdgeCorrectionButtonPanel.Visibility = Visibility.Visible;

        ClearEdgeCorrectionPoints();
        UpdateEdgeCorrectionPointCount();
    }

    private void HideEdgeCorrectionControls()
    {
        isEdgeCorrectionMode = false;
        EdgeCorrectionButtonPanel.Visibility = Visibility.Collapsed;

        ClearEdgeCorrectionPoints();
    }

    private void AddEdgeCorrectionPoint(Point displayPoint)
    {
        // Ensure the point is within image bounds
        if (displayPoint.X < 0 || displayPoint.Y < 0
            || displayPoint.X > MainImage.ActualWidth
            || displayPoint.Y > MainImage.ActualHeight)
            return;

        int index = edgeCorrectionPoints.Count;
        edgeCorrectionPoints.Add(displayPoint);

        // Get edge snap info for visual feedback
        (string edgeName, Point snappedPoint) = EdgeCorrectionHelper.GetEdgeSnapInfo(
            displayPoint, MainImage.ActualWidth, MainImage.ActualHeight);

        // Draw a snap line from the point to the edge
        Color markerColor = (Color)ColorConverter.ConvertFromString("#CC6600");
        Line snapLine = new()
        {
            X1 = displayPoint.X,
            Y1 = displayPoint.Y,
            X2 = snappedPoint.X,
            Y2 = snappedPoint.Y,
            Stroke = new SolidColorBrush(markerColor),
            StrokeThickness = 1,
            StrokeDashArray = [4, 2],
            Opacity = 0.6,
            IsHitTestVisible = false
        };
        ShapeCanvas.Children.Add(snapLine);
        edgeCorrectionSnapLines.Add(snapLine);

        // Create a marker at the user-placed point (interactive: drag to move, right-click to remove)
        Ellipse marker = new()
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush(markerColor),
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 1,
            Opacity = 0.9,
            Cursor = Cursors.SizeAll,
            IsHitTestVisible = true,
            Tag = index,
            ToolTip = $"{edgeName} edge – drag to move, right-click to remove"
        };
        marker.MouseDown += EdgeCorrectionMarker_MouseDown;
        Canvas.SetLeft(marker, displayPoint.X - 6);
        Canvas.SetTop(marker, displayPoint.Y - 6);
        ShapeCanvas.Children.Add(marker);
        edgeCorrectionMarkers.Add(marker);

        UpdateEdgeCorrectionPointCount();
    }

    private void EdgeCorrectionMarker_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse marker || marker.Tag is not int markerIndex)
            return;

        // Right-click removes the point
        if (e.ChangedButton == MouseButton.Right)
        {
            RemoveEdgeCorrectionPointAt(markerIndex);
            e.Handled = true;
            return;
        }

        // Left-click starts dragging the point
        if (e.ChangedButton == MouseButton.Left)
        {
            edgeCorrectionDragIndex = markerIndex;
            draggingMode = DraggingMode.EdgeCorrectionDragging;
            clickedPoint = e.GetPosition(ShapeCanvas);
            CaptureMouse();
            e.Handled = true;
        }
    }

    private void RemoveEdgeCorrectionPointAt(int index)
    {
        if (index < 0 || index >= edgeCorrectionPoints.Count)
            return;

        edgeCorrectionPoints.RemoveAt(index);

        // Remove the marker and snap line
        Ellipse marker = edgeCorrectionMarkers[index];
        marker.MouseDown -= EdgeCorrectionMarker_MouseDown;
        ShapeCanvas.Children.Remove(marker);
        edgeCorrectionMarkers.RemoveAt(index);

        Line snapLine = edgeCorrectionSnapLines[index];
        ShapeCanvas.Children.Remove(snapLine);
        edgeCorrectionSnapLines.RemoveAt(index);

        // Re-index remaining markers so Tag stays in sync
        for (int i = 0; i < edgeCorrectionMarkers.Count; i++)
        {
            edgeCorrectionMarkers[i].Tag = i;
            // Update tooltip with new edge classification
            Point pt = edgeCorrectionPoints[i];
            (string edgeName, _) = EdgeCorrectionHelper.GetEdgeSnapInfo(
                pt, MainImage.ActualWidth, MainImage.ActualHeight);
            edgeCorrectionMarkers[i].ToolTip = $"{edgeName} edge – drag to move, right-click to remove";
        }

        UpdateEdgeCorrectionPointCount();
    }

    private void MoveEdgeCorrectionPoint(int index, Point newPosition)
    {
        if (index < 0 || index >= edgeCorrectionPoints.Count)
            return;

        // Clamp to image bounds
        newPosition = new Point(
            Math.Clamp(newPosition.X, 0, MainImage.ActualWidth),
            Math.Clamp(newPosition.Y, 0, MainImage.ActualHeight));

        edgeCorrectionPoints[index] = newPosition;

        // Update marker position
        Ellipse marker = edgeCorrectionMarkers[index];
        Canvas.SetLeft(marker, newPosition.X - 6);
        Canvas.SetTop(marker, newPosition.Y - 6);

        // Update snap line
        (string edgeName, Point snappedPoint) = EdgeCorrectionHelper.GetEdgeSnapInfo(
            newPosition, MainImage.ActualWidth, MainImage.ActualHeight);
        Line snapLine = edgeCorrectionSnapLines[index];
        snapLine.X1 = newPosition.X;
        snapLine.Y1 = newPosition.Y;
        snapLine.X2 = snappedPoint.X;
        snapLine.Y2 = snappedPoint.Y;

        marker.ToolTip = $"{edgeName} edge – drag to move, right-click to remove";
    }

    private void UndoEdgePointButton_Click(object sender, RoutedEventArgs e)
    {
        if (edgeCorrectionPoints.Count == 0)
            return;

        RemoveEdgeCorrectionPointAt(edgeCorrectionPoints.Count - 1);
    }

    private void ClearEdgePointsButton_Click(object sender, RoutedEventArgs e)
    {
        ClearEdgeCorrectionPoints();
        UpdateEdgeCorrectionPointCount();
    }

    private void ClearEdgeCorrectionPoints()
    {
        edgeCorrectionDragIndex = -1;

        edgeCorrectionPoints.Clear();

        foreach (Ellipse marker in edgeCorrectionMarkers)
        {
            marker.MouseDown -= EdgeCorrectionMarker_MouseDown;
            ShapeCanvas.Children.Remove(marker);
        }
        edgeCorrectionMarkers.Clear();

        foreach (Line snapLine in edgeCorrectionSnapLines)
            ShapeCanvas.Children.Remove(snapLine);
        edgeCorrectionSnapLines.Clear();
    }

    private void UpdateEdgeCorrectionPointCount()
    {
        EdgeCorrectionPointCountText.Text = $"Points placed: {edgeCorrectionPoints.Count}";
    }

    private async void ApplyEdgeCorrectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
            return;

        if (edgeCorrectionPoints.Count == 0)
        {
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Edge Correction",
                Content = "Place at least one point near a wavy edge before applying.",
                PrimaryButtonText = "OK",
            };
            await uiMessageBox.ShowDialogAsync();
            return;
        }

        SetUiForLongTask();

        try
        {
            using MagickImage sizeCheck = new(ViewModel.ImagePath);
            double scaleFactor = sizeCheck.Width / MainImage.ActualWidth;

            MagickImage? result = await EdgeCorrectionHelper.CorrectEdgesAsync(
                ViewModel.ImagePath,
                edgeCorrectionPoints,
                MainImage.ActualWidth,
                MainImage.ActualHeight,
                scaleFactor);

            if (result is null)
            {
                SetUiForCompletedTask();
                return;
            }

            string tempFileName = System.IO.Path.GetTempFileName();
            await result.WriteAsync(tempFileName);

            MagickImageUndoRedoItem undoRedoItem = new(MainImage, ViewModel.ImagePath, tempFileName);
            UndoRedo.AddUndo(undoRedoItem);

            ViewModel.ImagePath = tempFileName;
            // Reset ImageGrid so it auto-sizes to the new image aspect ratio
            ImageGrid.Width = ImageWidthConst;
            ImageGrid.Height = double.NaN;
            MainImage.Source = result.ToBitmapSource();

            ViewModel.ActualImageSize = new Size(result.Width, result.Height);
            result.Dispose();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Edge Correction Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            HideEdgeCorrectionControls();
            SetUiForCompletedTask();
        }
    }

    #endregion Edge Correction

    #region Grid Straighten

    private void GridStraightenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowGridStraightenControls();
    }

    private void CancelGridStraightenButton_Click(object sender, RoutedEventArgs e)
    {
        HideGridStraightenControls();
    }

    private void ShowGridStraightenControls()
    {
        HideCroppingControls();
        HideTransformControls();
        HideResizeControls();
        HideTriFoldControls();
        HideUnWarpControls();
        HideObjectEraseControls();
        HideEdgeCorrectionControls();

        isGridStraightenMode = true;
        GridStraightenButtonPanel.Visibility = Visibility.Visible;

        gridStraightenRows = (int)(GridStraightenRowsBox.Value ?? 4);
        gridStraightenCols = (int)(GridStraightenColsBox.Value ?? 4);

        BuildGridOverlay();
    }

    private void HideGridStraightenControls()
    {
        isGridStraightenMode = false;
        isGridStraightenSpacePanning = false;
        GridStraightenButtonPanel.Visibility = Visibility.Collapsed;

        ClearGridOverlay();
    }

    private void GridStraightenSizeChanged(object sender, RoutedEventArgs e)
    {
        if (!isGridStraightenMode || GridStraightenRowsBox is null || GridStraightenColsBox is null)
            return;

        gridStraightenRows = (int)(GridStraightenRowsBox.Value ?? 4);
        gridStraightenCols = (int)(GridStraightenColsBox.Value ?? 4);

        BuildGridOverlay();
    }

    private void ResetGridButton_Click(object sender, RoutedEventArgs e)
    {
        BuildGridOverlay();
    }

    private void BuildGridOverlay()
    {
        ClearGridOverlay();

        double imgW = MainImage.ActualWidth;
        double imgH = MainImage.ActualHeight;

        if (imgW <= 0 || imgH <= 0)
            return;

        gridStraightenPoints = GridStraightenHelper.GenerateRegularGrid(imgW, imgH, gridStraightenRows, gridStraightenCols);

        Color gridColor = (Color)ColorConverter.ConvertFromString("#9966FF");
        SolidColorBrush lineBrush = new(gridColor) { Opacity = 0.5 };

        // Draw horizontal grid lines
        for (int row = 0; row < gridStraightenRows; row++)
        {
            for (int col = 0; col < gridStraightenCols - 1; col++)
            {
                int idx = row * gridStraightenCols + col;
                int nextIdx = idx + 1;
                Line line = new()
                {
                    X1 = gridStraightenPoints[idx].X,
                    Y1 = gridStraightenPoints[idx].Y,
                    X2 = gridStraightenPoints[nextIdx].X,
                    Y2 = gridStraightenPoints[nextIdx].Y,
                    Stroke = lineBrush,
                    StrokeThickness = 1.5,
                    IsHitTestVisible = false
                };
                ShapeCanvas.Children.Add(line);
                gridStraightenLines.Add(line);
            }
        }

        // Draw vertical grid lines
        for (int col = 0; col < gridStraightenCols; col++)
        {
            for (int row = 0; row < gridStraightenRows - 1; row++)
            {
                int idx = row * gridStraightenCols + col;
                int nextIdx = (row + 1) * gridStraightenCols + col;
                Line line = new()
                {
                    X1 = gridStraightenPoints[idx].X,
                    Y1 = gridStraightenPoints[idx].Y,
                    X2 = gridStraightenPoints[nextIdx].X,
                    Y2 = gridStraightenPoints[nextIdx].Y,
                    Stroke = lineBrush,
                    StrokeThickness = 1.5,
                    IsHitTestVisible = false
                };
                ShapeCanvas.Children.Add(line);
                gridStraightenLines.Add(line);
            }
        }

        // Create draggable markers at each grid intersection
        Color markerColor = (Color)ColorConverter.ConvertFromString("#9966FF");
        Color edgeColor = (Color)ColorConverter.ConvertFromString("#7744CC");
        for (int i = 0; i < gridStraightenPoints.Count; i++)
        {
            Point pt = gridStraightenPoints[i];
            int row = i / gridStraightenCols;
            int col = i % gridStraightenCols;
            bool isCorner = (row == 0 || row == gridStraightenRows - 1) && (col == 0 || col == gridStraightenCols - 1);
            bool isEdge = !isCorner && (row == 0 || row == gridStraightenRows - 1 || col == 0 || col == gridStraightenCols - 1);

            Cursor edgeCursor = row == 0 || row == gridStraightenRows - 1 ? Cursors.SizeWE : Cursors.SizeNS;

            Ellipse marker = new()
            {
                Width = isCorner ? 10 : 12,
                Height = isCorner ? 10 : 12,
                Fill = new SolidColorBrush(isCorner ? Colors.Gray : isEdge ? edgeColor : markerColor),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1,
                Opacity = isCorner ? 0.5 : 0.9,
                Cursor = isCorner ? Cursors.Arrow : isEdge ? edgeCursor : Cursors.SizeAll,
                IsHitTestVisible = !isCorner,
                Tag = i,
                ToolTip = isCorner ? "Corner point (fixed)" : isEdge ? "Drag to slide along edge" : "Drag to adjust grid"
            };

            if (!isCorner)
                marker.MouseDown += GridStraightenMarker_MouseDown;

            Canvas.SetLeft(marker, pt.X - marker.Width / 2);
            Canvas.SetTop(marker, pt.Y - marker.Height / 2);
            ShapeCanvas.Children.Add(marker);
            gridStraightenMarkers.Add(marker);
        }
    }

    private void ClearGridOverlay()
    {
        gridStraightenDragIndex = -1;
        gridStraightenPoints.Clear();

        foreach (Ellipse marker in gridStraightenMarkers)
        {
            marker.MouseDown -= GridStraightenMarker_MouseDown;
            ShapeCanvas.Children.Remove(marker);
        }
        gridStraightenMarkers.Clear();

        foreach (Line line in gridStraightenLines)
            ShapeCanvas.Children.Remove(line);
        gridStraightenLines.Clear();
    }

    private void GridStraightenMarker_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse marker || marker.Tag is not int markerIndex)
            return;

        if (e.ChangedButton == MouseButton.Left)
        {
            gridStraightenDragIndex = markerIndex;
            draggingMode = DraggingMode.GridStraightenDragging;
            clickedPoint = e.GetPosition(ShapeCanvas);
            CaptureMouse();
            e.Handled = true;
        }
    }

    private void MoveGridStraightenPoint(int index, Point newPosition)
    {
        if (index < 0 || index >= gridStraightenPoints.Count)
            return;

        int row = index / gridStraightenCols;
        int col = index % gridStraightenCols;
        bool isCorner = (row == 0 || row == gridStraightenRows - 1) && (col == 0 || col == gridStraightenCols - 1);

        // Don't move corner points
        if (isCorner)
            return;

        // Clamp to image bounds
        newPosition = new Point(
            Math.Clamp(newPosition.X, 0, MainImage.ActualWidth),
            Math.Clamp(newPosition.Y, 0, MainImage.ActualHeight));

        // Constrain edge points to slide along their edge only
        bool isTopEdge = row == 0;
        bool isBottomEdge = row == gridStraightenRows - 1;
        bool isLeftEdge = col == 0;
        bool isRightEdge = col == gridStraightenCols - 1;

        if (isTopEdge)
            newPosition = new Point(newPosition.X, gridStraightenPoints[index].Y);
        else if (isBottomEdge)
            newPosition = new Point(newPosition.X, gridStraightenPoints[index].Y);
        else if (isLeftEdge)
            newPosition = new Point(gridStraightenPoints[index].X, newPosition.Y);
        else if (isRightEdge)
            newPosition = new Point(gridStraightenPoints[index].X, newPosition.Y);

        gridStraightenPoints[index] = newPosition;

        // Update marker position
        Ellipse marker = gridStraightenMarkers[index];
        Canvas.SetLeft(marker, newPosition.X - marker.Width / 2);
        Canvas.SetTop(marker, newPosition.Y - marker.Height / 2);

        // Update connected grid lines
        UpdateGridLines();
    }

    private void UpdateGridLines()
    {
        int lineIdx = 0;

        // Horizontal lines
        for (int row = 0; row < gridStraightenRows; row++)
        {
            for (int col = 0; col < gridStraightenCols - 1; col++)
            {
                int idx = row * gridStraightenCols + col;
                int nextIdx = idx + 1;
                Line line = gridStraightenLines[lineIdx++];
                line.X1 = gridStraightenPoints[idx].X;
                line.Y1 = gridStraightenPoints[idx].Y;
                line.X2 = gridStraightenPoints[nextIdx].X;
                line.Y2 = gridStraightenPoints[nextIdx].Y;
            }
        }

        // Vertical lines
        for (int col = 0; col < gridStraightenCols; col++)
        {
            for (int row = 0; row < gridStraightenRows - 1; row++)
            {
                int idx = row * gridStraightenCols + col;
                int nextIdx = (row + 1) * gridStraightenCols + col;
                Line line = gridStraightenLines[lineIdx++];
                line.X1 = gridStraightenPoints[idx].X;
                line.Y1 = gridStraightenPoints[idx].Y;
                line.X2 = gridStraightenPoints[nextIdx].X;
                line.Y2 = gridStraightenPoints[nextIdx].Y;
            }
        }
    }

    private async void ApplyGridStraightenButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
            return;

        // Check if any points have been moved
        double imgW = MainImage.ActualWidth;
        double imgH = MainImage.ActualHeight;
        List<Point> regularGrid = GridStraightenHelper.GenerateRegularGrid(imgW, imgH, gridStraightenRows, gridStraightenCols);
        bool anyMoved = false;
        for (int i = 0; i < gridStraightenPoints.Count; i++)
        {
            if (Math.Abs(gridStraightenPoints[i].X - regularGrid[i].X) > 1.0
                || Math.Abs(gridStraightenPoints[i].Y - regularGrid[i].Y) > 1.0)
            {
                anyMoved = true;
                break;
            }
        }

        if (!anyMoved)
        {
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Grid Straighten",
                Content = "Drag at least one interior grid point before applying.",
                PrimaryButtonText = "OK",
            };
            await uiMessageBox.ShowDialogAsync();
            return;
        }

        SetUiForLongTask();

        try
        {
            using MagickImage sizeCheck = new(ViewModel.ImagePath);
            double scaleFactor = sizeCheck.Width / MainImage.ActualWidth;

            MagickImage? result = await GridStraightenHelper.StraightenAsync(
                ViewModel.ImagePath,
                gridStraightenPoints,
                gridStraightenRows,
                gridStraightenCols,
                MainImage.ActualWidth,
                MainImage.ActualHeight,
                scaleFactor);

            if (result is null)
            {
                SetUiForCompletedTask();
                return;
            }

            string tempFileName = System.IO.Path.GetTempFileName();
            await result.WriteAsync(tempFileName);

            MagickImageUndoRedoItem undoRedoItem = new(MainImage, ViewModel.ImagePath, tempFileName);
            UndoRedo.AddUndo(undoRedoItem);

            ViewModel.ImagePath = tempFileName;
            // Reset ImageGrid so it auto-sizes to the new image aspect ratio
            ImageGrid.Width = ImageWidthConst;
            ImageGrid.Height = double.NaN;
            MainImage.Source = result.ToBitmapSource();

            ViewModel.ActualImageSize = new Size(result.Width, result.Height);
            result.Dispose();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "Grid Straighten Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            HideGridStraightenControls();
            SetUiForCompletedTask();
        }
    }

    #endregion Grid Straighten

    private async void DetectShapeButton_Click(object sender, RoutedEventArgs e)
    {
        await RunTransformDetectionAsync();
    }

    private async Task RunTransformDetectionAsync()
    {
        TransformDetectInfoText.Visibility = Visibility.Collapsed;

        if (string.IsNullOrEmpty(ViewModel.ImagePath) || !File.Exists(ViewModel.ImagePath))
            return;

        ViewModel.IsBusy = true;

        try
        {
            QuadrilateralDetector.DetectionResult detectionResult = await Task.Run(() =>
                QuadrilateralDetector.DetectQuadrilateralsWithDimensions(ViewModel.ImagePath, minArea: QuadDetectionMinArea, maxResults: QuadDetectionMaxResults));

            if (detectionResult.Quadrilaterals.Count == 0)
            {
                TransformDetectInfoText.Text = "No shapes detected. Position the corner markers manually.";
                TransformDetectInfoText.Visibility = Visibility.Visible;
            }
            else
            {
                List<QuadrilateralDetector.DetectedQuadrilateral> scaledQuads = [.. detectionResult.Quadrilaterals.Select(q =>
                    QuadrilateralDetector.ScaleToDisplay(
                        q,
                        detectionResult.ImageWidth,
                        detectionResult.ImageHeight,
                        MainImage.ActualWidth,
                        MainImage.ActualHeight))];

                QuadrilateralSelectorControl.SetQuadrilaterals(scaledQuads);
                QuadrilateralSelectorControl.QuadrilateralHoverEnter -= QuadrilateralSelector_HoverEnter;
                QuadrilateralSelectorControl.QuadrilateralHoverExit -= QuadrilateralSelector_HoverExit;
                QuadrilateralSelectorControl.QuadrilateralHoverEnter += QuadrilateralSelector_HoverEnter;
                QuadrilateralSelectorControl.QuadrilateralHoverExit += QuadrilateralSelector_HoverExit;
                ShowQuadrilateralSelector();
            }
        }
        catch (Exception ex)
        {
            TransformDetectInfoText.Text = $"Detection failed: {ex.Message}";
            TransformDetectInfoText.Visibility = Visibility.Visible;
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }

    private void QuadrilateralSelector_Selected(object? sender, QuadrilateralDetector.DetectedQuadrilateral quad)
    {
        // Position corner markers at the selected quadrilateral's corners
        PositionCornerMarkers(quad);
        // Hide the selector overlay
        HideQuadrilateralSelector();
    }

    private void QuadrilateralSelector_ManualSelection(object? sender, EventArgs e)
    {
        // Hide selector overlay and let user position markers manually
        HideQuadrilateralSelector();
    }

    private void QuadrilateralSelector_Cancelled(object? sender, EventArgs e)
    {
        // Hide selector overlay
        HideQuadrilateralSelector();
    }

    private void PositionCornerMarkers(Helpers.QuadrilateralDetector.DetectedQuadrilateral quad)
    {
        // Position TopLeft marker
        Canvas.SetLeft(TopLeft, quad.TopLeft.X - (TopLeft.Width / 2));
        Canvas.SetTop(TopLeft, quad.TopLeft.Y - (TopLeft.Height / 2));

        // Position TopRight marker
        Canvas.SetLeft(TopRight, quad.TopRight.X - (TopRight.Width / 2));
        Canvas.SetTop(TopRight, quad.TopRight.Y - (TopRight.Height / 2));

        // Position BottomRight marker
        Canvas.SetLeft(BottomRight, quad.BottomRight.X - (BottomRight.Width / 2));
        Canvas.SetTop(BottomRight, quad.BottomRight.Y - (BottomRight.Height / 2));

        // Position BottomLeft marker
        Canvas.SetLeft(BottomLeft, quad.BottomLeft.X - (BottomLeft.Width / 2));
        Canvas.SetTop(BottomLeft, quad.BottomLeft.Y - (BottomLeft.Height / 2));

        // Update the polyline
        DrawPolyLine();

        UpdateCornerNavButtons();
    }

    private void ImageResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (isAdornerRotatingDrag)
        {
            e.Handled = true;
            return;
        }
        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            clickedPoint = e.GetPosition(ShapeCanvas);
            oldGridSize = new Size(ImageGrid.ActualWidth, ImageGrid.ActualHeight);
            draggingMode = DraggingMode.Resizing;
            isDraggingResizeGrip = true;

            // Uncheck aspect ratio lock when dragging the resize grip
            AspectRatioLockToggle.IsChecked = false;
        }
    }

    private async void ApplyResizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ViewModel.ImagePath))
            return;

        using MagickImage magickImage = new(ViewModel.ImagePath);

        // Get target dimensions from user input
        int targetWidth, targetHeight;

        if (isPixelMode)
        {
            targetWidth = int.Parse(WidthTextBox.Text);
            targetHeight = int.Parse(HeightTextBox.Text);
        }
        else
        {
            // Convert percentage to pixels based on current image size
            double widthPercent = double.Parse(WidthTextBox.Text) / 100.0;
            double heightPercent = double.Parse(HeightTextBox.Text) / 100.0;
            targetWidth = (int)(ViewModel.ActualImageSize.Width * widthPercent);
            targetHeight = (int)(ViewModel.ActualImageSize.Height * heightPercent);
        }

        MagickGeometry resizeGeometry = new((uint)targetWidth, (uint)targetHeight)
        {
            IgnoreAspectRatio = true
        };

        SetUiForLongTask();

        magickImage.Resize(resizeGeometry);

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        ResizeUndoRedoItem undoRedoItem = new(MainImage, ImageGrid, oldGridSize, ViewModel.ImagePath, tempFileName);
        UndoRedo.AddUndo(undoRedoItem);

        ViewModel.ImagePath = tempFileName;

        MainImage.Source = null;
        MainImage.Source = magickImage.ToBitmapSource();

        // Update ViewModel.ActualImageSize to reflect the new dimensions
        ViewModel.ActualImageSize = new Size(targetWidth, targetHeight);

        SetUiForCompletedTask();
        HideResizeControls();
    }

    private void CancelResizeButton_Click(object sender, RoutedEventArgs e)
    {
        ImageGrid.Width = oldGridSize.Width;
        ImageGrid.Height = oldGridSize.Height;
        ImageGrid.InvalidateMeasure();

        HideResizeControls();
    }

    private void HideResizeControls()
    {
        ResizeButtonsPanel.Visibility = Visibility.Collapsed;
        ImageResizeGrip.Visibility = Visibility.Hidden;
    }

    private void ShowResizeControls()
    {
        HideCroppingControls();
        HideTransformControls();
        HideTriFoldControls();
        HideUnWarpControls();
        HideObjectEraseControls();
        HideEdgeCorrectionControls();
        HideGridStraightenControls();

        // Initialize resize input controls
        InitializeResizeInputs();

        ResizeButtonsPanel.Visibility = Visibility.Visible;
        ImageResizeGrip.Visibility = Visibility.Visible;
    }

    private void InitializeResizeInputs()
    {
        if (MainImage.Source is BitmapSource bitmap)
        {
            ViewModel.ActualImageSize = new Size(bitmap.PixelWidth, bitmap.PixelHeight);
            aspectRatio = ViewModel.ActualImageSize.Width / ViewModel.ActualImageSize.Height;
        }
        else
        {
            ViewModel.ActualImageSize = ViewModel.OriginalImageSize;
            aspectRatio = ViewModel.ActualImageSize.Width / ViewModel.ActualImageSize.Height;
        }

        UpdateCurrentSizeDisplay();
        UpdateSizeInputFields();
    }

    private void UpdateCurrentSizeDisplay()
    {
        if (isPixelMode)
        {
            CurrentWidthDisplay.Text = ((int)ViewModel.ActualImageSize.Width).ToString();
            CurrentHeightDisplay.Text = ((int)ViewModel.ActualImageSize.Height).ToString();
            CurrentUnitsDisplay.Text = " px";
        }
        else
        {
            CurrentWidthDisplay.Text = "100";
            CurrentHeightDisplay.Text = "100";
            CurrentUnitsDisplay.Text = " %";
        }
    }

    private void UpdateSizeInputFields()
    {
        isUpdatingFromCode = true;

        if (isPixelMode)
        {
            WidthTextBox.Text = ((int)ViewModel.ActualImageSize.Width).ToString();
            HeightTextBox.Text = ((int)ViewModel.ActualImageSize.Height).ToString();
        }
        else
        {
            WidthTextBox.Text = "100";
            HeightTextBox.Text = "100";
        }

        isUpdatingFromCode = false;
    }

    private void SizeTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (isUpdatingFromCode) return;

        // Skip processing if user is actively dragging the resize grip
        if (isDraggingResizeGrip) return;

        if (sender is not Wpf.Ui.Controls.TextBox textBox) return;

        if (double.TryParse(textBox.Text, out double value) && value > 0)
        {
            if (isAspectRatioLocked)
            {
                isUpdatingFromCode = true;

                if (textBox == WidthTextBox)
                {
                    double newHeight = isPixelMode ? value / aspectRatio : value;
                    HeightTextBox.Text = ((int)newHeight).ToString();
                }
                else if (textBox == HeightTextBox)
                {
                    double newWidth = isPixelMode ? value * aspectRatio : value;
                    WidthTextBox.Text = ((int)newWidth).ToString();
                }

                isUpdatingFromCode = false;
            }

            ApplyManualResize();
        }
    }

    private void ApplyManualResize()
    {
        if (!double.TryParse(WidthTextBox.Text, out double width) || width <= 0) return;
        if (!double.TryParse(HeightTextBox.Text, out double height) || height <= 0) return;

        double targetWidth, targetHeight;

        if (isPixelMode)
        {
            targetWidth = width;
            targetHeight = height;
        }
        else
        {
            targetWidth = ViewModel.ActualImageSize.Width * (width / 100.0);
            targetHeight = ViewModel.ActualImageSize.Height * (height / 100.0);
        }

        // Calculate scale factors relative to original display size
        double widthScale = targetWidth / ViewModel.ActualImageSize.Width;
        double heightScale = targetHeight / ViewModel.ActualImageSize.Height;

        // Apply to ImageGrid (maintains the same logic as drag resize)
        ImageGrid.Width = ViewModel.OriginalImageSize.Width * widthScale;
        ImageGrid.Height = ViewModel.OriginalImageSize.Height * heightScale;
        ImageGrid.InvalidateMeasure();
    }

    private void PixelModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (PercentageModeToggle != null)
        {
            PercentageModeToggle.IsChecked = false;
            isPixelMode = true;
            UpdateCurrentSizeDisplay();
            UpdateSizeInputFields();
        }
    }

    private void PixelModeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (PercentageModeToggle != null && !PercentageModeToggle.IsChecked == true)
        {
            PercentageModeToggle.IsChecked = true;
        }
    }

    private void PercentageModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (PixelModeToggle is null)
            return;

        PixelModeToggle.IsChecked = false;
        isPixelMode = false;
        UpdateCurrentSizeDisplay();
        UpdateSizeInputFields();
    }

    private void PercentageModeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (PixelModeToggle is not null && PixelModeToggle.IsChecked is false)
        {
            PixelModeToggle.IsChecked = true;
        }
    }

    private void AspectRatioLockToggle_Checked(object sender, RoutedEventArgs e)
    {
        isAspectRatioLocked = true;
        MainImage.Stretch = Stretch.Uniform;
        AspectRatioIcon?.Symbol = SymbolRegular.Link24;
    }

    private void AspectRatioLockToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        isAspectRatioLocked = false;
        MainImage.Stretch = Stretch.Fill; // Allow stretching without maintaining aspect ratio
        AspectRatioIcon?.Symbol = SymbolRegular.LinkDismiss24;
    }

    #region Object Erase (AI)

    private bool isObjectEraseMode = false;

    private void ObjectEraseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
            return;

        ShowObjectEraseControls();
    }

    private void ShowObjectEraseControls()
    {
        HideCroppingControls();
        HideTransformControls();
        HideTriFoldControls();
        HideUnWarpControls();
        HideResizeControls();
        HideEdgeCorrectionControls();
        HideGridStraightenControls();

        HideThresholdControls();
        isObjectEraseMode = true;
        ObjectEraseButtonPanel.Visibility = Visibility.Visible;

        // Configure the erase mask canvas
        EraseMaskCanvas.Strokes.Clear();
        EraseMaskCanvas.Visibility = Visibility.Visible;
        EraseMaskCanvas.IsEnabled = true;
        EraseMaskCanvas.IsHitTestVisible = true;
        EraseMaskCanvas.EditingMode = InkCanvasEditingMode.Ink;
        EraseMaskCanvas.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = Colors.Red,
            Width = EraseBrushSizeSlider.Value,
            Height = EraseBrushSizeSlider.Value,
            IsHighlighter = true,
            StylusTip = StylusTip.Ellipse,
        };
    }

    private void HideObjectEraseControls()
    {
        isObjectEraseMode = false;
        ObjectEraseButtonPanel.Visibility = Visibility.Collapsed;

        EraseMaskCanvas.Strokes.Clear();
        EraseMaskCanvas.Visibility = Visibility.Collapsed;
        EraseMaskCanvas.IsEnabled = false;
        EraseMaskCanvas.IsHitTestVisible = false;
    }

    private void ThresholdMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
            return;

        ShowThresholdControls();
    }

    private void ShowThresholdControls()
    {
        HideObjectEraseControls();
        ThresholdPanel.Visibility = Visibility.Visible;
        EditHistogram.IsThresholdActive = true;
    }

    private void HideThresholdControls()
    {
        ThresholdPanel.Visibility = Visibility.Collapsed;
        EditHistogram.IsThresholdActive = false;
    }

    private async void ApplyThresholdButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyThresholdCommand.ExecuteAsync(null);
        HideThresholdControls();
    }

    private void CancelThresholdButton_Click(object sender, RoutedEventArgs e)
    {
        HideThresholdControls();
    }

    private void EraseBrushSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (EraseMaskCanvas is null)
            return;

        EraseMaskCanvas.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = Colors.Red,
            Width = e.NewValue,
            Height = e.NewValue,
            IsHighlighter = true,
            StylusTip = StylusTip.Ellipse,
        };
    }

    private void ClearEraseMaskButton_Click(object sender, RoutedEventArgs e)
    {
        EraseMaskCanvas.Strokes.Clear();
    }

    private async void ApplyObjectEraseButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
            return;

        if (EraseMaskCanvas.Strokes.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "Please paint over the objects you want to remove first.",
                "No Mask",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SetUiForLongTask();

        try
        {
            StrokeCollection strokes = EraseMaskCanvas.Strokes;
            double displayWidth = MainImage.ActualWidth;
            double displayHeight = MainImage.ActualHeight;

            string resultPath = await ObjectEraseHelper.EraseObjectsAsync(
                ViewModel.ImagePath!, strokes, displayWidth, displayHeight);

            MagickImageUndoRedoItem undoRedoItem = new(MainImage, ViewModel.ImagePath!, resultPath);
            UndoRedo.AddUndo(undoRedoItem);

            ViewModel.ImagePath = resultPath;

            using MagickImage resultImage = new(resultPath);
            MainImage.Source = resultImage.ToBitmapSource();
            ViewModel.ActualImageSize = new Size(resultImage.Width, resultImage.Height);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Object Erase failed: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            HideObjectEraseControls();
            SetUiForCompletedTask();
        }
    }

    private void CancelObjectEraseButton_Click(object sender, RoutedEventArgs e)
    {
        HideObjectEraseControls();
    }

    #endregion Object Erase (AI)

    private void MeasureDistanceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddNewMeasurementToolToCanvas();
    }

    private void MeasureAngleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddNewAngleMeasurementToolToCanvas();
    }

    private void MeasureRectangleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddNewRectangleMeasurementToolToCanvas();
    }

    private void AddNewMeasurementToolToCanvas()
    {
        double scale = ScaleInput.Value ?? 1.0;
        DistanceMeasurementControl measurementControl = new()
        {
            ScaleFactor = scale,
            Units = MeasurementUnits.Text
        };
        measurementControl.MeasurementPointMouseDown += MeasurementPoint_MouseDown;
        measurementControl.SetRealWorldLengthRequested += MeasurementControl_SetRealWorldLengthRequested;
        measurementControl.RemoveControlRequested += DistanceMeasurementControl_RemoveControlRequested;
        measurementTools.Add(measurementControl);
        ShapeCanvas.Children.Add(measurementControl);

        // Initialize with reasonable positions based on the canvas size
        measurementControl.InitializePositions(ShapeCanvas.ActualWidth, ShapeCanvas.ActualHeight);
    }

    private void AddNewAngleMeasurementToolToCanvas()
    {
        AngleMeasurementControl measurementControl = new();
        measurementControl.MeasurementPointMouseDown += AngleMeasurementPoint_MouseDown;
        measurementControl.RemoveControlRequested += AngleMeasurementControl_RemoveControlRequested;
        angleMeasurementTools.Add(measurementControl);
        ShapeCanvas.Children.Add(measurementControl);

        // Initialize with reasonable positions based on the canvas size
        measurementControl.InitializePositions(ShapeCanvas.ActualWidth, ShapeCanvas.ActualHeight);
    }

    private void AddNewRectangleMeasurementToolToCanvas()
    {
        double scale = ScaleInput.Value ?? 1.0;
        RectangleMeasurementControl measurementControl = new()
        {
            ScaleFactor = scale,
            Units = MeasurementUnits.Text
        };
        measurementControl.MeasurementPointMouseDown += RectangleMeasurementPoint_MouseDown;
        measurementControl.RemoveControlRequested += RectangleMeasurementControl_RemoveControlRequested;
        rectangleMeasurementTools.Add(measurementControl);
        ShapeCanvas.Children.Add(measurementControl);
        measurementControl.InitializePositions(ShapeCanvas.ActualWidth, ShapeCanvas.ActualHeight);
    }

    private void RemoveControlFromCanvas<T>(object sender, ObservableCollection<T> collection)
        where T : UIElement
    {
        if (sender is T control)
        {
            ShapeCanvas.Children.Remove(control);
            collection.Remove(control);
        }
    }

    private void DistanceMeasurementControl_RemoveControlRequested(object sender, EventArgs e) =>
        RemoveControlFromCanvas(sender, measurementTools);

    private void AngleMeasurementControl_RemoveControlRequested(object sender, EventArgs e) =>
        RemoveControlFromCanvas(sender, angleMeasurementTools);

    private void RectangleMeasurementControl_RemoveControlRequested(object sender, EventArgs e) =>
        RemoveControlFromCanvas(sender, rectangleMeasurementTools);

    private void PolygonMeasurementControl_RemoveControlRequested(object sender, EventArgs e) =>
        RemoveControlFromCanvas(sender, polygonMeasurementTools);

    private void CircleMeasurementControl_RemoveControlRequested(object sender, EventArgs e) =>
        RemoveControlFromCanvas(sender, circleMeasurementTools);

    private async void MeasurementControl_SetRealWorldLengthRequested(object sender, double pixelDistance)
    {
        if (sender is not DistanceMeasurementControl measurementControl)
            return;

        // Create and configure the number input dialog
        Wpf.Ui.Controls.TextBox inputTextBox = new()
        {
            PlaceholderText = "ex: 8.5 in",
            ClearButtonEnabled = true,
            Width = 250,
        };

        ContentDialog dialog = new()
        {
            Title = "Set Real World Length",
            Content = inputTextBox,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            // Show the dialog and handle the result
            DialogHost = Presenter
        };
        dialog.Closing += (s, args) =>
        {
            // Check if the primary button was clicked and input is valid
            string[] strings = inputTextBox.Text.Split(' ');
            if (args.Result == ContentDialogResult.Primary &&
                strings.Length > 0 &&
                double.TryParse(strings[0], out double realWorldLength) &&
                realWorldLength > 0)
            {
                // Calculate new scale factor (real-world units per pixel)
                double newScaleFactor = realWorldLength / pixelDistance;
                ScaleInput.Value = newScaleFactor;

                if (strings.Length > 1)
                    MeasurementUnits.Text = strings[1];
            }
        };

        await dialog.ShowAsync();
    }

    private void RemoveMeasurementControls()
    {
        foreach (DistanceMeasurementControl measurementControl in measurementTools)
        {
            measurementControl.MeasurementPointMouseDown -= MeasurementPoint_MouseDown;
            measurementControl.SetRealWorldLengthRequested -= MeasurementControl_SetRealWorldLengthRequested;
            measurementControl.RemoveControlRequested -= DistanceMeasurementControl_RemoveControlRequested;
            ShapeCanvas.Children.Remove(measurementControl);
        }

        measurementTools.Clear();

        foreach (AngleMeasurementControl measurementControl in angleMeasurementTools)
        {
            measurementControl.MeasurementPointMouseDown -= AngleMeasurementPoint_MouseDown;
            measurementControl.RemoveControlRequested -= AngleMeasurementControl_RemoveControlRequested;
            ShapeCanvas.Children.Remove(measurementControl);
        }

        angleMeasurementTools.Clear();

        foreach (RectangleMeasurementControl measurementControl in rectangleMeasurementTools)
        {
            measurementControl.MeasurementPointMouseDown -= RectangleMeasurementPoint_MouseDown;
            measurementControl.RemoveControlRequested -= RectangleMeasurementControl_RemoveControlRequested;
            ShapeCanvas.Children.Remove(measurementControl);
        }

        rectangleMeasurementTools.Clear();

        foreach (PolygonMeasurementControl measurementControl in polygonMeasurementTools)
        {
            measurementControl.MeasurementPointMouseDown -= PolygonMeasurementPoint_MouseDown;
            measurementControl.RemoveControlRequested -= PolygonMeasurementControl_RemoveControlRequested;
            ShapeCanvas.Children.Remove(measurementControl);
        }

        polygonMeasurementTools.Clear();

        foreach (CircleMeasurementControl measurementControl in circleMeasurementTools)
        {
            measurementControl.MeasurementPointMouseDown -= CircleMeasurementPoint_MouseDown;
            measurementControl.RemoveControlRequested -= CircleMeasurementControl_RemoveControlRequested;
            ShapeCanvas.Children.Remove(measurementControl);
        }

        circleMeasurementTools.Clear();

        foreach (VerticalLineControl lineControl in verticalLineControls)
        {
            lineControl.RemoveControlRequested -= VerticalLineControl_RemoveControlRequested;
            ShapeCanvas.Children.Remove(lineControl);
        }

        verticalLineControls.Clear();

        foreach (HorizontalLineControl lineControl in horizontalLineControls)
        {
            lineControl.RemoveControlRequested -= HorizontalLineControl_RemoveControlRequested;
            ShapeCanvas.Children.Remove(lineControl);
        }

        horizontalLineControls.Clear();

        ClearAllStrokesAndLengths();
        ClearAllMarkup();
        draggingMode = DraggingMode.None;
    }

    private bool HandleMeasurementMouseDown<T>(object sender, MouseButtonEventArgs? e, DraggingMode mode, Action<T> setActiveControl)
        where T : UIElement
    {
        if (isAdornerRotatingDrag)
        {
            e?.Handled = true;
            return false;
        }
        if (sender is Ellipse senderEllipse
            && senderEllipse.Parent is Canvas measureCanvas
            && measureCanvas.Parent is T measureControl)
        {
            setActiveControl(measureControl);
            draggingMode = mode;
            if (e is not null)
            {
                clickedPoint = e.GetPosition(ShapeCanvas);
                ShowPixelZoom(clickedPoint);
            }
            CaptureMouse();
            return true;
        }
        return false;
    }

    private void MeasurementPoint_MouseDown(object sender, MouseButtonEventArgs? e) =>
        HandleMeasurementMouseDown<DistanceMeasurementControl>(sender, e, DraggingMode.MeasureDistance, c => activeMeasureControl = c);

    private void AngleMeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e) =>
        HandleMeasurementMouseDown<AngleMeasurementControl>(sender, e, DraggingMode.MeasureAngle, c => activeAngleMeasureControl = c);

    private void RectangleMeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e) =>
        HandleMeasurementMouseDown<RectangleMeasurementControl>(sender, e, DraggingMode.MeasureRectangle, c => activeRectangleMeasureControl = c);

    private void PolygonMeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e) =>
        HandleMeasurementMouseDown<PolygonMeasurementControl>(sender, e, DraggingMode.MeasurePolygon, c => activePolygonMeasureControl = c);

    private void CircleMeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e) =>
        HandleMeasurementMouseDown<CircleMeasurementControl>(sender, e, DraggingMode.MeasureCircle, c => activeCircleMeasureControl = c);

    private void MarkupShapePoint_MouseDown(object sender, MouseButtonEventArgs? e)
    {
        if (HandleMeasurementMouseDown<MarkupShapeControl>(sender, e, DraggingMode.MarkupShape, c => activeMarkupShapeControl = c))
        {
            // Capture before-state for point-move undo (overridden to true by creation caller if needed)
            isMarkupShapeDragCreation = false;
            if (activeMarkupShapeControl is not null)
            {
                (markupShapeBeforePoint1, markupShapeBeforePoint2) = activeMarkupShapeControl.GetPoints();
                markupShapeBeforeDragIndex = activeMarkupShapeControl.GetActivePointIndex();
            }
        }
    }

    private async void SetImageScaleButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainImage.Source is not BitmapSource bitmap)
            return;

        int sourcePixelWidth = bitmap.PixelWidth;
        int sourcePixelHeight = bitmap.PixelHeight;

        double displayWidth = MainImage.ActualWidth;
        double displayHeight = MainImage.ActualHeight;

        if (displayWidth <= 0 || displayHeight <= 0)
            return;

        double imageAspectRatio = displayWidth / displayHeight;

        bool isUpdatingFromScale = false;

        Wpf.Ui.Controls.NumberBox widthInput = new()
        {
            Value = null,
            Minimum = 0.0001,
            SmallChange = 0.1,
            Width = 180,
            PlaceholderText = "Width",
        };

        Wpf.Ui.Controls.NumberBox heightInput = new()
        {
            Value = null,
            Minimum = 0.0001,
            SmallChange = 0.1,
            Width = 180,
            PlaceholderText = "Height",
        };

        Wpf.Ui.Controls.TextBox unitsInput = new()
        {
            Text = MeasurementUnits.Text,
            PlaceholderText = "e.g. in, cm, mm",
            Width = 180,
        };

        widthInput.ValueChanged += (s, args) =>
        {
            if (isUpdatingFromScale)
                return;

            double newWidth = widthInput.Value ?? 0;
            if (newWidth <= 0) return;

            isUpdatingFromScale = true;
            heightInput.Value = Math.Round(newWidth / imageAspectRatio, 4);
            isUpdatingFromScale = false;
        };

        heightInput.ValueChanged += (s, args) =>
        {
            if (isUpdatingFromScale)
                return;

            double newHeight = heightInput.Value ?? 0;
            if (newHeight <= 0) return;

            isUpdatingFromScale = true;
            widthInput.Value = Math.Round(newHeight * imageAspectRatio, 4);
            isUpdatingFromScale = false;
        };

        StackPanel content = new()
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                new WpfTextBlock
                {
                    Text = $"Source: {sourcePixelWidth} × {sourcePixelHeight} px",
                    Margin = new Thickness(0, 0, 0, 4),
                    FontWeight = FontWeights.SemiBold,
                },
                new WpfTextBlock
                {
                    Text = $"Display: {displayWidth:F0} × {displayHeight:F0} px",
                    Margin = new Thickness(0, 0, 0, 12),
                    Foreground = System.Windows.Media.Brushes.Gray,
                },
                new WpfTextBlock { Text = "Real-world width:", Margin = new Thickness(0, 0, 0, 4) },
                widthInput,
                new WpfTextBlock { Text = "Real-world height:", Margin = new Thickness(0, 8, 0, 4) },
                heightInput,
                new WpfTextBlock { Text = "Units:", Margin = new Thickness(0, 8, 0, 4) },
                unitsInput,
            },
        };

        ContentDialog dialog = new()
        {
            Title = "Set Image Scale",
            Content = content,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DialogHost = Presenter,
        };

        dialog.Closing += (s, args) =>
        {
            if (args.Result != ContentDialogResult.Primary)
                return;

            double realWidth = widthInput.Value ?? 0;
            double realHeight = heightInput.Value ?? 0;

            if (realWidth <= 0 && realHeight <= 0)
                return;

            // Calculate real-world units per display pixel using the actual control size
            double pixelsPerUnit = realWidth > 0
                ? realWidth / displayWidth
                : realHeight / displayHeight;

            ScaleInput.Value = pixelsPerUnit;

            string units = unitsInput.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(units))
                MeasurementUnits.Text = units;
        };

        await dialog.ShowAsync();
    }

    private void ScaleInput_ValueChanged(object sender, RoutedEventArgs e)
    {
        double newScale = ScaleInput.Value ?? 1.0;
        foreach (DistanceMeasurementControl tool in measurementTools)
            tool.ScaleFactor = newScale;

        foreach (RectangleMeasurementControl tool in rectangleMeasurementTools)
            tool.ScaleFactor = newScale;

        foreach (PolygonMeasurementControl tool in polygonMeasurementTools)
            tool.ScaleFactor = newScale;

        foreach (CircleMeasurementControl tool in circleMeasurementTools)
            tool.ScaleFactor = newScale;

        // Update stroke measurements
        UpdateStrokeMeasurements();
    }

    private void MeasurementUnits_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox || string.IsNullOrWhiteSpace(textBox.Text))
            return;

        foreach (DistanceMeasurementControl tool in measurementTools)
            tool.Units = textBox.Text;

        foreach (RectangleMeasurementControl tool in rectangleMeasurementTools)
            tool.Units = textBox.Text;

        foreach (PolygonMeasurementControl tool in polygonMeasurementTools)
            tool.Units = textBox.Text;

        foreach (CircleMeasurementControl tool in circleMeasurementTools)
            tool.Units = textBox.Text;

        // Update stroke measurements
        UpdateStrokeMeasurements();
    }

    private void UpdateStrokeMeasurements()
    {
        if (MeasurementUnits is null) return;

        double scaleFactor = ScaleInput.Value ?? 1.0;
        string units = MeasurementUnits.Text;

        Dictionary<Stroke, StrokeInfo> updatedMeasurements = [];

        foreach (KeyValuePair<Stroke, StrokeInfo> entry in strokeMeasurements)
        {
            Stroke stroke = entry.Key;
            StrokeInfo info = entry.Value;

            // Update the scaled length with new scale factor
            info.ScaledLength = info.PixelLength * scaleFactor;
            info.Units = units;

            updatedMeasurements[stroke] = info;
        }

        strokeMeasurements = updatedMeasurements;
    }

    private void HideMeasurementsToggle_Checked(object sender, RoutedEventArgs e)
    {
        SetMeasurementsVisibility(Visibility.Collapsed);
    }

    private void HideMeasurementsToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        SetMeasurementsVisibility(Visibility.Visible);
    }

    private void SetMeasurementsVisibility(Visibility visibility)
    {
        foreach (DistanceMeasurementControl control in measurementTools)
            control.Visibility = visibility;

        foreach (AngleMeasurementControl control in angleMeasurementTools)
            control.Visibility = visibility;

        foreach (RectangleMeasurementControl control in rectangleMeasurementTools)
            control.Visibility = visibility;

        foreach (PolygonMeasurementControl control in polygonMeasurementTools)
            control.Visibility = visibility;

        foreach (CircleMeasurementControl control in circleMeasurementTools)
            control.Visibility = visibility;

        foreach (VerticalLineControl control in verticalLineControls)
            control.Visibility = visibility;

        foreach (HorizontalLineControl control in horizontalLineControls)
            control.Visibility = visibility;

        DrawingCanvas.Visibility = visibility;
    }

    private void CloseMeasurementButton_Click(object sender, RoutedEventArgs e)
    {
        HideMeasurementsToggle.IsChecked = false;
        RemoveMeasurementControls();
        ClearAllStrokesAndLengths();
        isDrawingMode = false;
        DrawingCanvas.IsEnabled = false;
        DrawingOptionsPanel.Visibility = Visibility.Collapsed;
    }

    private MagickCropMeasurementPackage BuildCurrentPackage(PackageMetadata? metadataOverride = null)
    {
        PackageMetadata metadata = metadataOverride ?? new PackageMetadata
        {
            OriginalFilename = ViewModel.OpenedFileName,
            OriginalImageSize = ViewModel.OriginalImageSize,
            CurrentImageSize = new Size(ImageGrid.ActualWidth, ImageGrid.ActualHeight),
            ImageStretch = MainImage.Stretch
        };

        MagickCropMeasurementPackage package = new()
        {
            ImagePath = ViewModel.ImagePath,
            Metadata = metadata,
            Measurements = new MeasurementCollection
            {
                GlobalScaleFactor = ScaleInput.Value ?? 1.0,
                GlobalUnits = MeasurementUnits.Text
            }
        };

        foreach (DistanceMeasurementControl control in measurementTools)
            package.Measurements.DistanceMeasurements.Add(control.ToDto());

        foreach (AngleMeasurementControl control in angleMeasurementTools)
            package.Measurements.AngleMeasurements.Add(control.ToDto());

        foreach (RectangleMeasurementControl control in rectangleMeasurementTools)
            package.Measurements.RectangleMeasurements.Add(control.ToDto());

        foreach (CircleMeasurementControl control in circleMeasurementTools)
            package.Measurements.CircleMeasurements.Add(control.ToDto());

        foreach (PolygonMeasurementControl control in polygonMeasurementTools)
            package.Measurements.PolygonMeasurements.Add(control.ToDto());

        foreach (VerticalLineControl control in verticalLineControls)
            package.Measurements.VerticalLines.Add(control.ToDto());

        foreach (HorizontalLineControl control in horizontalLineControls)
            package.Measurements.HorizontalLines.Add(control.ToDto());

        foreach (KeyValuePair<Stroke, StrokeInfo> entry in strokeMeasurements)
        {
            Stroke stroke = entry.Key;
            StrokeInfo info = entry.Value;

            StrokeLengthDisplay? display = ShapeCanvas.Children.OfType<StrokeLengthDisplay>()
                .FirstOrDefault(d => d.GetStroke() == stroke);

            double displayX = 0;
            double displayY = 0;

            if (display is not null)
            {
                displayX = Canvas.GetLeft(display);
                displayY = Canvas.GetTop(display);
            }

            package.Measurements.InkStrokes.Add(StrokeDto.ConvertStrokeToDto(stroke));
            package.Measurements.StrokeInfos.Add(StrokeInfoDto.FromStrokeInfo(info, displayX, displayY));
        }

        foreach (MarkupShapeControl control in markupShapeControls)
            package.Measurements.MarkupShapes.Add(control.ToDto());

        foreach (MarkupTextControl control in markupTextControls)
            package.Measurements.MarkupTexts.Add(control.ToDto());

        foreach (Stroke stroke in MarkupCanvas.Strokes)
            package.Measurements.MarkupStrokes.Add(MarkupStrokeDto.FromStroke(stroke));

        return package;
    }

    private void SaveMeasurementsPackageToFile()
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
        {
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Error",
                Content = "No image loaded. Please open an image first.",
            };
            uiMessageBox.ShowDialogAsync();
            return;
        }

        MagickCropMeasurementPackage package = BuildCurrentPackage();

        // Show save file dialog
        SaveFileDialog saveFileDialog = new()
        {
            Filter = "MagickCrop Measurement Files|*.mcm",
            RestoreDirectory = true,
            FileName = $"{ViewModel.OpenedFileName}_measurements.mcm"
        };

        if (saveFileDialog.ShowDialog() != true)
            return;

        SetUiForLongTask();

        try
        {
            // Save to the selected file
            bool success = package.SaveToFileAsync(saveFileDialog.FileName);

            if (!success)
            {
                Wpf.Ui.Controls.MessageBox uiMessageBox = new()
                {
                    Title = "Error",
                    Content = "Failed to save the measurement package.",
                };
                uiMessageBox.ShowDialogAsync();
            }
        }
        finally
        {
            SetUiForCompletedTask();
        }
    }

    public async Task<bool> LoadMeasurementsPackageFromFile()
    {
        SetUiForLongTask();

        OpenFileDialog openFileDialog = new()
        {
            Filter = "Magick Crop Project Files|*.mcm|All Files|*.*",
            RestoreDirectory = true
        };

        if (openFileDialog.ShowDialog() is not true)
        {
            SetUiForCompletedTask();
            return false;
        }

        string fileName = openFileDialog.FileName;
        await LoadMeasurementPackageAsync(fileName);

        return true;
    }

    private async Task LoadMeasurementPackageAsync(string fileName)
    {
        MagickCropMeasurementPackage? package = null;
        try
        {
            package = MagickCropMeasurementPackage.LoadFromFileAsync(fileName);
            if (package is null
                || string.IsNullOrEmpty(package.ImagePath)
                || !File.Exists(package.ImagePath))
            {
                Wpf.Ui.Controls.MessageBox uiMessageBox = new()
                {
                    Title = "Error",
                    Content = "Failed to load measurement package. The image file may be missing or corrupted.",
                };
                await uiMessageBox.ShowDialogAsync();
                SetUiForCompletedTask();
                WelcomeMessageModal.Visibility = Visibility.Visible;
                return;
            }
            ViewModel.OpenedPackage = package;

            // Load the image
            await OpenImagePath(package.ImagePath);

            // Restore the original filename from the package metadata
            // This is important because OpenImagePath sets ViewModel.OpenedFileName from the temp file path
            if (!string.IsNullOrEmpty(package.Metadata.OriginalFilename))
            {
                ViewModel.OpenedFileName = package.Metadata.OriginalFilename;
            }
        }
        finally
        {
            SetUiForCompletedTask();
        }

        // Clear existing measurements
        RemoveMeasurementControls();

        // Apply saved resize if different from original
        if (package.Metadata.CurrentImageSize.Width > 0 && package.Metadata.CurrentImageSize.Height > 0)
        {
            if (package.Metadata.OriginalImageSize.Width > 0 && package.Metadata.OriginalImageSize.Height > 0)
            {
                ViewModel.OriginalImageSize = package.Metadata.OriginalImageSize;
                ImageGrid.Width = ViewModel.OriginalImageSize.Width;
                ImageGrid.Height = ViewModel.OriginalImageSize.Height;
            }

            // Apply the saved resize to the ImageGrid
            ImageGrid.Width = package.Metadata.CurrentImageSize.Width;
            ImageGrid.Height = package.Metadata.CurrentImageSize.Height;
        }

        // Restore the saved stretch mode
        MainImage.Stretch = package.Metadata.ImageStretch;

        // Guard against stale saved dimensions: if Stretch is Uniform and the saved
        // ImageGrid aspect ratio does not match the loaded image aspect ratio, the image
        // would render letterboxed inside the grid, causing a vertical/horizontal offset
        // for any subsequent crop-detection or edge-detection coordinate mapping.
        // In that case reset to auto-size so the grid always tightly wraps the image.
        if (MainImage.Stretch == Stretch.Uniform
            && MainImage.Source is System.Windows.Media.Imaging.BitmapSource bmpSrc
            && bmpSrc.PixelWidth > 0 && bmpSrc.PixelHeight > 0
            && ImageGrid.Height > 0)
        {
            double savedAspect = ImageGrid.Width / ImageGrid.Height;
            double imageAspect = (double)bmpSrc.PixelWidth / bmpSrc.PixelHeight;
            // Allow a small tolerance for floating-point rounding
            if (Math.Abs(savedAspect - imageAspect) > 0.01)
            {
                ImageGrid.Width = ImageWidthConst;
                ImageGrid.Height = double.NaN;
            }
        }

        // Set global measurement properties
        ScaleInput.Value = package.Measurements.GlobalScaleFactor;
        MeasurementUnits.Text = package.Measurements.GlobalUnits;

        // Add distance measurements
        foreach (DistanceMeasurementControlDto dto in package.Measurements.DistanceMeasurements)
        {
            DistanceMeasurementControl control = new()
            {
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            control.FromDto(dto);
            control.MeasurementPointMouseDown += MeasurementPoint_MouseDown;
            control.SetRealWorldLengthRequested += MeasurementControl_SetRealWorldLengthRequested;
            control.RemoveControlRequested += DistanceMeasurementControl_RemoveControlRequested;
            measurementTools.Add(control);
            ShapeCanvas.Children.Add(control);
        }

        // Add angle measurements
        foreach (AngleMeasurementControlDto dto in package.Measurements.AngleMeasurements)
        {
            AngleMeasurementControl control = new();
            control.FromDto(dto);
            control.MeasurementPointMouseDown += AngleMeasurementPoint_MouseDown;
            control.RemoveControlRequested += AngleMeasurementControl_RemoveControlRequested;
            angleMeasurementTools.Add(control);
            ShapeCanvas.Children.Add(control);
        }        // Add rectangle measurements
        foreach (RectangleMeasurementControlDto dto in package.Measurements.RectangleMeasurements)
        {
            RectangleMeasurementControl control = new()
            {
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            control.FromDto(dto);
            control.MeasurementPointMouseDown += RectangleMeasurementPoint_MouseDown;
            control.RemoveControlRequested += RectangleMeasurementControl_RemoveControlRequested;
            rectangleMeasurementTools.Add(control);
            ShapeCanvas.Children.Add(control);
        }

        // Add circle measurements
        foreach (CircleMeasurementControlDto dto in package.Measurements.CircleMeasurements)
        {
            CircleMeasurementControl control = new()
            {
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            control.FromDto(dto);
            control.MeasurementPointMouseDown += CircleMeasurementPoint_MouseDown;
            control.RemoveControlRequested += CircleMeasurementControl_RemoveControlRequested;
            circleMeasurementTools.Add(control);
            ShapeCanvas.Children.Add(control);
        }

        // Add polygon measurements
        Debug.WriteLine($"Loading {package.Measurements.PolygonMeasurements.Count} polygon measurements from package");
        foreach (PolygonMeasurementControlDto dto in package.Measurements.PolygonMeasurements)
        {
            PolygonMeasurementControl control = new()
            {
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            control.FromDto(dto);
            control.MeasurementPointMouseDown += PolygonMeasurementPoint_MouseDown;
            control.RemoveControlRequested += PolygonMeasurementControl_RemoveControlRequested;
            polygonMeasurementTools.Add(control);
            ShapeCanvas.Children.Add(control);
        }
        Debug.WriteLine($"Loaded polygon measurements. Total in collection: {polygonMeasurementTools.Count}");

        foreach (VerticalLineControlDto dto in package.Measurements.VerticalLines)
        {
            VerticalLineControl control = new();
            control.FromDto(dto);
            control.RemoveControlRequested += VerticalLineControl_RemoveControlRequested;
            verticalLineControls.Add(control);
            ShapeCanvas.Children.Add(control);
        }

        foreach (HorizontalLineControlDto dto in package.Measurements.HorizontalLines)
        {
            HorizontalLineControl control = new();
            control.FromDto(dto);
            control.RemoveControlRequested += HorizontalLineControl_RemoveControlRequested;
            horizontalLineControls.Add(control);
            ShapeCanvas.Children.Add(control);
        }

        ClearAllStrokesAndLengths();

        for (int i = 0; i < package.Measurements.InkStrokes.Count; i++)
        {
            if (i >= package.Measurements.StrokeInfos.Count) break;

            StrokeDto strokeDto = package.Measurements.InkStrokes[i];
            StrokeInfoDto infoDto = package.Measurements.StrokeInfos[i];

            Stroke stroke = StrokeDto.ConvertDtoToStroke(strokeDto);
            DrawingCanvas.Strokes.Add(stroke);

            StrokeLengthDisplay lengthDisplay = new(infoDto.ToStrokeInfo(), stroke, DrawingCanvas, ShapeCanvas);
            lengthDisplay.RemoveControlRequested += LengthDisplay_RemoveControlRequested;
            Canvas.SetTop(lengthDisplay, infoDto.DisplayPositionY);
            Canvas.SetLeft(lengthDisplay, infoDto.DisplayPositionX);
            ShapeCanvas.Children.Add(lengthDisplay);

            strokeMeasurements.Add(stroke, infoDto.ToStrokeInfo());
        }

        // Restore markup shapes
        foreach (MarkupShapeDto dto in package.Measurements.MarkupShapes)
        {
            MarkupShapeControl control = new();
            control.FromDto(dto);
            control.IsDragGizmoVisible = MarkupTabItem?.IsSelected == true;
            control.IsHitTestVisible = MarkupTabItem?.IsSelected == true;
            control.MeasurementPointMouseDown += MarkupShapePoint_MouseDown;
            control.RemoveControlRequested += MarkupShapeControl_RemoveControlRequested;
            markupShapeControls.Add(control);
            ShapeCanvas.Children.Add(control);
        }

        // Restore markup text annotations
        foreach (MarkupTextDto dto in package.Measurements.MarkupTexts)
        {
            MarkupTextControl control = new();
            control.FromDto(dto);
            control.IsHitTestVisible = MarkupTabItem?.IsSelected == true;
            control.RemoveControlRequested += MarkupTextControl_RemoveControlRequested;
            control.EditCommitted += MarkupTextControl_EditCommitted;
            control.TextMoved += MarkupTextControl_TextMoved;
            Canvas.SetLeft(control, dto.PositionX);
            Canvas.SetTop(control, dto.PositionY);
            markupTextControls.Add(control);
            ShapeCanvas.Children.Add(control);
        }

        // Restore markup canvas strokes
        MarkupCanvas.Strokes.Clear();
        foreach (MarkupStrokeDto dto in package.Measurements.MarkupStrokes)
            MarkupCanvas.Strokes.Add(dto.ToStroke());

        if (package?.Metadata?.ProjectId is not null)
            ViewModel.CurrentProjectId = package.Metadata.ProjectId;
        else
            ViewModel.CurrentProjectId = Guid.NewGuid().ToString();

        MeasureTabItem.IsSelected = true;

        // Center and zoom to fit the image in the viewport
        CenterAndZoomToFit();
    }

    public async void LoadMeasurementsPackageFromFile(string filePath)
    {
        SetUiForLongTask();
        WelcomeMessageModal.Visibility = Visibility.Collapsed;

        await LoadMeasurementPackageAsync(filePath);
    }

    public async Task OpenSharedImageAsync(string filePath)
    {
        ViewModel.WindowTitle = $"Magick Crop & Measure: {System.IO.Path.GetFileName(filePath)}";
        await OpenImagePath(filePath);
    }

    private void SavePackageButton_Click(object sender, RoutedEventArgs e)
    {
        SaveMeasurementsPackageToFile();
    }

    private void InitializeProjectManager()
    {
        recentProjectsManager = Singleton<RecentProjectsManager>.Instance;

        // Setup autosave timer
        autoSaveTimer = new System.Timers.Timer(AutoSaveIntervalMs);
        autoSaveTimer.Elapsed += AutoSaveTimer_Elapsed;
        autoSaveTimer.AutoReset = true;

        // Create a new project ID
        ViewModel.CurrentProjectId = Guid.NewGuid().ToString();
    }

    private void AutoSaveTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (ViewModel.IsBusy)
            return; // Don't autosave if the UI is busy

        // Run on UI thread
        Dispatcher.Invoke(() =>
        {
            // Only autosave if we have an image and measurements that need saving
            if (MainImage.Source == null || string.IsNullOrEmpty(ViewModel.ImagePath))
                return;

            AutosaveCurrentState();
        });
    }

    private void AutosaveCurrentState()
    {
        if (recentProjectsManager == null || MainImage.Source == null || string.IsNullOrEmpty(ViewModel.ImagePath))
            return;

        try
        {
            PackageMetadata packageMetadata = new()
            {
                OriginalFilename = ViewModel.OpenedFileName,
                ProjectId = ViewModel.CurrentProjectId,
                LastModified = DateTime.Now,
                OriginalImageSize = ViewModel.OriginalImageSize,
                CurrentImageSize = new Size(ImageGrid.ActualWidth, ImageGrid.ActualHeight),
                ImageStretch = MainImage.Stretch
            };

            if (ViewModel.OpenedPackage is not null)
            {
                packageMetadata = ViewModel.OpenedPackage.Metadata;
                packageMetadata.LastModified = DateTime.Now;
                packageMetadata.OriginalImageSize = ViewModel.OriginalImageSize;
                packageMetadata.CurrentImageSize = new Size(ImageGrid.ActualWidth, ImageGrid.ActualHeight);
                packageMetadata.ImageStretch = MainImage.Stretch;
            }

            MagickCropMeasurementPackage package = BuildCurrentPackage(packageMetadata);
            Debug.WriteLine($"AutoSave: Saved {polygonMeasurementTools.Count} polygon measurements");

            recentProjectsManager.AutosaveProject(package, MainImage.Source as BitmapSource);
        }
        catch (Exception ex)
        {
            // Log error but don't show to user since this is automatic
            Debug.WriteLine($"Error autosaving project: {ex.Message}");
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Stop the autosave timer
        autoSaveTimer?.Stop();

        // Save the current project state one last time
        AutosaveCurrentState();

        base.OnClosing(e);
    }

    private void CloseFileIcon_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        AutosaveCurrentState();
        WelcomeMessageModal.UpdateRecentProjects();
        ResetApplicationState();
    }

    /// <summary>
    /// Resets all transient interaction state: transform corner markers, rotation,
    /// measurement placement modes, picker modes, drawing, selectors, and pixel zoom.
    /// Call this when closing a project or before opening a new image.
    /// </summary>
    private void ResetTransientState()
    {
        // --- Cancel any active placement / creation state ---
        isCreatingMeasurement = false;
        draggingMode = DraggingMode.None;
        EndTransformHandleDrag();
        clickedElement = null;
        pointDraggingIndex = -1;
        lastActiveTransformHandle = null;
        lastActiveTransformIndex = -1;
        ShapeCanvas.ReleaseMouseCapture();
        ReleaseMouseCapture();

        // --- Rotation state ---
        if (isRotateMode)
            ToggleRotateMode(false);
        isFreeRotatingDrag = false;
        currentPreviewRotation = 0;
        HideRotationOverlay();

        // --- White / black point picker ---
        isWhitePointPickerMode = false;
        isBlackPointPickerMode = false;
        WhitePointPickerToggle.IsChecked = false;
        BlackPointPickerToggle.IsChecked = false;
        WhitePointColorPreview.Visibility = Visibility.Collapsed;

        // --- Angle measurement placement ---
        isPlacingAngleMeasurement = false;
        anglePlacementStep = AnglePlacementStep.None;
        if (activeAnglePlacementControl != null)
        {
            ShapeCanvas.Children.Remove(activeAnglePlacementControl);
            activeAnglePlacementControl = null;
        }

        // --- Rectangle measurement placement ---
        isPlacingRectangleMeasurement = false;
        if (activeRectanglePlacementControl != null)
        {
            ShapeCanvas.Children.Remove(activeRectanglePlacementControl);
            activeRectanglePlacementControl = null;
        }

        // --- Polygon measurement placement ---
        isPlacingPolygonMeasurement = false;
        if (activePolygonPlacementControl != null)
        {
            ShapeCanvas.Children.Remove(activePolygonPlacementControl);
            activePolygonPlacementControl = null;
        }

        // --- Circle measurement placement ---
        isPlacingCircleMeasurement = false;
        if (activeCirclePlacementControl != null)
        {
            ShapeCanvas.Children.Remove(activeCirclePlacementControl);
            activeCirclePlacementControl = null;
        }

        // --- Active measure control references ---
        activeMeasureControl = null;
        activeAngleMeasureControl = null;
        activeRectangleMeasureControl = null;
        activePolygonMeasureControl = null;
        activeCircleMeasureControl = null;

        // --- Drawing mode ---
        isDrawingMode = false;
        DrawingCanvas.IsEnabled = false;
        DrawingCanvas.IsHitTestVisible = false;
        DrawingOptionsPanel.Visibility = Visibility.Collapsed;

        // --- Measurement tool toggles ---
        UncheckAllBut();

        // --- Quadrilateral selectors ---
        HideQuadrilateralSelector();
        HideCropQuadrilateralSelector();

        // --- Pixel zoom ---
        HidePixelZoom();

        // --- Cursor ---
        Cursor = null;

        // --- Transform corner markers: reset to XAML defaults ---
        ResetTransformCornerMarkers();

        // --- Tri-fold state ---
        HideTriFoldControls();

        // --- Un-warp state ---
        HideUnWarpControls();

        // --- Resize drag state ---
        isDraggingResizeGrip = false;
        ViewModel.ActualImageSize = new Size();
    }

    /// <summary>
    /// Resets the perspective-transform corner markers and polyline to the XAML default positions.
    /// </summary>
    private void ResetTransformCornerMarkers()
    {
        Canvas.SetLeft(TopLeft, 100);
        Canvas.SetTop(TopLeft, 100);

        Canvas.SetLeft(TopRight, 700);
        Canvas.SetTop(TopRight, 100);

        Canvas.SetLeft(BottomRight, 700);
        Canvas.SetTop(BottomRight, 525);

        Canvas.SetLeft(BottomLeft, 100);
        Canvas.SetTop(BottomLeft, 525);

        // Rebuild the polyline so it matches the reset marker positions
        DrawPolyLine();

        UpdateCornerNavButtons();
    }

    private void ResetApplicationState()
    {
        // Stop the autosave timer
        autoSaveTimer?.Stop();
        AutosaveCurrentState();

        // Clear the image
        MainImage.Source = null;
        ViewModel.ImagePath = null;
        ViewModel.OpenedFileName = string.Empty;
        ViewModel.OpenedPackage = null;
        ViewModel.SavedPath = null;

        // Reset the title
        ViewModel.WindowTitle = "Magick Crop & Measure by TheJoeFin";

        // Reset all transient interaction / control state
        ResetTransientState();

        // Reset UI elements
        RemoveMeasurementControls();
        HideTransformControls();
        HideCroppingControls();
        HideResizeControls();
        HideObjectEraseControls();
        HideThresholdControls();
        HideSidebar();
        WelcomeMessageModal.Visibility = Visibility.Visible;
        OpenFolderButton.IsEnabled = false;
        Save.IsEnabled = false;

        // Reset the canvas transform
        StopCanvasTranslateAnimation();
        canvasScale.ScaleX = 1;
        canvasScale.ScaleY = 1;
        canvasScale.CenterX = 0;
        canvasScale.CenterY = 0;
        canvasTranslate.X = 0;
        canvasTranslate.Y = 0;

        // Reset undo/redo
        UndoRedo.Clear();

        // Create a new project ID
        ViewModel.CurrentProjectId = Guid.NewGuid().ToString();
    }

    private void AddVerticalLine()
    {
        VerticalLineControl lineControl = new();
        lineControl.RemoveControlRequested += VerticalLineControl_RemoveControlRequested;
        verticalLineControls.Add(lineControl);
        ShapeCanvas.Children.Add(lineControl);

        // Initialize with reasonable positions based on the canvas size
        lineControl.Initialize(MainImage.ActualHeight, MainImage.ActualHeight);
    }

    private void VerticalLineControl_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is VerticalLineControl control)
        {
            ShapeCanvas.Children.Remove(control);
            verticalLineControls.Remove(control);
        }
    }

    private void AddHorizontalLine()
    {
        HorizontalLineControl lineControl = new();
        lineControl.RemoveControlRequested += HorizontalLineControl_RemoveControlRequested;
        horizontalLineControls.Add(lineControl);
        ShapeCanvas.Children.Add(lineControl);

        // Initialize with reasonable positions based on the canvas size
        lineControl.Initialize(MainImage.ActualWidth, MainImage.ActualWidth);
    }

    private void HorizontalLineControl_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is HorizontalLineControl control)
        {
            ShapeCanvas.Children.Remove(control);
            horizontalLineControls.Remove(control);
        }
    }

    private void ShapeCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        foreach (VerticalLineControl control in verticalLineControls)
        {
            control.Resize(e.NewSize.Height);
        }

        foreach (HorizontalLineControl control in horizontalLineControls)
        {
            control.Resize(e.NewSize.Width);
        }
    }

    private void HorizontalLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddHorizontalLine();
    }

    private void VerticalLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddVerticalLine();
    }

    private void DrawingCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        Stroke stroke = e.Stroke;

        double pixelLength = CalculateStrokeLength(stroke);

        // Calculate scaled length based on current scale factor and units
        double scaleFactor = ScaleInput.Value ?? 1.0;
        double scaledLength = pixelLength * scaleFactor;
        string units = MeasurementUnits.Text;

        StrokeInfo strokeInfo = new()
        {
            PixelLength = pixelLength,
            ScaledLength = scaledLength,
            Units = units
        };

        strokeMeasurements[stroke] = strokeInfo;
        DrawingCanvas.Strokes.Remove(stroke);
        DrawingCanvas.Strokes.Add(stroke);

        ShowStrokeMeasurement(stroke, strokeInfo);
    }

    private void ShowStrokeMeasurement(Stroke stroke, StrokeInfo strokeInfo)
    {
        StrokeLengthDisplay lengthDisplay = new(strokeInfo, stroke, DrawingCanvas, ShapeCanvas);
        lengthDisplay.SetRealWorldLengthRequested += MeasurementControl_SetRealWorldLengthRequested;
        lengthDisplay.RemoveControlRequested += LengthDisplay_RemoveControlRequested;

        Point endPoint = stroke.StylusPoints.Last().ToPoint();
        Canvas.SetLeft(lengthDisplay, endPoint.X + 10);
        Canvas.SetTop(lengthDisplay, endPoint.Y - 30);
        ShapeCanvas.Children.Add(lengthDisplay);
    }

    private void LengthDisplay_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is StrokeLengthDisplay control)
        {
            ShapeCanvas.Children.Remove(control);
            strokeMeasurements.Remove(control.GetStroke());
        }
    }

    private static double CalculateStrokeLength(Stroke stroke)
    {
        double length = 0;
        StylusPointCollection points = stroke.StylusPoints;

        for (int i = 1; i < points.Count; i++)
        {
            Point p1 = points[i - 1].ToPoint();
            Point p2 = points[i].ToPoint();

            // Calculate distance between consecutive points
            double segmentLength = Math.Sqrt(
                Math.Pow(p2.X - p1.X, 2) +
                Math.Pow(p2.Y - p1.Y, 2));

            length += segmentLength;
        }

        return length;
    }

    private void StrokeThicknessSlider_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (DrawingCanvas == null) return;

        DrawingAttributes drawingAttributes = DrawingCanvas.DefaultDrawingAttributes;
        drawingAttributes.Width = StrokeThicknessSlider.Value;
        drawingAttributes.Height = StrokeThicknessSlider.Value;
        drawingAttributes.Color = Color.FromArgb(255, 0, 102, 255);
        DrawingCanvas.DefaultDrawingAttributes = drawingAttributes;
    }

    private void ClearDrawingsButton_Click(object sender, RoutedEventArgs e)
    {
        ClearAllStrokesAndLengths();
    }

    private void ClearAllStrokesAndLengths()
    {
        DrawingCanvas.Strokes.Clear();
        strokeMeasurements.Clear();

        List<StrokeLengthDisplay> strokeLengthDisplays = [.. ShapeCanvas.Children.OfType<StrokeLengthDisplay>()];
        foreach (StrokeLengthDisplay? display in strokeLengthDisplays)
            ShapeCanvas.Children.Remove(display);
    }

    private void CreateMeasurementFromDrag(Point startPoint, Point endPoint)
    {
        if (MeasureDistanceToggle.IsChecked == true)
        {
            CreateDistanceMeasurement(startPoint, endPoint);
        }
        else if (MeasureAngleToggle.IsChecked == true)
        {
            // For angle measurement, we need three points
            // We'll create a right angle with the drag defining two points
            Point midPoint = new(
                startPoint.X,
                endPoint.Y
            );
            CreateAngleMeasurement(startPoint, midPoint, endPoint);
        }
        else if (RectangleMeasureToggle.IsChecked == true)
        {
            CreateRectangleMeasurement(startPoint, endPoint);
        }
        else if (CircleMeasureToggle.IsChecked == true)
        {
            CreateCircleMeasurement(startPoint, endPoint);
        }
    }

    private void CreateDistanceMeasurement(Point startPoint, Point endPoint)
    {
        double scale = ScaleInput.Value ?? 1.0;
        DistanceMeasurementControl measurementControl = new()
        {
            ScaleFactor = scale,
            Units = MeasurementUnits.Text
        };
        measurementControl.MeasurementPointMouseDown += MeasurementPoint_MouseDown;
        measurementControl.SetRealWorldLengthRequested += MeasurementControl_SetRealWorldLengthRequested;
        measurementControl.RemoveControlRequested += DistanceMeasurementControl_RemoveControlRequested;
        measurementTools.Add(measurementControl);
        ShapeCanvas.Children.Add(measurementControl);

        // Set the start and end positions of the measurement
        measurementControl.MovePoint(0, startPoint);
        measurementControl.MovePoint(1, endPoint);
    }

    private void CreateAngleMeasurement(Point point1, Point vertex, Point point3)
    {
        AngleMeasurementControl measurementControl = new();
        measurementControl.MeasurementPointMouseDown += AngleMeasurementPoint_MouseDown;
        measurementControl.RemoveControlRequested += AngleMeasurementControl_RemoveControlRequested;
        angleMeasurementTools.Add(measurementControl);
        ShapeCanvas.Children.Add(measurementControl);

        // Set the three points of the angle
        measurementControl.MovePoint(0, point1);
        measurementControl.MovePoint(1, vertex);
        measurementControl.MovePoint(2, point3);
    }

    private void CreateRectangleMeasurement(Point topLeft, Point bottomRight)
    {
        double scale = ScaleInput.Value ?? 1.0;
        string units = MeasurementUnits.Text;

        RectangleMeasurementControl measurementControl = new()
        {
            ScaleFactor = scale,
            Units = units
        };
        measurementControl.MeasurementPointMouseDown += RectangleMeasurementPoint_MouseDown;
        measurementControl.RemoveControlRequested += RectangleMeasurementControl_RemoveControlRequested;
        rectangleMeasurementTools.Add(measurementControl);
        ShapeCanvas.Children.Add(measurementControl);

        measurementControl.MovePoint(0, topLeft);
        measurementControl.MovePoint(1, bottomRight);
    }

    private void CreateCircleMeasurement(Point center, Point edge)
    {
        CircleMeasurementControl measurementControl = new();
        measurementControl.MeasurementPointMouseDown += CircleMeasurementPoint_MouseDown;
        measurementControl.RemoveControlRequested += CircleMeasurementControl_RemoveControlRequested;
        circleMeasurementTools.Add(measurementControl);
        ShapeCanvas.Children.Add(measurementControl);

        // Set the center and edge points of the circle
        measurementControl.MovePoint(0, center);
        measurementControl.MovePoint(1, edge);
    }

    private void AddVerticalLineAtPosition(double xPosition)
    {
        VerticalLineControl lineControl = new();
        lineControl.RemoveControlRequested += VerticalLineControl_RemoveControlRequested;
        verticalLineControls.Add(lineControl);
        ShapeCanvas.Children.Add(lineControl);

        // Initialize at the specific X position
        lineControl.Initialize(MainImage.ActualHeight, MainImage.ActualHeight, xPosition);
    }

    private void AddHorizontalLineAtPosition(double yPosition)
    {
        HorizontalLineControl lineControl = new();
        lineControl.RemoveControlRequested += HorizontalLineControl_RemoveControlRequested;
        horizontalLineControls.Add(lineControl);
        ShapeCanvas.Children.Add(lineControl);

        // Initialize at the specific Y position
        lineControl.Initialize(MainImage.ActualWidth, MainImage.ActualWidth, yPosition);
    }

    private void DrawingLinesRadio_Checked(object sender, RoutedEventArgs e)
    {
        isDrawingMode = true;
        DrawingCanvas.IsEnabled = isDrawingMode;

        DrawingOptionsPanel.Visibility = Visibility.Visible;
        DrawingCanvas.IsHitTestVisible = true;

        if (sender is ToggleButton toggleButton)
            UncheckAllBut(toggleButton);
    }

    private void DrawingLinesRadio_Unchecked(object sender, RoutedEventArgs e)
    {
        isDrawingMode = true;
        DrawingCanvas.IsEnabled = isDrawingMode;

        DrawingOptionsPanel.Visibility = Visibility.Collapsed;
        DrawingCanvas.IsHitTestVisible = false;
    }

    private void ToolSelector_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggleButton)
            return;

        UncheckAllBut(toggleButton);
    }

    private bool IsAnyToolSelected()
    {
        List<ToggleButton> toolToggleButtons = [.. MeasureToolsPanel.Children.OfType<ToggleButton>()];

        foreach (ToggleButton button in toolToggleButtons)
            if (button.IsChecked == true)
                return true;

        return false;
    }

    private void UncheckAllBut(ToggleButton? toggleButton = null)
    {
        List<ToggleButton> toolToggleButtons = [.. MeasureToolsPanel.Children.OfType<ToggleButton>()];

        foreach (ToggleButton button in toolToggleButtons)
            if (button != toggleButton)
                button.IsChecked = false;

        if (toggleButton is null)
        {
            draggingMode = DraggingMode.None;
            isCreatingMeasurement = false;
        }
    }

    private void ToolSelector_Clicked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle)
            return;

        if (toggle.IsChecked is true)
            return;

        isDrawingMode = false;
        isCreatingMeasurement = false;
        draggingMode = DraggingMode.None;
    }

    private void FluentWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle Ctrl+V for pasting
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
        {
            // Only paste if welcome screen is visible or no image is loaded
            if (WelcomeMessageModal.Visibility == Visibility.Visible || string.IsNullOrEmpty(ViewModel.ImagePath))
            {
                PasteButton_Click(sender, e);
                e.Handled = true;
                return;
            }
        }

        // Handle Ctrl+C for copying image to clipboard
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.C)
        {
            if (!string.IsNullOrEmpty(ViewModel.ImagePath) && MainImage.Source is BitmapSource)
            {
                ViewModel.CopyToClipboardCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // While a text box has keyboard focus (e.g. editing a markup text),
        // leave Ctrl+Z/Ctrl+Y to the text box's own undo
        bool typingInTextBox = Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase;

        // Arrow keys nudge the transform handle that was last grabbed, for placement that is finer
        // than the mouse can manage. Controls that use the arrow keys themselves keep priority.
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
            && !typingInTextBox
            && Keyboard.FocusedElement is not Slider
            && Keyboard.FocusedElement is not System.Windows.Controls.ComboBox
            && (Keyboard.Modifiers & ModifierKeys.Alt) != ModifierKeys.Alt
            && TryNudgeTransformHandle(e.Key))
        {
            e.Handled = true;
            return;
        }

        // Handle Ctrl+Z for undo
        if (!typingInTextBox && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.Z)
        {
            if (UndoRedo.CanUndo)
            {
                ViewModel.UndoCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Handle Ctrl+Y for redo
        if (!typingInTextBox && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.Y)
        {
            if (UndoRedo.CanRedo)
            {
                ViewModel.RedoCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Handle Delete key for selected markup ink strokes, shapes, and text
        if (e.Key == Key.Delete && isMarkupSelectMode)
        {
            bool deletedAnything = false;

            if (MarkupCanvas.GetSelectedStrokes().Count > 0)
            {
                DeleteSelectedMarkupStrokes();
                deletedAnything = true;
            }

            foreach (MarkupShapeControl shape in selectedMarkupShapes.ToList())
            {
                MarkupShapeControl_RemoveControlRequested(shape, EventArgs.Empty);
                deletedAnything = true;
            }

            foreach (MarkupTextControl text in selectedMarkupTexts.ToList())
            {
                MarkupTextControl_RemoveControlRequested(text, EventArgs.Empty);
                deletedAnything = true;
            }

            if (deletedAnything)
            {
                ClearMarkupGroupSelection();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Escape)
        {
            // Escape while editing a markup text cancels just that edit
            foreach (MarkupTextControl control in markupTextControls.ToList())
            {
                if (control.IsEditing)
                {
                    control.CancelEdit();
                    e.Handled = true;
                    return;
                }
            }

            UncheckAllBut();

            // Cancel white point picker mode
            if (isWhitePointPickerMode)
            {
                isWhitePointPickerMode = false;
                draggingMode = DraggingMode.None;
                Cursor = null;
                WhitePointPickerToggle.IsChecked = false;
            }

            // Cancel black point picker mode
            if (isBlackPointPickerMode)
            {
                isBlackPointPickerMode = false;
                draggingMode = DraggingMode.None;
                Cursor = null;
                BlackPointPickerToggle.IsChecked = false;
            }

            isPlacingAngleMeasurement = false;
            anglePlacementStep = AnglePlacementStep.None;
            ShapeCanvas.Children.Remove(activeAngleMeasureControl);
            activeAnglePlacementControl = null;

            isPlacingPolygonMeasurement = false;
            if (activePolygonPlacementControl != null)
            {
                ShapeCanvas.Children.Remove(activePolygonPlacementControl);
                activePolygonPlacementControl = null;
            }

            isPlacingCircleMeasurement = false;
            if (activeCirclePlacementControl != null)
            {
                ShapeCanvas.Children.Remove(activeCirclePlacementControl);
                activeCirclePlacementControl = null;
            }

            isCreatingMeasurement = false;
            draggingMode = DraggingMode.None;
            ShapeCanvas.ReleaseMouseCapture();

            // Cancel edge correction mode
            if (isEdgeCorrectionMode)
            {
                HideEdgeCorrectionControls();
            }

            // Cancel grid straighten mode
            if (isGridStraightenMode)
            {
                HideGridStraightenControls();
            }
        }

        // Space bar enables pan mode while in edge correction
        if (e.Key == Key.Space && isEdgeCorrectionMode && !isEdgeCorrectionSpacePanning)
        {
            isEdgeCorrectionSpacePanning = true;
            Cursor = Cursors.Hand;
            e.Handled = true;
        }

        // Space bar enables pan mode while in grid straighten
        if (e.Key == Key.Space && isGridStraightenMode && !isGridStraightenSpacePanning)
        {
            isGridStraightenSpacePanning = true;
            Cursor = Cursors.Hand;
            e.Handled = true;
        }
    }

    private void FluentWindow_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && isEdgeCorrectionSpacePanning)
        {
            isEdgeCorrectionSpacePanning = false;

            // If we were panning, release it
            if (draggingMode == DraggingMode.Panning)
            {
                draggingMode = DraggingMode.None;
                ShapeCanvas.ReleaseMouseCapture();
            }

            Cursor = null;
            e.Handled = true;
        }

        if (e.Key == Key.Space && isGridStraightenSpacePanning)
        {
            isGridStraightenSpacePanning = false;

            if (draggingMode == DraggingMode.Panning)
            {
                draggingMode = DraggingMode.None;
                ShapeCanvas.ReleaseMouseCapture();
            }

            Cursor = null;
            e.Handled = true;
        }
    }

    // Precise rotation implementation
    private const double RotationSnapIncrement = 5.0; // degrees when Shift held
    private const double FineRotationMultiplier = 0.25; // slow factor when Ctrl held

    private void ShowRotationOverlay()
    {
        rotationOverlayLabel?.Visibility = Visibility.Visible;
    }
    private void HideRotationOverlay()
    {
        rotationOverlayLabel?.Visibility = Visibility.Collapsed;
    }
    private void UpdateRotationOverlay()
    {
        rotationOverlayLabel?.Text = $"{currentPreviewRotation:0.0}°";
    }

    private void ToggleRotateMode(bool enable)
    {
        if (enable)
        {
            // Hide other panels that conflict
            HideCroppingControls();
            HideTransformControls();
            HideResizeControls();
            HideTriFoldControls();
            HideUnWarpControls();
            RotateControlsPanel.Visibility = Visibility.Visible;
            isRotateMode = true;
            EnsurePreviewRotateTransform();
            ApplyPreviewRotation();
            UpdateRotationOverlay();

            // If user had Free Rotate checked, ensure adorner is present
            if (FreeRotateToggle == null || FreeRotateToggle.IsChecked != true)
            {
                return;
            }

            try
            {
                rotateAdornerLayer ??= AdornerLayer.GetAdornerLayer(ImageGrid);
                if (rotateAdornerLayer != null && rotateAdorner == null)
                {
                    rotateAdorner = new RotateAdorner(ImageGrid)
                    {
                        Angle = currentPreviewRotation
                    };
                    rotateAdorner.AngleChanging += RotateAdorner_AngleChanging;
                    rotateAdorner.AngleChangedFinal += RotateAdorner_AngleChangedFinal;
                    rotateAdornerLayer.Add(rotateAdorner);
                }
            }
            catch { /* ignore if controls not yet available */ }
        }
        else
        {
            RotateControlsPanel.Visibility = Visibility.Collapsed;
            isRotateMode = false;
            RemovePreviewRotation();
            currentPreviewRotation = 0;
            UpdateRotationUiValues(0);
            HideRotationOverlay();
            // Ensure adorner is removed and toggle unchecked
            RemoveRotateAdorner();
            try { FreeRotateToggle?.IsChecked = false; } catch { }
            isFreeRotatingDrag = false;
        }
    }

    private void EnsurePreviewRotateTransform()
    {
        if (previewRotateTransform != null)
        {
            ApplyPreviewRotation();
            return;
        }

        Transform current = MainImage.RenderTransform;
        if (current is TransformGroup tg)
        {
            previewRotateTransform = new RotateTransform(0);
            tg.Children.Add(previewRotateTransform);
        }
        else if (current == null || current == Transform.Identity)
        {
            previewRotateTransform = new RotateTransform(0);
            MainImage.RenderTransform = new TransformGroup { Children = [previewRotateTransform] };
        }
        else
        {
            // Wrap existing transform in group
            TransformGroup group = new();
            group.Children.Add(current);
            previewRotateTransform = new RotateTransform(0);
            group.Children.Add(previewRotateTransform);
            MainImage.RenderTransform = group;
        }

        MainImage.RenderTransformOrigin = new Point(0.5, 0.5);
    }

    private void RemovePreviewRotation()
    {
        if (previewRotateTransform == null)
            return;

        if (MainImage.RenderTransform is TransformGroup tg)
        {
            tg.Children.Remove(previewRotateTransform);
        }
        previewRotateTransform = null;
        MainImage.RenderTransformOrigin = new Point(0.5, 0.5);
    }

    private void ApplyPreviewRotation()
    {
        if (previewRotateTransform == null)
            return;

        previewRotateTransform.Angle = currentPreviewRotation;
    }

    private void UpdateRotationUiValues(double angle)
    {
        if (RotateAngleSlider == null || RotateAngleNumberBox == null)
            return;
        suppressRotateEvents = true;
        RotateAngleSlider.Value = angle;
        RotateAngleNumberBox.Value = angle;
        suppressRotateEvents = false;
        // Keep adorner handle in sync with current angle when not actively dragging via adorner
        if (!isAdornerRotatingDrag && rotateAdorner != null)
            rotateAdorner.SetAngle(angle);
    }

    private void RotateAngleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (suppressRotateEvents || !isRotateMode)
            return;

        currentPreviewRotation = e.NewValue;
        UpdateRotationUiValues(currentPreviewRotation); // keep number box in sync
        ApplyPreviewRotation();
        if (!isAdornerRotatingDrag && rotateAdorner != null)
            rotateAdorner.SetAngle(currentPreviewRotation);
    }

    private void RotateAngleNumberBox_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (suppressRotateEvents || !isRotateMode)
            return;
        if (RotateAngleNumberBox.Value is double val)
        {
            currentPreviewRotation = val;
            UpdateRotationUiValues(currentPreviewRotation); // keep slider in sync
            ApplyPreviewRotation();
            if (!isAdornerRotatingDrag && rotateAdorner != null)
                rotateAdorner.SetAngle(currentPreviewRotation);
        }
    }

    private void FreeRotateToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (!isRotateMode)
        {
            FreeRotateToggle.IsChecked = false;
            return;
        }

        // Add RotateAdorner to MainImage
        rotateAdornerLayer ??= AdornerLayer.GetAdornerLayer(ImageGrid);
        if (rotateAdornerLayer != null && rotateAdorner == null)
        {
            rotateAdorner = new RotateAdorner(ImageGrid)
            {
                Angle = currentPreviewRotation
            };
            rotateAdorner.AngleChanging += RotateAdorner_AngleChanging;
            rotateAdorner.AngleChangedFinal += RotateAdorner_AngleChangedFinal;
            rotateAdornerLayer.Add(rotateAdorner);
        }
    }

    private void FreeRotateToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        RemoveRotateAdorner();
    }

    private void RotateAdorner_AngleChanging(object? sender, double angle)
    {
        if (!isAdornerRotatingDrag)
        {
            isAdornerRotatingDrag = true;
            // Ensure no other element holds mouse capture to avoid contention
            if (Mouse.Captured != rotateAdorner)
            {
                try { Mouse.Captured?.ReleaseMouseCapture(); } catch { }
            }
            try { ShapeCanvas?.IsHitTestVisible = false; } catch { }
        }
        // Throttle to reduce jitter and UI thrash
        long now = Environment.TickCount64;
        if (now - lastRotateUpdateTicks < RotateUpdateMinIntervalMs && Math.Abs(angle - lastAppliedAdornerAngle) < RotateMinDelta)
            return;

        lastRotateUpdateTicks = now;
        lastAppliedAdornerAngle = angle;

        currentPreviewRotation = angle;
        UpdateRotationUiValues(currentPreviewRotation);
        ApplyPreviewRotation();
        ShowRotationOverlay();
        UpdateRotationOverlay();
    }

    private void RotateAdorner_AngleChangedFinal(object? sender, double angle)
    {
        // Finalize rotation preview
        currentPreviewRotation = angle;
        lastAppliedAdornerAngle = angle;
        lastRotateUpdateTicks = Environment.TickCount64;
        UpdateRotationUiValues(currentPreviewRotation);
        ApplyPreviewRotation();
        ShowRotationOverlay();
        UpdateRotationOverlay();
        isAdornerRotatingDrag = false;
        try { ShapeCanvas?.IsHitTestVisible = true; } catch { }
    }

    private void RemoveRotateAdorner()
    {
        if (rotateAdornerLayer != null && rotateAdorner != null)
        {
            rotateAdorner.AngleChanging -= RotateAdorner_AngleChanging;
            rotateAdorner.AngleChangedFinal -= RotateAdorner_AngleChangedFinal;
            rotateAdornerLayer.Remove(rotateAdorner);
            rotateAdorner = null;
        }
        isAdornerRotatingDrag = false;
        try { ShapeCanvas?.IsHitTestVisible = true; } catch { }
    }

    private void ResetRotationButton_Click(object sender, RoutedEventArgs e)
    {
        isFreeRotatingDrag = false;
        HideRotationOverlay();
        currentPreviewRotation = 0;
        UpdateRotationUiValues(0);
        ApplyPreviewRotation();
        UpdateRotationOverlay();
    }

    private async void ApplyRotationButton_Click(object sender, RoutedEventArgs e)
    {
        isFreeRotatingDrag = false; // reset drag state
        HideRotationOverlay();
        if (!isRotateMode || string.IsNullOrWhiteSpace(ViewModel.ImagePath))
            return;

        double angle = currentPreviewRotation;
        if (Math.Abs(angle) < 0.0001)
        {
            ToggleRotateMode(false);
            return; // no-op
        }

        SetUiForLongTask();
        try
        {
            string previousPath = ViewModel.ImagePath!;
            string tempFileName = System.IO.Path.GetTempFileName();

            await Task.Run(() =>
            {
                using MagickImage mi = new(previousPath);
                mi.BackgroundColor = MagickColors.Transparent;
                mi.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                mi.Rotate(angle);
                mi.Write(tempFileName);
            });

            MagickImageUndoRedoItem undoItem = new(MainImage, previousPath, tempFileName);
            UndoRedo.AddUndo(undoItem);
            ViewModel.ImagePath = tempFileName;

            using MagickImage newImage = new(ViewModel.ImagePath);
            MainImage.Source = newImage.ToBitmapSource();

            // Update ViewModel.ActualImageSize to reflect current dimensions
            ViewModel.ActualImageSize = new Size(newImage.Width, newImage.Height);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Rotation Error", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ToggleRotateMode(false);
            SetUiForCompletedTask();
        }
    }

    private void HandleFreeRotateDrag(MouseEventArgs e)
    {
        if (!isRotateMode || FreeRotateToggle == null || FreeRotateToggle.IsChecked != true)
            return;
        Point p = e.GetPosition(MainImage);
        Vector delta = p - freeRotateLastPoint;
        if (delta.LengthSquared < 0.25)
            return;
        freeRotateLastPoint = p;

        double sensitivity = FreeRotateSensitivity;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            sensitivity *= FineRotationMultiplier; // fine adjustment

        currentPreviewRotation += delta.X * sensitivity;
        if (currentPreviewRotation > 180) currentPreviewRotation -= 360;
        if (currentPreviewRotation < -180) currentPreviewRotation += 360;

        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            currentPreviewRotation = Math.Round(currentPreviewRotation / RotationSnapIncrement) * RotationSnapIncrement;

        UpdateRotationUiValues(currentPreviewRotation);
        ApplyPreviewRotation();
        ShowRotationOverlay();
        UpdateRotationOverlay();
    }

    private void CancelRotationButton_Click(object sender, RoutedEventArgs e)
    {
        isFreeRotatingDrag = false;
        HideRotationOverlay();
        ToggleRotateMode(false);
    }

    private void PreciseRotateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath))
            return;
        ToggleRotateMode(true);
    }

    #region Pixel Precision Zoom

    /// <summary>
    /// Shows the pixel precision zoom control.
    /// </summary>
    /// <param name="targetPoint">
    /// The point to magnify, in ShapeCanvas coordinates. When a handle is being dragged by its
    /// edge this is the handle centre, not the cursor, so the crosshair marks where the point
    /// will actually land.
    /// </param>
    /// <param name="cursorPoint">
    /// Where to park the loupe, in ShapeCanvas coordinates. Defaults to <paramref name="targetPoint"/>.
    /// </param>
    private void ShowPixelZoom(Point targetPoint, Point? cursorPoint = null)
    {
        if (MainImage.Source == null)
            return;

        try
        {
            // Set the source image for the zoom control
            PixelZoomControl.SourceImage = MainImage.Source;
            UpdateLoupeMagnification();

            // Convert the magnified point to image coordinates
            PixelZoomControl.CurrentPosition = ConvertCanvasToImageCoordinates(targetPoint);

            PositionPixelZoom(cursorPoint ?? targetPoint);

            // Show the control
            PixelZoomControl.Visibility = Visibility.Visible;
        }
        catch (Exception)
        {
            // Silently handle any errors
            HidePixelZoom();
        }
    }

    /// <summary>
    /// Updates the pixel precision zoom control position and preview.
    /// </summary>
    /// <param name="targetPoint">The point to magnify, in ShapeCanvas coordinates</param>
    /// <param name="cursorPoint">Where to park the loupe, in ShapeCanvas coordinates</param>
    private void UpdatePixelZoom(Point targetPoint, Point? cursorPoint = null)
    {
        if (PixelZoomControl.Visibility != Visibility.Visible)
            return;

        try
        {
            UpdateLoupeMagnification();
            PixelZoomControl.CurrentPosition = ConvertCanvasToImageCoordinates(targetPoint);
            PositionPixelZoom(cursorPoint ?? targetPoint);
        }
        catch (Exception)
        {
            // Silently handle any errors
        }
    }

    /// <summary>
    /// Moves the loupe next to the cursor. ShapeCanvas is pan/zoom transformed, so the point has
    /// to be projected into MainGrid coordinates first.
    /// </summary>
    private void PositionPixelZoom(Point cursorPoint)
    {
        Point mainGridPosition = ShapeCanvas.TransformToAncestor(MainGrid).Transform(cursorPoint);
        PixelZoomControl.PositionNearCursor(mainGridPosition, MainGrid.ActualWidth, MainGrid.ActualHeight);
    }

    /// <summary>
    /// Keeps the loupe more magnified than the canvas itself. The loupe magnifies source pixels,
    /// so on a small image at a high canvas zoom a fixed factor would actually show *less* detail
    /// than the canvas underneath it.
    /// </summary>
    private void UpdateLoupeMagnification()
    {
        if (MainImage.Source is not BitmapSource source
            || source.PixelWidth <= 0
            || MainImage.ActualWidth <= 0)
            return;

        double canvasPixelsPerSourcePixel = MainImage.ActualWidth / source.PixelWidth * canvasScale.ScaleX;
        PixelZoomControl.ZoomFactor = Math.Clamp(canvasPixelsPerSourcePixel * 2.5, 6.0, 24.0);
    }

    /// <summary>
    /// Hides the pixel precision zoom control.
    /// </summary>
    private void HidePixelZoom()
    {
        PixelZoomControl.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Converts a point from ShapeCanvas coordinates to MainImage pixel coordinates.
    /// </summary>
    /// <param name="canvasPoint">Point in ShapeCanvas coordinates</param>
    /// <returns>Point in image pixel coordinates</returns>
    private Point ConvertCanvasToImageCoordinates(Point canvasPoint)
    {
        if (MainImage.Source is not BitmapSource source
            || MainImage.ActualWidth <= 0
            || MainImage.ActualHeight <= 0)
            return new Point(0, 0);

        // ImageGrid is anchored at the ShapeCanvas origin, so these logical canvas
        // coordinates stay independent of the viewport's pan and zoom transform.
        double pixelX = canvasPoint.X * source.PixelWidth / MainImage.ActualWidth;
        double pixelY = canvasPoint.Y * source.PixelHeight / MainImage.ActualHeight;
        return new Point(
            Math.Clamp(pixelX, 0, source.PixelWidth - 1),
            Math.Clamp(pixelY, 0, source.PixelHeight - 1));
    }

    /// <summary>
    /// Checks if pixel zoom should be shown for the current operation.
    /// Shows when a measurement tool is active, including hover before first placement.
    /// </summary>
    /// <returns>True if pixel zoom should be active</returns>
    private bool ShouldShowPixelZoom()
    {
        // Show pixel zoom when dragging corner markers for transform
        if (draggingMode == DraggingMode.MoveElement && clickedElement != null)
            return true;

        // Show pixel zoom when placing/dragging measurement points
        if (draggingMode is DraggingMode.MeasureDistance or
            DraggingMode.MeasureAngle or
            DraggingMode.MeasureRectangle or
            DraggingMode.MeasurePolygon or
            DraggingMode.MeasureCircle)
            return true;

        // Show during measurement creation (active drag)
        if (isCreatingMeasurement && draggingMode == DraggingMode.CreatingMeasurement)
            return true;

        // Show during angle placement (active placement)
        if (isPlacingAngleMeasurement && anglePlacementStep != AnglePlacementStep.None)
            return true;

        // Show during polygon placement (active placement)
        if (isPlacingPolygonMeasurement && activePolygonPlacementControl != null)
            return true;

        // Show during rectangle placement (active drag)
        if (isPlacingRectangleMeasurement && draggingMode == DraggingMode.CreatingMeasurement)
            return true;

        // Show during circle placement (active drag)
        if (isPlacingCircleMeasurement && draggingMode == DraggingMode.CreatingMeasurement)
            return true;

        // Show when any measurement tool is active, even before first placement
        if (MeasureDistanceToggle?.IsChecked == true ||
            MeasureAngleToggle?.IsChecked == true ||
            RectangleMeasureToggle?.IsChecked == true ||
            CircleMeasureToggle?.IsChecked == true ||
            PolygonMeasureToggle?.IsChecked == true ||
            HorizontalLineRadio?.IsChecked == true ||
            VerticalLineToggle?.IsChecked == true)
            return true;

        return false;
    }

    #endregion Pixel Precision Zoom

    #region Markup Tab

    private void ToolsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not TabControl)
            return;

        bool measurementTabActive = MeasureTabItem?.IsSelected == true;
        bool markupTabActive = MarkupTabItem?.IsSelected == true;
        if (!markupTabActive)
            DeactivateAllMarkupTools();

        SetMeasurementDragGizmosVisibility(measurementTabActive);
        SetMarkupDragGizmosVisibility(markupTabActive);
    }

    private void DeactivateAllMarkupTools()
    {
        isMarkupPenMode = false;
        isMarkupHighlighterMode = false;
        isMarkupSelectMode = false;
        isMarkupShapeMode = false;
        isMarkupTextMode = false;
        if (MarkupCanvas is not null)
        {
            MarkupCanvas.IsEnabled = false;
            MarkupCanvas.IsHitTestVisible = false;
        }
        UncheckMarkupAllBut();
    }

    private void SetMarkupDragGizmosVisibility(bool visible)
    {
        if (!visible)
            MarkupCanvas.Select(new StrokeCollection());

        foreach (MarkupShapeControl control in markupShapeControls)
        {
            control.IsDragGizmoVisible = visible;
            control.IsHitTestVisible = visible;
        }

        foreach (MarkupTextControl control in markupTextControls)
            control.IsHitTestVisible = visible;
    }

    private void SetMeasurementDragGizmosVisibility(bool visible)
    {
        if (!visible)
            DrawingCanvas.Select(new StrokeCollection());

        foreach (DistanceMeasurementControl control in measurementTools)
        {
            SetMeasurementGizmoState(control, visible);
            control.IsHitTestVisible = visible;
        }

        foreach (AngleMeasurementControl control in angleMeasurementTools)
        {
            SetMeasurementGizmoState(control, visible);
            control.IsHitTestVisible = visible;
        }

        foreach (RectangleMeasurementControl control in rectangleMeasurementTools)
        {
            SetMeasurementGizmoState(control, visible);
            control.IsHitTestVisible = visible;
        }

        foreach (PolygonMeasurementControl control in polygonMeasurementTools)
        {
            SetMeasurementGizmoState(control, visible);
            control.IsHitTestVisible = visible;
        }

        foreach (CircleMeasurementControl control in circleMeasurementTools)
        {
            SetMeasurementGizmoState(control, visible);
            control.IsHitTestVisible = visible;
        }

        foreach (VerticalLineControl control in verticalLineControls)
            control.IsHitTestVisible = visible;

        foreach (HorizontalLineControl control in horizontalLineControls)
            control.IsHitTestVisible = visible;

        foreach (StrokeLengthDisplay control in ShapeCanvas.Children.OfType<StrokeLengthDisplay>())
            control.IsHitTestVisible = visible;
    }

    private static void SetMeasurementGizmoState<T>(T control, bool visible)
        where T : class
    {
        switch (control)
        {
            case DistanceMeasurementControl distance:
                distance.IsEndpointCapVisible = !visible;
                distance.IsDragGizmoVisible = true;
                break;
            case AngleMeasurementControl angle:
                angle.IsEndpointCapVisible = !visible;
                angle.IsDragGizmoVisible = true;
                break;
            case RectangleMeasurementControl rectangle:
                rectangle.IsEndpointCapVisible = !visible;
                rectangle.IsDragGizmoVisible = true;
                break;
            case PolygonMeasurementControl polygon:
                polygon.IsEndpointCapVisible = !visible;
                polygon.IsDragGizmoVisible = true;
                break;
            case CircleMeasurementControl circle:
                circle.IsEndpointCapVisible = !visible;
                circle.IsDragGizmoVisible = true;
                break;
        }
    }

    private void UncheckMarkupAllBut(ToggleButton? keep = null)
    {
        // Switching markup tools drops any active multi-item group selection
        ClearMarkupGroupSelection();

        if (MarkupToolsPanel is null || MarkupShapeToolsPanel is null) return;
        foreach (ToggleButton btn in MarkupToolsPanel.Children.OfType<ToggleButton>())
            if (btn != keep) btn.IsChecked = false;
        foreach (ToggleButton btn in MarkupShapeToolsPanel.Children.OfType<ToggleButton>())
            if (btn != keep) btn.IsChecked = false;
    }

    private void UpdateMarkupCanvasForPen()
    {
        MarkupCanvas.IsEnabled = true;
        MarkupCanvas.IsHitTestVisible = true;
        MarkupCanvas.EditingMode = InkCanvasEditingMode.Ink;
        DrawingAttributes attrs = new()
        {
            Color = markupColor,
            Width = markupSize,
            Height = markupSize,
            IsHighlighter = false,
            StylusTip = StylusTip.Ellipse
        };
        MarkupCanvas.DefaultDrawingAttributes = attrs;
    }

    private void UpdateMarkupCanvasForHighlighter()
    {
        MarkupCanvas.IsEnabled = true;
        MarkupCanvas.IsHitTestVisible = true;
        MarkupCanvas.EditingMode = InkCanvasEditingMode.Ink;
        System.Windows.Media.Color highlightColor = markupColor;
        highlightColor.A = 100;
        DrawingAttributes attrs = new()
        {
            Color = highlightColor,
            Width = markupSize * 6,
            Height = markupSize * 6,
            IsHighlighter = true,
            StylusTip = StylusTip.Rectangle
        };
        MarkupCanvas.DefaultDrawingAttributes = attrs;
    }

    private void DisableMarkupCanvas()
    {
        MarkupCanvas.IsEnabled = false;
        MarkupCanvas.IsHitTestVisible = false;
    }

    private void MarkupPenToggle_Checked(object sender, RoutedEventArgs e)
    {
        isMarkupPenMode = true;
        isMarkupHighlighterMode = false;
        isMarkupSelectMode = false;
        isMarkupShapeMode = false;
        isMarkupTextMode = false;
        UpdateMarkupCanvasForPen();
        UncheckMarkupAllBut(sender as ToggleButton);
    }

    private void MarkupHighlighterToggle_Checked(object sender, RoutedEventArgs e)
    {
        isMarkupHighlighterMode = true;
        isMarkupPenMode = false;
        isMarkupSelectMode = false;
        isMarkupShapeMode = false;
        isMarkupTextMode = false;
        UpdateMarkupCanvasForHighlighter();
        UncheckMarkupAllBut(sender as ToggleButton);
    }

    private void MarkupEraserToggle_Checked(object sender, RoutedEventArgs e)
    {
        isMarkupPenMode = false;
        isMarkupHighlighterMode = false;
        isMarkupSelectMode = false;
        isMarkupShapeMode = false;
        isMarkupTextMode = false;
        MarkupCanvas.IsEnabled = true;
        MarkupCanvas.IsHitTestVisible = true;
        MarkupCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
        UncheckMarkupAllBut(sender as ToggleButton);
    }

    private void MarkupSelectToggle_Checked(object sender, RoutedEventArgs e)
    {
        isMarkupSelectMode = true;
        isMarkupPenMode = false;
        isMarkupHighlighterMode = false;
        isMarkupShapeMode = false;
        isMarkupTextMode = false;
        MarkupCanvas.IsEnabled = true;
        MarkupCanvas.IsHitTestVisible = true;
        MarkupCanvas.EditingMode = InkCanvasEditingMode.Select;
        UncheckMarkupAllBut(sender as ToggleButton);
    }

    #region Markup Group Selection

    /// <summary>
    /// Starts a rubber-band marquee (in ShapeCanvas coordinates) that, on release, selects
    /// every ink stroke, shape, and text control it fully encloses as one group.
    /// </summary>
    private void BeginMarkupMarquee(Point startPoint)
    {
        markupMarqueeStartPoint = startPoint;
        draggingMode = DraggingMode.MarkupGroupSelect;
        MarkupMarqueeRectangle.Visibility = Visibility.Visible;
        UpdateMarkupMarqueeVisual(startPoint);
        CaptureMouse();
    }

    private void UpdateMarkupMarqueeVisual(Point currentPoint)
    {
        if (markupMarqueeStartPoint is not Point start)
            return;

        double x = Math.Min(start.X, currentPoint.X);
        double y = Math.Min(start.Y, currentPoint.Y);
        double width = Math.Abs(currentPoint.X - start.X);
        double height = Math.Abs(currentPoint.Y - start.Y);

        Canvas.SetLeft(MarkupMarqueeRectangle, x);
        Canvas.SetTop(MarkupMarqueeRectangle, y);
        MarkupMarqueeRectangle.Width = width;
        MarkupMarqueeRectangle.Height = height;
    }

    private void FinishMarkupMarquee(Point endPoint)
    {
        MarkupMarqueeRectangle.Visibility = Visibility.Collapsed;

        if (markupMarqueeStartPoint is not Point start)
            return;

        markupMarqueeStartPoint = null;

        Rect marqueeRect = new(start, endPoint);

        // Ignore accidental micro-drags (treat them as a deselect click on empty canvas)
        if (marqueeRect.Width < 2 && marqueeRect.Height < 2)
        {
            ClearMarkupGroupSelection();
            return;
        }

        StrokeCollection enclosedStrokes = [];
        foreach (Stroke stroke in MarkupCanvas.Strokes)
            if (marqueeRect.Contains(stroke.GetBounds()))
                enclosedStrokes.Add(stroke);

        List<MarkupShapeControl> enclosedShapes = [.. markupShapeControls.Where(s => marqueeRect.Contains(GetMarkupShapeBounds(s)))];
        List<MarkupTextControl> enclosedTexts = [.. markupTextControls.Where(t => marqueeRect.Contains(GetMarkupTextBounds(t)))];

        ApplyMarkupGroupSelection(enclosedStrokes, enclosedShapes, enclosedTexts);
    }

    private static Rect GetMarkupShapeBounds(MarkupShapeControl shape)
    {
        (Point p1, Point p2) = shape.GetPoints();
        Rect rect = new(p1, p2);
        rect.Inflate(6, 6);
        return rect;
    }

    private static Rect GetMarkupTextBounds(MarkupTextControl text)
    {
        double left = Canvas.GetLeft(text);
        double top = Canvas.GetTop(text);
        double width = text.ActualWidth > 0 ? text.ActualWidth : 40;
        double height = text.ActualHeight > 0 ? text.ActualHeight : 20;
        return new Rect(left, top, width, height);
    }

    private void ApplyMarkupGroupSelection(StrokeCollection strokes, List<MarkupShapeControl> shapes, List<MarkupTextControl> texts)
    {
        ClearMarkupGroupSelection();

        MarkupCanvas.Select(strokes);

        foreach (MarkupShapeControl shape in shapes)
        {
            selectedMarkupShapes.Add(shape);
            shape.IsDragGizmoVisible = true;
        }

        foreach (MarkupTextControl text in texts)
        {
            selectedMarkupTexts.Add(text);
            AddMarkupSelectionHighlight(text);
        }
    }

    private void AddMarkupSelectionHighlight(MarkupTextControl text)
    {
        Rect bounds = GetMarkupTextBounds(text);
        System.Windows.Shapes.Rectangle highlight = new()
        {
            Width = bounds.Width + 8,
            Height = bounds.Height + 8,
            Stroke = System.Windows.Media.Brushes.DeepSkyBlue,
            StrokeThickness = 1.5,
            StrokeDashArray = [3, 2],
            Fill = System.Windows.Media.Brushes.Transparent,
            IsHitTestVisible = false,
            Tag = text
        };
        Canvas.SetLeft(highlight, bounds.X - 4);
        Canvas.SetTop(highlight, bounds.Y - 4);
        Panel.SetZIndex(highlight, 2000);
        ShapeCanvas.Children.Add(highlight);
        markupSelectionHighlights.Add(highlight);
    }

    private void RefreshMarkupSelectionHighlights()
    {
        foreach (System.Windows.Shapes.Rectangle highlight in markupSelectionHighlights)
        {
            if (highlight.Tag is not MarkupTextControl text) continue;
            Rect bounds = GetMarkupTextBounds(text);
            Canvas.SetLeft(highlight, bounds.X - 4);
            Canvas.SetTop(highlight, bounds.Y - 4);
        }
    }

    /// <summary>
    /// Clears the current multi-item markup selection (ink strokes, shapes, and text) along
    /// with its visual affordances. Safe to call even when nothing is selected.
    /// </summary>
    private void ClearMarkupGroupSelection()
    {
        foreach (MarkupShapeControl shape in selectedMarkupShapes)
            shape.IsDragGizmoVisible = MarkupTabItem?.IsSelected == true;
        selectedMarkupShapes.Clear();

        selectedMarkupTexts.Clear();

        foreach (System.Windows.Shapes.Rectangle highlight in markupSelectionHighlights)
            ShapeCanvas.Children.Remove(highlight);
        markupSelectionHighlights.Clear();

        if (MarkupCanvas.GetSelectedStrokes().Count > 0)
            MarkupCanvas.Select(new StrokeCollection());
    }

    private void BeginMarkupGroupMove(Point startPoint)
    {
        markupGroupDragLastPoint = startPoint;
        markupGroupDragTotalDeltaX = 0;
        markupGroupDragTotalDeltaY = 0;
        markupGroupMoveStrokes = new StrokeCollection(MarkupCanvas.GetSelectedStrokes());
        markupGroupMoveShapes = [.. selectedMarkupShapes];
        markupGroupMoveTexts = [.. selectedMarkupTexts];
        draggingMode = DraggingMode.MarkupGroupMove;
        CaptureMouse();
    }

    private void ApplyMarkupGroupDelta(double deltaX, double deltaY)
    {
        if (markupGroupMoveStrokes is { Count: > 0 })
        {
            Matrix m = new();
            m.Translate(deltaX, deltaY);
            foreach (Stroke stroke in markupGroupMoveStrokes)
                stroke.Transform(m, false);
        }

        if (markupGroupMoveShapes is not null)
        {
            foreach (MarkupShapeControl shape in markupGroupMoveShapes)
            {
                (Point p1, Point p2) = shape.GetPoints();
                shape.MovePoint(0, new Point(p1.X + deltaX, p1.Y + deltaY));
                shape.MovePoint(1, new Point(p2.X + deltaX, p2.Y + deltaY));
            }
        }

        if (markupGroupMoveTexts is not null)
        {
            foreach (MarkupTextControl text in markupGroupMoveTexts)
            {
                Canvas.SetLeft(text, Canvas.GetLeft(text) + deltaX);
                Canvas.SetTop(text, Canvas.GetTop(text) + deltaY);
            }
        }

        RefreshMarkupSelectionHighlights();
    }

    private void FinishMarkupGroupMove()
    {
        if (Math.Abs(markupGroupDragTotalDeltaX) > 0.01 || Math.Abs(markupGroupDragTotalDeltaY) > 0.01)
        {
            UndoRedo.AddUndo(new MarkupGroupMovedItem(
                markupGroupMoveStrokes ?? [],
                markupGroupMoveShapes ?? [],
                markupGroupMoveTexts ?? [],
                markupGroupDragTotalDeltaX,
                markupGroupDragTotalDeltaY));
        }

        markupGroupMoveStrokes = null;
        markupGroupMoveShapes = null;
        markupGroupMoveTexts = null;
    }

    #endregion Markup Group Selection

    private void MarkupLineToggle_Checked(object sender, RoutedEventArgs e)
    {
        activeMarkupShapeType = MagickCrop.Models.MarkupShapeType.Line;
        isMarkupShapeMode = true;
        isMarkupPenMode = false;
        isMarkupHighlighterMode = false;
        isMarkupSelectMode = false;
        isMarkupTextMode = false;
        DisableMarkupCanvas();
        UncheckMarkupAllBut(sender as ToggleButton);
    }

    private void MarkupArrowToggle_Checked(object sender, RoutedEventArgs e)
    {
        activeMarkupShapeType = MagickCrop.Models.MarkupShapeType.Arrow;
        isMarkupShapeMode = true;
        isMarkupPenMode = false;
        isMarkupHighlighterMode = false;
        isMarkupSelectMode = false;
        isMarkupTextMode = false;
        DisableMarkupCanvas();
        UncheckMarkupAllBut(sender as ToggleButton);
    }

    private void MarkupRectangleToggle_Checked(object sender, RoutedEventArgs e)
    {
        activeMarkupShapeType = MagickCrop.Models.MarkupShapeType.Rectangle;
        isMarkupShapeMode = true;
        isMarkupPenMode = false;
        isMarkupHighlighterMode = false;
        isMarkupSelectMode = false;
        isMarkupTextMode = false;
        DisableMarkupCanvas();
        UncheckMarkupAllBut(sender as ToggleButton);
    }

    private void MarkupEllipseToggle_Checked(object sender, RoutedEventArgs e)
    {
        activeMarkupShapeType = MagickCrop.Models.MarkupShapeType.Ellipse;
        isMarkupShapeMode = true;
        isMarkupPenMode = false;
        isMarkupHighlighterMode = false;
        isMarkupSelectMode = false;
        isMarkupTextMode = false;
        DisableMarkupCanvas();
        UncheckMarkupAllBut(sender as ToggleButton);
    }

    private void MarkupTextToggle_Checked(object sender, RoutedEventArgs e)
    {
        isMarkupTextMode = true;
        isMarkupPenMode = false;
        isMarkupHighlighterMode = false;
        isMarkupSelectMode = false;
        isMarkupShapeMode = false;
        DisableMarkupCanvas();
        UncheckMarkupAllBut(sender as ToggleButton);
    }

    private void MarkupToolToggle_Clicked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle || toggle.IsChecked is true)
            return;

        isMarkupPenMode = false;
        isMarkupHighlighterMode = false;
        isMarkupSelectMode = false;
        isMarkupShapeMode = false;
        isMarkupTextMode = false;
        DisableMarkupCanvas();
        draggingMode = DraggingMode.None;
    }

    private void MarkupToolToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        // Handled by MarkupToolToggle_Clicked
    }

    private void MarkupColorButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not string colorName)
            return;

        // Uncheck other color buttons
        foreach (ToggleButton other in MarkupColorPalette.Children.OfType<ToggleButton>())
            if (other != btn) other.IsChecked = false;

        try
        {
            markupColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorName);
        }
        catch
        {
            markupColor = System.Windows.Media.Colors.Red;
        }

        // Apply color to active ink tool immediately
        if (isMarkupPenMode) UpdateMarkupCanvasForPen();
        else if (isMarkupHighlighterMode) UpdateMarkupCanvasForHighlighter();
        else if (isMarkupSelectMode) ApplyColorToSelectedStrokes();
    }

    private void MarkupSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        markupSize = e.NewValue;

        if (isMarkupPenMode) UpdateMarkupCanvasForPen();
        else if (isMarkupHighlighterMode) UpdateMarkupCanvasForHighlighter();
        else if (isMarkupSelectMode) ApplySizeToSelectedStrokes();
    }

    private void ApplyColorToSelectedStrokes()
    {
        StrokeCollection selected = MarkupCanvas.GetSelectedStrokes();
        if (selected.Count == 0) return;

        List<(Stroke, DrawingAttributes, DrawingAttributes)> changes = [];
        foreach (Stroke stroke in selected)
        {
            DrawingAttributes before = stroke.DrawingAttributes.Clone();
            System.Windows.Media.Color color = markupColor;
            if (stroke.DrawingAttributes.IsHighlighter)
                color.A = 100;
            DrawingAttributes after = stroke.DrawingAttributes.Clone();
            after.Color = color;
            stroke.DrawingAttributes = after;
            changes.Add((stroke, before, after));
        }
        UndoRedo.AddUndo(new MarkupStrokePropertiesChangedItem(changes));
    }

    private void ApplySizeToSelectedStrokes()
    {
        StrokeCollection selected = MarkupCanvas.GetSelectedStrokes();
        if (selected.Count == 0) return;

        List<(Stroke, DrawingAttributes, DrawingAttributes)> changes = [];
        foreach (Stroke stroke in selected)
        {
            DrawingAttributes before = stroke.DrawingAttributes.Clone();
            double size = stroke.DrawingAttributes.IsHighlighter ? markupSize * 6 : markupSize;
            DrawingAttributes after = stroke.DrawingAttributes.Clone();
            after.Width = size;
            after.Height = size;
            stroke.DrawingAttributes = after;
            changes.Add((stroke, before, after));
        }
        UndoRedo.AddUndo(new MarkupStrokePropertiesChangedItem(changes));
    }

    private void MarkupCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        UndoRedo.AddUndo(new MarkupStrokeAddedItem(MarkupCanvas, e.Stroke));
    }

    private void MarkupCanvas_StrokeErasing(object sender, InkCanvasStrokeErasingEventArgs e)
    {
        UndoRedo.AddUndo(new MarkupStrokeDeletedItem(MarkupCanvas, [e.Stroke]));
    }

    private void MarkupCanvas_SelectionMoving(object sender, InkCanvasSelectionEditingEventArgs e)
    {
        _selectionBoundsBeforeMove = e.OldRectangle;
        _strokesBeforeMove = new StrokeCollection(MarkupCanvas.GetSelectedStrokes());
    }

    private void MarkupCanvas_SelectionMoved(object sender, EventArgs e)
    {
        if (_strokesBeforeMove is null || _selectionBoundsBeforeMove is null) return;

        Rect newBounds = MarkupCanvas.GetSelectionBounds();
        double deltaX = newBounds.X - _selectionBoundsBeforeMove.Value.X;
        double deltaY = newBounds.Y - _selectionBoundsBeforeMove.Value.Y;

        if (Math.Abs(deltaX) > 0.01 || Math.Abs(deltaY) > 0.01)
        {
            // If shapes/text were also part of the group selection (from a marquee), move
            // them by the same delta so dragging the native ink adorner moves the whole group
            if (selectedMarkupShapes.Count > 0 || selectedMarkupTexts.Count > 0)
            {
                List<MarkupShapeControl> coShapes = [.. selectedMarkupShapes];
                List<MarkupTextControl> coTexts = [.. selectedMarkupTexts];
                foreach (MarkupShapeControl shape in coShapes)
                {
                    (Point p1, Point p2) = shape.GetPoints();
                    shape.MovePoint(0, new Point(p1.X + deltaX, p1.Y + deltaY));
                    shape.MovePoint(1, new Point(p2.X + deltaX, p2.Y + deltaY));
                }
                foreach (MarkupTextControl text in coTexts)
                {
                    Canvas.SetLeft(text, Canvas.GetLeft(text) + deltaX);
                    Canvas.SetTop(text, Canvas.GetTop(text) + deltaY);
                }
                RefreshMarkupSelectionHighlights();

                UndoRedo.AddUndo(new MarkupGroupMovedItem(_strokesBeforeMove, coShapes, coTexts, deltaX, deltaY));
            }
            else
            {
                UndoRedo.AddUndo(new MarkupStrokeMovedItem(_strokesBeforeMove, deltaX, deltaY));
            }
        }

        _strokesBeforeMove = null;
        _selectionBoundsBeforeMove = null;
    }

    private void MarkupCanvas_SelectionResizing(object sender, InkCanvasSelectionEditingEventArgs e)
    {
        _selectionBoundsBeforeResize = e.OldRectangle;
        _strokesBeforeResize = new StrokeCollection(MarkupCanvas.GetSelectedStrokes());
    }

    private void MarkupCanvas_SelectionResized(object sender, EventArgs e)
    {
        if (_strokesBeforeResize is null || _selectionBoundsBeforeResize is null) return;

        Rect oldBounds = _selectionBoundsBeforeResize.Value;
        Rect newBounds = MarkupCanvas.GetSelectionBounds();

        if (oldBounds.Width > 0 && oldBounds.Height > 0
            && (Math.Abs(newBounds.X - oldBounds.X) > 0.01
                || Math.Abs(newBounds.Y - oldBounds.Y) > 0.01
                || Math.Abs(newBounds.Width - oldBounds.Width) > 0.01
                || Math.Abs(newBounds.Height - oldBounds.Height) > 0.01))
        {
            UndoRedo.AddUndo(new MarkupStrokeResizedItem(_strokesBeforeResize, oldBounds, newBounds));
        }

        _strokesBeforeResize = null;
        _selectionBoundsBeforeResize = null;
    }

    private void DeleteSelectedMarkupStrokes()
    {
        StrokeCollection selected = MarkupCanvas.GetSelectedStrokes();
        if (selected.Count == 0) return;
        UndoRedo.AddUndo(new MarkupStrokeDeletedItem(MarkupCanvas, selected));
        foreach (Stroke stroke in selected.ToList())
            MarkupCanvas.Strokes.Remove(stroke);
    }

    private async void FillHollowStrokesButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath)) return;

        FillHollowStrokesButton.IsEnabled = false;
        FillHollowStrokesProgressRing.Visibility = Visibility.Visible;

        try
        {
            string? resultPath = await WhiteboardInkConverter.FillHollowStrokesAsync(ViewModel.ImagePath);

            if (resultPath is null)
            {
                System.Windows.MessageBox.Show(
                    "No hollow stroke interiors were found in the image.",
                    "Fill Hollow Strokes",
                    System.Windows.MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MagickImageUndoRedoItem undoRedoItem = new(MainImage, ViewModel.ImagePath, resultPath);
            UndoRedo.AddUndo(undoRedoItem);

            ViewModel.ImagePath = resultPath;
            using MagickImage resultImage = new(resultPath);
            MainImage.Source = resultImage.ToBitmapSource();
            ViewModel.ActualImageSize = new Size(resultImage.Width, resultImage.Height);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Fill Hollow Strokes failed: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            FillHollowStrokesButton.IsEnabled = true;
            FillHollowStrokesProgressRing.Visibility = Visibility.Collapsed;
        }
    }

    private async void ConvertToInkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ViewModel.ImagePath)) return;

        ConvertToInkButton.IsEnabled = false;
        ConvertToInkProgressRing.Visibility = Visibility.Visible;

        try
        {
            List<Stroke> strokes = await WhiteboardInkConverter.ConvertToStrokesAsync(
                ViewModel.ImagePath,
                MarkupCanvas.ActualWidth,
                MarkupCanvas.ActualHeight);

            if (strokes.Count == 0) return;

            foreach (Stroke stroke in strokes)
                MarkupCanvas.Strokes.Add(stroke);

            UndoRedo.AddUndo(new MarkupStrokeBatchAddedItem(MarkupCanvas, strokes));
        }
        finally
        {
            ConvertToInkButton.IsEnabled = true;
            ConvertToInkProgressRing.Visibility = Visibility.Collapsed;
        }
    }

    private async void CleanWhiteboardButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.ImagePath)) return;

        CleanWhiteboardButton.IsEnabled = false;
        CleanWhiteboardProgressRing.Visibility = Visibility.Visible;

        try
        {
            string? resultPath = await WhiteboardInkConverter.RemoveSpecklesAsync(ViewModel.ImagePath);

            if (resultPath is null)
            {
                System.Windows.MessageBox.Show(
                    "No small speckles were found in the image.",
                    "Clean Whiteboard",
                    System.Windows.MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MagickImageUndoRedoItem undoRedoItem = new(MainImage, ViewModel.ImagePath, resultPath);
            UndoRedo.AddUndo(undoRedoItem);

            ViewModel.ImagePath = resultPath;
            using MagickImage resultImage = new(resultPath);
            MainImage.Source = resultImage.ToBitmapSource();
            ViewModel.ActualImageSize = new Size(resultImage.Width, resultImage.Height);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Clean Whiteboard failed: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            CleanWhiteboardButton.IsEnabled = true;
            CleanWhiteboardProgressRing.Visibility = Visibility.Collapsed;
        }
    }

    private void MarkupShapeControl_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is not MarkupShapeControl control) return;
        control.MeasurementPointMouseDown -= MarkupShapePoint_MouseDown;
        control.RemoveControlRequested -= MarkupShapeControl_RemoveControlRequested;
        markupShapeControls.Remove(control);
        ShapeCanvas.Children.Remove(control);

        UndoRedo.AddUndo(new MarkupControlRemovedItem<MarkupShapeControl>(
            control, markupShapeControls, ShapeCanvas,
            wireEvents: () =>
            {
                control.MeasurementPointMouseDown += MarkupShapePoint_MouseDown;
                control.RemoveControlRequested += MarkupShapeControl_RemoveControlRequested;
            },
            unwireEvents: () =>
            {
                control.MeasurementPointMouseDown -= MarkupShapePoint_MouseDown;
                control.RemoveControlRequested -= MarkupShapeControl_RemoveControlRequested;
            }));
    }

    private void MarkupTextControl_EditCommitted(object? sender, EventArgs e)
    {
        if (sender is not MarkupTextControl control) return;
        if (control.TextBeforeEdit != control.MarkupText)
            UndoRedo.AddUndo(new MarkupTextChangedItem(control, control.TextBeforeEdit, control.MarkupText));
    }

    private void MarkupTextControl_TextMoved(object sender, Point before, Point after)
    {
        if (sender is not MarkupTextControl control) return;
        UndoRedo.AddUndo(new MarkupTextMovedItem(control, before, after));
    }

    /// <summary>
    /// Commits any markup text control still in edit mode. Returns true if one was open.
    /// </summary>
    private bool CommitPendingMarkupTextEdit()
    {
        bool committed = false;
        // Committing empty text cancels the edit, which can remove the control
        // from the collection — iterate over a copy
        foreach (MarkupTextControl control in markupTextControls.ToList())
        {
            if (control.IsEditing)
            {
                control.CommitEdit();
                committed = true;
            }
        }

        return committed;
    }

    private void MarkupTextControl_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is not MarkupTextControl control) return;
        control.RemoveControlRequested -= MarkupTextControl_RemoveControlRequested;
        markupTextControls.Remove(control);
        ShapeCanvas.Children.Remove(control);

        UndoRedo.AddUndo(new MarkupControlRemovedItem<MarkupTextControl>(
            control, markupTextControls, ShapeCanvas,
            wireEvents: () => control.RemoveControlRequested += MarkupTextControl_RemoveControlRequested,
            unwireEvents: () => control.RemoveControlRequested -= MarkupTextControl_RemoveControlRequested));
    }

    private void ClearAllMarkup()
    {
        foreach (MarkupShapeControl control in markupShapeControls.ToList())
        {
            control.MeasurementPointMouseDown -= MarkupShapePoint_MouseDown;
            control.RemoveControlRequested -= MarkupShapeControl_RemoveControlRequested;
            ShapeCanvas.Children.Remove(control);
        }
        markupShapeControls.Clear();

        foreach (MarkupTextControl control in markupTextControls.ToList())
        {
            control.RemoveControlRequested -= MarkupTextControl_RemoveControlRequested;
            ShapeCanvas.Children.Remove(control);
        }
        markupTextControls.Clear();

        MarkupCanvas.Strokes.Clear();
    }

    private void ClearMarkupButton_Click(object sender, RoutedEventArgs e)
    {
        if (markupShapeControls.Count == 0 && markupTextControls.Count == 0 && MarkupCanvas.Strokes.Count == 0)
            return;

        List<MarkupShapeControl> shapes = [.. markupShapeControls];
        List<MarkupTextControl> texts = [.. markupTextControls];
        List<System.Windows.Ink.Stroke> strokes = [.. MarkupCanvas.Strokes];

        UndoRedo.AddUndo(new MarkupClearedItem(
            shapes, texts, strokes,
            markupShapeControls, markupTextControls,
            ShapeCanvas, MarkupCanvas,
            wireEvents: () =>
            {
                foreach (MarkupShapeControl shape in shapes)
                {
                    shape.MeasurementPointMouseDown += MarkupShapePoint_MouseDown;
                    shape.RemoveControlRequested += MarkupShapeControl_RemoveControlRequested;
                }
                foreach (MarkupTextControl text in texts)
                    text.RemoveControlRequested += MarkupTextControl_RemoveControlRequested;
            },
            unwireEvents: () =>
            {
                foreach (MarkupShapeControl shape in shapes)
                {
                    shape.MeasurementPointMouseDown -= MarkupShapePoint_MouseDown;
                    shape.RemoveControlRequested -= MarkupShapeControl_RemoveControlRequested;
                }
                foreach (MarkupTextControl text in texts)
                    text.RemoveControlRequested -= MarkupTextControl_RemoveControlRequested;
            }));

        ClearAllMarkup();
    }

    private void HideMarkupToggle_Checked(object sender, RoutedEventArgs e)
    {
        SetMarkupVisibility(false);
    }

    private void HideMarkupToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        SetMarkupVisibility(true);
    }

    private void SetMarkupVisibility(bool visible)
    {
        Visibility v = visible ? Visibility.Visible : Visibility.Collapsed;
        foreach (MarkupShapeControl control in markupShapeControls)
            control.Visibility = v;
        foreach (MarkupTextControl control in markupTextControls)
            control.Visibility = v;
        MarkupCanvas.Visibility = v;
    }

    #endregion Markup Tab
}
internal enum AnglePlacementStep
{
    None,
    DraggingFirstLeg,
    PlacingThirdPoint
}
