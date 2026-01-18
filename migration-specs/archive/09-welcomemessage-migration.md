# Step 09: WelcomeMessage Control Migration

## Objective
Migrate the WelcomeMessage UserControl to MVVM, demonstrating how to handle complex controls with events and commands.

## Prerequisites
- Step 08 completed (SaveWindow migration)

---

## ⚠️ This step has been broken into smaller sub-steps for easier implementation

Complete these sub-steps in order:

| Sub-Step | Description | Estimated Effort |
|----------|-------------|-----------------|
| **09a** | Create WelcomeViewModel with basic properties (WelcomeText, SubtitleText, HasRecentProjects) | 30 min |
| **09b** | Add RecentProjects ObservableCollection and LoadRecentProjectsAsync method | 30 min |
| **09c** | Add clipboard detection logic (CanPasteFromClipboard, RefreshClipboard) | 30 min |
| **09d** | Add commands for recent project actions (OpenRecentProject, DeleteRecentProject) | 30 min |
| **09e** | Add bridging commands (OpenFileCommand, PasteFromClipboardCommand, etc.) | 30 min |
| **09f** | Create InverseBooleanToVisibilityConverter | 15 min |
| **09g** | Update WelcomeMessage.xaml with data bindings | 45 min |
| **09h** | Update WelcomeMessage.xaml.cs with DependencyProperties and ViewModel wiring | 30 min |
| **09i** | Update DI registration and test | 20 min |

Each sub-step should be its own commit with a working build.

---

## Current State Analysis

**WelcomeMessage control characteristics:**
- Displays welcome text and recent projects
- Has multiple button events (PrimaryButtonEvent, PasteButtonEvent, OverlayButtonEvent)
- Uses DependencyProperties for event handlers
- Populates recent projects from Singleton<RecentProjectsManager>
- Has async clipboard detection logic

## Changes Required

### 1. Create WelcomeViewModel

**File: `ViewModels/WelcomeViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MagickCrop.Messages;
using MagickCrop.Models;
using MagickCrop.Services.Interfaces;
using MagickCrop.ViewModels.Base;

namespace MagickCrop.ViewModels;

/// <summary>
/// ViewModel for the Welcome message control.
/// </summary>
public partial class WelcomeViewModel : ViewModelBase
{
    private readonly IRecentProjectsService _recentProjectsService;
    private readonly IClipboardService _clipboardService;
    private readonly IFileDialogService _fileDialogService;

    [ObservableProperty]
    private string _welcomeText = "Welcome to Magic Crop & Measure";

    [ObservableProperty]
    private string _subtitleText = "Open an image to get started";

    [ObservableProperty]
    private bool _hasRecentProjects;

    [ObservableProperty]
    private bool _canPasteFromClipboard;

    [ObservableProperty]
    private bool _isCheckingClipboard;

    /// <summary>
    /// Collection of recent projects to display.
    /// </summary>
    public ObservableCollection<RecentProjectInfo> RecentProjects { get; } = [];

    // Commands that parent can bind to
    public ICommand? OpenFileCommand { get; set; }
    public ICommand? PasteFromClipboardCommand { get; set; }
    public ICommand? OpenOverlayCommand { get; set; }

    public WelcomeViewModel() : this(
        App.GetService<IRecentProjectsService>(),
        App.GetService<IClipboardService>(),
        App.GetService<IFileDialogService>())
    {
    }

    public WelcomeViewModel(
        IRecentProjectsService recentProjectsService,
        IClipboardService clipboardService,
        IFileDialogService fileDialogService)
    {
        _recentProjectsService = recentProjectsService;
        _clipboardService = clipboardService;
        _fileDialogService = fileDialogService;
        
        Title = "Welcome";
    }

    public override async Task InitializeAsync()
    {
        await LoadRecentProjectsAsync();
        await CheckClipboardAsync();
        
        // Register for project changes
        Register<RecentProjectsChangedMessage>(_ => _ = LoadRecentProjectsAsync());
    }

    private async Task LoadRecentProjectsAsync()
    {
        try
        {
            await _recentProjectsService.LoadRecentProjectsAsync();
            
            RecentProjects.Clear();
            foreach (var project in _recentProjectsService.RecentProjects.Take(10))
            {
                RecentProjects.Add(project);
            }
            
            HasRecentProjects = RecentProjects.Count > 0;
        }
        catch
        {
            HasRecentProjects = false;
        }
    }

    private async Task CheckClipboardAsync()
    {
        try
        {
            IsCheckingClipboard = true;
            
            // Run on UI thread since Clipboard requires it
            await Task.Run(() =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    CanPasteFromClipboard = _clipboardService.ContainsImage();
                });
            });
        }
        catch
        {
            CanPasteFromClipboard = false;
        }
        finally
        {
            IsCheckingClipboard = false;
        }
    }

    /// <summary>
    /// Refresh clipboard state (call when window gains focus).
    /// </summary>
    [RelayCommand]
    private async Task RefreshClipboard()
    {
        await CheckClipboardAsync();
    }

    /// <summary>
    /// Called when a recent project is clicked.
    /// </summary>
    [RelayCommand]
    private void OpenRecentProject(RecentProjectInfo? project)
    {
        if (project == null)
            return;

        Send(new ProjectOpenedMessage(project.FilePath, project.Id));
    }

    /// <summary>
    /// Called when a recent project delete is requested.
    /// </summary>
    [RelayCommand]
    private async Task DeleteRecentProject(RecentProjectInfo? project)
    {
        if (project == null)
            return;

        await _recentProjectsService.RemoveRecentProjectAsync(project.Id);
        RecentProjects.Remove(project);
        HasRecentProjects = RecentProjects.Count > 0;
    }

    /// <summary>
    /// Opens file browser to select an image.
    /// </summary>
    [RelayCommand]
    private void BrowseForImage()
    {
        // Execute the parent's command if set
        if (OpenFileCommand?.CanExecute(null) == true)
        {
            OpenFileCommand.Execute(null);
            return;
        }

        // Fallback: Use file dialog directly
        var filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.mcm|All Files|*.*";
        var filePath = _fileDialogService.ShowOpenFileDialog(filter, "Open Image");
        
        if (!string.IsNullOrEmpty(filePath))
        {
            Send(new ImageLoadedMessage(filePath, 0, 0));
        }
    }

    /// <summary>
    /// Pastes image from clipboard.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPasteFromClipboard))]
    private void PasteImage()
    {
        if (PasteFromClipboardCommand?.CanExecute(null) == true)
        {
            PasteFromClipboardCommand.Execute(null);
        }
    }

    /// <summary>
    /// Opens the overlay/welcome screen.
    /// </summary>
    [RelayCommand]
    private void ShowOverlay()
    {
        if (OpenOverlayCommand?.CanExecute(null) == true)
        {
            OpenOverlayCommand.Execute(null);
        }
    }

    /// <summary>
    /// Clears all recent projects.
    /// </summary>
    [RelayCommand]
    private async Task ClearAllRecentProjects()
    {
        await _recentProjectsService.ClearRecentProjectsAsync();
        RecentProjects.Clear();
        HasRecentProjects = false;
    }
}
```

### 2. Update WelcomeMessage XAML

**File: `Controls/WelcomeMessage.xaml`**

```xml
<UserControl
    x:Class="MagickCrop.Controls.WelcomeMessage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    xmlns:controls="clr-namespace:MagickCrop.Controls"
    xmlns:vm="clr-namespace:MagickCrop.ViewModels"
    mc:Ignorable="d"
    d:DataContext="{d:DesignInstance Type=vm:WelcomeViewModel, IsDesignTimeCreatable=True}"
    d:DesignHeight="450"
    d:DesignWidth="800">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Welcome Header -->
        <StackPanel Grid.Row="1" HorizontalAlignment="Center" Margin="0,0,0,30">
            <TextBlock Text="{Binding WelcomeText}" 
                       FontSize="32" 
                       FontWeight="SemiBold" 
                       HorizontalAlignment="Center"/>
            <TextBlock Text="{Binding SubtitleText}" 
                       FontSize="16"
                       Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                       HorizontalAlignment="Center"
                       Margin="0,8,0,0"/>
        </StackPanel>

        <!-- Action Buttons -->
        <StackPanel Grid.Row="2" HorizontalAlignment="Center">
            <!-- Primary Actions -->
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,20">
                <ui:Button Content="Open Image" 
                           Command="{Binding BrowseForImageCommand}"
                           Appearance="Primary"
                           Icon="{ui:SymbolIcon FolderOpen24}"
                           Width="150"
                           Margin="0,0,10,0"/>
                
                <ui:Button Content="Paste" 
                           Command="{Binding PasteImageCommand}"
                           IsEnabled="{Binding CanPasteFromClipboard}"
                           Icon="{ui:SymbolIcon ClipboardPaste24}"
                           Width="120"
                           Margin="0,0,10,0"/>
                
                <ui:Button Content="Overlay" 
                           Command="{Binding ShowOverlayCommand}"
                           Icon="{ui:SymbolIcon Layer24}"
                           Width="120"/>
            </StackPanel>

            <!-- Recent Projects Section -->
            <StackPanel Visibility="{Binding HasRecentProjects, Converter={StaticResource BooleanToVisibilityConverter}}">
                <Grid Margin="0,20,0,10">
                    <TextBlock Text="Recent Projects" 
                               FontWeight="SemiBold" 
                               HorizontalAlignment="Left"/>
                    <ui:HyperlinkButton Content="Clear All" 
                                        Command="{Binding ClearAllRecentProjectsCommand}"
                                        HorizontalAlignment="Right"
                                        FontSize="11"/>
                </Grid>
                
                <ItemsControl ItemsSource="{Binding RecentProjects}" 
                              MaxWidth="600">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <controls:RecentProjectItem 
                                Project="{Binding}"
                                ProjectClickedCommand="{Binding DataContext.OpenRecentProjectCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                ProjectDeletedCommand="{Binding DataContext.DeleteRecentProjectCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                Margin="0,0,0,5"/>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </StackPanel>

            <!-- Empty State -->
            <TextBlock Text="No recent projects" 
                       Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                       HorizontalAlignment="Center"
                       Margin="0,20,0,0"
                       Visibility="{Binding HasRecentProjects, Converter={StaticResource InverseBooleanToVisibilityConverter}}"/>
        </StackPanel>
    </Grid>
</UserControl>
```

### 3. Update WelcomeMessage Code-Behind

**File: `Controls/WelcomeMessage.xaml.cs`**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MagickCrop.ViewModels;

namespace MagickCrop.Controls;

/// <summary>
/// Welcome message control displayed when no image is loaded.
/// </summary>
public partial class WelcomeMessage : UserControl
{
    public WelcomeViewModel ViewModel => (WelcomeViewModel)DataContext;

    #region Dependency Properties (for backward compatibility)

    public static readonly DependencyProperty OpenFileCommandProperty =
        DependencyProperty.Register(nameof(OpenFileCommand), typeof(ICommand), typeof(WelcomeMessage),
            new PropertyMetadata(null, OnOpenFileCommandChanged));

    public static readonly DependencyProperty PasteCommandProperty =
        DependencyProperty.Register(nameof(PasteCommand), typeof(ICommand), typeof(WelcomeMessage),
            new PropertyMetadata(null, OnPasteCommandChanged));

    public static readonly DependencyProperty OverlayCommandProperty =
        DependencyProperty.Register(nameof(OverlayCommand), typeof(ICommand), typeof(WelcomeMessage),
            new PropertyMetadata(null, OnOverlayCommandChanged));

    public ICommand? OpenFileCommand
    {
        get => (ICommand?)GetValue(OpenFileCommandProperty);
        set => SetValue(OpenFileCommandProperty, value);
    }

    public ICommand? PasteCommand
    {
        get => (ICommand?)GetValue(PasteCommandProperty);
        set => SetValue(PasteCommandProperty, value);
    }

    public ICommand? OverlayCommand
    {
        get => (ICommand?)GetValue(OverlayCommandProperty);
        set => SetValue(OverlayCommandProperty, value);
    }

    private static void OnOpenFileCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WelcomeMessage control && e.NewValue is ICommand command)
        {
            control.ViewModel.OpenFileCommand = command;
        }
    }

    private static void OnPasteCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WelcomeMessage control && e.NewValue is ICommand command)
        {
            control.ViewModel.PasteFromClipboardCommand = command;
        }
    }

    private static void OnOverlayCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WelcomeMessage control && e.NewValue is ICommand command)
        {
            control.ViewModel.OpenOverlayCommand = command;
        }
    }

    #endregion

    public WelcomeMessage()
    {
        var viewModel = App.GetService<WelcomeViewModel>();
        DataContext = viewModel;
        InitializeComponent();
        
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        
        // Refresh clipboard when control gains focus
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.Activated += Window_Activated;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Cleanup();
        
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.Activated -= Window_Activated;
        }
    }

    private async void Window_Activated(object? sender, EventArgs e)
    {
        // Refresh clipboard state when window gains focus
        await ViewModel.RefreshClipboardCommand.ExecuteAsync(null);
    }
}
```

### 4. Create InverseBooleanToVisibilityConverter

**File: `Converters/InverseBooleanToVisibilityConverter.cs`**

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MagickCrop.Converters;

/// <summary>
/// Converts boolean values to Visibility (inverted).
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return true;
    }
}
```

**Add to App.xaml:**

```xml
<converters:InverseBooleanToVisibilityConverter x:Key="InverseBooleanToVisibilityConverter"/>
```

### 5. Update DI Registration

**File: `App.xaml.cs`**

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // ... existing registrations ...

    // Register ViewModels
    services.AddTransient<WelcomeViewModel>();
    
    // ... rest of registrations ...
}
```

---

## Implementation Steps

1. Create `ViewModels/WelcomeViewModel.cs`
2. Create `Converters/InverseBooleanToVisibilityConverter.cs`
3. Update `Controls/WelcomeMessage.xaml` with bindings
4. Update `Controls/WelcomeMessage.xaml.cs`
5. Add converter to App.xaml
6. Update DI registration
7. Build and test

---

## Validation Checklist

- [ ] WelcomeViewModel compiles
- [ ] WelcomeMessage displays correctly
- [ ] Recent projects load and display
- [ ] Open Image button works
- [ ] Paste button shows/hides based on clipboard
- [ ] Recent project click opens project
- [ ] Recent project delete works
- [ ] Clear All removes all recent projects

---

## Files Changed/Created

| File | Change Type |
|------|-------------|
| `ViewModels/WelcomeViewModel.cs` | Created |
| `Converters/InverseBooleanToVisibilityConverter.cs` | Created |
| `Controls/WelcomeMessage.xaml` | Modified |
| `Controls/WelcomeMessage.xaml.cs` | Modified |
| `App.xaml` | Modified |
| `App.xaml.cs` | Modified |

---

## Notes

### Bridging Commands Pattern

The control exposes DependencyProperties for parent commands while using ViewModel commands internally. This allows:
1. Parent can pass commands via XAML binding
2. ViewModel handles the logic
3. Backward compatibility with existing code

### Clipboard Detection

Clipboard operations must run on UI thread:
```csharp
Application.Current?.Dispatcher.Invoke(() =>
{
    CanPasteFromClipboard = _clipboardService.ContainsImage();
});
```

### Event to Command Migration

Old pattern (events):
```csharp
public event RoutedEventHandler? PrimaryButtonEvent;
```

New pattern (commands):
```csharp
public ICommand? OpenFileCommand { get; set; }
```

Commands are more flexible and testable than events.

---

## Next Steps

Proceed to **Step 10: RecentProjectItem Control Migration** to complete the welcome screen controls.
