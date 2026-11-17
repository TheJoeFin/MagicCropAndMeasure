using ImageMagick;
using System.IO;

namespace MagickCrop.Helpers;

/// <summary>
/// An RGB copy of an image, held in memory so it can be sampled many times per second
/// without touching disk.
///
/// The app's canonical image is a temp file that is rewritten by every edit, so anything
/// that wants pixel data normally re-reads and re-decodes it. That is fine for a one-shot
/// operation and far too slow for a gesture that samples while the mouse moves — hence
/// this buffer, built once per image and rebuilt when the path changes.
/// </summary>
/// <remarks>
/// Colour is kept rather than reduced to luminance: a boundary between two colours of
/// similar brightness — yellow against blue, or red against a grey of the same lightness —
/// is plain to the eye and almost invisible in a greyscale copy.
/// </remarks>
public sealed class ImageSampleBuffer
{
    private const int Channels = 3;

    private readonly byte[] pixels;

    public int Width { get; }
    public int Height { get; }

    private ImageSampleBuffer(byte[] pixels, int width, int height)
    {
        this.pixels = pixels;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Reads an image off disk into memory. Returns null when the file cannot be read, so
    /// callers can degrade rather than throw mid-gesture.
    /// </summary>
    /// <remarks>
    /// Deliberately just a decode — no denoising pass. A blur here cost ten times as long
    /// as everything else combined (~960ms of ~1070ms on a 6MP photo) and bought nothing:
    /// <see cref="BoundaryProbeAnalyzer"/> averages a band of parallel lanes, which smooths
    /// along the boundary, and runs a Gaussian over the profile, which smooths across it.
    /// That is a separable 2D blur already, done over the few hundred samples a probe
    /// actually touches rather than over all six million pixels.
    /// </remarks>
    public static ImageSampleBuffer? FromFile(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            return null;

        try
        {
            using MagickImage image = new(imagePath);

            int width = (int)image.Width;
            int height = (int)image.Height;
            if (width <= 0 || height <= 0)
                return null;

            // ToByteArray scales this Q16 build's values to 0-255 per channel.
            byte[]? rgb = image.GetPixelsUnsafe().ToByteArray(PixelMapping.RGB);
            if (rgb is null || rgb.Length < width * height * Channels)
                return null;

            return new ImageSampleBuffer(rgb, width, height);
        }
        catch (Exception)
        {
            // A missing or half-written temp file must not take down the gesture.
            return null;
        }
    }

    /// <summary>
    /// Colour at a fractional pixel position, bilinearly interpolated. Coordinates are
    /// clamped to the image, so a sample lane that runs off the edge repeats the border
    /// rather than reading a wrapped or zero pixel — a fabricated black border would look
    /// exactly like a boundary.
    /// </summary>
    public void SampleBilinear(double x, double y, out double red, out double green, out double blue)
    {
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);

        int x0 = (int)x;
        int y0 = (int)y;
        int x1 = Math.Min(x0 + 1, Width - 1);
        int y1 = Math.Min(y0 + 1, Height - 1);

        double fx = x - x0;
        double fy = y - y0;

        int topLeft = ((y0 * Width) + x0) * Channels;
        int topRight = ((y0 * Width) + x1) * Channels;
        int bottomLeft = ((y1 * Width) + x0) * Channels;
        int bottomRight = ((y1 * Width) + x1) * Channels;

        red = Interpolate(topLeft, topRight, bottomLeft, bottomRight, 0, fx, fy);
        green = Interpolate(topLeft, topRight, bottomLeft, bottomRight, 1, fx, fy);
        blue = Interpolate(topLeft, topRight, bottomLeft, bottomRight, 2, fx, fy);
    }

    private double Interpolate(
        int topLeft, int topRight, int bottomLeft, int bottomRight,
        int channel, double fx, double fy)
    {
        double top = (pixels[topLeft + channel] * (1 - fx)) + (pixels[topRight + channel] * fx);
        double bottom = (pixels[bottomLeft + channel] * (1 - fx)) + (pixels[bottomRight + channel] * fx);

        return (top * (1 - fy)) + (bottom * fy);
    }
}
