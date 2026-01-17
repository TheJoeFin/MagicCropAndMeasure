using System.Windows.Media.Imaging;
using ImageMagick;

namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Service for image processing operations.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Loads an image from a file path.
    /// </summary>
    Task<MagickImage?> LoadImageAsync(string filePath);

    /// <summary>
    /// Saves an image to a file.
    /// </summary>
    Task<bool> SaveImageAsync(MagickImage image, string filePath, MagickFormat format, int quality = 90);

    /// <summary>
    /// Rotates an image by the specified degrees.
    /// </summary>
    MagickImage Rotate(MagickImage image, double degrees);

    /// <summary>
    /// Crops an image to the specified region.
    /// </summary>
    MagickImage Crop(MagickImage image, int x, int y, int width, int height);

    /// <summary>
    /// Resizes an image to the specified dimensions.
    /// </summary>
    MagickImage Resize(MagickImage image, int width, int height);

    /// <summary>
    /// Applies perspective correction to an image.
    /// </summary>
    MagickImage ApplyPerspectiveCorrection(MagickImage image, double[] sourcePoints, double[] targetPoints);

    /// <summary>
    /// Converts a MagickImage to a WPF BitmapSource.
    /// </summary>
    BitmapSource ToBitmapSource(MagickImage image);

    /// <summary>
    /// Converts a WPF BitmapSource to a MagickImage.
    /// </summary>
    MagickImage FromBitmapSource(BitmapSource bitmapSource);

    /// <summary>
    /// Flips an image horizontally.
    /// </summary>
    void FlipHorizontal(MagickImage image);

    /// <summary>
    /// Flips an image vertically.
    /// </summary>
    void FlipVertical(MagickImage image);
}
