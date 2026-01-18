using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MagickCrop.ViewModels.Measurements;

namespace MagickCrop.Converters;

/// <summary>
/// Converts a PolygonMeasurementViewModel to a PathGeometry representing the polygon shape.
/// </summary>
public class PolygonPathConverter : IValueConverter
{
    /// <summary>
    /// Converts the ViewModel to a PathGeometry for the polygon path.
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PolygonMeasurementViewModel viewModel || viewModel.Vertices.Count < 2)
        {
            return null;
        }

        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = viewModel.Vertices[0] };

        for (int i = 1; i < viewModel.Vertices.Count; i++)
        {
            figure.Segments.Add(new LineSegment(viewModel.Vertices[i], true));
        }

        if (viewModel.IsClosed && viewModel.Vertices.Count >= 3)
        {
            figure.IsClosed = true;
        }

        geometry.Figures.Add(figure);
        return geometry;
    }

    /// <summary>
    /// Not implemented for one-way binding.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
