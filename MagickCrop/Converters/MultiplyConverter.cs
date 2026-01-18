using System.Globalization;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Multiplies a value by the parameter.
/// </summary>
public class MultiplyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d && double.TryParse(parameter?.ToString(), out double multiplier))
        {
            return d * multiplier;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d && double.TryParse(parameter?.ToString(), out double multiplier) && multiplier != 0)
        {
            return d / multiplier;
        }
        return value;
    }
}
