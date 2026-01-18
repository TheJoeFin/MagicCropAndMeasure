using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Converts Point to formatted string.
/// </summary>
public class PointToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Point point)
        {
            var format = parameter?.ToString() ?? "F1";
            return $"({point.X.ToString(format, culture)}, {point.Y.ToString(format, culture)})";
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
