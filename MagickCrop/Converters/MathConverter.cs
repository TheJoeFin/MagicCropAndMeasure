using System.Globalization;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Performs math operations on values.
/// Parameter format: "+10", "-5", "*2", "/4", "%100"
/// </summary>
public class MathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (!double.TryParse(value?.ToString(), out double numValue))
            return value ?? 0.0;

        var param = parameter?.ToString() ?? "";
        if (param.Length < 2)
            return numValue;

        char op = param[0];
        if (!double.TryParse(param[1..], out double operand))
            return numValue;

        return op switch
        {
            '+' => numValue + operand,
            '-' => numValue - operand,
            '*' => numValue * operand,
            '/' when operand != 0 => numValue / operand,
            '%' when operand != 0 => numValue % operand,
            _ => numValue
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (!double.TryParse(value?.ToString(), out double numValue))
            return value ?? 0.0;

        var param = parameter?.ToString() ?? "";
        if (param.Length < 2)
            return numValue;

        char op = param[0];
        if (!double.TryParse(param[1..], out double operand))
            return numValue;

        // Reverse operation
        return op switch
        {
            '+' => numValue - operand,
            '-' => numValue + operand,
            '*' when operand != 0 => numValue / operand,
            '/' => numValue * operand,
            _ => numValue
        };
    }
}
