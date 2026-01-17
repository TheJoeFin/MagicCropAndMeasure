using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MagickCrop.Models;
using MagickCrop.Services.Interfaces;
using MagickCrop.ViewModels.Base;

namespace MagickCrop.ViewModels;

/// <summary>
/// ViewModel for the Save window.
/// </summary>
public partial class SaveWindowViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialogService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _imagePath = string.Empty;

    [ObservableProperty]
    private BitmapImage? _displayImage;

    [ObservableProperty]
    private int _imageWidth;

    [ObservableProperty]
    private int _imageHeight;

    [ObservableProperty]
    private string _fileSize = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public SaveWindowViewModel() : this(
        App.GetService<IFileDialogService>(),
        App.GetService<INavigationService>())
    {
    }

    public SaveWindowViewModel(
        IFileDialogService fileDialogService,
        INavigationService navigationService)
    {
        _fileDialogService = fileDialogService;
        _navigationService = navigationService;
        Title = "Save Image";
    }

    /// <summary>
    /// Initializes the ViewModel with the image path.
    /// </summary>
    public void Initialize(string imagePath)
    {
        ImagePath = imagePath;
        LoadImage();
    }

    private void LoadImage()
    {
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
        {
            return;
        }

        try
        {
            IsLoading = true;

            // Load image with caching disabled for proper resource management
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(ImagePath);
            bitmap.EndInit();
            bitmap.Freeze(); // Make it thread-safe

            DisplayImage = bitmap;
            ImageWidth = bitmap.PixelWidth;
            ImageHeight = bitmap.PixelHeight;

            // Get file size
            var fileInfo = new FileInfo(ImagePath);
            FileSize = FormatFileSize(fileInfo.Length);
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to load image: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Copies the image to clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyToClipboard()
    {
        if (DisplayImage == null)
            return;

        try
        {
            Clipboard.SetImage(DisplayImage);
            _navigationService.ShowMessage("Image copied to clipboard.", "Success");
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to copy: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens file location in Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenFileLocation()
    {
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
            return;

        try
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{ImagePath}\"");
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to open location: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the image to a new location.
    /// </summary>
    [RelayCommand]
    private async Task SaveAs()
    {
        var filter = "JPEG Image|*.jpg|PNG Image|*.png|All Files|*.*";
        var defaultName = Path.GetFileName(ImagePath);
        
        var savePath = _fileDialogService.ShowSaveFileDialog(filter, defaultName, "Save Image As");
        
        if (string.IsNullOrEmpty(savePath))
            return;

        try
        {
            IsLoading = true;
            await Task.Run(() => File.Copy(ImagePath, savePath, overwrite: true));
            _navigationService.ShowMessage($"Image saved to:\n{savePath}", "Saved");
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to save: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Gets data for drag operations.
    /// </summary>
    public DataObject GetDragData()
    {
        var dataObject = new DataObject();
        
        if (!string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath))
        {
            // Add file path for file drop
            dataObject.SetFileDropList(new System.Collections.Specialized.StringCollection { ImagePath });
            
            // Add image data for apps that accept images directly
            if (DisplayImage != null)
            {
                dataObject.SetImage(DisplayImage);
            }
        }

        return dataObject;
    }

    /// <summary>
    /// Cleanup resources.
    /// </summary>
    public override void Cleanup()
    {
        base.Cleanup();
        
        // Clear the image reference
        DisplayImage = null;
        
        // Request garbage collection for the bitmap
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}
