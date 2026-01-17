# Step 17: Value Converters

## Objective
Create a comprehensive set of value converters to support data binding throughout the application.

## Prerequisites
- Step 16 completed (File operations)

---

## ⚠️ This step has been broken into smaller sub-steps for easier implementation

**Complete these sub-steps in order (group by purpose):**

| Sub-Step | Description | Estimated Effort |
|----------|-------------|-----------------|
| **17a** | Create Converters folder and verify existing converters work | 15 min |
| **17b** | Create NullToVisibilityConverter | 20 min |
| **17c** | Create EnumToBooleanConverter (for radio buttons) | 25 min |
| **17d** | Create EnumToVisibilityConverter | 20 min |
| **17e** | Create MultiplyConverter | 15 min |
| **17f** | Create BooleanToOpacityConverter | 15 min |
| **17g** | Create PointToStringConverter | 15 min |
| **17h** | Create StringFormatConverter | 20 min |
| **17i** | Register all converters as static resources in App.xaml | 30 min |
| **17j** | Verify converters work by testing one binding for each | 30 min |

Each sub-step should be its own commit with a working build.

**Note:** Some converters may already exist (BooleanToVisibilityConverter, InverseBooleanToVisibilityConverter, ColorToBrushConverter, SubtractHalfConverter). Only create ones that don't exist yet.

---

## Converters to Create

| Converter | Purpose |
|-----------|---------|
| BooleanToVisibilityConverter | Already exists |
| InverseBooleanToVisibilityConverter | Already exists |
| ColorToBrushConverter | Already created in Step 12 |
| SubtractHalfConverter | Already created in Step 12 |
| NullToVisibilityConverter | Show/hide based on null |
| EnumToBooleanConverter | Radio button binding |
| StringFormatConverter | Format strings with parameters |
| MultiplyConverter | Multiply value by parameter |
| BooleanToOpacityConverter | Fade elements based on state |
| PointToStringConverter | Display point values |
| EnumToVisibilityConverter | Show based on enum value |

## Converter Implementations

### 1. NullToVisibilityConverter

**File: `Converters/NullToVisibilityConverter.cs`**

```csharp
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
```

### 2. EnumToBooleanConverter

**File: `Converters/EnumToBooleanConverter.cs`**

```csharp
using System.Globalization;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Converts enum values to boolean for radio button binding.
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            return Enum.Parse(targetType, parameter.ToString()!);
        }
        return Binding.DoNothing;
    }
}
```

### 3. StringFormatConverter

**File: `Converters/StringFormatConverter.cs`**

```csharp
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
```

### 4. MultiplyConverter

**File: `Converters/MultiplyConverter.cs`**

```csharp
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
```

### 5. BooleanToOpacityConverter

**File: `Converters/BooleanToOpacityConverter.cs`**

```csharp
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
```

### 6. PointToStringConverter

**File: `Converters/PointToStringConverter.cs`**

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Converts Point to formatted string.
/// </summary>
public class PointToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Point point)
        {
            var format = parameter?.ToString() ?? "F1";
            return $"({point.X.ToString(format, culture)}, {point.Y.ToString(format, culture)})";
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

### 7. EnumToVisibilityConverter

**File: `Converters/EnumToVisibilityConverter.cs`**

```csharp
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
```

### 8. ComparisonToVisibilityConverter

**File: `Converters/ComparisonToVisibilityConverter.cs`**

```csharp
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
```

### 9. FilePathToNameConverter

**File: `Converters/FilePathToNameConverter.cs`**

```csharp
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
```

### 10. MathConverter (Multi-purpose)

**File: `Converters/MathConverter.cs`**

```csharp
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
```

## Register Converters in App.xaml

**File: `App.xaml`**

```xml
<Application
    x:Class="MagickCrop.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    xmlns:converters="clr-namespace:MagickCrop.Converters">
    
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ThemesDictionary Theme="Dark" />
                <ui:ControlsDictionary />
            </ResourceDictionary.MergedDictionaries>
            
            <!-- Converters -->
            <converters:BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter"/>
            <converters:InverseBooleanToVisibilityConverter x:Key="InverseBooleanToVisibilityConverter"/>
            <converters:NullToVisibilityConverter x:Key="NullToVisibilityConverter"/>
            <converters:ColorToBrushConverter x:Key="ColorToBrushConverter"/>
            <converters:SubtractHalfConverter x:Key="SubtractHalfConverter"/>
            <converters:EnumToBooleanConverter x:Key="EnumToBooleanConverter"/>
            <converters:StringFormatConverter x:Key="StringFormatConverter"/>
            <converters:MultiplyConverter x:Key="MultiplyConverter"/>
            <converters:BooleanToOpacityConverter x:Key="BooleanToOpacityConverter"/>
            <converters:PointToStringConverter x:Key="PointToStringConverter"/>
            <converters:EnumToVisibilityConverter x:Key="EnumToVisibilityConverter"/>
            <converters:ComparisonToVisibilityConverter x:Key="ComparisonToVisibilityConverter"/>
            <converters:FilePathToNameConverter x:Key="FilePathToNameConverter"/>
            <converters:MathConverter x:Key="MathConverter"/>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

---

## Usage Examples

### BooleanToVisibility
```xml
<Grid Visibility="{Binding HasImage, Converter={StaticResource BooleanToVisibilityConverter}}"/>
```

### EnumToBoolean (Radio Buttons)
```xml
<RadioButton Content="Distance" 
             IsChecked="{Binding CurrentTool, Converter={StaticResource EnumToBooleanConverter}, ConverterParameter=MeasureDistance}"/>
```

### EnumToVisibility (Tool-specific UI)
```xml
<StackPanel Visibility="{Binding CurrentTool, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=Crop}">
    <!-- Crop-specific controls -->
</StackPanel>
```

### MathConverter (Positioning)
```xml
<Canvas.Left>
    <Binding Path="Position.X" Converter="{StaticResource MathConverter}" ConverterParameter="-6"/>
</Canvas.Left>
```

### ComparisonToVisibility
```xml
<TextBlock Text="Multiple measurements" 
           Visibility="{Binding TotalMeasurementCount, Converter={StaticResource ComparisonToVisibilityConverter}, ConverterParameter='gt:1'}"/>
```

### NullToVisibility
```xml
<Image Source="{Binding Thumbnail}"
       Visibility="{Binding Thumbnail, Converter={StaticResource NullToVisibilityConverter}}"/>
```

---

## Validation Checklist

- [ ] All converter files created
- [ ] All converters registered in App.xaml
- [ ] Application builds without errors
- [ ] Basic bindings using converters work

---

## Files Created

| File | Description |
|------|-------------|
| `Converters/NullToVisibilityConverter.cs` | Null check visibility |
| `Converters/EnumToBooleanConverter.cs` | Enum radio binding |
| `Converters/StringFormatConverter.cs` | String formatting |
| `Converters/MultiplyConverter.cs` | Value multiplication |
| `Converters/BooleanToOpacityConverter.cs` | Opacity from bool |
| `Converters/PointToStringConverter.cs` | Point display |
| `Converters/EnumToVisibilityConverter.cs` | Enum visibility |
| `Converters/ComparisonToVisibilityConverter.cs` | Numeric comparisons |
| `Converters/FilePathToNameConverter.cs` | File name extraction |
| `Converters/MathConverter.cs` | Math operations |

---

## Notes

### Design-Time Support

Converters work at design time in the Visual Studio designer, enabling WYSIWYG editing.

### Performance

Converters are called frequently during binding updates. Keep them lightweight:
- Avoid allocations in hot paths
- Cache computed values when possible
- Use value types over reference types

### Multi-Value Converters

For complex scenarios, consider `IMultiValueConverter`:
```csharp
public class MultiValueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // Combine multiple values
    }
}
```

---

## Next Steps

Proceed to **Step 18: Commands Cleanup** to standardize command implementations.
