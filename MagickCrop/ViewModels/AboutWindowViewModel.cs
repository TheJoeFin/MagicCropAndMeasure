using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MagickCrop.ViewModels.Base;
using Windows.ApplicationModel;

namespace MagickCrop.ViewModels;

/// <summary>
/// ViewModel for the About window.
/// </summary>
public partial class AboutWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _appName = "Magic Crop & Measure";

    [ObservableProperty]
    private string _version = string.Empty;

    [ObservableProperty]
    private string _copyright = string.Empty;

    [ObservableProperty]
    private string _description = "A WPF application for image cropping and measurement.";

    public AboutWindowViewModel()
    {
        Title = "About";
        LoadVersionInfo();
    }

    private void LoadVersionInfo()
    {
        try
        {
            // Try to get version from package (MSIX)
            var package = Package.Current;
            var packageVersion = package.Id.Version;
            Version = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}";
        }
        catch
        {
            // Fallback to assembly version
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version;
            Version = assemblyVersion?.ToString(3) ?? "1.0.0";
        }

        Copyright = $"© {DateTime.Now.Year} Joe Finney";
    }

    /// <summary>
    /// Opens the GitHub repository in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenGitHub()
    {
        OpenUrl("https://github.com/TheJoeFin/MagicCropAndMeasure");
    }

    /// <summary>
    /// Opens the creator's website in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenWebsite()
    {
        OpenUrl("https://joefinapps.com");
    }

    /// <summary>
    /// Opens the ImageMagick website in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenImageMagick()
    {
        OpenUrl("https://imagemagick.org");
    }

    /// <summary>
    /// Opens the Magick.NET GitHub in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenMagickNet()
    {
        OpenUrl("https://github.com/dlemstra/Magick.NET");
    }

    /// <summary>
    /// Opens the WPF-UI GitHub in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenWpfUi()
    {
        OpenUrl("https://github.com/lepoco/wpfui");
    }

    /// <summary>
    /// Closes the window.
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        // This will be handled by the view - the command just signals intent
        // The view will close itself when this command executes
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Silently fail if browser can't be opened
        }
    }
}
