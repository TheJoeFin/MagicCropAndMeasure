using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Shows element when enum matches parameter value.
/// </summary>
public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        return value.ToString() == parameter.ToString() 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
