using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;
using MagickCrop.Services.Interfaces;

namespace MagickCrop.Services;

/// <summary>
/// Service for image processing operations using ImageMagick.
/// </summary>
public class ImageProcessingService : IImageProcessingService
{
    public async Task<MagickImage?> LoadImageAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        return await Task.Run(() =>
        {
            try
            {
                var image = new MagickImage(filePath);
                image.AutoOrient(); // Apply EXIF orientation
                return image;
            }
            catch
            {
                return null;
            }
        });
    }

    public async Task<bool> SaveImageAsync(MagickImage image, string filePath, MagickFormat format, int quality = 90)
    {
        return await Task.Run(() =>
        {
            try
            {
                image.Format = format;
                image.Quality = (uint)quality;
                image.Write(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public MagickImage Rotate(MagickImage image, double degrees)
    {
        var clone = (MagickImage)image.Clone();
        clone.Rotate(degrees);
        return clone;
    }

    public MagickImage Crop(MagickImage image, int x, int y, int width, int height)
    {
        var clone = (MagickImage)image.Clone();
        var geometry = new MagickGeometry(x, y, (uint)width, (uint)height);
        clone.Crop(geometry);
        return clone;
    }

    public MagickImage Resize(MagickImage image, int width, int height)
    {
        var clone = (MagickImage)image.Clone();
        clone.Resize((uint)width, (uint)height);
        return clone;
    }

    public MagickImage ApplyPerspectiveCorrection(MagickImage image, double[] sourcePoints, double[] targetPoints)
    {
        var clone = (MagickImage)image.Clone();
        clone.Distort(DistortMethod.Perspective, sourcePoints.Concat(targetPoints).ToArray());
        return clone;
    }

    public BitmapSource ToBitmapSource(MagickImage image)
    {
        using var memoryStream = new MemoryStream();
        image.Write(memoryStream, MagickFormat.Png);
        memoryStream.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = memoryStream;
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }

    public MagickImage FromBitmapSource(BitmapSource bitmapSource)
    {
        using var memoryStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
        encoder.Save(memoryStream);
        memoryStream.Position = 0;

        var image = new MagickImage(memoryStream);
        return image;
    }

    public void FlipHorizontal(MagickImage image)
    {
        image.Flop();
    }

    public void FlipVertical(MagickImage image)
    {
        image.Flip();
    }
}
