using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MagickCrop.Controls;

/// <summary>
/// Shows a thumbnail of the current image with a rectangle indicating which part of it
/// is currently visible in the canvas viewport. Dragging inside the map pans the canvas.
/// </summary>
public partial class MiniMap : UserControl
{
    private const double MaxMapWidth = 168;
    private const double MaxMapHeight = 140;

    /// <summary>
    /// Raised with a point in canvas coordinates that should be centered in the viewport.
    /// </summary>
    public event EventHandler<Point>? ViewportCenterRequested;

    private double mapScale = 1;
    private bool isDragging;

    public MiniMap()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the map contents.
    /// </summary>
    /// <param name="source">The image currently shown on the canvas.</param>
    /// <param name="imageCanvasSize">The size of the image in canvas coordinates.</param>
    /// <param name="viewportInCanvas">The visible canvas region in canvas coordinates.</param>
    /// <returns><see langword="true"/> when the map has valid content to display.</returns>
    public bool UpdateMap(ImageSource? source, Size imageCanvasSize, Rect viewportInCanvas)
    {
        if (source is null || imageCanvasSize.Width <= 0 || imageCanvasSize.Height <= 0)
        {
            MiniImage.Source = null;
            return false;
        }

        if (!ReferenceEquals(MiniImage.Source, source))
            MiniImage.Source = source;

        mapScale = Math.Min(MaxMapWidth / imageCanvasSize.Width, MaxMapHeight / imageCanvasSize.Height);

        double mapWidth = Math.Max(1, imageCanvasSize.Width * mapScale);
        double mapHeight = Math.Max(1, imageCanvasSize.Height * mapScale);
        MapHost.Width = mapWidth;
        MapHost.Height = mapHeight;

        double left = Math.Clamp(viewportInCanvas.Left * mapScale, 0, mapWidth);
        double top = Math.Clamp(viewportInCanvas.Top * mapScale, 0, mapHeight);
        double right = Math.Clamp(viewportInCanvas.Right * mapScale, 0, mapWidth);
        double bottom = Math.Clamp(viewportInCanvas.Bottom * mapScale, 0, mapHeight);

        Rect viewportOnMap = new(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));

        ViewportRectangle.Margin = new Thickness(viewportOnMap.X, viewportOnMap.Y, 0, 0);
        ViewportRectangle.Width = viewportOnMap.Width;
        ViewportRectangle.Height = viewportOnMap.Height;

        OutsideDim.Data = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0, 0, mapWidth, mapHeight)),
            new RectangleGeometry(viewportOnMap));

        return true;
    }

    private void RequestCenter(Point mapPoint)
    {
        if (mapScale <= 0)
            return;

        ViewportCenterRequested?.Invoke(this, new Point(mapPoint.X / mapScale, mapPoint.Y / mapScale));
    }

    private void MapHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        isDragging = true;
        MapHost.CaptureMouse();
        RequestCenter(e.GetPosition(MapHost));
        e.Handled = true;
    }

    private void MapHost_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isDragging)
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag();
            return;
        }

        RequestCenter(e.GetPosition(MapHost));
        e.Handled = true;
    }

    private void MapHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isDragging)
            return;

        EndDrag();
        e.Handled = true;
    }

    private void MapHost_LostMouseCapture(object sender, MouseEventArgs e)
    {
        isDragging = false;
    }

    private void EndDrag()
    {
        isDragging = false;

        if (MapHost.IsMouseCaptured)
            MapHost.ReleaseMouseCapture();
    }
}
