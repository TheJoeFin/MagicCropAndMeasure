using ImageMagick;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MagickCrop.Controls;

public partial class RgbHistogramControl : UserControl
{
    private long[]? _rBins, _gBins, _bBins;
    private CancellationTokenSource? _cts;
    private bool _isThresholdActive;

    public bool IsThresholdActive
    {
        get => _isThresholdActive;
        set { _isThresholdActive = value; UpdateThresholdLine(); }
    }

    public static readonly DependencyProperty ImagePathProperty =
        DependencyProperty.Register(
            nameof(ImagePath),
            typeof(string),
            typeof(RgbHistogramControl),
            new PropertyMetadata(null, OnImagePathChanged));

    public string? ImagePath
    {
        get => (string?)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public static readonly DependencyProperty ThresholdValueProperty =
        DependencyProperty.Register(
            nameof(ThresholdValue),
            typeof(double),
            typeof(RgbHistogramControl),
            new PropertyMetadata(128.0, OnThresholdValueChanged));

    public double ThresholdValue
    {
        get => (double)GetValue(ThresholdValueProperty);
        set => SetValue(ThresholdValueProperty, value);
    }

    public RgbHistogramControl()
    {
        InitializeComponent();
    }

    private static void OnImagePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((RgbHistogramControl)d).LoadHistogramAsync((string?)e.NewValue);
    }

    private static void OnThresholdValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((RgbHistogramControl)d).UpdateThresholdLine();
    }

    private async void LoadHistogramAsync(string? path)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            _rBins = _gBins = _bBins = null;
            Render();
            return;
        }

        long[] r = new long[256], g = new long[256], b = new long[256];

        try
        {
            await Task.Run(() =>
            {
                using MagickImage image = new(path);
                // Resize to limit memory and speed up computation
                if (image.Width > 512 || image.Height > 512)
                    image.Resize(new MagickGeometry(512, 512) { Greater = true });

                // ToByteArray scales Q16 values to 0-255 per channel
                byte[]? data = image.GetPixelsUnsafe().ToByteArray(PixelMapping.RGB);
                if (data is null) return;

                for (int i = 0; i + 2 < data.Length; i += 3)
                {
                    token.ThrowIfCancellationRequested();
                    r[data[i]]++;
                    g[data[i + 1]]++;
                    b[data[i + 2]]++;
                }
            }, token);
        }
        catch (OperationCanceledException) { return; }

        // A newer load may have started while this one was finishing
        if (token.IsCancellationRequested)
            return;

        _rBins = r;
        _gBins = g;
        _bBins = b;
        Render();
    }

    private void Render()
    {
        HistogramCanvas.Children.Clear();

        if (_rBins is null)
        {
            NoImageText.Visibility = Visibility.Visible;
            return;
        }

        NoImageText.Visibility = Visibility.Collapsed;

        double w = HistogramCanvas.ActualWidth;
        double h = HistogramCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        long max = 0;
        for (int i = 0; i < 256; i++)
        {
            max = Math.Max(max, _rBins[i]);
            max = Math.Max(max, _gBins![i]);
            max = Math.Max(max, _bBins![i]);
        }
        if (max == 0) return;

        DrawChannel(_rBins, Color.FromArgb(80, 220, 50, 50), Color.FromArgb(180, 220, 50, 50), w, h, max);
        DrawChannel(_gBins!, Color.FromArgb(80, 50, 200, 80), Color.FromArgb(180, 50, 200, 80), w, h, max);
        DrawChannel(_bBins!, Color.FromArgb(80, 50, 120, 220), Color.FromArgb(180, 50, 120, 220), w, h, max);

        UpdateThresholdLine();
    }

    private void UpdateThresholdLine()
    {
        double w = HistogramCanvas.ActualWidth;
        if (_rBins is null || !_isThresholdActive || w <= 0)
        {
            ThresholdIndicator.Visibility = Visibility.Collapsed;
            return;
        }
        double x = ThresholdValue / 255.0 * w;
        ThresholdIndicator.Margin = new Thickness(x, 0, 0, 0);
        ThresholdIndicator.Visibility = Visibility.Visible;
    }

    private void DrawChannel(long[] bins, Color fillColor, Color strokeColor, double w, double h, long max)
    {
        var points = new PointCollection(258)
        {
            new Point(0, h)
        };
        for (int i = 0; i < 256; i++)
        {
            double x = i / 255.0 * w;
            double y = h - (bins[i] / (double)max * h);
            points.Add(new Point(x, y));
        }
        points.Add(new Point(w, h));

        var polygon = new Polygon
        {
            Points = points,
            Fill = new SolidColorBrush(fillColor),
            Stroke = new SolidColorBrush(strokeColor),
            StrokeThickness = 1,
            IsHitTestVisible = false,
        };
        HistogramCanvas.Children.Add(polygon);
    }

    private void HistogramCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Render();
        UpdateThresholdLine();
    }
}
