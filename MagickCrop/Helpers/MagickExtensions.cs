using ImageMagick;
using System.IO;

namespace MagickCrop;

public static class MagickExtensions
{
    internal static void ScaleAll(this MagickGeometry geometry, double factor)
    {
        geometry.X = (int)(geometry.X * factor);
        geometry.Y = (int)(geometry.Y * factor);
        geometry.Width = (uint)(geometry.Width * factor);
        geometry.Height = (uint)(geometry.Height * factor);
    }

    /// <summary>
    /// Picks an encoder that can actually represent the image. Falls back to PNG when the
    /// image has no known format, when the format has no encoder, or when the image carries
    /// transparency that the current format cannot store.
    /// </summary>
    internal static MagickFormat GetSafeWriteFormat(this IMagickImage<ushort> image)
    {
        MagickFormat format = image.Format;

        if (format is MagickFormat.Unknown)
            return MagickFormat.Png;

        if (image.HasAlpha && format is MagickFormat.Jpeg or MagickFormat.Jpg or MagickFormat.Jpe or MagickFormat.Bmp)
            return MagickFormat.Png;

        IMagickFormatInfo? formatInfo = MagickFormatInfo.Create(format);

        if (formatInfo is null || !formatInfo.SupportsWriting)
            return MagickFormat.Png;

        return format;
    }

    /// <summary>
    /// Writes the image to a new temp file using an encoder that is guaranteed to exist.
    /// <see cref="Path.GetTempFileName"/> hands back a ".tmp" name and ImageMagick resolves the
    /// encoder from the extension, which yields <see cref="MagickFormat.Unknown"/> and throws
    /// <c>no encode delegate for this image format</c>. This gives the temp file a real
    /// extension and passes the format explicitly.
    /// </summary>
    internal static async Task<string> WriteToTempFileAsync(this IMagickImage<ushort> image, MagickFormat? format = null)
    {
        MagickFormat targetFormat = format ?? image.GetSafeWriteFormat();
        string tempFileName = CreateTempFileName(targetFormat);

        image.Format = targetFormat;
        await image.WriteAsync(tempFileName, targetFormat);

        return tempFileName;
    }

    /// <inheritdoc cref="WriteToTempFileAsync"/>
    internal static string WriteToTempFile(this IMagickImage<ushort> image, MagickFormat? format = null)
    {
        MagickFormat targetFormat = format ?? image.GetSafeWriteFormat();
        string tempFileName = CreateTempFileName(targetFormat);

        image.Format = targetFormat;
        image.Write(tempFileName, targetFormat);

        return tempFileName;
    }

    private static string CreateTempFileName(MagickFormat format)
    {
        string tempFileName = Path.GetTempFileName();
        string withExtension = Path.ChangeExtension(tempFileName, format.ToString().ToLowerInvariant());

        if (!string.Equals(tempFileName, withExtension, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                File.Delete(tempFileName);
            }
            catch (IOException)
            {
                // Leaving the zero-byte placeholder behind is harmless.
            }
        }

        return withExtension;
    }
}
