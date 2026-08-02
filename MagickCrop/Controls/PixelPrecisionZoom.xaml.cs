using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MagickCrop.Controls;

/// <summary>
/// Pixel precision zoom control that displays a magnified view of the image at the cursor position.
/// Provides visual feedback for precise point placement similar to PowerToys Color Picker.
/// </summary>
public partial class PixelPrecisionZoom : UserControl
{
    private const double DefaultZoomFactor = 6.0;
    private const int DefaultPreviewSize = 150;

    /// <summary>
    /// Gets or sets the zoom magnification factor.
    /// </summary>
    public double ZoomFactor { get; set; } = DefaultZoomFactor;

    /// <summary>
    /// Gets or sets the source image to magnify.
    /// </summary>
    public ImageSource? SourceImage
    {
        get => sourceImage;
        set
        {
            sourceImage = value;
            UpdateZoomPreview();
        }
    }
    private ImageSource? sourceImage;

    /// <summary>
    /// Gets or sets the current mouse position in image coordinates.
    /// </summary>
    public Point CurrentPosition
    {
        get => currentPosition;
        set
        {
            currentPosition = value;
            UpdateZoomPreview();
            UpdateCoordinateDisplay();
        }
    }
    private Point currentPosition;

    public PixelPrecisionZoom()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the zoom preview to show the magnified region around the current position.
    /// </summary>
    private void UpdateZoomPreview()
    {
        if (sourceImage == null)
            return;

        if (sourceImage is not BitmapSource bitmapSource)
            return;

        // Render a fixed-size source region so the loupe retains its scale at image edges.
        int captureWidth = Math.Max(1, (int)Math.Ceiling(DefaultPreviewSize / ZoomFactor));
        int captureHeight = Math.Max(1, (int)Math.Ceiling(DefaultPreviewSize / ZoomFactor));
        int originX = (int)Math.Floor(currentPosition.X - (captureWidth / 2.0));
        int originY = (int)Math.Floor(currentPosition.Y - (captureHeight / 2.0));

        int sourceLeft = Math.Clamp(originX, 0, bitmapSource.PixelWidth);
        int sourceTop = Math.Clamp(originY, 0, bitmapSource.PixelHeight);
        int sourceRight = Math.Clamp(originX + captureWidth, 0, bitmapSource.PixelWidth);
        int sourceBottom = Math.Clamp(originY + captureHeight, 0, bitmapSource.PixelHeight);
        int sourceWidth = sourceRight - sourceLeft;
        int sourceHeight = sourceBottom - sourceTop;

        RenderTargetBitmap preview = new(
            captureWidth,
            captureHeight,
            bitmapSource.DpiX,
            bitmapSource.DpiY,
            PixelFormats.Pbgra32);
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.Black, null, new Rect(0, 0, captureWidth, captureHeight));
            if (sourceWidth > 0 && sourceHeight > 0)
            {
                Int32Rect sourceRect = new(
                    sourceLeft,
                    sourceTop,
                    sourceWidth,
                    sourceHeight);
                CroppedBitmap croppedBitmap = new(bitmapSource, sourceRect);
                context.DrawImage(
                    croppedBitmap,
                    new Rect(sourceLeft - originX, sourceTop - originY, sourceWidth, sourceHeight));
            }
        }

        preview.Render(visual);
        preview.Freeze();
        ZoomImage.Source = new TransformedBitmap(preview, new ScaleTransform(ZoomFactor, ZoomFactor));
    }

    /// <summary>
    /// Updates the coordinate display with the current position.
    /// </summary>
    private void UpdateCoordinateDisplay()
    {
        CoordinateText.Text = $"X: {(int)currentPosition.X}, Y: {(int)currentPosition.Y}";
    }

    /// <summary>
    /// Positions the zoom control near the cursor position without blocking the view.
    /// </summary>
    /// <param name="cursorPosition">The cursor position in parent coordinates</param>
    /// <param name="parentWidth">Width of the parent container</param>
    /// <param name="parentHeight">Height of the parent container</param>
    public void PositionNearCursor(Point cursorPosition, double parentWidth, double parentHeight)
    {
        // Offset from cursor to avoid blocking the view
        double offsetX = 40;
        double offsetY = 40;

        double left = cursorPosition.X + offsetX;
        double top = cursorPosition.Y - Height - offsetY;

        // Keep within parent bounds
        if (left + Width > parentWidth)
            left = cursorPosition.X - Width - offsetX;

        if (top < 0)
            top = cursorPosition.Y + offsetY;

        // Use Margin for positioning since control is in a Grid, not a Canvas
        Margin = new Thickness(left, top, 0, 0);
    }
}
