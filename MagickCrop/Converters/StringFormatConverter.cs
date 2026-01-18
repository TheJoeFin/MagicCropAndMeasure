using System.Globalization;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Formats a value using the parameter as format string.
/// </summary>
public class StringFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        if (parameter is string format && !string.IsNullOrEmpty(format))
        {
            return string.Format(culture, format, value);
        }

        return value.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
