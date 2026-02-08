using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using Windows.ApplicationModel;
using Wpf.Ui.Controls;

namespace MagickCrop.Windows;

public partial class AboutWindow : FluentWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionTextBlock.Text = $"Version {GetAppVersion()}";
        PopulateLibraryVersions();
    }

    private void PopulateLibraryVersions()
    {
        MagickVersionText.Text = GetAssemblyVersion("Magick.NET-Q16-AnyCPU");
        EmguCvVersionText.Text = GetAssemblyVersion("Emgu.CV");
        WpfUiVersionText.Text = GetAssemblyVersion("Wpf.Ui");
    }

    private static string GetAssemblyVersion(string assemblyName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name == assemblyName)
            {
                Version? version = assembly.GetName().Version;
                if (version is not null)
                    return $"v{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        return string.Empty;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private static string GetAppVersion()
    {
        PackageVersion version = Package.Current.Id.Version;
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private void RatingControl_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not RatingControl ratingControl)
            return;

        double value = ratingControl.Value;

        if (value <= 0)
            return;

        if (value <= 3)
        {
            string subject = Uri.EscapeDataString("Magick Crop & Measure Feedback");
            string body = Uri.EscapeDataString($"Rating: {value}/5\nVersion: {GetAppVersion()}\n\nFeedback:\n");
            Process.Start(new ProcessStartInfo($"mailto:joe@joefinapps.com?subject={subject}&body={body}") { UseShellExecute = true });
        }
        else
        {
            Process.Start(new ProcessStartInfo("ms-windows-store://review/?ProductId=9pf6zx8rj8q2") { UseShellExecute = true });
        }
    }
}
