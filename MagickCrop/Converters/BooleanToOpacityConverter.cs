using System.Globalization;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Converts boolean to opacity (true = 1.0, false = parameter or 0.5).
/// </summary>
public class BooleanToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            if (isEnabled)
                return 1.0;
            
            if (double.TryParse(parameter?.ToString(), out double disabledOpacity))
                return disabledOpacity;
            
            return 0.5;
        }
        return 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
