using MagickCrop.Models;
using System.Windows.Controls;
using System.Windows.Media;

namespace MagickCrop;

public partial class AspectRatioTransform : UserControl
{
    private AspectRatioItem? _ratioItem;
    public AspectRatioItem? RatioItem
    {
        get
        {
            return _ratioItem;
        }
        set
        {
            _ratioItem = value;

            if (_ratioItem is null)
                return;

            RectanglePoly.Points = GetPointsFromAspectRatio(_ratioItem.RatioValue);
        }
    }

    private PointCollection _transformPoints = [new(-5, -5), new(-7, 6), new(8, 6), new(5, -5)];
    public PointCollection TransformPoints
    {
        get
        {
            return _transformPoints;
        }
        set
        {
            _transformPoints = value;
            Polygon.Points = _transformPoints;
        }
    }

    public AspectRatioTransform()
    {
        InitializeComponent();

        Polygon.Points = _transformPoints;
    }

    public void SetAndScalePoints(PointCollection fullPoints)
    {
        if (fullPoints == null || fullPoints.Count == 0)
            return;

        TransformPoints = ScalePointsToSquare(fullPoints);
    }


    private static PointCollection GetPointsFromAspectRatio(double aspectRatio)
    {
        double height = 12;
        double width = height / aspectRatio;

        if (aspectRatio > 1)
        {
            width = 12;
            height = width * aspectRatio;
        }

        double halfHeight = height / 2;
        double halfWidth = width / 2;

        return
        [
            new(-halfWidth, -halfHeight),
            new(-halfWidth, halfHeight),
            new(halfWidth, halfHeight),
            new(halfWidth, -halfHeight)
        ];
    }

    public static PointCollection ScalePointsToSquare(PointCollection fullPoints)
    {
        if (fullPoints == null || fullPoints.Count == 0)
            return [];

        float squareSize = 20;

        // Find the bounding box of the points
        double minX = fullPoints.Min(p => p.X);
        double minY = fullPoints.Min(p => p.Y);
        double maxX = fullPoints.Max(p => p.X);
        double maxY = fullPoints.Max(p => p.Y);

        // Calculate width and height of the bounding box
        double width = maxX - minX;
        double height = maxY - minY;

        // Determine the scaling factor to fit within the square
        double scale = squareSize / Math.Max(width, height);

        // Calculate the center of the original bounding box
        double centerX = (minX + maxX) / 2;
        double centerY = (minY + maxY) / 2;

        // Scale and center the points around (0, 0)
        PointCollection scaledPoints = [];

        foreach (System.Windows.Point p in fullPoints)
        {
            // Translate to origin, scale, then center at (0, 0)
            double scaledX = (p.X - centerX) * scale;
            double scaledY = (p.Y - centerY) * scale;
            scaledPoints.Add(new System.Windows.Point(scaledX, scaledY));
        }

        return scaledPoints;
    }
}
