using ImageMagick;
using MagickCrop.Controls;
using MagickCrop.Helpers;
using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace MagickCrop;

public partial class MainWindow : FluentWindow
{
    private Point clickedPoint = new();
    private Size oldGridSize = new();
    private Size originalImageSize = new();
    private Size actualImageSize = new();
    private FrameworkElement? clickedElement;

    // Size input properties
    private bool isUpdatingFromCode = false;
    private bool isPixelMode = true;
    private bool isAspectRatioLocked = true;
    private double aspectRatio = 1.0;
    private int pointDraggingIndex = -1;
    private Polygon? lines;
    private string? imagePath;
    private string? savedPath;
    private readonly int ImageWidthConst = 700;

    // Quadrilateral detection parameters
    private const double QuadDetectionMinArea = 0.02;
    private const int QuadDetectionMaxResults = 5;

    private DraggingMode draggingMode = DraggingMode.None;

    private string openedFileName = string.Empty;
    private MagickCropMeasurementPackage? openedPackage;
    private readonly List<UIElement> _polygonElements;

    private readonly UndoRedo undoRedo = new();
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

    private Services.RecentProjectsManager? recentProjectsManager;
    private string? currentProjectId;
    private System.Timers.Timer? autoSaveTimer;
    private readonly int AutoSaveIntervalMs = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;

    private static readonly List<FormatItem> _formats =
    [
        new FormatItem { Name = "JPEG Image", Format = MagickFormat.Jpg, Extension = ".jpg", SupportsQuality = true },
        new FormatItem { Name = "PNG Image", Format = MagickFormat.Png, Extension = ".png", SupportsQuality = false },
        new FormatItem { Name = "BMP Image", Format = MagickFormat.Bmp, Extension = ".bmp", SupportsQuality = false },
        new FormatItem { Name = "TIFF Image", Format = MagickFormat.Tiff, Extension = ".tiff", SupportsQuality = false },
        new FormatItem { Name = "WebP Image", Format = MagickFormat.WebP, Extension = ".webp", SupportsQuality = true },
        // new FormatItem { Name = "HEIC Image", Format = MagickFormat.Heic, Extension = ".heic", SupportsQuality = true }
    ];

    private readonly ObservableCollection<Line> verticalLines = [];
    private readonly ObservableCollection<Line> horizontalLines = [];

    private bool isDrawingMode = false;
    private Dictionary<Stroke, StrokeInfo> strokeMeasurements = [];

    private bool isCreatingMeasurement = false;

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
    private WpfTextBlock? rotationOverlayLabel;
    private long lastRotateUpdateTicks = 0;
    private double lastAppliedAdornerAngle = 0.0;
    private const int RotateUpdateMinIntervalMs = 12; // throttle to reduce UI thrash
    private const double RotateMinDelta = 0.1; // degrees

    private RotateAdorner? rotateAdorner;
    private AdornerLayer? rotateAdornerLayer;
    private bool isAdornerRotatingDrag = false; // true while adorner has the mouse captured

    // Hover highlight polygon for quadrilateral selector
    private Polygon? hoverHighlightPolygon;

    public MainWindow()
    {
        ThemeService themeService = new();
        themeService.SetTheme(ApplicationTheme.Dark);

        Color teal = (Color)ColorConverter.ConvertFromString("#0066FF");
        ApplicationAccentColorManager.Apply(teal);

        InitializeComponent();
        // Ensure zoom still works if mouse wheel fires at window level (after a pan or when mouse over other element)
        PreviewMouseWheel += ShapeCanvas_PreviewMouseWheel;

        DrawPolyLine();
        _polygonElements = [lines, TopLeft, TopRight, BottomRight, BottomLeft];

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Collapsed;

        try
        {
            PackageVersion version = Package.Current.Id.Version;
            wpfuiTitleBar.Title += $" v{version.Major}.{version.Minor}.{version.Build}";
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
        UpdateOpenedFileNameText();

        ShapeCanvas.MouseUp += ShapeCanvas_MouseUp;
        ShapeCanvas.LostMouseCapture += ShapeCanvas_LostMouseCapture; // safety to ensure capture released
        rotationOverlayLabel = FindName("RotationOverlayLabel") as WpfTextBlock; // cache
    }

    private void ShapeCanvas_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (draggingMode == DraggingMode.Panning)
            draggingMode = DraggingMode.None;
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

        List<Ellipse> ellipseList = [.. ShapeCanvas.Children.OfType<Ellipse>()];

        foreach (Ellipse ellipse in ellipseList)
        {
            lines.Points.Add(
                new Point(Canvas.GetLeft(ellipse) + (ellipse.Width / 2),
                                Canvas.GetTop(ellipse) + (ellipse.Height / 2)));
        }

        ShapeCanvas.Children.Add(lines);
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
        CaptureMouse();

        // Show pixel zoom for precise corner placement
        ShowPixelZoom(clickedPoint);
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

        // Update pixel zoom if it should be shown
        Point mousePos = e.GetPosition(ShapeCanvas);
        if (ShouldShowPixelZoom())
        {
            // Show zoom if not already visible
            if (PixelZoomControl.Visibility != Visibility.Visible && MainImage.Source != null)
            {
                ShowPixelZoom(mousePos);
            }
            else
            {
                UpdatePixelZoom(mousePos);
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

            clickedElement = null;
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

        if (draggingMode != DraggingMode.MoveElement || clickedElement is null)
            return;

        Canvas.SetTop(clickedElement, movingPoint.Y - (clickedElement.Height / 2));
        Canvas.SetLeft(clickedElement, movingPoint.X - (clickedElement.Width / 2));

        MovePolyline(movingPoint);

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

        e.Handled = true;
    }

    private void PanCanvas(MouseEventArgs e)
    {
        Point currentPosition = e.GetPosition(this);
        Vector delta = currentPosition - clickedPoint;

        // Update the translation
        canvasTranslate.X += delta.X;
        canvasTranslate.Y += delta.Y;

        clickedPoint = currentPosition;
    }

    private void MovePolyline(Point newPoint)
    {
        if (pointDraggingIndex < 0 || lines is null)
            return;

        lines.Points[pointDraggingIndex] = newPoint;
        AspectRatioTransformPreview.SetAndScalePoints(lines.Points);
    }

    private async Task<MagickImage?> CorrectDistortion(string pathOfImage)
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

        if (selectedAspectRatio.AspectRatioEnum == AspectRatio.Custom)
        {
            if (CustomHeight.Value is double height
                && CustomWidth.Value is double width
                && height != 0
                && width != 0)
                aspectRatio = height / width;
            else
                return null;
        }

        Rect? visualContentBounds = GetPrivatePropertyValue(lines, "VisualContentBounds") as Rect?;
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

        return image;
    }

    /// <summary>
    /// Applies the perspective distortion correction to the currently loaded image.
    /// This method also adjusts the position and size of any visible cropping rectangle
    /// to account for changes in image dimensions after distortion correction.
    /// </summary>
    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
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

        MagickImage? image = await CorrectDistortion(imagePath);

        if (image is null)
        {
            SetUiForCompletedTask();
            return;
        }

        string tempFileName = System.IO.Path.GetTempFileName();
        await image.WriteAsync(tempFileName);
        imagePath = tempFileName;

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
            FileName = $"{openedFileName}_corrected.jpg",
        };

        if (saveFileDialog.ShowDialog() is not true || lines is null)
        {
            BottomPane.IsEnabled = true;
            Cursor = null;
            SetUiForCompletedTask();
            return;
        }

        string correctedImageFileName = saveFileDialog.FileName;

        if (string.IsNullOrWhiteSpace(imagePath) || string.IsNullOrWhiteSpace(correctedImageFileName))
        {
            SetUiForCompletedTask();
            return;
        }

        MagickImage? image = await CorrectDistortion(imagePath);


        if (image is null)
        {
            SetUiForCompletedTask();
            return;
        }

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
            savedPath = correctedImageFileName;

            SetUiForCompletedTask();
            image.Dispose();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(imagePath))
            return;

        SetUiForLongTask();

        try
        {
            // Get current image dimensions
            MagickImage magickImage = new(imagePath);
            double width = magickImage.Width;
            double height = magickImage.Height;
            magickImage.Dispose();

            // Create and show save options dialog in a window
            SaveOptionsDialog saveOptionsDialog = new(width, height);
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
                            _formats.FirstOrDefault(f => f.Format == options.Format)
                            ?? _formats[0]),
                DefaultExt = options.Extension,
                RestoreDirectory = true,
                FileName = $"{openedFileName}_edited{options.Extension}",
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                SetUiForCompletedTask();
                return;
            }

            string correctedImageFileName = saveFileDialog.FileName;

            // Load image and apply options
            using MagickImage image = new(imagePath);

            // Resize if requested
            if (options.Resize)
            {
                MagickGeometry resizeGeometry = new((uint)options.Width, (uint)options.Height)
                {
                    IgnoreAspectRatio = !options.MaintainAspectRatio
                };
                image.Resize(resizeGeometry);
            }

            // Set quality for formats that support it
            image.Quality = (uint)options.Quality;

            // Save with the selected format
            await image.WriteAsync(correctedImageFileName, options.Format);

            // Show preview and enable open folder button
            OpenFolderButton.IsEnabled = true;
            SaveWindow saveWindow = new(correctedImageFileName);
            saveWindow.Show();

            // Store the saved path for the open folder button
            savedPath = correctedImageFileName;
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

    private void SetUiForLongTask()
    {
        BottomPane.IsEnabled = false;
        Cursor = Cursors.Wait;
        IsWorkingBar.Visibility = Visibility.Visible;
        autoSaveTimer?.Stop();
    }

    private void SetUiForCompletedTask()
    {
        IsWorkingBar.Visibility = Visibility.Collapsed;
        Cursor = null;
        BottomPane.IsEnabled = true;

        autoSaveTimer?.Stop();
        autoSaveTimer?.Start();
    }

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        SetUiForLongTask();

        OpenFileDialog openFileDialog = new()
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.heic;*.bmp|All files (*.*)|*.*",
            RestoreDirectory = true,
        };

        if (openFileDialog.ShowDialog() != true)
        {
            SetUiForCompletedTask();
            WelcomeMessageModal.Visibility = Visibility.Visible;
            return;
        }

        RemoveMeasurementControls();
        wpfuiTitleBar.Title = $"Magick Crop & Measure: {System.IO.Path.GetFileName(openFileDialog.FileName)}";
        await OpenImagePath(openFileDialog.FileName);
    }

    private async void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        // Check if clipboard contains image data
        if (!Clipboard.ContainsImage())
        {
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Paste Error",
                Content = "No image found in clipboard. Copy an image first.",
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
            BitmapSource clipboardImage = Clipboard.GetImage();

            if (clipboardImage is null)
            {
                Wpf.Ui.Controls.MessageBox uiMessageBox = new()
                {
                    Title = "Paste Error",
                    Content = "Could not retrieve a valid image from the clipboard.",
                };
                await uiMessageBox.ShowDialogAsync();
                return;
            }

            // Create a temporary file for the image
            string tempFileName = System.IO.Path.GetTempFileName();
            tempFileName = System.IO.Path.ChangeExtension(tempFileName, ".jpg");

            // Save the clipboard image to the temporary file
            using FileStream stream = new(tempFileName, FileMode.Create);

            JpegBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(clipboardImage));
            encoder.Save(stream);

            // Reset any current measurements
            RemoveMeasurementControls();
            openedFileName = "Pasted_Image_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // Open the image in the application
            await OpenImagePath(tempFileName);

            // Update UI
            BottomBorder.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            WelcomeMessageModal.Visibility = Visibility.Visible;
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Error",
                Content = $"Error pasting image: {ex.Message}",
            };
        }
        finally
        {
            SetUiForCompletedTask();
        }
    }

    private void OverlayButton_Click(object sender, RoutedEventArgs e)
    {
        WelcomeMessageModal.Visibility = Visibility.Collapsed;
        BottomBorder.Visibility = Visibility.Visible;
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
        ReOpenFileText.Text = "Overlay Mode";
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
        Save.IsEnabled = true;
        ImageGrid.Width = ImageWidthConst;
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

        imagePath = tempFileName;
        openedFileName = System.IO.Path.GetFileNameWithoutExtension(imageFilePath);
        MainImage.Source = bitmapImage.ToBitmapSource();

        // Update original size after image is loaded (will be the default ImageWidthConst height calculated from aspect ratio)
        originalImageSize = new Size(bitmapImage.Width, bitmapImage.Height);

        BottomBorder.Visibility = Visibility.Visible;
        SetUiForCompletedTask();

        // Create a new project ID for this image
        currentProjectId = Guid.NewGuid().ToString();

        // Update the ReOpenFileButton to show the current file name
        UpdateOpenedFileNameText();
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        string? folderPath = System.IO.Path.GetDirectoryName(savedPath);

        if (folderPath is null || lines is null)
            return;

        Process.Start("explorer.exe", folderPath);
    }

    private static object? GetPrivatePropertyValue(object obj, string propName)
    {
        ArgumentNullException.ThrowIfNull(obj);

        Type t = obj.GetType();
        PropertyInfo? pi = t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new ArgumentOutOfRangeException(nameof(propName), string.Format("Field {0} was not found in Type {1}", propName, obj.GetType().FullName));
        return pi.GetValue(obj, null);
    }

    private const double ZoomFactor = 0.1;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;

    private void ShapeCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Get the current mouse position relative to the canvas
        Point mousePosition = e.GetPosition(ShapeCanvas);

        // Calculate new scale based on wheel delta
        double zoomChange = e.Delta > 0 ? ZoomFactor : -ZoomFactor;
        double newScaleX = canvasScale.ScaleX + (canvasScale.ScaleX * zoomChange);
        double newScaleY = canvasScale.ScaleY + (canvasScale.ScaleY * zoomChange);

        // Limit zoom to min/max values
        newScaleX = Math.Clamp(newScaleX, MinZoom, MaxZoom);
        newScaleY = Math.Clamp(newScaleY, MinZoom, MaxZoom);

        // Adjust the zoom center to the mouse position
        Point relativePt = mousePosition;

        // Calculate new transform origin
        double absoluteX = (relativePt.X * canvasScale.ScaleX) + canvasTranslate.X;
        double absoluteY = (relativePt.Y * canvasScale.ScaleY) + canvasTranslate.Y;

        // Calculate the new translate values to maintain mouse position
        canvasTranslate.X = absoluteX - (relativePt.X * newScaleX);
        canvasTranslate.Y = absoluteY - (relativePt.Y * newScaleY);

        // Apply new scale
        canvasScale.ScaleX = newScaleX;
        canvasScale.ScaleY = newScaleY;

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

        // Middle mouse always initiates panning regardless of tool (quick navigation)
        if (e.ChangedButton == MouseButton.Middle)
        {
            draggingMode = DraggingMode.Panning;
            clickedPoint = e.GetPosition(this);
            ShapeCanvas.CaptureMouse();
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
            return;
        }

        CustomButtonGrid.Visibility = Visibility.Collapsed;
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
        canvasScale.ScaleX = 1;
        canvasScale.ScaleY = 1;

        canvasScale.CenterX = 0;
        canvasScale.CenterY = 0;

        canvasTranslate.X = 0;
        canvasTranslate.Y = 0;
    }

    private async void AutoContrastMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.SigmoidalContrast(10));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void WhiteBalanceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.WhiteBalance());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void BlackPointMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.BlackThreshold(new Percentage(10)));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void WhitePointMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.WhiteThreshold(new Percentage(90)));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void GrayscaleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Grayscale());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void InvertMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Negate());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void AutoLevelsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.AutoLevel());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void AutoGammaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.AutoGamma());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void BlurMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Blur(20, 10));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void FindEdgesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.CannyEdge());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void Rotate90CwMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Rotate(90));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void Rotate90CcwMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Rotate(-90));

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void FlipVertMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Flip());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private async void FlipHozMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;

        SetUiForLongTask();

        MagickImage magickImage = new(imagePath);
        await Task.Run(() => magickImage.Flop());

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

        SetUiForCompletedTask();
    }

    private void StretchMenuItem_Click(object sender, RoutedEventArgs e)
    {
        oldGridSize = new(ImageGrid.ActualWidth, ImageGrid.ActualHeight);
        ShowResizeControls();
    }

    private void CropImage_Click(object sender, RoutedEventArgs e)
    {
        ShowCroppingControls();
    }

    private void ShowCroppingControls()
    {
        HideResizeControls();
        HideTransformControls();

        CropButtonPanel.Visibility = Visibility.Visible;
        CroppingRectangle.Visibility = Visibility.Visible;
    }

    private async void ApplyCropButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(imagePath))
            return;

        MagickGeometry cropGeometry = CroppingRectangle.CropShape;
        MagickImage magickImage = new(imagePath);
        MagickGeometry actualSize = new(magickImage.Width, magickImage.Height);

        double factor = actualSize.Height / MainImage.ActualHeight;
        cropGeometry.ScaleAll(factor);

        SetUiForLongTask();

        magickImage.Crop(cropGeometry);

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = magickImage.ToBitmapSource();

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
    }

    private void PerspectiveCorrectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowTransformControls();
    }

    private void CancelTransformButton_Click(object sender, RoutedEventArgs e)
    {
        HideTransformControls();
    }

    private void ShowTransformControls()
    {
        HideCroppingControls();
        HideResizeControls();

        TransformButtonPanel.Visibility = Visibility.Visible;

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Visible;
    }

    private void HideTransformControls()
    {
        TransformButtonPanel.Visibility = Visibility.Collapsed;

        foreach (UIElement element in _polygonElements)
            element.Visibility = Visibility.Collapsed;

        if (lines is not null)
            lines.Visibility = Visibility.Collapsed;
    }

    private async void DetectShapeButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            _ = System.Windows.MessageBox.Show("Please open an image first.", "No Image", System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Show progress indicator
        IsWorkingBar.Visibility = Visibility.Visible;

        try
        {
            // Detect quadrilaterals in background thread
            QuadrilateralDetector.DetectionResult detectionResult = await Task.Run(() =>
                QuadrilateralDetector.DetectQuadrilateralsWithDimensions(imagePath, minArea: QuadDetectionMinArea, maxResults: QuadDetectionMaxResults));

            if (detectionResult.Quadrilaterals.Count == 0)
            {
                _ = System.Windows.MessageBox.Show(
                    "No quadrilaterals detected in the image.\n\nPlease position the corner markers manually.",
                    "No Shapes Detected",
                    System.Windows.MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                // Scale quadrilaterals to display coordinates
                List<QuadrilateralDetector.DetectedQuadrilateral> scaledQuads = [.. detectionResult.Quadrilaterals.Select(q =>
                    QuadrilateralDetector.ScaleToDisplay(
                        q,
                        detectionResult.ImageWidth,
                        detectionResult.ImageHeight,
                        MainImage.ActualWidth,
                        MainImage.ActualHeight))];

                // Show selector
                QuadrilateralSelectorControl.SetQuadrilaterals(scaledQuads);
                QuadrilateralSelectorControl.QuadrilateralHoverEnter += QuadrilateralSelector_HoverEnter;
                QuadrilateralSelectorControl.QuadrilateralHoverExit += QuadrilateralSelector_HoverExit;
                ShowQuadrilateralSelector();
            }
        }
        catch (IOException ioEx)
        {
            _ = System.Windows.MessageBox.Show(
                $"File error while detecting quadrilaterals: {ioEx.Message}",
                "File Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (UnauthorizedAccessException uaEx)
        {
            _ = System.Windows.MessageBox.Show(
                $"Access denied while detecting quadrilaterals: {uaEx.Message}",
                "Access Denied",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            _ = System.Windows.MessageBox.Show(
                $"Error detecting quadrilaterals: {ex.Message}",
                "Detection Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsWorkingBar.Visibility = Visibility.Collapsed;
        }
    }

    private void QuadrilateralSelector_Selected(object? sender, Helpers.QuadrilateralDetector.DetectedQuadrilateral quad)
    {
        // Hide selector overlay
        HideQuadrilateralSelector();

        // Position the corner markers
        PositionCornerMarkers(quad);
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
            draggingMode = DraggingMode.Resizing;
        }
    }

    private async void ApplyResizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(imagePath))
            return;

        MagickImage magickImage = new(imagePath);

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
            targetWidth = (int)(actualImageSize.Width * widthPercent);
            targetHeight = (int)(actualImageSize.Height * heightPercent);
        }

        MagickGeometry resizeGeometry = new((uint)targetWidth, (uint)targetHeight)
        {
            IgnoreAspectRatio = true
        };

        SetUiForLongTask();

        magickImage.Resize(resizeGeometry);

        string tempFileName = System.IO.Path.GetTempFileName();
        await magickImage.WriteAsync(tempFileName);

        ResizeUndoRedoItem undoRedoItem = new(MainImage, ImageGrid, oldGridSize, imagePath, tempFileName);
        undoRedo.AddUndo(undoRedoItem);

        imagePath = tempFileName;

        MainImage.Source = null;
        MainImage.Source = magickImage.ToBitmapSource();

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

        // Initialize resize input controls
        InitializeResizeInputs();

        ResizeButtonsPanel.Visibility = Visibility.Visible;
        ImageResizeGrip.Visibility = Visibility.Visible;
    }

    private void InitializeResizeInputs()
    {
        if (MainImage.Source is BitmapSource bitmap)
        {
            actualImageSize = new Size(bitmap.PixelWidth, bitmap.PixelHeight);
            aspectRatio = actualImageSize.Width / actualImageSize.Height;
        }
        else
        {
            actualImageSize = originalImageSize;
            aspectRatio = actualImageSize.Width / actualImageSize.Height;
        }

        UpdateCurrentSizeDisplay();
        UpdateSizeInputFields();
    }

    private void UpdateCurrentSizeDisplay()
    {
        if (isPixelMode)
        {
            CurrentWidthDisplay.Text = ((int)actualImageSize.Width).ToString();
            CurrentHeightDisplay.Text = ((int)actualImageSize.Height).ToString();
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
            WidthTextBox.Text = ((int)actualImageSize.Width).ToString();
            HeightTextBox.Text = ((int)actualImageSize.Height).ToString();
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
            targetWidth = actualImageSize.Width * (width / 100.0);
            targetHeight = actualImageSize.Height * (height / 100.0);
        }

        // Calculate scale factors relative to original display size
        double widthScale = targetWidth / actualImageSize.Width;
        double heightScale = targetHeight / actualImageSize.Height;

        // Apply to ImageGrid (maintains the same logic as drag resize)
        ImageGrid.Width = originalImageSize.Width * widthScale;
        ImageGrid.Height = originalImageSize.Height * heightScale;
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
        if (AspectRatioIcon is not null)
            AspectRatioIcon.Symbol = SymbolRegular.Link24;
    }

    private void AspectRatioLockToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        isAspectRatioLocked = false;
        MainImage.Stretch = Stretch.Fill; // Allow stretching without maintaining aspect ratio
        if (AspectRatioIcon is not null)
            AspectRatioIcon.Symbol = SymbolRegular.LinkDismiss24;
    }

    private void UndoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string path = undoRedo.Undo();
        if (!string.IsNullOrWhiteSpace(path))
            imagePath = path;
    }

    private void RedoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string path = undoRedo.Redo();
        if (!string.IsNullOrWhiteSpace(path))
            imagePath = path;
    }

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

    private void DistanceMeasurementControl_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is DistanceMeasurementControl control)
        {
            ShapeCanvas.Children.Remove(control);
            measurementTools.Remove(control);
        }
    }

    private void AngleMeasurementControl_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is AngleMeasurementControl control)
        {
            ShapeCanvas.Children.Remove(control);
            angleMeasurementTools.Remove(control);
        }
    }

    private void RectangleMeasurementControl_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is RectangleMeasurementControl control)
        {
            ShapeCanvas.Children.Remove(control);
            rectangleMeasurementTools.Remove(control);
        }
    }
    private void PolygonMeasurementControl_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is PolygonMeasurementControl control)
        {
            ShapeCanvas.Children.Remove(control);
            polygonMeasurementTools.Remove(control);
        }
    }

    private void CircleMeasurementControl_RemoveControlRequested(object sender, EventArgs e)
    {
        if (sender is CircleMeasurementControl control)
        {
            ShapeCanvas.Children.Remove(control);
            circleMeasurementTools.Remove(control);
        }
    }

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
            CloseButtonText = "Cancel"
        };

        // Show the dialog and handle the result
        ContentDialogService dialogService = new();
        dialog.DialogHost = Presenter;
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
        draggingMode = DraggingMode.None;
    }

    private void MeasurementPoint_MouseDown(object sender, MouseButtonEventArgs? e)
    {
        if (isAdornerRotatingDrag)
        {
            if (e is not null)
                e.Handled = true;
            return;
        }
        if (sender is Ellipse senderEllipse
            && senderEllipse.Parent is Canvas measureCanvas
            && measureCanvas.Parent is DistanceMeasurementControl measureControl
            )
        {
            activeMeasureControl = measureControl;

            draggingMode = DraggingMode.MeasureDistance;
            if (e is not null)
            {
                clickedPoint = e.GetPosition(ShapeCanvas);
                // Show pixel zoom for precise point adjustment
                ShowPixelZoom(clickedPoint);
            }
            CaptureMouse();
        }
    }

    private void AngleMeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (isAdornerRotatingDrag)
        {
            e.Handled = true;
            return;
        }
        if (sender is Ellipse senderEllipse
            && senderEllipse.Parent is Canvas measureCanvas
            && measureCanvas.Parent is AngleMeasurementControl measureControl
            )
        {
            activeAngleMeasureControl = measureControl;

            draggingMode = DraggingMode.MeasureAngle;
            clickedPoint = e.GetPosition(ShapeCanvas);
            // Show pixel zoom for precise point adjustment
            ShowPixelZoom(clickedPoint);
            CaptureMouse();
        }
    }

    private void RectangleMeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (isAdornerRotatingDrag)
        {
            e.Handled = true;
            return;
        }
        if (sender is System.Windows.Shapes.Ellipse senderEllipse &&
            senderEllipse.Parent is Canvas measureCanvas &&
            measureCanvas.Parent is RectangleMeasurementControl measureControl)
        {
            activeRectangleMeasureControl = measureControl;
            draggingMode = DraggingMode.MeasureRectangle;
            clickedPoint = e.GetPosition(ShapeCanvas);
            // Show pixel zoom for precise point adjustment
            ShowPixelZoom(clickedPoint);
            CaptureMouse();
        }
    }

    private void PolygonMeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (isAdornerRotatingDrag)
        {
            e.Handled = true;
            return;
        }
        if (sender is System.Windows.Shapes.Ellipse senderEllipse &&
            senderEllipse.Parent is Canvas measureCanvas &&
            measureCanvas.Parent is PolygonMeasurementControl measureControl)
        {
            activePolygonMeasureControl = measureControl;
            draggingMode = DraggingMode.MeasurePolygon;
            clickedPoint = e.GetPosition(ShapeCanvas);
            // Show pixel zoom for precise point adjustment
            ShowPixelZoom(clickedPoint);
            CaptureMouse();
        }
    }

    private void CircleMeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (isAdornerRotatingDrag)
        {
            e.Handled = true;
            return;
        }
        if (sender is System.Windows.Shapes.Ellipse senderEllipse &&
            senderEllipse.Parent is Canvas measureCanvas &&
            measureCanvas.Parent is CircleMeasurementControl measureControl)
        {
            activeCircleMeasureControl = measureControl;
            draggingMode = DraggingMode.MeasureCircle;
            clickedPoint = e.GetPosition(ShapeCanvas);
            // Show pixel zoom for precise point adjustment
            ShowPixelZoom(clickedPoint);
            CaptureMouse();
        }
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

    private void CloseMeasurementButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveMeasurementControls();
        ClearAllStrokesAndLengths();
        isDrawingMode = false;
        DrawingCanvas.IsEnabled = false;
        DrawingOptionsPanel.Visibility = Visibility.Collapsed;
    }

    private void SaveMeasurementsPackageToFile()
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Error",
                Content = "No image loaded. Please open an image first.",
            };
            uiMessageBox.ShowDialogAsync();
            return;
        }

        // Create the package
        MagickCropMeasurementPackage package = new()
        {
            ImagePath = imagePath,
            Metadata = new PackageMetadata
            {
                OriginalFilename = openedFileName,
                OriginalImageSize = originalImageSize,
                CurrentImageSize = new Size(ImageGrid.ActualWidth, ImageGrid.ActualHeight),
                ImageStretch = MainImage.Stretch
            },
            Measurements = new MeasurementCollection
            {
                GlobalScaleFactor = ScaleInput.Value ?? 1.0,
                GlobalUnits = MeasurementUnits.Text
            }
        };

        // Add all measurements to the package
        foreach (DistanceMeasurementControl control in measurementTools)
        {
            package.Measurements.DistanceMeasurements.Add(control.ToDto());
        }
        foreach (AngleMeasurementControl control in angleMeasurementTools)
        {
            package.Measurements.AngleMeasurements.Add(control.ToDto());
        }

        foreach (RectangleMeasurementControl control in rectangleMeasurementTools)
        {
            package.Measurements.RectangleMeasurements.Add(control.ToDto());
        }

        foreach (CircleMeasurementControl control in circleMeasurementTools)
        {
            package.Measurements.CircleMeasurements.Add(control.ToDto());
        }

        foreach (PolygonMeasurementControl control in polygonMeasurementTools)
        {
            package.Measurements.PolygonMeasurements.Add(control.ToDto());
        }

        foreach (VerticalLineControl control in verticalLineControls)
            package.Measurements.VerticalLines.Add(control.ToDto());

        foreach (HorizontalLineControl control in horizontalLineControls)
            package.Measurements.HorizontalLines.Add(control.ToDto());

        // Save ink strokes and their stroke length displays
        foreach (KeyValuePair<Stroke, StrokeInfo> entry in strokeMeasurements)
        {
            Stroke stroke = entry.Key;
            StrokeInfo info = entry.Value;

            // Find the corresponding display control
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

        // Show save file dialog
        SaveFileDialog saveFileDialog = new()
        {
            Filter = "MagickCrop Measurement Files|*.mcm",
            RestoreDirectory = true,
            FileName = $"{openedFileName}_measurements.mcm"
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
            openedPackage = package;

            // Load the image
            await OpenImagePath(package.ImagePath);
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
                originalImageSize = package.Metadata.OriginalImageSize;
                ImageGrid.Width = originalImageSize.Width;
                ImageGrid.Height = originalImageSize.Height;
            }

            // Apply the saved resize to the ImageGrid
            ImageGrid.Width = package.Metadata.CurrentImageSize.Width;
            ImageGrid.Height = package.Metadata.CurrentImageSize.Height;
        }

        // Restore the saved stretch mode
        MainImage.Stretch = package.Metadata.ImageStretch;

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

        if (package?.Metadata?.ProjectId is not null)
            currentProjectId = package.Metadata.ProjectId;
        else
            currentProjectId = Guid.NewGuid().ToString();

        MeasureTabItem.IsSelected = true;
        UpdateOpenedFileNameText();
    }

    public async void LoadMeasurementsPackageFromFile(string filePath)
    {
        SetUiForLongTask();
        WelcomeMessageModal.Visibility = Visibility.Collapsed;

        await LoadMeasurementPackageAsync(filePath);
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
        currentProjectId = Guid.NewGuid().ToString();
    }

    private void AutoSaveTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (IsWorkingBar.Visibility == Visibility.Visible)
            return; // Don't autosave if the UI is busy

        // Run on UI thread
        Dispatcher.Invoke(() =>
        {
            // Only autosave if we have an image and measurements that need saving
            if (MainImage.Source == null || string.IsNullOrEmpty(imagePath))
                return;

            AutosaveCurrentState();
        });
    }

    private void AutosaveCurrentState()
    {
        if (recentProjectsManager == null || MainImage.Source == null || string.IsNullOrEmpty(imagePath))
            return;

        try
        {
            PackageMetadata packageMetadata = new()
            {
                OriginalFilename = openedFileName,
                ProjectId = currentProjectId,
                LastModified = DateTime.Now,
                OriginalImageSize = originalImageSize,
                CurrentImageSize = new Size(ImageGrid.ActualWidth, ImageGrid.ActualHeight),
                ImageStretch = MainImage.Stretch
            };

            if (openedPackage is not null)
            {
                packageMetadata = openedPackage.Metadata;
                packageMetadata.LastModified = DateTime.Now;
                packageMetadata.OriginalImageSize = originalImageSize;
                packageMetadata.CurrentImageSize = new Size(ImageGrid.ActualWidth, ImageGrid.ActualHeight);
                packageMetadata.ImageStretch = MainImage.Stretch;
            }

            // Create a package with the current state
            MagickCropMeasurementPackage package = new()
            {
                ImagePath = imagePath,
                Metadata = packageMetadata,
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

    private void UpdateOpenedFileNameText()
    {
        if (string.IsNullOrEmpty(openedFileName))
        {
            ReOpenFileText.Text = "Image/Project Name";
            CloseFileIcon.Visibility = Visibility.Collapsed;
        }
        else
        {
            ReOpenFileText.Text = openedFileName;

            if (openedPackage is not null)
                ReOpenFileText.Text = $" {openedPackage.Metadata.OriginalFilename}";
            CloseFileIcon.Visibility = Visibility.Visible;
        }
    }

    private void CloseFileIcon_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        AutosaveCurrentState();
        WelcomeMessageModal.UpdateRecentProjects();
        ResetApplicationState();
    }

    private void ResetApplicationState()
    {
        // Stop the autosave timer
        autoSaveTimer?.Stop();
        AutosaveCurrentState();

        // Clear the image
        MainImage.Source = null;
        imagePath = null;
        openedFileName = string.Empty;
        openedPackage = null;
        savedPath = null;

        // Reset the title
        wpfuiTitleBar.Title = "Magick Crop & Measure by TheJoeFin";

        // Reset UI elements
        RemoveMeasurementControls();
        HideTransformControls();
        HideCroppingControls();
        HideResizeControls();
        BottomBorder.Visibility = Visibility.Collapsed;
        WelcomeMessageModal.Visibility = Visibility.Visible;
        OpenFolderButton.IsEnabled = false;
        Save.IsEnabled = false;

        // Reset the canvas transform
        if (ShapeCanvas.RenderTransform is MatrixTransform matTrans)
        {
            matTrans.Matrix = new Matrix();
        }

        // Reset undo/redo
        undoRedo.Clear();

        // Create a new project ID
        currentProjectId = Guid.NewGuid().ToString();

        // Update the button state
        UpdateOpenedFileNameText();
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
        if (e.Key == Key.Escape)
        {
            UncheckAllBut();

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
        }
    }

    // Precise rotation implementation
    private const double RotationSnapIncrement = 5.0; // degrees when Shift held
    private const double FineRotationMultiplier = 0.25; // slow factor when Ctrl held

    private void ShowRotationOverlay()
    {
        if (rotationOverlayLabel is not null)
            rotationOverlayLabel.Visibility = Visibility.Visible;
    }
    private void HideRotationOverlay()
    {
        if (rotationOverlayLabel is not null)
            rotationOverlayLabel.Visibility = Visibility.Collapsed;
    }
    private void UpdateRotationOverlay()
    {
        if (rotationOverlayLabel is not null)
            rotationOverlayLabel.Text = $"{currentPreviewRotation:0.0}°";
    }

    private void ToggleRotateMode(bool enable)
    {
        if (enable)
        {
            // Hide other panels that conflict
            HideCroppingControls();
            HideTransformControls();
            HideResizeControls();
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
            try { if (FreeRotateToggle != null) FreeRotateToggle.IsChecked = false; } catch { }
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
            try { if (ShapeCanvas != null) ShapeCanvas.IsHitTestVisible = false; } catch { }
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
        try { if (ShapeCanvas != null) ShapeCanvas.IsHitTestVisible = true; } catch { }
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
        try { if (ShapeCanvas != null) ShapeCanvas.IsHitTestVisible = true; } catch { }
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
        if (!isRotateMode || string.IsNullOrWhiteSpace(imagePath))
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
            string previousPath = imagePath!;
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
            undoRedo.AddUndo(undoItem);
            imagePath = tempFileName;

            using MagickImage newImage = new(imagePath);
            MainImage.Source = newImage.ToBitmapSource();
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
        if (string.IsNullOrWhiteSpace(imagePath))
            return;
        ToggleRotateMode(true);
    }

    #region Pixel Precision Zoom

    /// <summary>
    /// Shows the pixel precision zoom control at the current mouse position.
    /// </summary>
    /// <param name="mousePosition">Mouse position in ShapeCanvas coordinates</param>
    private void ShowPixelZoom(Point mousePosition)
    {
        if (MainImage.Source == null)
            return;

        try
        {
            // Set the source image for the zoom control
            PixelZoomControl.SourceImage = MainImage.Source;

            // Convert mouse position to image coordinates
            Point imagePosition = ConvertCanvasToImageCoordinates(mousePosition);
            PixelZoomControl.CurrentPosition = imagePosition;

            // Position the zoom control near the cursor
            Point canvasPosition = mousePosition;
            PixelZoomControl.PositionNearCursor(canvasPosition, ShapeCanvas.ActualWidth, ShapeCanvas.ActualHeight);

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
    /// <param name="mousePosition">Mouse position in ShapeCanvas coordinates</param>
    private void UpdatePixelZoom(Point mousePosition)
    {
        if (PixelZoomControl.Visibility != Visibility.Visible)
            return;

        try
        {
            // Convert mouse position to image coordinates
            Point imagePosition = ConvertCanvasToImageCoordinates(mousePosition);
            PixelZoomControl.CurrentPosition = imagePosition;

            // Update the zoom control position
            Point canvasPosition = mousePosition;
            PixelZoomControl.PositionNearCursor(canvasPosition, ShapeCanvas.ActualWidth, ShapeCanvas.ActualHeight);
        }
        catch (Exception)
        {
            // Silently handle any errors
        }
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
        if (MainImage.Source == null)
            return new Point(0, 0);

        try
        {
            // Get the transform from canvas to image
            GeneralTransform transform = ShapeCanvas.TransformToVisual(MainImage);
            Point imagePoint = transform.Transform(canvasPoint);

            // MainImage might have its own transform/scale, so we need to map to actual pixels
            double imageWidth = MainImage.Source.Width;
            double imageHeight = MainImage.Source.Height;
            double actualWidth = MainImage.ActualWidth;
            double actualHeight = MainImage.ActualHeight;

            // Calculate scale based on Stretch mode
            double scaleX = imageWidth / actualWidth;
            double scaleY = imageHeight / actualHeight;

            // For Uniform stretch, use the same scale for both dimensions
            if (MainImage.Stretch == Stretch.Uniform)
            {
                double scale = Math.Max(scaleX, scaleY);
                scaleX = scaleY = scale;
            }

            // Convert to pixel coordinates
            double pixelX = imagePoint.X * scaleX;
            double pixelY = imagePoint.Y * scaleY;

            // Clamp to image bounds
            pixelX = Math.Max(0, Math.Min(imageWidth - 1, pixelX));
            pixelY = Math.Max(0, Math.Min(imageHeight - 1, pixelY));

            return new Point(pixelX, pixelY);
        }
        catch (Exception)
        {
            return new Point(0, 0);
        }
    }

    /// <summary>
    /// Checks if pixel zoom should be shown for the current operation.
    /// </summary>
    /// <returns>True if pixel zoom should be active</returns>
    private bool ShouldShowPixelZoom()
    {
        // Show pixel zoom when dragging corner markers for transform
        if (draggingMode == DraggingMode.MoveElement && clickedElement != null)
            return true;

        // Show pixel zoom when placing/dragging measurement points
        if (draggingMode == DraggingMode.MeasureDistance ||
            draggingMode == DraggingMode.MeasureAngle ||
            draggingMode == DraggingMode.MeasureRectangle ||
            draggingMode == DraggingMode.MeasurePolygon ||
            draggingMode == DraggingMode.MeasureCircle)
            return true;

        // Show during measurement creation
        if (isCreatingMeasurement)
            return true;

        // Show during angle placement
        if (isPlacingAngleMeasurement)
            return true;

        // Show during polygon placement
        if (isPlacingPolygonMeasurement)
            return true;

        // Show during rectangle placement
        if (isPlacingRectangleMeasurement)
            return true;

        // Show during circle placement
        if (isPlacingCircleMeasurement)
            return true;

        // Show when any measurement tool is selected (even before clicking)
        if (MeasureDistanceToggle?.IsChecked == true ||
            MeasureAngleToggle?.IsChecked == true ||
            RectangleMeasureToggle?.IsChecked == true ||
            CircleMeasureToggle?.IsChecked == true ||
            PolygonMeasureToggle?.IsChecked == true)
            return true;

        // Show when transform mode is active
        if (TransformButtonPanel?.Visibility == Visibility.Visible)
            return true;

        return false;
    }

    #endregion Pixel Precision Zoom
}
internal enum AnglePlacementStep
{
    None,
    DraggingFirstLeg,
    PlacingThirdPoint
}
