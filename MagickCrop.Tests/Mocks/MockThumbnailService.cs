using System.Windows.Media.Imaging;
using MagickCrop.Services.Interfaces;

namespace MagickCrop.Tests.Mocks;

/// <summary>
/// Mock implementation of IThumbnailService for testing
/// </summary>
public class MockThumbnailService : IThumbnailService
{
    public List<(BitmapSource Source, string ProjectId, int Width)> CreatedThumbnails { get; } = [];

    public string CreateThumbnail(BitmapSource imageSource, string projectId, int thumbnailWidth = 200)
    {
        CreatedThumbnails.Add((imageSource, projectId, thumbnailWidth));
        return $"/mock/thumbnail/{projectId}.jpg";
    }
}
