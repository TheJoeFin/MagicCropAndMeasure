using ImageMagick;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;

namespace MagickCrop.Helpers;

internal static class ObjectEraseHelper
{
    /// <summary>
    /// Checks whether the Object Erase AI feature is supported on this device.
    /// Returns <c>false</c> if the API is unavailable (e.g. not a Copilot+ PC).
    /// </summary>
    internal static bool IsSupported()
    {
        try
        {
            _ = ImageObjectRemover.GetReadyState();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ensures the Object Erase model is downloaded and ready.
    /// </summary>
    internal static async Task EnsureReadyAsync()
    {
        if (ImageObjectRemover.GetReadyState() == AIFeatureReadyState.NotReady)
        {
            AIFeatureReadyResult result = await ImageObjectRemover.EnsureReadyAsync();
            if (result.Status != AIFeatureReadyResultState.Success)
                throw result.ExtendedError;
        }
    }

    /// <summary>
    /// Erases the masked region from the image and returns the path to a temp file containing the result.
    /// Must be called on the UI thread (mask rendering uses WPF drawing).
    /// </summary>
    /// <param name="imagePath">Path to the source image on disk.</param>
    /// <param name="maskStrokes">InkCanvas strokes that define the erase mask.</param>
    /// <param name="displayWidth">Display width of the image in the UI.</param>
    /// <param name="displayHeight">Display height of the image in the UI.</param>
    /// <returns>Path to a temp file containing the erased image.</returns>
    internal static async Task<string> EraseObjectsAsync(
        string imagePath,
        StrokeCollection maskStrokes,
        double displayWidth,
        double displayHeight)
    {
        await EnsureReadyAsync();

        using MagickImage sourceImage = new(imagePath);
        int imageWidth = (int)sourceImage.Width;
        int imageHeight = (int)sourceImage.Height;

        // Convert source image to SoftwareBitmap
        SoftwareBitmap imageBitmap = MagickImageToSoftwareBitmap(sourceImage);

        // Create Gray8 mask from InkCanvas strokes (must run on UI thread)
        SoftwareBitmap maskBitmap = CreateMaskFromStrokes(
            maskStrokes, displayWidth, displayHeight, imageWidth, imageHeight);

        // Perform the erase
        using ImageObjectRemover remover = await ImageObjectRemover.CreateAsync();
        SoftwareBitmap resultBitmap = remover.RemoveFromSoftwareBitmap(imageBitmap, maskBitmap);

        // Save result to a temp file via MagickImage
        string tempPath = await Task.Run(() => SoftwareBitmapToTempFile(resultBitmap));

        imageBitmap.Dispose();
        maskBitmap.Dispose();
        resultBitmap.Dispose();

        return tempPath;
    }

    private static SoftwareBitmap MagickImageToSoftwareBitmap(MagickImage image)
    {
        // Ensure BGRA byte order
        byte[] bgra = image.GetPixelsUnsafe().ToByteArray(PixelMapping.BGRA)
            ?? throw new InvalidOperationException("Failed to read pixel data from image.");

        SoftwareBitmap sb = new(
            BitmapPixelFormat.Bgra8,
            (int)image.Width,
            (int)image.Height,
            BitmapAlphaMode.Premultiplied);

        sb.CopyFromBuffer(bgra.AsBuffer());
        return sb;
    }

    private static SoftwareBitmap CreateMaskFromStrokes(
        StrokeCollection strokes,
        double displayWidth,
        double displayHeight,
        int imageWidth,
        int imageHeight)
    {
        double scaleX = imageWidth / displayWidth;
        double scaleY = imageHeight / displayHeight;

        // Render strokes at image resolution
        DrawingVisual visual = new();
        using (DrawingContext ctx = visual.RenderOpen())
        {
            ctx.PushTransform(new ScaleTransform(scaleX, scaleY));
            foreach (Stroke stroke in strokes)
            {
                stroke.Draw(ctx);
            }
            ctx.Pop();
        }

        RenderTargetBitmap rtb = new(imageWidth, imageHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        // Convert BGRA → Gray8: any visible pixel → 255 (erase), transparent → 0 (keep)
        int pixelCount = imageWidth * imageHeight;
        byte[] rgbaPixels = new byte[pixelCount * 4];
        rtb.CopyPixels(rgbaPixels, imageWidth * 4, 0);

        byte[] grayPixels = new byte[pixelCount];
        for (int i = 0; i < pixelCount; i++)
        {
            // Alpha channel is at offset +3 for each BGRA pixel
            grayPixels[i] = rgbaPixels[i * 4 + 3] > 0 ? (byte)255 : (byte)0;
        }

        SoftwareBitmap maskBitmap = new(
            BitmapPixelFormat.Gray8,
            imageWidth,
            imageHeight,
            BitmapAlphaMode.Ignore);

        maskBitmap.CopyFromBuffer(grayPixels.AsBuffer());
        return maskBitmap;
    }

    private static string SoftwareBitmapToTempFile(SoftwareBitmap bitmap)
    {
        using SoftwareBitmap converted = SoftwareBitmap.Convert(
            bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);

        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[height * stride];
        converted.CopyToBuffer(pixels.AsBuffer());

        PixelReadSettings settings = new(
            (uint)width, (uint)height, StorageType.Char, PixelMapping.BGRA);
        using MagickImage result = new();
        result.ReadPixels(pixels, settings);

        string tempPath = Path.GetTempFileName();
        result.Write(tempPath, MagickFormat.Png);
        return tempPath;
    }
}
