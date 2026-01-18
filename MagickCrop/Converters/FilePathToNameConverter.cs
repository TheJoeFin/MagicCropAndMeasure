using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Extracts file name from path.
/// </summary>
public class FilePathToNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            bool withExtension = parameter?.ToString()?.ToLower() != "noext";
            return withExtension 
                ? Path.GetFileName(path) 
                : Path.GetFileNameWithoutExtension(path);
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
