using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.Models;

/// <summary>
/// Metadata about a MagickCrop measurement package
/// </summary>
public partial class PackageMetadata : ObservableObject
{
    [ObservableProperty]
    private int _formatVersion = 1;

    [ObservableProperty]
    private DateTime _creationDate = DateTime.Now;

    [ObservableProperty]
    private string? _originalFilename;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _projectId;

    [ObservableProperty]
    private DateTime _lastModified = DateTime.Now;

    [ObservableProperty]
    private Size _originalImageSize = new();

    [ObservableProperty]
    private Size _currentImageSize = new();

    [ObservableProperty]
    private Stretch _imageStretch = Stretch.Uniform;

    /// <summary>
    /// Updates the modified date to now.
    /// </summary>
    public void MarkAsModified()
    {
        LastModified = DateTime.Now;
    }

    /// <summary>
    /// Gets whether the image has been resized from original.
    /// </summary>
    public bool IsResized => OriginalImageSize.Width != CurrentImageSize.Width || 
                             OriginalImageSize.Height != CurrentImageSize.Height;
}
