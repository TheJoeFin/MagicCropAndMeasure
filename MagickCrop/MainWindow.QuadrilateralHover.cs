using MagickCrop.Helpers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MagickCrop;

public partial class MainWindow
{
    private void QuadrilateralSelector_HoverEnter(object? sender, QuadrilateralDetector.DetectedQuadrilateral quad)
    {
        ShowHoverHighlight(quad);
    }

    private void QuadrilateralSelector_HoverExit(object? sender, EventArgs e)
    {
        RemoveHoverHighlight();
    }

    private void ShowHoverHighlight(QuadrilateralDetector.DetectedQuadrilateral quad)
    {
        // Remove existing highlight if any
        RemoveHoverHighlight();

        // Create highlight polygon
        hoverHighlightPolygon = new Polygon
        {
            Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 165, 0)), // Orange
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromArgb(60, 255, 165, 0)), // Semi-transparent orange
            IsHitTestVisible = false,
            StrokeLineJoin = PenLineJoin.Round,
            Points =
            [
                quad.TopLeft,
                quad.TopRight,
                quad.BottomRight,
                quad.BottomLeft
            ]
        };

        // Add to canvas
        ShapeCanvas.Children.Add(hoverHighlightPolygon);
    }

    private void RemoveHoverHighlight()
    {
        if (hoverHighlightPolygon != null)
        {
            ShapeCanvas.Children.Remove(hoverHighlightPolygon);
            hoverHighlightPolygon = null;
        }
    }

    private void UnwireQuadrilateralHoverEvents()
    {
        QuadrilateralSelectorControl.QuadrilateralHoverEnter -= QuadrilateralSelector_HoverEnter;
        QuadrilateralSelectorControl.QuadrilateralHoverExit -= QuadrilateralSelector_HoverExit;
        RemoveHoverHighlight();
    }

    private void HideQuadrilateralSelector()
    {
        QuadrilateralSelectorControl.Visibility = Visibility.Collapsed;
        UnwireQuadrilateralHoverEvents();
    }

    private void ShowQuadrilateralSelector()
    {
        QuadrilateralSelectorControl.Visibility = Visibility.Visible;
    }
}
