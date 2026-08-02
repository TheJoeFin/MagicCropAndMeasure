using ImageMagick;
using MagickCrop.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace MagickCrop.Windows;

public partial class LensCorrectionWindow : FluentWindow
{
    private const uint ProxyLongestEdge = 1000;

    private readonly MainWindowViewModel viewModel;
    private readonly DispatcherTimer previewDebounce;
    private IMainWindowView? mainView;
    private MagickImage? proxyImage;
    private BitmapSource? originalSource;
    private int previewToken;
    private bool applied;

    public LensCorrectionWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        this.viewModel = viewModel;
        DataContext = viewModel;

        previewDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        previewDebounce.Tick += PreviewDebounce_Tick;

        Loaded += LensCorrectionWindow_Loaded;
        Closed += LensCorrectionWindow_Closed;
    }

    private async void LensCorrectionWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            mainView = Owner as IMainWindowView ?? Application.Current.MainWindow as IMainWindowView;

            if (mainView is null || string.IsNullOrWhiteSpace(viewModel.ImagePath) || !File.Exists(viewModel.ImagePath))
                return;

            originalSource = mainView.ImageSource;

            string imagePath = viewModel.ImagePath;
            proxyImage = await Task.Run(() => BuildProxy(imagePath));

            viewModel.LoadLensProfiles();
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            viewModel.AutoDetectLensCorrectionCommand.Execute(null);

            await RenderPreviewAsync();
        }
        catch (Exception)
        {
            // Fall back to a blank preview rather than crashing the dialog.
        }
    }

    private static MagickImage? BuildProxy(string imagePath)
    {
        try
        {
            MagickImage image = new(imagePath);
            uint longestEdge = Math.Max(image.Width, image.Height);

            if (longestEdge > ProxyLongestEdge)
            {
                double scale = ProxyLongestEdge / (double)longestEdge;
                image.Resize(new MagickGeometry((uint)(image.Width * scale), (uint)(image.Height * scale)));
            }

            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.LensCorrectionA)
            or nameof(MainWindowViewModel.LensCorrectionB)
            or nameof(MainWindowViewModel.LensCorrectionC))
        {
            previewDebounce.Stop();
            previewDebounce.Start();
        }
    }

    private async void PreviewDebounce_Tick(object? sender, EventArgs e)
    {
        previewDebounce.Stop();

        try
        {
            await RenderPreviewAsync();
        }
        catch (Exception)
        {
            // A failed preview frame should never crash the dialog.
        }
    }

    private async Task RenderPreviewAsync()
    {
        if (mainView is null || proxyImage is null)
            return;

        int token = Interlocked.Increment(ref previewToken);

        double a = viewModel.LensCorrectionA;
        double b = viewModel.LensCorrectionB;
        double c = viewModel.LensCorrectionC;
        double d = 1.0 - a - b - c;

        MagickImage source = proxyImage;

        BitmapSource? rendered = await Task.Run(() =>
        {
            try
            {
                using MagickImage clone = new(source);
                clone.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                clone.Distort(DistortMethod.Barrel, a, b, c, d);

                BitmapSource bitmap = clone.ToBitmapSource();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception)
            {
                return null;
            }
        });

        // Latest render wins; discard stale frames produced during a fast drag.
        if (rendered is null || token != Volatile.Read(ref previewToken))
            return;

        mainView.ImageSource = rendered;
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Restore the real image first so the full-resolution operation and its
            // undo entry are built from the unmodified source, not the proxy preview.
            RestoreOriginalPreview();

            applied = true;

            // Get the dialog out of the way immediately; the full-resolution work
            // reports progress through the main window's busy indicator. When
            // measurements exist the command prompts for confirmation first, so
            // stay visible until that has been answered.
            if (mainView?.HasMeasurements != true)
                Hide();

            await viewModel.ApplyLensCorrectionCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Lens correction could not be applied.\n\n{ex.Message}",
                "Lens Correction Failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }

        Close();
    }

    private async void AutoOrientButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Auto-orient rewrites the underlying image, so drop the preview first and
            // rebuild the proxy from the newly oriented result afterwards.
            RestoreOriginalPreview();
            await viewModel.ApplyExifOrientationCommand.ExecuteAsync(null);

            if (mainView is null || string.IsNullOrWhiteSpace(viewModel.ImagePath))
                return;

            originalSource = mainView.ImageSource;

            proxyImage?.Dispose();
            proxyImage = null;

            string imagePath = viewModel.ImagePath;
            proxyImage = await Task.Run(() => BuildProxy(imagePath));

            await RenderPreviewAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Auto-orient could not be applied.\n\n{ex.Message}",
                "Auto-orient Failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void LensCorrectionWindow_Closed(object? sender, EventArgs e)
    {
        previewDebounce.Stop();
        previewDebounce.Tick -= PreviewDebounce_Tick;
        viewModel.PropertyChanged -= ViewModel_PropertyChanged;

        if (!applied)
            RestoreOriginalPreview();

        proxyImage?.Dispose();
        proxyImage = null;
    }

    private void RestoreOriginalPreview()
    {
        if (mainView is not null && originalSource is not null)
            mainView.ImageSource = originalSource;
    }
}
