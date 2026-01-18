using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Shows element based on numeric comparison.
/// Parameter format: "gt:5" (greater than 5), "lt:10", "eq:0", "gte:1", "lte:100"
/// </summary>
public class ComparisonToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        if (!double.TryParse(value.ToString(), out double numValue))
            return Visibility.Collapsed;

        var parts = parameter.ToString()!.Split(':');
        if (parts.Length != 2 || !double.TryParse(parts[1], out double compareValue))
            return Visibility.Collapsed;

        bool result = parts[0].ToLower() switch
        {
            "gt" => numValue > compareValue,
            "lt" => numValue < compareValue,
            "eq" => Math.Abs(numValue - compareValue) < 0.001,
            "gte" => numValue >= compareValue,
            "lte" => numValue <= compareValue,
            "neq" => Math.Abs(numValue - compareValue) >= 0.001,
            _ => false
        };

        return result ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
