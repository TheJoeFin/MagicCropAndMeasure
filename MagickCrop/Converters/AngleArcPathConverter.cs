using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MagickCrop.ViewModels.Measurements;

namespace MagickCrop.Converters;

/// <summary>
/// Converts an AngleMeasurementViewModel to a PathGeometry representing the angle arc.
/// </summary>
public class AngleArcPathConverter : IValueConverter
{
    /// <summary>
    /// Converts the ViewModel to a PathGeometry for the angle arc.
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AngleMeasurementViewModel viewModel)
        {
            return null;
        }

        double arcRadius = 25;

        // Calculate vectors from vertex to points
        Vector vector1 = new(viewModel.Point1.X - viewModel.Vertex.X, viewModel.Point1.Y - viewModel.Vertex.Y);
        Vector vector2 = new(viewModel.Point2.X - viewModel.Vertex.X, viewModel.Point2.Y - viewModel.Vertex.Y);

        if (vector1.Length == 0 || vector2.Length == 0)
        {
            return null;
        }

        // Normalize vectors and scale to arc radius
        vector1.Normalize();
        vector2.Normalize();
        vector1 *= arcRadius;
        vector2 *= arcRadius;

        // Calculate arc points
        Point arcStart = new(viewModel.Vertex.X + vector1.X, viewModel.Vertex.Y + vector1.Y);
        Point arcEnd = new(viewModel.Vertex.X + vector2.X, viewModel.Vertex.Y + vector2.Y);

        // Calculate angle to determine arc size and direction
        double angle = viewModel.AngleDegrees;
        bool isLargeArc = angle > 180;

        // Create path geometry for the arc
        PathGeometry pathGeometry = new();
        PathFigure pathFigure = new()
        {
            StartPoint = viewModel.Vertex,
            IsClosed = true
        };

        // Add line segments to outer arc points
        pathFigure.Segments.Add(new LineSegment(arcStart, true));

        // Add arc segment
        ArcSegment arcSegment = new()
        {
            Point = arcEnd,
            Size = new Size(arcRadius, arcRadius),
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Clockwise,
        };
        pathFigure.Segments.Add(arcSegment);

        // Close path back to center
        pathFigure.Segments.Add(new LineSegment(viewModel.Vertex, true));

        pathGeometry.Figures.Add(pathFigure);
        return pathGeometry;
    }

    /// <summary>
    /// Not implemented for one-way binding.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
