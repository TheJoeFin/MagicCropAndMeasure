using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MagickCrop.Converters;

/// <summary>
/// Converts a WPF Color to a SolidColorBrush for data binding.
/// </summary>
public class ColorToBrushConverter : IValueConverter
{
    /// <summary>
    /// Converts a Color to a SolidColorBrush.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(Colors.Cyan);
    }

    /// <summary>
    /// Not implemented for one-way binding.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
