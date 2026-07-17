using ImageMagick;
using MagickCrop.Models;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MagickCrop.Controls;

public partial class SaveOptionsDialog : UserControl
{
    private static readonly List<FormatItem> _formats =
    [
        new FormatItem { Name = "PNG Image", Format = MagickFormat.Png, Extension = ".png", SupportsQuality = false },
        new FormatItem { Name = "JPEG Image", Format = MagickFormat.Jpg, Extension = ".jpg", SupportsQuality = true },
        new FormatItem { Name = "BMP Image", Format = MagickFormat.Bmp, Extension = ".bmp", SupportsQuality = false },
        new FormatItem { Name = "TIFF Image", Format = MagickFormat.Tiff, Extension = ".tiff", SupportsQuality = false },
        new FormatItem { Name = "WebP Image", Format = MagickFormat.WebP, Extension = ".webp", SupportsQuality = true },
        // new FormatItem { Name = "HEIC Image", Format = MagickFormat.Heic, Extension = ".heic", SupportsQuality = true }
    ];

    private double originalWidth;
    private double originalHeight;
    private double aspectRatio;
    private bool updatingDimensions = false;
    private readonly Func<SaveOptions, CancellationToken, Task<long>> estimateFileSizeAsync;
    private readonly DispatcherTimer estimateDebounceTimer;
    private readonly SemaphoreSlim estimateGate = new(1, 1);
    private CancellationTokenSource? estimateCancellation;
    private int estimateRequestId;

    public SaveOptions Options { get; private set; }

    public SaveOptionsDialog(
        double imageWidth,
        double imageHeight,
        Func<SaveOptions, CancellationToken, Task<long>> estimateFileSizeAsync)
    {
        InitializeComponent();
        this.estimateFileSizeAsync = estimateFileSizeAsync;
        estimateDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        estimateDebounceTimer.Tick += EstimateDebounceTimer_Tick;
        Loaded += SaveOptionsDialog_Loaded;
        Unloaded += SaveOptionsDialog_Unloaded;

        // Store original dimensions and calculate aspect ratio
        originalWidth = imageWidth;
        originalHeight = imageHeight;
        aspectRatio = originalHeight / originalWidth;

        // Initialize format dropdown
        FormatComboBox.ItemsSource = _formats;
        FormatComboBox.SelectedIndex = 0;

        // Set initial dimensions
        WidthBox.Value = originalWidth;
        HeightBox.Value = originalHeight;

        // Initialize options object
        Options = new SaveOptions
        {
            Format = MagickFormat.Png,
            Extension = ".png",
            Quality = (int)QualitySlider.Value,
            Resize = false,
            Width = (int)originalWidth,
            Height = (int)originalHeight,
            MaintainAspectRatio = true,
            IncludeMarkup = false,
            IncludeMeasurements = false
        };
    }

    private void SaveOptionsDialog_Loaded(object sender, RoutedEventArgs e) => ScheduleSizeEstimate();

    private void SaveOptionsDialog_Unloaded(object sender, RoutedEventArgs e)
    {
        estimateDebounceTimer.Stop();
        estimateCancellation?.Cancel();
    }

    private void FormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        if (FormatComboBox.SelectedItem is FormatItem selectedFormat)
        {
            Options.Format = selectedFormat.Format;
            Options.Extension = selectedFormat.Extension;

            // Show/hide quality slider based on format
            QualityGrid.Visibility = selectedFormat.SupportsQuality ? Visibility.Visible : Visibility.Collapsed;
            ScheduleSizeEstimate();
        }
    }

    private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;

        int quality = (int)QualitySlider.Value;
        QualityValueText.Text = $"{quality}%";
        Options.Quality = quality;
        ScheduleSizeEstimate();
    }

    private void ResizeCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        bool isChecked = ResizeCheckBox.IsChecked == true;

        WidthBox.IsEnabled = isChecked;
        HeightBox.IsEnabled = isChecked;
        MaintainAspectRatioCheckBox.IsEnabled = isChecked;

        Options.Resize = isChecked;
        ScheduleSizeEstimate();
    }

    private void WidthBox_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        if (updatingDimensions || WidthBox.Value == null)
            return;

        Options.Width = (int)WidthBox.Value.Value;

        if (MaintainAspectRatioCheckBox.IsChecked == true)
        {
            updatingDimensions = true;
            HeightBox.Value = (int)(WidthBox.Value.Value * aspectRatio);
            Options.Height = (int)HeightBox.Value.Value;
            updatingDimensions = false;
        }

        ScheduleSizeEstimate();
    }

    private void HeightBox_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        if (updatingDimensions || HeightBox.Value == null)
            return;

        Options.Height = (int)HeightBox.Value.Value;

        if (MaintainAspectRatioCheckBox.IsChecked == true)
        {
            updatingDimensions = true;
            WidthBox.Value = (int)(HeightBox.Value.Value / aspectRatio);
            Options.Width = (int)WidthBox.Value.Value;
            updatingDimensions = false;
        }

        ScheduleSizeEstimate();
    }

    private void IncludeMarkupCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        Options.IncludeMarkup = IncludeMarkupCheckBox.IsChecked == true;
        ScheduleSizeEstimate();
    }

    private void IncludeMeasurementsCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        Options.IncludeMeasurements = IncludeMeasurementsCheckBox.IsChecked == true;
        ScheduleSizeEstimate();
    }

    private void ScheduleSizeEstimate()
    {
        if (!IsLoaded)
            return;

        estimateCancellation?.Cancel();
        estimateDebounceTimer.Stop();
        estimateDebounceTimer.Start();
        EstimatedSizeText.Text = "Estimated file size: Calculating...";
    }

    private async void EstimateDebounceTimer_Tick(object? sender, EventArgs e)
    {
        estimateDebounceTimer.Stop();
        estimateCancellation?.Cancel();

        CancellationTokenSource cancellation = new();
        estimateCancellation = cancellation;
        int requestId = ++estimateRequestId;
        bool enteredEstimateGate = false;

        try
        {
            await estimateGate.WaitAsync(cancellation.Token);
            enteredEstimateGate = true;
            long size = await estimateFileSizeAsync(Options.Clone(), cancellation.Token);
            if (!cancellation.IsCancellationRequested && requestId == estimateRequestId && IsLoaded)
                EstimatedSizeText.Text = $"Estimated file size: {FormatFileSize(size)}";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to estimate saved image size: {ex}");
            if (requestId == estimateRequestId && IsLoaded)
                EstimatedSizeText.Text = "Estimated file size: Unavailable";
        }
        finally
        {
            if (enteredEstimateGate)
                estimateGate.Release();

            if (ReferenceEquals(estimateCancellation, cancellation))
                estimateCancellation = null;

            cancellation.Dispose();
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{size:N0} {units[unitIndex]}" : $"{size:N1} {units[unitIndex]}";
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Update final options
        Options.MaintainAspectRatio = MaintainAspectRatioCheckBox.IsChecked == true;
        Options.IncludeMarkup = IncludeMarkupCheckBox.IsChecked == true;
        Options.IncludeMeasurements = IncludeMeasurementsCheckBox.IsChecked == true;
        estimateCancellation?.Cancel();

        if (WidthBox.Value is 0 || HeightBox.Value is 0)
        {
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Invalid Dimensions",
                Content = "Width and Height must be greater than 0.",
            };

            await uiMessageBox.ShowDialogAsync();
            return;
        }

        Window.GetWindow(this).DialogResult = true;
        Window.GetWindow(this).Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        estimateCancellation?.Cancel();
        Window.GetWindow(this).DialogResult = false;
        Window.GetWindow(this).Close();
    }

    public static string GetFileFilter(FormatItem format)
    {
        return $"{format.Name}|*{format.Extension}";
    }

    public static string GetAllFileFilters()
    {
        string allFilters = "All supported formats|";
        string individualFilters = "";

        foreach (FormatItem format in _formats)
        {
            allFilters += $"*{format.Extension};";
            individualFilters += $"|{format.Name}|*{format.Extension}";
        }

        // Remove trailing semicolon
        allFilters = allFilters.TrimEnd(';');

        return allFilters + individualFilters;
    }
}
