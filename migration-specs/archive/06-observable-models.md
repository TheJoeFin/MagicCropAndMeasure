# Step 06: ObservableObject for Models

## Objective
Update key data models to implement `INotifyPropertyChanged` so they can be used effectively in data binding scenarios.

## Prerequisites
- Step 05 completed (Navigation service)

---

## ⚠️ This step has been broken into smaller sub-steps for easier implementation

Complete these sub-steps in order:

| Sub-Step | Description | Estimated Effort |
|----------|-------------|-----------------|
| **06a** | Update RecentProjectInfo to inherit from ObservableObject | 30 min |
| **06b** | Update StrokeInfo to inherit from ObservableObject | 20 min |
| **06c** | Update PackageMetadata to inherit from ObservableObject | 20 min |
| **06d** | Update SaveOptions to inherit from ObservableValidator (with validation) | 45 min |
| **06e** | Update AspectRatioItem to inherit from ObservableObject (optional) | 20 min |
| **06f** | Test JSON serialization still works for all updated models | 30 min |

Each sub-step should be its own commit with a working build.

**Important:** DTOs (Data Transfer Objects like `DistanceMeasurementControlDto`) should **NOT** be made observable - they remain POCOs for serialization.

---

## Why Observable Models?

Current models are plain POCOs:
```csharp
public class RecentProjectInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    // Changes to these properties don't notify the UI
}
```

After making observable:
```csharp
public partial class RecentProjectInfo : ObservableObject
{
    [ObservableProperty]
    private Guid _id;
    
    [ObservableProperty]
    private string _name = string.Empty;
    // Changes automatically notify the UI
}
```

## Models to Update

Based on analysis, these models need INotifyPropertyChanged:

| Model | Priority | Reason |
|-------|----------|--------|
| `RecentProjectInfo` | High | Displayed in UI list, needs updates |
| `SaveOptions` | High | Two-way binding in save dialog |
| `StrokeInfo` | Medium | Displayed in measurement UI |
| `PackageMetadata` | Medium | Shown in project info |
| `AspectRatioItem` | Low | Selection changes |

**Note:** DTOs (Data Transfer Objects) should remain as simple POCOs since they're for serialization only.

## Changes Required

### 1. Update RecentProjectInfo

**File: `Models/RecentProjectInfo.cs`**

```csharp
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.Models;

/// <summary>
/// Information about a recent project for display in the UI.
/// </summary>
public partial class RecentProjectInfo : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private DateTime _lastOpened;

    [ObservableProperty]
    private DateTime _created;

    [ObservableProperty]
    private string _thumbnailPath = string.Empty;

    private BitmapImage? _thumbnail;

    /// <summary>
    /// Gets or sets the thumbnail image for the project.
    /// Excluded from JSON serialization.
    /// </summary>
    [JsonIgnore]
    public BitmapImage? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    /// <summary>
    /// Gets the formatted last opened time string.
    /// </summary>
    [JsonIgnore]
    public string LastOpenedFormatted => FormatRelativeTime(LastOpened);

    /// <summary>
    /// Creates a new RecentProjectInfo with a new ID and current timestamps.
    /// </summary>
    public static RecentProjectInfo Create(string name, string filePath)
    {
        return new RecentProjectInfo
        {
            Id = Guid.NewGuid(),
            Name = name,
            FilePath = filePath,
            Created = DateTime.Now,
            LastOpened = DateTime.Now
        };
    }

    /// <summary>
    /// Updates the last opened timestamp to now.
    /// </summary>
    public void MarkAsOpened()
    {
        LastOpened = DateTime.Now;
        OnPropertyChanged(nameof(LastOpenedFormatted));
    }

    private static string FormatRelativeTime(DateTime dateTime)
    {
        var span = DateTime.Now - dateTime;
        
        return span.TotalMinutes < 1 ? "Just now" :
               span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min ago" :
               span.TotalDays < 1 ? $"{(int)span.TotalHours} hours ago" :
               span.TotalDays < 7 ? $"{(int)span.TotalDays} days ago" :
               dateTime.ToString("MMM d, yyyy");
    }
}
```

### 2. Update SaveOptions with Validation

**File: `Models/SaveOptions.cs`**

For models that need validation, use `ObservableValidator` instead of `ObservableObject`. This enables data annotation validation with automatic error notifications.

```csharp
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagick;

namespace MagickCrop.Models;

/// <summary>
/// Options for saving/exporting an image.
/// Inherits from ObservableValidator to support data validation.
/// </summary>
public partial class SaveOptions : ObservableValidator
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupportsQuality))]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Please select an output format")]
    private FormatItem? _selectedFormat;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 100, ErrorMessage = "Quality must be between 1 and 100")]
    private int _quality = 90;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResizeValid))]
    private bool _shouldResize;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResizeValid))]
    [NotifyDataErrorInfo]
    [Range(1, 32000, ErrorMessage = "Width must be between 1 and 32000")]
    private int _resizeWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResizeValid))]
    [NotifyDataErrorInfo]
    [Range(1, 32000, ErrorMessage = "Height must be between 1 and 32000")]
    private int _resizeHeight;

    [ObservableProperty]
    private bool _maintainAspectRatio = true;

    [ObservableProperty]
    private int _originalWidth;

    [ObservableProperty]
    private int _originalHeight;

    partial void OnResizeWidthChanged(int value)
    {
        if (MaintainAspectRatio && OriginalWidth > 0)
        {
            var ratio = (double)OriginalHeight / OriginalWidth;
            var newHeight = (int)(value * ratio);
            if (newHeight != ResizeHeight)
            {
                _resizeHeight = newHeight;
                OnPropertyChanged(nameof(ResizeHeight));
            }
        }
    }

    partial void OnResizeHeightChanged(int value)
    {
        if (MaintainAspectRatio && OriginalHeight > 0)
        {
            var ratio = (double)OriginalWidth / OriginalHeight;
            var newWidth = (int)(value * ratio);
            if (newWidth != ResizeWidth)
            {
                _resizeWidth = newWidth;
                OnPropertyChanged(nameof(ResizeWidth));
            }
        }
    }

    /// <summary>
    /// Gets whether quality adjustment is supported for the selected format.
    /// </summary>
    public bool SupportsQuality => SelectedFormat?.SupportsQuality ?? false;

    /// <summary>
    /// Gets whether the resize dimensions are valid.
    /// </summary>
    public bool IsResizeValid => !ShouldResize || (ResizeWidth > 0 && ResizeHeight > 0);

    /// <summary>
    /// Validates all properties and returns whether the options are valid.
    /// </summary>
    public bool IsValid()
    {
        ValidateAllProperties();
        return !HasErrors;
    }
}
```

### Using Validation in the View

**XAML for validation display:**
```xml
<TextBox Text="{Binding Quality, UpdateSourceTrigger=PropertyChanged, ValidatesOnDataErrors=True}"
         Validation.ErrorTemplate="{StaticResource ValidationErrorTemplate}"/>

<!-- Error message display -->
<TextBlock Text="{Binding (Validation.Errors)[0].ErrorContent, RelativeSource={RelativeSource Self}}"
           Foreground="Red"
           Visibility="{Binding (Validation.HasError), RelativeSource={RelativeSource Self}, 
                        Converter={StaticResource BooleanToVisibilityConverter}}"/>
```

**In ViewModel before saving:**
```csharp
[RelayCommand]
private async Task SaveImage()
{
    if (!SaveOptions.IsValid())
    {
        _navigationService.ShowError("Please fix validation errors before saving.");
        return;
    }
    
    // Proceed with save
}
```

### 3. Update StrokeInfo

**File: `Models/StrokeInfo.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.Models;

/// <summary>
/// Information about a measurement stroke.
/// </summary>
public partial class StrokeInfo : ObservableObject
{
    [ObservableProperty]
    private double _pixelLength;

    [ObservableProperty]
    private double _scaledLength;

    [ObservableProperty]
    private string _units = "px";

    /// <summary>
    /// Gets the formatted display string.
    /// </summary>
    public string DisplayText => Units == "px" 
        ? $"{PixelLength:F1} px" 
        : $"{ScaledLength:F2} {Units}";

    partial void OnPixelLengthChanged(double value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnScaledLengthChanged(double value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnUnitsChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }
}
```

### 4. Update PackageMetadata

**File: `Models/MeasurementControls/PackageMetadata.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.Models.MeasurementControls;

/// <summary>
/// Metadata for a measurement package.
/// </summary>
public partial class PackageMetadata : ObservableObject
{
    [ObservableProperty]
    private string _formatVersion = "1.0";

    [ObservableProperty]
    private DateTime _createdDate = DateTime.Now;

    [ObservableProperty]
    private DateTime _modifiedDate = DateTime.Now;

    [ObservableProperty]
    private string _originalFileName = string.Empty;

    [ObservableProperty]
    private int _originalWidth;

    [ObservableProperty]
    private int _originalHeight;

    [ObservableProperty]
    private int _currentWidth;

    [ObservableProperty]
    private int _currentHeight;

    [ObservableProperty]
    private string _stretchMode = "Uniform";

    [ObservableProperty]
    private string _notes = string.Empty;

    /// <summary>
    /// Updates the modified date to now.
    /// </summary>
    public void MarkAsModified()
    {
        ModifiedDate = DateTime.Now;
    }

    /// <summary>
    /// Gets whether the image has been resized from original.
    /// </summary>
    public bool IsResized => OriginalWidth != CurrentWidth || OriginalHeight != CurrentHeight;
}
```

### 5. Update AspectRatioItem (if needed for selection)

**File: `Models/AspectRatioItem.cs`**

If this needs observable properties:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.Models;

/// <summary>
/// Represents an aspect ratio option for selection.
/// </summary>
public partial class AspectRatioItem : ObservableObject
{
    [ObservableProperty]
    private AspectRatio _ratio;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private double _widthRatio;

    [ObservableProperty]
    private double _heightRatio;

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayName => $"{Name} ({WidthRatio}:{HeightRatio})";

    public static List<AspectRatioItem> GetStandardAspectRatios()
    {
        return
        [
            new() { Ratio = AspectRatio.Original, Name = "Original", WidthRatio = 0, HeightRatio = 0 },
            new() { Ratio = AspectRatio.Square, Name = "Square", WidthRatio = 1, HeightRatio = 1 },
            new() { Ratio = AspectRatio.Letter, Name = "Letter", WidthRatio = 8.5, HeightRatio = 11 },
            new() { Ratio = AspectRatio.A4, Name = "A4", WidthRatio = 210, HeightRatio = 297 },
            new() { Ratio = AspectRatio.USBill, Name = "US Bill", WidthRatio = 2.61, HeightRatio = 6.14 },
            new() { Ratio = AspectRatio.Custom, Name = "Custom", WidthRatio = 1, HeightRatio = 1 },
        ];
    }
}
```

---

## Implementation Steps

1. Update `RecentProjectInfo.cs` to be observable
2. Update `SaveOptions.cs` to be observable  
3. Update `StrokeInfo.cs` to be observable
4. Update `PackageMetadata.cs` to be observable
5. Optionally update `AspectRatioItem.cs`
6. Ensure JSON serialization still works (test save/load)
7. Build and verify

---

## Validation Checklist

- [ ] All updated models compile
- [ ] JSON serialization still works for DTOs
- [ ] Recent projects list updates correctly
- [ ] Save options dialog bindings work
- [ ] No breaking changes to existing functionality

---

## Files Changed

| File | Change Type |
|------|-------------|
| `Models/RecentProjectInfo.cs` | Modified |
| `Models/SaveOptions.cs` | Modified |
| `Models/StrokeInfo.cs` | Modified |
| `Models/MeasurementControls/PackageMetadata.cs` | Modified |
| `Models/AspectRatioItem.cs` | Modified (optional) |

---

## Notes

### ObservableProperty Source Generator

The `[ObservableProperty]` attribute generates:
```csharp
// From this:
[ObservableProperty]
private string _name;

// Generates this:
public string Name
{
    get => _name;
    set => SetProperty(ref _name, value);
}
```

### Partial Methods for Property Changes

You can react to property changes:
```csharp
partial void OnNameChanging(string value) { /* Before change */ }
partial void OnNameChanged(string value) { /* After change */ }
```

### JSON Serialization Compatibility

`ObservableObject` doesn't interfere with System.Text.Json:
- Public properties are still serialized
- Use `[JsonIgnore]` for non-serialized properties
- Private backing fields are not serialized

### Don't Make DTOs Observable

DTOs like `DistanceMeasurementControlDto` should stay as POCOs:
- They're only for serialization/deserialization
- No UI directly binds to them
- Keep them simple and fast

### ObservableValidator vs ObservableObject

Choose the right base class for your models:

| Use Case | Base Class | Features |
|----------|-----------|----------|
| Simple data binding | `ObservableObject` | `INotifyPropertyChanged` only |
| Form with validation | `ObservableValidator` | Adds `INotifyDataErrorInfo`, validation attributes |
| ViewModel with commands | `ObservableObject` (or custom base) | Use with `[RelayCommand]` |

**ObservableValidator provides:**
- `[NotifyDataErrorInfo]` attribute support
- `ValidateAllProperties()` method
- `HasErrors` property
- `GetErrors()` method
- Integration with WPF validation error templates

**Common Validation Attributes:**
```csharp
[Required(ErrorMessage = "This field is required")]
[Range(1, 100, ErrorMessage = "Value must be between 1 and 100")]
[StringLength(50, MinimumLength = 3, ErrorMessage = "Must be 3-50 characters")]
[EmailAddress(ErrorMessage = "Invalid email format")]
[RegularExpression(@"^\d+$", ErrorMessage = "Must be numeric")]
[CustomValidation(typeof(MyValidator), nameof(MyValidator.ValidateCustom))]
```

---

## Next Steps

Proceed to **Step 07: AboutWindow MVVM Migration** to migrate the first simple window to MVVM.
