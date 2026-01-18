using System.Globalization;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Subtracts a value from the input to center elements.
/// Used to center handle elements on measurement points.
/// Typically used with parameter=6 to center a 12px element on a point.
/// </summary>
public class SubtractHalfConverter : IValueConverter
{
    /// <summary>
    /// Subtracts the parameter value from the input double value.
    /// If parameter is not provided, defaults to half of the input value.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            if (parameter is string paramStr && double.TryParse(paramStr, out double paramValue))
            {
                return doubleValue - paramValue;
            }

            // Default: subtract half the value
            return doubleValue - (doubleValue / 2);
        }

        return 0.0;
    }

    /// <summary>
    /// Not implemented for one-way binding.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
