using System.Windows.Media.Imaging;

namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Service for creating and managing thumbnail images
/// </summary>
public interface IThumbnailService
{
    /// <summary>
    /// Creates a thumbnail from a BitmapSource image
    /// </summary>
    /// <param name="imageSource">Source image to create thumbnail from</param>
    /// <param name="projectId">Project ID for naming the thumbnail file</param>
    /// <param name="thumbnailWidth">Width of the thumbnail in pixels (default: 200)</param>
    /// <returns>Path to the created thumbnail file, or empty string if creation failed</returns>
    string CreateThumbnail(BitmapSource imageSource, string projectId, int thumbnailWidth = 200);
}
