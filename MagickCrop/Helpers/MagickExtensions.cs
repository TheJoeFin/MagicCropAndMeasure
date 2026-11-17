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
    /// Writes the image to a unique temp file with a real extension and an explicit encoder.
    /// </summary>
    internal static async Task<string> WriteToTempFileAsync(this IMagickImage<ushort> image, MagickFormat? format = null)
    {
        MagickFormat targetFormat = format ?? image.GetSafeWriteFormat();
        string tempFileName = CreateTempFileName(targetFormat);

        try
        {
            image.Format = targetFormat;
            await image.WriteAsync(tempFileName, targetFormat);
            return tempFileName;
        }
        catch
        {
            DeleteIncompleteTempFile(tempFileName);
            throw;
        }
    }

    /// <inheritdoc cref="WriteToTempFileAsync"/>
    internal static string WriteToTempFile(this IMagickImage<ushort> image, MagickFormat? format = null)
    {
        MagickFormat targetFormat = format ?? image.GetSafeWriteFormat();
        string tempFileName = CreateTempFileName(targetFormat);

        try
        {
            image.Format = targetFormat;
            image.Write(tempFileName, targetFormat);
            return tempFileName;
        }
        catch
        {
            DeleteIncompleteTempFile(tempFileName);
            throw;
        }
    }

    private static string CreateTempFileName(MagickFormat format)
    {
        string extension = format.ToString().ToLowerInvariant();
        return Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.{extension}");
    }

    private static void DeleteIncompleteTempFile(string tempFileName)
    {
        try
        {
            if (File.Exists(tempFileName))
                File.Delete(tempFileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Could not delete incomplete temp image '{tempFileName}': {ex.Message}");
        }
    }
}
