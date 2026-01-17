# Step 08: SaveWindow MVVM Migration

## Objective
Migrate SaveWindow to MVVM pattern, demonstrating how to handle windows that receive parameters.

## Prerequisites
- Step 07 completed (AboutWindow migration)

## Current State Analysis

**Current SaveWindow characteristics:**
- Takes `imagePath` as constructor parameter
- Displays a BitmapImage
- Supports drag-and-drop to export
- Has cleanup logic in Closing event
- Relatively simple but needs parameterization

## Changes Required

### 1. Create SaveWindowViewModel

**File: `ViewModels/SaveWindowViewModel.cs`**

```csharp
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MagickCrop.Models;
using MagickCrop.Services.Interfaces;
using MagickCrop.ViewModels.Base;

namespace MagickCrop.ViewModels;

/// <summary>
/// ViewModel for the Save window.
/// </summary>
public partial class SaveWindowViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialogService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _imagePath = string.Empty;

    [ObservableProperty]
    private BitmapImage? _displayImage;

    [ObservableProperty]
    private int _imageWidth;

    [ObservableProperty]
    private int _imageHeight;

    [ObservableProperty]
    private string _fileSize = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public SaveWindowViewModel() : this(
        App.GetService<IFileDialogService>(),
        App.GetService<INavigationService>())
    {
    }

    public SaveWindowViewModel(
        IFileDialogService fileDialogService,
        INavigationService navigationService)
    {
        _fileDialogService = fileDialogService;
        _navigationService = navigationService;
        Title = "Save Image";
    }

    /// <summary>
    /// Initializes the ViewModel with the image path.
    /// </summary>
    public void Initialize(string imagePath)
    {
        ImagePath = imagePath;
        LoadImage();
    }

    private void LoadImage()
    {
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
        {
            return;
        }

        try
        {
            IsLoading = true;

            // Load image with caching disabled for proper resource management
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(ImagePath);
            bitmap.EndInit();
            bitmap.Freeze(); // Make it thread-safe

            DisplayImage = bitmap;
            ImageWidth = bitmap.PixelWidth;
            ImageHeight = bitmap.PixelHeight;

            // Get file size
            var fileInfo = new FileInfo(ImagePath);
            FileSize = FormatFileSize(fileInfo.Length);
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to load image: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Copies the image to clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyToClipboard()
    {
        if (DisplayImage == null)
            return;

        try
        {
            Clipboard.SetImage(DisplayImage);
            _navigationService.ShowMessage("Image copied to clipboard.", "Success");
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to copy: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens file location in Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenFileLocation()
    {
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
            return;

        try
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{ImagePath}\"");
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to open location: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the image to a new location.
    /// </summary>
    [RelayCommand]
    private async Task SaveAs()
    {
        var filter = "JPEG Image|*.jpg|PNG Image|*.png|All Files|*.*";
        var defaultName = Path.GetFileName(ImagePath);
        
        var savePath = _fileDialogService.ShowSaveFileDialog(filter, defaultName, "Save Image As");
        
        if (string.IsNullOrEmpty(savePath))
            return;

        try
        {
            IsLoading = true;
            await Task.Run(() => File.Copy(ImagePath, savePath, overwrite: true));
            _navigationService.ShowMessage($"Image saved to:\n{savePath}", "Saved");
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to save: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Gets data for drag operations.
    /// </summary>
    public DataObject GetDragData()
    {
        var dataObject = new DataObject();
        
        if (!string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath))
        {
            // Add file path for file drop
            dataObject.SetFileDropList(new System.Collections.Specialized.StringCollection { ImagePath });
            
            // Add image data for apps that accept images directly
            if (DisplayImage != null)
            {
                dataObject.SetImage(DisplayImage);
            }
        }

        return dataObject;
    }

    /// <summary>
    /// Cleanup resources.
    /// </summary>
    public override void Cleanup()
    {
        base.Cleanup();
        
        // Clear the image reference
        DisplayImage = null;
        
        // Request garbage collection for the bitmap
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}
```

### 2. Update SaveWindow XAML

**File: `SaveWindow.xaml`**

```xml
<ui:FluentWindow
    x:Class="MagickCrop.SaveWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    xmlns:vm="clr-namespace:MagickCrop.ViewModels"
    mc:Ignorable="d"
    Title="{Binding Title}"
    Width="600"
    Height="500"
    MinWidth="400"
    MinHeight="300"
    WindowStartupLocation="CenterOwner"
    Closing="Window_Closing"
    d:DataContext="{d:DesignInstance Type=vm:SaveWindowViewModel, IsDesignTimeCreatable=True}">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Image Display (Draggable) -->
        <Border Grid.Row="0" 
                Margin="20,20,20,10" 
                BorderThickness="1"
                BorderBrush="{DynamicResource ControlStrokeColorDefaultBrush}"
                Background="{DynamicResource ControlFillColorSecondaryBrush}"
                CornerRadius="4"
                MouseMove="Image_MouseMove"
                Cursor="Hand">
            <Grid>
                <Image Source="{Binding DisplayImage}" 
                       Stretch="Uniform"
                       RenderOptions.BitmapScalingMode="HighQuality"/>
                
                <!-- Loading Overlay -->
                <Border Background="#80000000"
                        Visibility="{Binding IsLoading, Converter={StaticResource BooleanToVisibilityConverter}}">
                    <ui:ProgressRing IsIndeterminate="True" 
                                     Width="50" 
                                     Height="50"/>
                </Border>
                
                <!-- Drag Hint -->
                <TextBlock Text="Drag image to another app" 
                           HorizontalAlignment="Center" 
                           VerticalAlignment="Bottom"
                           Margin="0,0,0,10"
                           Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                           FontSize="11"/>
            </Grid>
        </Border>

        <!-- Image Info -->
        <StackPanel Grid.Row="1" 
                    Orientation="Horizontal" 
                    HorizontalAlignment="Center" 
                    Margin="0,0,0,10">
            <TextBlock Text="{Binding ImageWidth}" />
            <TextBlock Text=" × " />
            <TextBlock Text="{Binding ImageHeight}" />
            <TextBlock Text=" px  •  " />
            <TextBlock Text="{Binding FileSize}" />
        </StackPanel>

        <!-- Actions -->
        <StackPanel Grid.Row="2" 
                    Orientation="Horizontal" 
                    HorizontalAlignment="Center" 
                    Margin="0,0,0,20">
            <ui:Button Content="Copy to Clipboard" 
                       Command="{Binding CopyToClipboardCommand}"
                       Margin="0,0,10,0"
                       Icon="{ui:SymbolIcon Copy24}"/>
            
            <ui:Button Content="Open Location" 
                       Command="{Binding OpenFileLocationCommand}"
                       Margin="0,0,10,0"
                       Icon="{ui:SymbolIcon Folder24}"/>
            
            <ui:Button Content="Save As..." 
                       Command="{Binding SaveAsCommand}"
                       Appearance="Primary"
                       Icon="{ui:SymbolIcon Save24}"/>
        </StackPanel>
    </Grid>
</ui:FluentWindow>
```

### 3. Update SaveWindow Code-Behind

**File: `SaveWindow.xaml.cs`**

```csharp
using System.Windows;
using System.Windows.Input;
using MagickCrop.ViewModels;
using Wpf.Ui.Controls;

namespace MagickCrop;

/// <summary>
/// Window for displaying and saving processed images.
/// </summary>
public partial class SaveWindow : FluentWindow
{
    private SaveWindowViewModel ViewModel => (SaveWindowViewModel)DataContext;

    /// <summary>
    /// Parameterless constructor for DI.
    /// </summary>
    public SaveWindow() : this(new SaveWindowViewModel())
    {
    }

    /// <summary>
    /// Constructor with ViewModel injection.
    /// </summary>
    public SaveWindow(SaveWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>
    /// Constructor with image path (backward compatibility).
    /// </summary>
    public SaveWindow(string imagePath) : this(new SaveWindowViewModel())
    {
        ViewModel.Initialize(imagePath);
    }

    /// <summary>
    /// Initialize with an image path after construction.
    /// </summary>
    public void Initialize(string imagePath)
    {
        ViewModel.Initialize(imagePath);
    }

    private void Image_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var data = ViewModel.GetDragData();
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
            }
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        ViewModel.Cleanup();
    }
}
```

### 4. Update WindowFactory

**File: `Services/WindowFactory.cs`**

```csharp
using MagickCrop.Services.Interfaces;
using MagickCrop.ViewModels;

namespace MagickCrop.Services;

/// <summary>
/// Factory for creating windows that require special initialization.
/// </summary>
public class WindowFactory : IWindowFactory
{
    private readonly IServiceProvider _serviceProvider;

    public WindowFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public SaveWindow CreateSaveWindow(string imagePath)
    {
        var viewModel = _serviceProvider.GetService(typeof(SaveWindowViewModel)) as SaveWindowViewModel
            ?? new SaveWindowViewModel();
        
        viewModel.Initialize(imagePath);
        return new SaveWindow(viewModel);
    }
}
```

### 5. Update DI Registration

**File: `App.xaml.cs`**

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // ... existing registrations ...

    // Register ViewModels
    services.AddTransient<AboutWindowViewModel>();
    services.AddTransient<SaveWindowViewModel>();

    // Register Windows/Views
    services.AddTransient<Windows.AboutWindow>();
    services.AddTransient<SaveWindow>();

    // Register Factory
    services.AddSingleton<IWindowFactory, WindowFactory>();
    
    // ... rest of registrations ...
}
```

### 6. Add BooleanToVisibilityConverter

**File: `Converters/BooleanToVisibilityConverter.cs`**

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Converts boolean values to Visibility.
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}
```

**Add to App.xaml resources:**

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ui:ThemesDictionary Theme="Dark" />
            <ui:ControlsDictionary />
        </ResourceDictionary.MergedDictionaries>
        
        <converters:BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter"/>
    </ResourceDictionary>
</Application.Resources>
```

---

## Implementation Steps

1. Create `Converters` folder
2. Create `BooleanToVisibilityConverter.cs`
3. Create `ViewModels/SaveWindowViewModel.cs`
4. Update `SaveWindow.xaml` with bindings
5. Update `SaveWindow.xaml.cs` with minimal code-behind
6. Update `WindowFactory.cs`
7. Update DI registration
8. Add converter to App.xaml resources
9. Build and test

---

## Validation Checklist

- [ ] SaveWindowViewModel compiles
- [ ] SaveWindow displays image correctly
- [ ] Drag-and-drop works
- [ ] Copy to clipboard works
- [ ] Open file location works
- [ ] Save As works
- [ ] Loading indicator shows during operations
- [ ] Window closes and cleans up properly

---

## Files Changed/Created

| File | Change Type |
|------|-------------|
| `Converters/BooleanToVisibilityConverter.cs` | Created |
| `ViewModels/SaveWindowViewModel.cs` | Created |
| `SaveWindow.xaml` | Modified |
| `SaveWindow.xaml.cs` | Modified |
| `Services/WindowFactory.cs` | Modified |
| `App.xaml` | Modified |
| `App.xaml.cs` | Modified |

---

## Notes

### Handling Window Parameters

Three approaches for parameterized windows:

1. **Initialize Method** (recommended):
   ```csharp
   var window = new SaveWindow(viewModel);
   viewModel.Initialize(imagePath);
   ```

2. **Constructor Parameter**:
   ```csharp
   public SaveWindow(string imagePath) // Backward compatible
   ```

3. **Property Setting**:
   ```csharp
   window.ViewModel.ImagePath = imagePath;
   ```

We use Initialize() because it:
- Works with DI (parameterless constructor)
- Allows async initialization
- Clear separation of creation vs initialization

### Drag and Drop

The drag-and-drop logic stays in code-behind because:
- It's a View concern (mouse events)
- Requires Window reference
- ViewModel provides the data, View handles the gesture

### Resource Cleanup

The `Cleanup()` method is called on window closing:
- Clears image references
- Forces garbage collection
- Prevents memory leaks from cached bitmaps

---

## Next Steps

Proceed to **Step 09: WelcomeMessage Control Migration** to migrate the first UserControl to MVVM.
