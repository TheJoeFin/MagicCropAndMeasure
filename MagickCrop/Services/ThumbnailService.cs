using MagickCrop.Services.Interfaces;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MagickCrop.Services;

/// <summary>
/// Provides thumbnail generation and management functionality
/// </summary>
public class ThumbnailService : IThumbnailService
{
    private readonly IAppPaths _appPaths;

    public ThumbnailService(IAppPaths appPaths)
    {
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
    }

    /// <summary>
    /// Creates a thumbnail from a BitmapSource image
    /// </summary>
    /// <param name="imageSource">Source image to create thumbnail from</param>
    /// <param name="projectId">Project ID for naming the thumbnail file</param>
    /// <param name="thumbnailWidth">Width of the thumbnail in pixels (default: 200)</param>
    /// <returns>Path to the created thumbnail file, or empty string if creation failed</returns>
    public string CreateThumbnail(BitmapSource imageSource, string projectId, int thumbnailWidth = 200)
    {
        if (imageSource == null || string.IsNullOrEmpty(projectId))
            return string.Empty;

        string thumbnailPath = _appPaths.GetThumbnailFilePath(projectId);

        try
        {
            // Create a smaller version for the thumbnail
            double scale = thumbnailWidth / imageSource.Width;
            int thumbnailHeight = (int)(imageSource.Height * scale);

            TransformedBitmap resizedImage = new(
                imageSource,
                new ScaleTransform(scale, scale)
            );

            JpegBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(resizedImage));

            using (FileStream fileStream = new(thumbnailPath, FileMode.Create))
            {
                encoder.Save(fileStream);
            }

            return thumbnailPath;
        }
        catch
        {
            return string.Empty;
        }
    }
}
