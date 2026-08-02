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

    internal static async Task<string> WriteToTempFileAsync(this MagickImage image)
    {
        string tempFileName = CreateTempPngPath();

        try
        {
            await image.WriteAsync(tempFileName, MagickFormat.Png);
            return tempFileName;
        }
        catch
        {
            File.Delete(tempFileName);
            throw;
        }
    }

    internal static string WriteToTempFile(this MagickImage image)
    {
        string tempFileName = CreateTempPngPath();

        try
        {
            image.Write(tempFileName, MagickFormat.Png);
            return tempFileName;
        }
        catch
        {
            File.Delete(tempFileName);
            throw;
        }
    }

    private static string CreateTempPngPath()
    {
        return Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
    }
}
