# Step 13: MainWindow ViewModel - State Management

## Objective
Create the MainWindowViewModel and extract application state management from MainWindow.xaml.cs.

## Prerequisites
- Step 12 completed (Measurement controls migration)

---

## ⚠️ This step has been broken into smaller sub-steps for easier implementation

**Complete these sub-steps in order:**

| Sub-Step | Description | Estimated Effort | Status |
|----------|-------------|-----------------|--------|
| **13a** ✅ | Create MainWindowViewModel.cs with basic constructor and service injection | 30 min | **DONE** |
| **13b** ✅ | Add image state properties (CurrentImage, HasImage, ImageWidth, ImageHeight, OriginalImage) | 30 min | **DONE** |
| **13c** ✅ | Add tool state properties (CurrentTool enum, SelectedTool, IsToolSelected) | 30 min | **DONE** |
| **13d** ✅ | Add UI state properties (Zoom, StatusBarText, WindowTitle) | 20 min | **DONE** |
| **13e** ✅ | Add project state properties (HasUnsavedChanges, ProjectFileName, IsProjectLoaded) | 30 min | **DONE** |
| **13f** ✅ | Add tool selection commands (SelectToolCommand for each tool type) | 45 min | **DONE** |
| **13g** ✅ | Add undo/redo state (UndoStack, CanUndo, CanRedo) - properties only | 30 min | **DONE** |
| **13h** ✅ | Add UndoCommand and RedoCommand | 30 min | **DONE** |
| **13i** ✅ | Wire MainWindowViewModel in App.xaml.cs DI registration | 20 min | **DONE** |
| **13j** ✅ | Add DataContext binding in MainWindow.xaml.cs constructor | 20 min | **DONE** |
| **13k** ✅ | Bind first set of properties in MainWindow.xaml (test with simple bindings) | 30 min | **DONE** |

Each sub-step should be its own commit with a working build.

**Note:** Do NOT remove the existing code-behind yet. This step adds the ViewModel alongside existing code. Later steps will gradually move logic from code-behind to ViewModel.

---

## Why State Management First?

The MainWindow has ~3,500 lines of code. We break it down by responsibility:
1. **State Management** (this step) - App state, tool selection, undo/redo
2. **Image Operations** (Step 14) - Load, crop, rotate, etc.
3. **Measurement Management** (Step 15) - Collections, adding/removing
4. **File Operations** (Step 16) - Save, load, export

Starting with state management establishes the ViewModel foundation.

## Changes Required

### 1. Create MainWindowViewModel

**File: `ViewModels/MainWindowViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MagickCrop.Messages;
using MagickCrop.Models;
using MagickCrop.Services.Interfaces;
using MagickCrop.ViewModels.Base;
using MagickCrop.ViewModels.Measurements;

namespace MagickCrop.ViewModels;

/// <summary>
/// Main ViewModel for the application.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IRecentProjectsService _recentProjectsService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IClipboardService _clipboardService;
    private readonly INavigationService _navigationService;

    #region Application State

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveProjectCommand))]
    [NotifyCanExecuteChangedFor(nameof(CropCommand))]
    [NotifyCanExecuteChangedFor(nameof(RotateCommand))]
    [NotifyCanExecuteChangedFor(nameof(FlipHorizontalCommand))]
    [NotifyCanExecuteChangedFor(nameof(FlipVerticalCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartMeasurementPlacementCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyImageToClipboardCommand))]
    [NotifyPropertyChangedFor(nameof(CanPerformImageOperations))]
    private bool _hasImage;

    [ObservableProperty]
    private bool _isWelcomeVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string? _currentFilePath;

    [ObservableProperty]
    private Guid _currentProjectId;

    /// <summary>
    /// Gets whether image operations can be performed.
    /// Used by multiple commands as CanExecute.
    /// </summary>
    public bool CanPerformImageOperations => HasImage && !IsLoading;

    #endregion

    #region Tool State

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelPlacementCommand))]
    private ToolMode _currentTool = ToolMode.None;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelPlacementCommand))]
    private bool _isPlacingMeasurement;

    [ObservableProperty]
    private PlacementState _placementState = PlacementState.NotPlacing;

    [ObservableProperty]
    private int _placementStep;

    #endregion

    #region Image State

    [ObservableProperty]
    private BitmapSource? _currentImage;

    [ObservableProperty]
    private int _imageWidth;

    [ObservableProperty]
    private int _imageHeight;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyPreciseRotationCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelRotationCommand))]
    private bool _isRotating;

    [ObservableProperty]
    private double _rotationAngle;

    #endregion

    #region Scale/Units

    [ObservableProperty]
    private double _globalScaleFactor = 1.0;

    [ObservableProperty]
    private string _globalUnits = "px";

    partial void OnGlobalScaleFactorChanged(double value)
    {
        Send(new ScaleFactorChangedMessage(value, GlobalUnits));
    }

    partial void OnGlobalUnitsChanged(string value)
    {
        Send(new ScaleFactorChangedMessage(GlobalScaleFactor, value));
    }

    #endregion

    #region Undo/Redo State

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private bool _canUndo;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RedoCommand))]
    private bool _canRedo;

    private readonly UndoRedo _undoRedo = new();

    #endregion

    #region UI State

    [ObservableProperty]
    private bool _showMeasurementPanel = true;

    [ObservableProperty]
    private bool _showToolbar = true;

    [ObservableProperty]
    private AspectRatioItem? _selectedAspectRatio;

    public ObservableCollection<AspectRatioItem> AspectRatios { get; } = [];

    #endregion

    public MainWindowViewModel() : this(
        App.GetService<IRecentProjectsService>(),
        App.GetService<IFileDialogService>(),
        App.GetService<IClipboardService>(),
        App.GetService<INavigationService>())
    {
    }

    public MainWindowViewModel(
        IRecentProjectsService recentProjectsService,
        IFileDialogService fileDialogService,
        IClipboardService clipboardService,
        INavigationService navigationService)
    {
        _recentProjectsService = recentProjectsService;
        _fileDialogService = fileDialogService;
        _clipboardService = clipboardService;
        _navigationService = navigationService;

        Title = "Magic Crop & Measure";
        InitializeAspectRatios();
        SetupUndoRedoCallbacks();
    }

    public override Task InitializeAsync()
    {
        // Register for messages
        Register<ImageLoadedMessage>(OnImageLoaded);
        Register<ProjectOpenedMessage>(OnProjectOpened);
        Register<ImageModifiedMessage>(OnImageModified);
        
        return base.InitializeAsync();
    }

    private void InitializeAspectRatios()
    {
        foreach (var ratio in AspectRatioItem.GetStandardAspectRatios())
        {
            AspectRatios.Add(ratio);
        }
        SelectedAspectRatio = AspectRatios.FirstOrDefault();
    }

    private void SetupUndoRedoCallbacks()
    {
        _undoRedo.StateChanged += (_, _) =>
        {
            CanUndo = _undoRedo.CanUndo;
            CanRedo = _undoRedo.CanRedo;
            Send(new UndoRedoStateChangedMessage(CanUndo, CanRedo));
        };
    }

    #region Message Handlers

    private void OnImageLoaded(ImageLoadedMessage message)
    {
        HasImage = true;
        IsWelcomeVisible = false;
        CurrentFilePath = message.FilePath;
        ImageWidth = message.Width;
        ImageHeight = message.Height;
        UpdateWindowTitle();
    }

    private void OnProjectOpened(ProjectOpenedMessage message)
    {
        CurrentProjectId = message.ProjectId;
        CurrentFilePath = message.FilePath;
    }

    private void OnImageModified(ImageModifiedMessage message)
    {
        IsDirty = true;
        UpdateWindowTitle();
    }

    #endregion

    #region Tool Commands

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void SelectTool(ToolMode tool)
    {
        // Cancel any in-progress placement
        if (IsPlacingMeasurement)
        {
            CancelPlacement();
        }

        CurrentTool = tool;
        Send(new ActiveToolChangedMessage(tool.ToString()));
    }

    [RelayCommand(CanExecute = nameof(CanPerformImageOperations))]
    private void StartMeasurementPlacement(string measurementType)
    {
        IsPlacingMeasurement = true;
        PlacementState = PlacementState.WaitingForFirstPoint;
        PlacementStep = 0;
        
        // Set tool based on measurement type
        CurrentTool = measurementType switch
        {
            "Distance" => ToolMode.MeasureDistance,
            "Angle" => ToolMode.MeasureAngle,
            "Rectangle" => ToolMode.MeasureRectangle,
            "Circle" => ToolMode.MeasureCircle,
            "Polygon" => ToolMode.MeasurePolygon,
            _ => ToolMode.None
        };
    }

    [RelayCommand(CanExecute = nameof(IsPlacingMeasurement))]
    private void CancelPlacement()
    {
        IsPlacingMeasurement = false;
        PlacementState = PlacementState.NotPlacing;
        PlacementStep = 0;
        CurrentTool = ToolMode.None;
    }

    [RelayCommand]
    private void AdvancePlacementStep()
    {
        PlacementStep++;
        
        PlacementState = CurrentTool switch
        {
            ToolMode.MeasureDistance => PlacementStep >= 1 ? PlacementState.Complete : PlacementState.WaitingForNextPoint,
            ToolMode.MeasureAngle => PlacementStep >= 2 ? PlacementState.Complete : PlacementState.WaitingForNextPoint,
            ToolMode.MeasureRectangle => PlacementStep >= 1 ? PlacementState.Complete : PlacementState.WaitingForNextPoint,
            ToolMode.MeasureCircle => PlacementStep >= 1 ? PlacementState.Complete : PlacementState.WaitingForNextPoint,
            _ => PlacementState.WaitingForNextPoint
        };

        if (PlacementState == PlacementState.Complete)
        {
            IsPlacingMeasurement = false;
            CurrentTool = ToolMode.None;
        }
    }

    #endregion

    #region Undo/Redo Commands

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _undoRedo.Undo();
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _undoRedo.Redo();
        IsDirty = true;
    }

    /// <summary>
    /// Adds an item to the undo stack.
    /// </summary>
    public void AddUndoItem(UndoRedoItem item)
    {
        _undoRedo.AddUndo(item);
    }

    /// <summary>
    /// Clears the undo/redo history.
    /// </summary>
    public void ClearUndoHistory()
    {
        _undoRedo.Clear();
    }

    #endregion

    #region UI Commands

    [RelayCommand]
    private void ToggleMeasurementPanel()
    {
        ShowMeasurementPanel = !ShowMeasurementPanel;
    }

    [RelayCommand]
    private void ResetView()
    {
        ZoomLevel = 1.0;
        RotationAngle = 0;
        IsRotating = false;
    }

    [RelayCommand]
    private void ShowAbout()
    {
        _navigationService.ShowDialog<Windows.AboutWindow>();
    }

    #endregion

    #region Scale Commands

    [RelayCommand]
    private void SetScale(double scaleFactor, string units)
    {
        GlobalScaleFactor = scaleFactor;
        GlobalUnits = units;
    }

    [RelayCommand]
    private void SetScaleFromMeasurement(double pixelLength)
    {
        // Show dialog to get real-world length
        // This would typically show a dialog, for now just log
        // The actual dialog will be handled by the View
    }

    #endregion

    #region Helpers

    private void UpdateWindowTitle()
    {
        var fileName = string.IsNullOrEmpty(CurrentFilePath) 
            ? "Untitled" 
            : System.IO.Path.GetFileName(CurrentFilePath);
        
        var dirtyMarker = IsDirty ? "*" : "";
        WindowTitle = $"{fileName}{dirtyMarker} - Magic Crop & Measure";
    }

    /// <summary>
    /// Marks the document as saved (not dirty).
    /// </summary>
    public void MarkAsSaved()
    {
        IsDirty = false;
        UpdateWindowTitle();
    }

    /// <summary>
    /// Resets the application state for a new document.
    /// </summary>
    public void ResetForNewDocument()
    {
        HasImage = false;
        IsWelcomeVisible = true;
        IsDirty = false;
        CurrentFilePath = null;
        CurrentProjectId = Guid.Empty;
        CurrentImage = null;
        ZoomLevel = 1.0;
        RotationAngle = 0;
        GlobalScaleFactor = 1.0;
        GlobalUnits = "px";
        ClearUndoHistory();
        UpdateWindowTitle();
    }

    #endregion
}

#region Enums

/// <summary>
/// Available tool modes.
/// </summary>
public enum ToolMode
{
    None,
    Pan,
    Crop,
    Rotate,
    MeasureDistance,
    MeasureAngle,
    MeasureRectangle,
    MeasureCircle,
    MeasurePolygon,
    DrawLine,
    DrawFreehand,
    HorizontalGuide,
    VerticalGuide
}

/// <summary>
/// States for measurement placement.
/// </summary>
public enum PlacementState
{
    NotPlacing,
    WaitingForFirstPoint,
    WaitingForNextPoint,
    Complete
}

#endregion
```

### 2. Create State-Related Messages

Add to **`Messages/AppMessages.cs`**:

```csharp
/// <summary>
/// Sent when dirty state changes.
/// </summary>
public class DirtyStateChangedMessage
{
    public bool IsDirty { get; }
    
    public DirtyStateChangedMessage(bool isDirty)
    {
        IsDirty = isDirty;
    }
}

/// <summary>
/// Sent when tool mode changes.
/// </summary>
public class ToolModeChangedMessage
{
    public ToolMode NewMode { get; }
    public ToolMode OldMode { get; }

    public ToolModeChangedMessage(ToolMode newMode, ToolMode oldMode)
    {
        NewMode = newMode;
        OldMode = oldMode;
    }
}
```

### 3. Begin MainWindow Code-Behind Migration

**File: `MainWindow.xaml.cs`** (partial update)

Add ViewModel integration while keeping existing code working:

```csharp
public partial class MainWindow : FluentWindow
{
    // Add ViewModel
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    // Keep existing fields during transition
    private readonly RecentProjectsManager _recentProjectsManager;
    // ... other existing fields ...

    public MainWindow() : this(new MainWindowViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        
        // Keep existing initialization for now
        _recentProjectsManager = Singleton<RecentProjectsManager>.Instance;
        
        // Initialize ViewModel
        _ = viewModel.InitializeAsync();
        
        // ... rest of existing initialization ...
    }
    
    // Gradually migrate methods to use ViewModel
    // Example: Replace direct field access with ViewModel properties
    
    // OLD:
    // private bool _hasImage;
    // if (_hasImage) { ... }
    
    // NEW:
    // if (ViewModel.HasImage) { ... }
}
```

### 4. Update MainWindow XAML Bindings

**File: `MainWindow.xaml`** (partial update)

Add bindings for state properties:

```xml
<ui:FluentWindow
    ...
    Title="{Binding WindowTitle}"
    d:DataContext="{d:DesignInstance Type=vm:MainWindowViewModel, IsDesignTimeCreatable=False}">
    
    <!-- Example bindings -->
    
    <!-- Welcome message visibility -->
    <controls:WelcomeMessage 
        Visibility="{Binding IsWelcomeVisible, Converter={StaticResource BooleanToVisibilityConverter}}"
        .../>
    
    <!-- Toolbar tool buttons -->
    <ui:Button 
        Content="Distance"
        Command="{Binding SelectToolCommand}"
        CommandParameter="{x:Static vm:ToolMode.MeasureDistance}"
        IsEnabled="{Binding HasImage}"/>
    
    <!-- Undo/Redo -->
    <ui:Button 
        Content="Undo"
        Command="{Binding UndoCommand}"
        IsEnabled="{Binding CanUndo}"/>
    
    <ui:Button 
        Content="Redo"
        Command="{Binding RedoCommand}"
        IsEnabled="{Binding CanRedo}"/>
        
    <!-- Zoom display -->
    <TextBlock Text="{Binding ZoomLevel, StringFormat='{}{0:P0}'}"/>
</ui:FluentWindow>
```

### 5. Update DI Registration

**File: `App.xaml.cs`**

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // ... existing registrations ...

    // Register MainWindowViewModel
    services.AddTransient<MainWindowViewModel>();

    // Register MainWindow with ViewModel injection
    services.AddTransient<MainWindow>(sp => 
        new MainWindow(sp.GetRequiredService<MainWindowViewModel>()));
}
```

---

## Implementation Steps

1. Create `ViewModels/MainWindowViewModel.cs`
2. Add new messages to `Messages/AppMessages.cs`
3. Update `MainWindow.xaml.cs` constructor to use ViewModel
4. Begin binding state properties in `MainWindow.xaml`
5. Update DI registration
6. Build and test incrementally

---

## Validation Checklist

- [ ] MainWindowViewModel compiles
- [ ] MainWindow still launches
- [ ] Window title binds correctly
- [ ] Tool selection commands work
- [ ] Undo/Redo state tracking works
- [ ] Scale factor changes propagate via messaging
- [ ] No regressions in existing functionality

---

## Files Changed/Created

| File | Change Type |
|------|-------------|
| `ViewModels/MainWindowViewModel.cs` | Created |
| `Messages/AppMessages.cs` | Modified |
| `MainWindow.xaml.cs` | Modified |
| `MainWindow.xaml` | Modified |
| `App.xaml.cs` | Modified |

---

## Notes

### Incremental Migration Strategy

We don't migrate everything at once:
1. Create ViewModel with state properties
2. Bind simple properties first (Title, IsEnabled, etc.)
3. Migrate commands one at a time
4. Keep code-behind working during transition

### Dual State During Transition

During migration, state might exist in both places:
- ViewModel (new, preferred)
- Code-behind fields (old, gradually removed)

Use properties that delegate to ViewModel:
```csharp
// In code-behind, delegate to ViewModel
private bool HasImage => ViewModel.HasImage;
```

### Testing Commands

Commands can be tested independently:
```csharp
var vm = new MainWindowViewModel(mockServices...);
vm.SelectToolCommand.Execute(ToolMode.MeasureDistance);
Assert.Equal(ToolMode.MeasureDistance, vm.CurrentTool);
```

### Automatic Command State with `[NotifyCanExecuteChangedFor]`

The CommunityToolkit.Mvvm provides the `[NotifyCanExecuteChangedFor]` attribute to automatically notify commands when their CanExecute state might have changed. This eliminates manual `CanExecuteChanged` raising.

#### How It Works

When a property with `[NotifyCanExecuteChangedFor]` changes, the source generator automatically calls `Command.NotifyCanExecuteChanged()`:

```csharp
// This attribute on _hasImage
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(SaveProjectCommand))]
[NotifyCanExecuteChangedFor(nameof(CropCommand))]
private bool _hasImage;

// Automatically generates in the setter:
// SaveProjectCommand.NotifyCanExecuteChanged();
// CropCommand.NotifyCanExecuteChanged();
```

#### Commands with CanExecute

Use the `CanExecute` parameter on `[RelayCommand]` to specify the condition:

```csharp
// Property-based CanExecute
[RelayCommand(CanExecute = nameof(HasImage))]
private void Crop() { ... }

// Computed property CanExecute
[RelayCommand(CanExecute = nameof(CanPerformImageOperations))]
private void StartMeasurementPlacement(string measurementType) { ... }

// Computed property (derived from multiple states)
public bool CanPerformImageOperations => HasImage && !IsLoading;
```

#### Best Practices

| Scenario | Pattern |
|----------|---------|
| Single property controls command | `CanExecute = nameof(PropertyName)` |
| Multiple conditions | Create computed property, use `[NotifyPropertyChangedFor]` on dependencies |
| Async operation completion | `CanExecute = nameof(PropertyName)` with state property |
| Undo/Redo | Direct binding to `CanUndo`/`CanRedo` properties |

#### Example: Complete Pattern

```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
[NotifyCanExecuteChangedFor(nameof(CropCommand))]
[NotifyPropertyChangedFor(nameof(CanPerformImageOperations))]
private bool _hasImage;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(CanPerformImageOperations))]
private bool _isLoading;

public bool CanPerformImageOperations => HasImage && !IsLoading;

[RelayCommand(CanExecute = nameof(CanPerformImageOperations))]
private async Task SaveAsync() { ... }

[RelayCommand(CanExecute = nameof(CanPerformImageOperations))]
private void Crop() { ... }
```

---

## Next Steps

Proceed to **Step 14: MainWindow ViewModel - Image Operations** to migrate image loading, cropping, and rotation.
