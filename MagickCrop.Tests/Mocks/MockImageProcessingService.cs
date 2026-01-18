using System.Collections.Generic;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace MagickCrop.Tests.Mocks;

/// <summary>
/// Mock implementation of IImageProcessingService for testing
/// </summary>
public class MockImageProcessingService : IImageProcessingService
{
    public List<string> LoadedFiles { get; } = [];
    public List<(string Path, MagickFormat Format, int Quality)> SavedFiles { get; } = [];

    public MagickImage? LastRotatedImage { get; private set; }
    public double? LastRotationDegrees { get; private set; }

    public Task<MagickImage?> LoadImageAsync(string filePath)
    {
        LoadedFiles.Add(filePath);
        // Return null - tests can override by setting up return values
        return Task.FromResult<MagickImage?>(null);
    }

    public Task<bool> SaveImageAsync(MagickImage image, string filePath, MagickFormat format, int quality = 90)
    {
        SavedFiles.Add((filePath, format, quality));
        return Task.FromResult(true);
    }

    public MagickImage Rotate(MagickImage image, double degrees)
    {
        LastRotatedImage = image;
        LastRotationDegrees = degrees;
        return image;
    }

    public MagickImage Crop(MagickImage image, int x, int y, int width, int height)
    {
        return image;
    }

    public MagickImage Resize(MagickImage image, int width, int height)
    {
        return image;
    }

    public MagickImage ApplyPerspectiveCorrection(MagickImage image, double[] sourcePoints, double[] targetPoints)
    {
        return image;
    }

    public BitmapSource ToBitmapSource(MagickImage image)
    {
        // Return a dummy BitmapSource - tests should mock this properly if needed
        return new WriteableBitmap(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
    }

    public MagickImage FromBitmapSource(BitmapSource bitmapSource)
    {
        // Return a dummy 1x1 image - tests should mock this properly if needed
        return new MagickImage(MagickColors.White, 1, 1);
    }

    public void FlipHorizontal(MagickImage image)
    {
        // No-op for testing
    }

    public void FlipVertical(MagickImage image)
    {
        // No-op for testing
    }
}
