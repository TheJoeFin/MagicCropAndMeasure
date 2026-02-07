using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.Models;

/// <summary>
/// Information about a recent project for display in the UI.
/// </summary>
public partial class RecentProjectInfo : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _packagePath = string.Empty;

    [ObservableProperty]
    private DateTime _lastModified = DateTime.Now;

    [ObservableProperty]
    private string _thumbnailPath = string.Empty;

    private BitmapImage? _thumbnail;

    /// <summary>
    /// Gets or sets the thumbnail image for the project.
    /// Excluded from JSON serialization.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public BitmapImage? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    /// <summary>
    /// Gets the formatted last modified time for display.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string LastModifiedFormatted => FormatRelativeTime(LastModified);

    /// <summary>
    /// Loads the thumbnail image on the UI thread
    /// </summary>
    public void LoadThumbnail()
    {
        if (string.IsNullOrEmpty(ThumbnailPath) || !File.Exists(ThumbnailPath))
            return;

        // Ensure we're on the UI thread since BitmapImage must be created there
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => LoadThumbnail());
            return;
        }

        try
        {
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(ThumbnailPath);
            bitmap.EndInit();
            bitmap.Freeze(); // Make it thread-safe for cross-thread access
            Thumbnail = bitmap;
        }
        catch
        {
            // If loading fails, we'll just have no thumbnail
            Thumbnail = null;
        }
    }

    /// <summary>
    /// Formats a relative time string for display.
    /// </summary>
    private static string FormatRelativeTime(DateTime dateTime)
    {
        TimeSpan span = DateTime.Now - dateTime;

        if (span.TotalDays > 30)
        {
            return $"Edited {dateTime:MMM d, yyyy}";
        }
        if (span.TotalDays > 1)
        {
            return $"Edited {(int)span.TotalDays} days ago";
        }
        if (span.TotalHours > 1)
        {
            return $"Edited {(int)span.TotalHours} hours ago";
        }
        if (span.TotalMinutes > 1)
        {
            return $"Edited {(int)span.TotalMinutes} minutes ago";
        }

        return "Edited just now";
    }
}
