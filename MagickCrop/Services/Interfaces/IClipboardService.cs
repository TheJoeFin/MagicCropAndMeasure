using System.Windows.Media.Imaging;

namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Service for clipboard operations.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Gets whether an image is available on the clipboard.
    /// </summary>
    bool ContainsImage();

    /// <summary>
    /// Gets whether a file drop list is available on the clipboard.
    /// </summary>
    bool ContainsFileDropList();

    /// <summary>
    /// Gets an image from the clipboard.
    /// </summary>
    /// <returns>The image, or null if not available.</returns>
    BitmapSource? GetImage();

    /// <summary>
    /// Gets file paths from the clipboard.
    /// </summary>
    /// <returns>List of file paths, or empty if not available.</returns>
    IReadOnlyList<string> GetFileDropList();

    /// <summary>
    /// Sets an image to the clipboard.
    /// </summary>
    /// <param name="image">The image to set.</param>
    void SetImage(BitmapSource image);

    /// <summary>
    /// Copies text to the clipboard.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    void SetText(string text);
}
