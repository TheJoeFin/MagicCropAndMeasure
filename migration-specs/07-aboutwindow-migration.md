# Step 07: AboutWindow MVVM Migration

## Objective
Migrate the AboutWindow to MVVM pattern as a learning exercise for the simpler windows before tackling more complex ones.

## Prerequisites
- Step 06 completed (Observable models)

## Why Start with AboutWindow?

AboutWindow is ideal for a first MVVM migration because:
1. **Simple** - Minimal state, mostly display-only
2. **No dependencies** - Doesn't need services
3. **Low risk** - Easy to verify correctness
4. **Pattern establishment** - Sets the template for other windows

## Current State Analysis

**File: `Windows/AboutWindow.xaml.cs`**

Current implementation:
- Displays app version
- Shows hyperlinks to GitHub/website
- Click handlers for hyperlinks
- Minimal code-behind logic

## Changes Required

### 1. Create AboutWindowViewModel

**File: `ViewModels/AboutWindowViewModel.cs`**

```csharp
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MagickCrop.ViewModels.Base;

namespace MagickCrop.ViewModels;

/// <summary>
/// ViewModel for the About window.
/// </summary>
public partial class AboutWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _appName = "Magic Crop & Measure";

    [ObservableProperty]
    private string _version = string.Empty;

    [ObservableProperty]
    private string _copyright = string.Empty;

    [ObservableProperty]
    private string _description = "A WPF application for image cropping and measurement.";

    public AboutWindowViewModel()
    {
        Title = "About";
        LoadVersionInfo();
    }

    private void LoadVersionInfo()
    {
        try
        {
            // Try to get version from package (MSIX)
            var package = Windows.ApplicationModel.Package.Current;
            var packageVersion = package.Id.Version;
            Version = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}";
        }
        catch
        {
            // Fallback to assembly version
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version;
            Version = assemblyVersion?.ToString(3) ?? "1.0.0";
        }

        Copyright = $"© {DateTime.Now.Year} Joe Finney";
    }

    /// <summary>
    /// Opens the GitHub repository in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenGitHub()
    {
        OpenUrl("https://github.com/TheJoeFin/MagicCropAndMeasure");
    }

    /// <summary>
    /// Opens the creator's website in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenWebsite()
    {
        OpenUrl("https://joefinapps.com");
    }

    /// <summary>
    /// Opens the ImageMagick website in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenImageMagick()
    {
        OpenUrl("https://imagemagick.org");
    }

    /// <summary>
    /// Opens the Magick.NET GitHub in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenMagickNet()
    {
        OpenUrl("https://github.com/dlemstra/Magick.NET");
    }

    /// <summary>
    /// Opens the WPF-UI GitHub in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenWpfUi()
    {
        OpenUrl("https://github.com/lepoco/wpfui");
    }

    /// <summary>
    /// Closes the window.
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        // This will be handled by the view - the command just signals intent
        // The view will close itself when this command executes
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Silently fail if browser can't be opened
        }
    }
}
```

### 2. Update AboutWindow XAML

**File: `Windows/AboutWindow.xaml`**

```xml
<ui:FluentWindow
    x:Class="MagickCrop.Windows.AboutWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    xmlns:vm="clr-namespace:MagickCrop.ViewModels"
    mc:Ignorable="d"
    Title="{Binding Title}"
    Width="400"
    Height="350"
    ResizeMode="NoResize"
    WindowStartupLocation="CenterOwner"
    d:DataContext="{d:DesignInstance Type=vm:AboutWindowViewModel, IsDesignTimeCreatable=True}">
    
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- App Icon and Name -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,10">
            <Image Source="/Iconv3.png" Width="48" Height="48" Margin="0,0,10,0"/>
            <TextBlock Text="{Binding AppName}" FontSize="24" FontWeight="SemiBold" VerticalAlignment="Center"/>
        </StackPanel>

        <!-- Version -->
        <TextBlock Grid.Row="1" 
                   Text="{Binding Version, StringFormat='Version {0}'}" 
                   HorizontalAlignment="Center" 
                   Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                   Margin="0,0,0,10"/>

        <!-- Description -->
        <TextBlock Grid.Row="2" 
                   Text="{Binding Description}" 
                   TextWrapping="Wrap" 
                   TextAlignment="Center"
                   Margin="0,0,0,20"/>

        <!-- Links Section -->
        <StackPanel Grid.Row="3" VerticalAlignment="Top">
            <TextBlock Text="Dependencies" FontWeight="SemiBold" Margin="0,0,0,10"/>
            
            <ui:HyperlinkButton Content="ImageMagick" 
                                Command="{Binding OpenImageMagickCommand}"
                                Margin="0,0,0,5"/>
            
            <ui:HyperlinkButton Content="Magick.NET" 
                                Command="{Binding OpenMagickNetCommand}"
                                Margin="0,0,0,5"/>
            
            <ui:HyperlinkButton Content="WPF-UI" 
                                Command="{Binding OpenWpfUiCommand}"
                                Margin="0,0,0,15"/>
            
            <TextBlock Text="Links" FontWeight="SemiBold" Margin="0,0,0,10"/>
            
            <ui:HyperlinkButton Content="GitHub Repository" 
                                Command="{Binding OpenGitHubCommand}"
                                Margin="0,0,0,5"/>
            
            <ui:HyperlinkButton Content="Creator's Website" 
                                Command="{Binding OpenWebsiteCommand}"/>
        </StackPanel>

        <!-- Footer -->
        <StackPanel Grid.Row="4">
            <TextBlock Text="{Binding Copyright}" 
                       HorizontalAlignment="Center"
                       Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                       Margin="0,0,0,10"/>
            
            <ui:Button Content="Close" 
                       Command="{Binding CloseCommand}"
                       HorizontalAlignment="Center"
                       Width="100"
                       Click="CloseButton_Click"/>
        </StackPanel>
    </Grid>
</ui:FluentWindow>
```

### 3. Update AboutWindow Code-Behind

**File: `Windows/AboutWindow.xaml.cs`**

```csharp
using MagickCrop.ViewModels;
using Wpf.Ui.Controls;

namespace MagickCrop.Windows;

/// <summary>
/// About window displaying application information.
/// </summary>
public partial class AboutWindow : FluentWindow
{
    public AboutWindow() : this(new AboutWindowViewModel())
    {
    }

    public AboutWindow(AboutWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }
}
```

### 4. Update DI Registration

**File: `App.xaml.cs`**

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // ... existing registrations ...

    // Register ViewModels
    services.AddTransient<AboutWindowViewModel>();

    // Register Windows/Views
    services.AddTransient<Windows.AboutWindow>(sp => 
        new Windows.AboutWindow(sp.GetRequiredService<AboutWindowViewModel>()));
    
    // ... rest of registrations ...
}
```

---

## Implementation Steps

1. Create `ViewModels/AboutWindowViewModel.cs`
2. Update `Windows/AboutWindow.xaml` with bindings
3. Update `Windows/AboutWindow.xaml.cs` to minimal code-behind
4. Update DI registration
5. Build and test

---

## Validation Checklist

- [ ] AboutWindowViewModel compiles
- [ ] AboutWindow displays correctly
- [ ] Version number shows correctly
- [ ] All hyperlinks work
- [ ] Close button works
- [ ] Window opens centered on parent

---

## Files Changed/Created

| File | Change Type |
|------|-------------|
| `ViewModels/AboutWindowViewModel.cs` | Created |
| `Windows/AboutWindow.xaml` | Modified |
| `Windows/AboutWindow.xaml.cs` | Modified |
| `App.xaml.cs` | Modified |

---

## Notes

### Why Keep Close Button Click Handler?

The Close button uses both:
- `Command="{Binding CloseCommand}"` - For ViewModel awareness
- `Click="CloseButton_Click"` - To actually close the window

This is because closing a window is a View concern. The ViewModel signals intent, but the View performs the action. Alternatively, you could use a behavior or `Window.Close()` via a service, but for simple cases this is acceptable.

### Design-Time DataContext

```xml
d:DataContext="{d:DesignInstance Type=vm:AboutWindowViewModel, IsDesignTimeCreatable=True}"
```

This enables:
- IntelliSense for bindings
- Visual preview in designer
- Compile-time binding error detection (with x:Compiled bindings)

### Pattern for Other Simple Windows

This pattern works for any simple window:
1. Create ViewModel with commands for actions
2. Create View with bindings to ViewModel
3. Minimal code-behind for View-specific operations (like Close)
4. Register both in DI

---

## Next Steps

Proceed to **Step 08: SaveWindow MVVM Migration** to migrate a more complex window with parameters.
