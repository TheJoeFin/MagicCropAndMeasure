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

        try
        {
            // Create a RenderTargetBitmap to capture the source image
            if (sourceImage is BitmapSource bitmapSource)
            {
                // Calculate the region to capture (centered on current position)
                double captureWidth = DefaultPreviewSize / ZoomFactor;
                double captureHeight = DefaultPreviewSize / ZoomFactor;

                // Create a cropped version of the source
                Int32Rect sourceRect = new Int32Rect(
                    (int)Math.Max(0, currentPosition.X - captureWidth / 2),
                    (int)Math.Max(0, currentPosition.Y - captureHeight / 2),
                    (int)Math.Min(captureWidth, bitmapSource.PixelWidth - (currentPosition.X - captureWidth / 2)),
                    (int)Math.Min(captureHeight, bitmapSource.PixelHeight - (currentPosition.Y - captureHeight / 2))
                );

                // Ensure valid rectangle
                if (sourceRect.Width > 0 && sourceRect.Height > 0 &&
                    sourceRect.X >= 0 && sourceRect.Y >= 0 &&
                    sourceRect.X + sourceRect.Width <= bitmapSource.PixelWidth &&
                    sourceRect.Y + sourceRect.Height <= bitmapSource.PixelHeight)
                {
                    CroppedBitmap croppedBitmap = new CroppedBitmap(bitmapSource, sourceRect);

                    // Apply scaling transform
                    TransformedBitmap transformedBitmap = new TransformedBitmap(croppedBitmap, new ScaleTransform(ZoomFactor, ZoomFactor));

                    ZoomImage.Source = transformedBitmap;
                }
            }
        }
        catch (Exception)
        {
            // Silently handle any rendering errors
        }
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

        Canvas.SetLeft(this, left);
        Canvas.SetTop(this, top);
    }
}
