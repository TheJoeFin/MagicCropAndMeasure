using MagickCrop.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MagickCrop.Controls;

/// <summary>
/// Control for selecting from detected quadrilaterals
/// </summary>
public partial class QuadrilateralSelector : UserControl
{
    public class QuadrilateralViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public PointCollection PreviewPoints { get; set; } = [];
        public QuadrilateralDetector.DetectedQuadrilateral Quadrilateral { get; set; }

        public QuadrilateralViewModel(QuadrilateralDetector.DetectedQuadrilateral quad, int index)
        {
            Quadrilateral = quad ?? throw new ArgumentNullException(nameof(quad));
            Name = $"Quadrilateral {index + 1}";
            Description = $"Confidence: {quad.Confidence:P0}";

            // Scale points for preview (60x60 canvas)
            PreviewPoints = ScalePointsForPreview(quad);
        }

        private static PointCollection ScalePointsForPreview(QuadrilateralDetector.DetectedQuadrilateral quad)
        {
            // Get bounds of the quadrilateral
            double minX = Math.Min(Math.Min(quad.TopLeft.X, quad.TopRight.X), Math.Min(quad.BottomLeft.X, quad.BottomRight.X));
            double maxX = Math.Max(Math.Max(quad.TopLeft.X, quad.TopRight.X), Math.Max(quad.BottomLeft.X, quad.BottomRight.X));
            double minY = Math.Min(Math.Min(quad.TopLeft.Y, quad.TopRight.Y), Math.Min(quad.BottomLeft.Y, quad.BottomRight.Y));
            double maxY = Math.Max(Math.Max(quad.TopLeft.Y, quad.TopRight.Y), Math.Max(quad.BottomLeft.Y, quad.BottomRight.Y));

            double width = maxX - minX;
            double height = maxY - minY;

            // Target size for preview (with some padding)
            double targetSize = 50;
            double padding = 5;

            // Calculate scale to fit in preview
            double scale = Math.Min(targetSize / width, targetSize / height);

            // Create scaled points
            var scaledPoints = new PointCollection
            {
                new Point((quad.TopLeft.X - minX) * scale + padding, (quad.TopLeft.Y - minY) * scale + padding),
                new Point((quad.TopRight.X - minX) * scale + padding, (quad.TopRight.Y - minY) * scale + padding),
                new Point((quad.BottomRight.X - minX) * scale + padding, (quad.BottomRight.Y - minY) * scale + padding),
                new Point((quad.BottomLeft.X - minX) * scale + padding, (quad.BottomLeft.Y - minY) * scale + padding)
            };

            return scaledPoints;
        }
    }

    public event EventHandler<QuadrilateralDetector.DetectedQuadrilateral>? QuadrilateralSelected;
    public event EventHandler? ManualSelection;
    public event EventHandler? Cancelled;
    public event EventHandler<QuadrilateralDetector.DetectedQuadrilateral>? QuadrilateralHoverEnter;
    public event EventHandler? QuadrilateralHoverExit;

    public QuadrilateralSelector()
    {
        InitializeComponent();
    }

    public void SetQuadrilaterals(List<QuadrilateralDetector.DetectedQuadrilateral> quadrilaterals)
    {
        var viewModels = quadrilaterals.Select((q, i) => new QuadrilateralViewModel(q, i)).ToList();
        QuadrilateralList.ItemsSource = viewModels;
    }

    private void QuadrilateralItem_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is QuadrilateralViewModel vm)
        {
            QuadrilateralSelected?.Invoke(this, vm.Quadrilateral);
            e.Handled = true;
        }
    }

    private void QuadrilateralItem_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border border && border.DataContext is QuadrilateralViewModel vm)
        {
            QuadrilateralHoverEnter?.Invoke(this, vm.Quadrilateral);
        }
    }

    private void QuadrilateralItem_MouseLeave(object sender, MouseEventArgs e)
    {
        QuadrilateralHoverExit?.Invoke(this, EventArgs.Empty);
    }

    private void ManualButton_Click(object sender, RoutedEventArgs e)
    {
        ManualSelection?.Invoke(this, EventArgs.Empty);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}
