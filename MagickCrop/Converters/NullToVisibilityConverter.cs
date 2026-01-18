using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Converts null values to Visibility.
/// Null = Collapsed, Not Null = Visible (or inverse with parameter).
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value == null;
        bool invert = parameter?.ToString()?.ToLower() == "invert";
        
        if (invert)
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
