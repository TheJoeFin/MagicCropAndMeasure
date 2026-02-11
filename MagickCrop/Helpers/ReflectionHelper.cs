using System.Reflection;

namespace MagickCrop.Helpers;

public static class ReflectionHelper
{
    public static object? GetPrivatePropertyValue(object obj, string propName)
    {
        ArgumentNullException.ThrowIfNull(obj);

        Type t = obj.GetType();
        PropertyInfo? pi = t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new ArgumentOutOfRangeException(nameof(propName),
                string.Format("Field {0} was not found in Type {1}", propName, obj.GetType().FullName));
        return pi.GetValue(obj, null);
    }
}
