# Step 18: Commands Cleanup and Standardization

## Objective
Standardize command implementations throughout the application, ensuring consistent patterns and proper CanExecute handling.

## Prerequisites
- Step 17 completed (Value converters)

---

## ⚠️ This step has been broken into smaller sub-steps for easier implementation

**Complete these sub-steps in order (group by area of code):**

| Sub-Step | Description | Estimated Effort |
|----------|-------------|-----------------|
| **18a** | Audit all remaining event handlers in MainWindow.xaml.cs and list them | 30 min |
| **18b** | Convert menu File commands (if any remaining) | 30 min |
| **18c** | Convert menu Edit commands (Undo, Redo, Copy, Paste) | 30 min |
| **18d** | Convert menu View commands (Zoom, etc.) | 20 min |
| **18e** | Convert toolbar button commands | 30 min |
| **18f** | Convert context menu commands | 20 min |
| **18g** | Convert keyboard shortcut handlers to commands with InputBindings | 45 min |
| **18h** | Add CanExecute logic to commands that need state validation | 45 min |
| **18i** | Remove the old RelayCommand.cs if no longer used | 15 min |
| **18j** | Verify all commands work correctly | 30 min |

Each sub-step should be its own commit with a working build.

**Keep in code-behind (do NOT convert to commands):**
- Mouse event handlers (drag, resize, etc.)
- Keyboard navigation handlers that manipulate focus
- Window lifecycle events (Loaded, Closing, etc.)
- Complex UI interactions (scroll synchronization, etc.)

---

## Current Command State

The application has:
1. `RelayCommand<T>` in `Models/RelayCommand.cs` (existing)
2. `[RelayCommand]` attributes from CommunityToolkit.Mvvm (new)
3. Various event handlers that should become commands

## Changes Required

### 1. Audit and Migrate Event Handlers to Commands

Review all remaining Click event handlers and convert to commands where appropriate.

**Pattern: Event Handler to Command**

Before:
```csharp
// In XAML
<Button Click="SomeButton_Click"/>

// In code-behind
private void SomeButton_Click(object sender, RoutedEventArgs e)
{
    // Logic here
}
```

After:
```csharp
// In XAML
<Button Command="{Binding SomeCommand}"/>

// In ViewModel
[RelayCommand]
private void Some()
{
    // Logic here
}
```

### 2. Create Commands for Remaining MainWindow Event Handlers

**File: `ViewModels/MainWindowViewModel.cs`** (add remaining commands)

```csharp
public partial class MainWindowViewModel : ViewModelBase
{
    #region Drawing Commands

    [ObservableProperty]
    private bool _isDrawingMode;

    [ObservableProperty]
    private System.Windows.Media.Color _drawingColor = System.Windows.Media.Colors.Red;

    [ObservableProperty]
    private double _drawingThickness = 3.0;

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void StartDrawingMode()
    {
        IsDrawingMode = true;
        CurrentTool = ToolMode.DrawFreehand;
    }

    [RelayCommand]
    private void StopDrawingMode()
    {
        IsDrawingMode = false;
        CurrentTool = ToolMode.None;
    }

    [RelayCommand]
    private void SetDrawingColor(System.Windows.Media.Color color)
    {
        DrawingColor = color;
    }

    [RelayCommand]
    private void SetDrawingThickness(double thickness)
    {
        DrawingThickness = Math.Clamp(thickness, 1.0, 20.0);
    }

    #endregion

    #region Zoom Commands

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(ZoomLevel * 1.25, 10.0);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(ZoomLevel / 1.25, 0.1);
    }

    [RelayCommand]
    private void ZoomToFit()
    {
        // Signal to view to fit image in viewport
        Send(new ZoomToFitRequestMessage());
    }

    [RelayCommand]
    private void ZoomTo100Percent()
    {
        ZoomLevel = 1.0;
    }

    [RelayCommand]
    private void SetZoomLevel(double level)
    {
        ZoomLevel = Math.Clamp(level, 0.1, 10.0);
    }

    #endregion

    #region Rotation Commands (Interactive)

    [ObservableProperty]
    private double _preciseRotationAngle;

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void StartPreciseRotation()
    {
        IsRotating = true;
        PreciseRotationAngle = 0;
    }

    [RelayCommand]
    private void CancelRotation()
    {
        IsRotating = false;
        PreciseRotationAngle = 0;
    }

    [RelayCommand]
    private async Task ApplyPreciseRotation()
    {
        if (PreciseRotationAngle != 0)
        {
            await RotateByAngle(PreciseRotationAngle);
        }
        IsRotating = false;
        PreciseRotationAngle = 0;
    }

    [RelayCommand]
    private void ResetRotation()
    {
        PreciseRotationAngle = 0;
    }

    #endregion

    #region Line Guide Commands

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void StartHorizontalLinePlacement()
    {
        CurrentTool = ToolMode.HorizontalGuide;
        IsPlacingMeasurement = true;
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void StartVerticalLinePlacement()
    {
        CurrentTool = ToolMode.VerticalGuide;
        IsPlacingMeasurement = true;
    }

    #endregion

    #region Aspect Ratio Commands

    [RelayCommand]
    private void SetAspectRatio(AspectRatioItem? ratio)
    {
        SelectedAspectRatio = ratio;
    }

    [RelayCommand]
    private void ClearAspectRatio()
    {
        SelectedAspectRatio = AspectRatios.FirstOrDefault(r => r.Ratio == AspectRatio.Original);
    }

    #endregion

    #region Copy Commands

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void CopyImageToClipboard()
    {
        if (CurrentImage != null)
        {
            _clipboardService.SetImage(CurrentImage);
            _navigationService.ShowMessage("Image copied to clipboard.", "Copied");
        }
    }

    [RelayCommand(CanExecute = nameof(HasMeasurements))]
    private void CopyMeasurementsToClipboard()
    {
        var text = FormatMeasurementsAsText();
        _clipboardService.SetText(text);
        _navigationService.ShowMessage("Measurements copied to clipboard.", "Copied");
    }

    private string FormatMeasurementsAsText()
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"Magic Crop & Measure - Measurements");
        sb.AppendLine($"Scale: {GlobalScaleFactor} px/{GlobalUnits}");
        sb.AppendLine();

        if (DistanceMeasurements.Count > 0)
        {
            sb.AppendLine("Distance Measurements:");
            foreach (var m in DistanceMeasurements)
            {
                sb.AppendLine($"  {m.DisplayText}");
            }
            sb.AppendLine();
        }

        if (AngleMeasurements.Count > 0)
        {
            sb.AppendLine("Angle Measurements:");
            foreach (var m in AngleMeasurements)
            {
                sb.AppendLine($"  {m.DisplayText}");
            }
            sb.AppendLine();
        }

        if (RectangleMeasurements.Count > 0)
        {
            sb.AppendLine("Rectangle Measurements:");
            foreach (var m in RectangleMeasurements)
            {
                sb.AppendLine($"  {m.DisplayText}");
            }
            sb.AppendLine();
        }

        if (CircleMeasurements.Count > 0)
        {
            sb.AppendLine("Circle Measurements:");
            foreach (var m in CircleMeasurements)
            {
                sb.AppendLine($"  {m.DisplayText}");
            }
            sb.AppendLine();
        }

        if (PolygonMeasurements.Count > 0)
        {
            sb.AppendLine("Polygon Measurements:");
            foreach (var m in PolygonMeasurements)
            {
                sb.AppendLine($"  {m.DisplayText}");
            }
        }

        return sb.ToString();
    }

    #endregion
}
```

### 3. Add Missing Messages

**File: `Messages/AppMessages.cs`** (add)

```csharp
/// <summary>
/// Request to zoom to fit the image in the viewport.
/// </summary>
public class ZoomToFitRequestMessage { }

/// <summary>
/// Sent when zoom level changes.
/// </summary>
public class ZoomLevelChangedMessage
{
    public double NewZoomLevel { get; }
    
    public ZoomLevelChangedMessage(double zoomLevel)
    {
        NewZoomLevel = zoomLevel;
    }
}
```

### 4. Update MainWindow Code-Behind

Remove migrated event handlers and keep only View-specific ones:

**File: `MainWindow.xaml.cs`** (cleanup)

```csharp
public partial class MainWindow : FluentWindow
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    public MainWindow(MainWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        ViewModel.StartAutoSave(60);
        
        // Register for view-specific messages
        WeakReferenceMessenger.Default.Register<ZoomToFitRequestMessage>(this, OnZoomToFitRequested);
    }

    private void OnZoomToFitRequested(object recipient, ZoomToFitRequestMessage message)
    {
        // View handles the zoom-to-fit calculation
        // This requires knowledge of the viewport size
        if (ImageViewer != null && ViewModel.CurrentImage != null)
        {
            var viewportWidth = ImageViewerContainer.ActualWidth;
            var viewportHeight = ImageViewerContainer.ActualHeight;
            var imageWidth = ViewModel.ImageWidth;
            var imageHeight = ViewModel.ImageHeight;

            if (imageWidth > 0 && imageHeight > 0)
            {
                var scaleX = viewportWidth / imageWidth;
                var scaleY = viewportHeight / imageHeight;
                ViewModel.ZoomLevel = Math.Min(scaleX, scaleY) * 0.95; // 95% to add margin
            }
        }
    }

    private async void OnClosing(object sender, CancelEventArgs e)
    {
        // Unregister messages
        WeakReferenceMessenger.Default.UnregisterAll(this);
        
        if (ViewModel.IsDirty)
        {
            e.Cancel = true;
            
            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    await ViewModel.SaveProjectCommand.ExecuteAsync(null);
                    if (!ViewModel.IsDirty)
                        Close();
                    break;
                case MessageBoxResult.No:
                    ViewModel.Cleanup();
                    // Remove handler to prevent re-entry
                    Closing -= OnClosing;
                    Close();
                    break;
                // Cancel - do nothing, window stays open
            }
        }
        else
        {
            ViewModel.Cleanup();
        }
    }

    // View-specific handlers that must stay in code-behind

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Handle measurement point placement
        if (ViewModel.IsPlacingMeasurement)
        {
            var position = e.GetPosition(MeasurementCanvas);
            HandleMeasurementClick(position);
        }
    }

    private void HandleMeasurementClick(Point position)
    {
        // This logic bridges View mouse events to ViewModel commands
        // Complex placement state machine
        switch (ViewModel.CurrentTool)
        {
            case ToolMode.MeasureDistance:
                HandleDistancePlacement(position);
                break;
            case ToolMode.MeasureAngle:
                HandleAnglePlacement(position);
                break;
            case ToolMode.MeasureRectangle:
                HandleRectanglePlacement(position);
                break;
            case ToolMode.MeasureCircle:
                HandleCirclePlacement(position);
                break;
            case ToolMode.HorizontalGuide:
                ViewModel.AddHorizontalLineCommand.Execute(position.Y);
                ViewModel.CancelPlacementCommand.Execute(null);
                break;
            case ToolMode.VerticalGuide:
                ViewModel.AddVerticalLineCommand.Execute(position.X);
                ViewModel.CancelPlacementCommand.Execute(null);
                break;
        }
    }

    // Placement helper fields
    private readonly List<Point> _placementPoints = [];

    private void HandleDistancePlacement(Point position)
    {
        _placementPoints.Add(position);
        if (_placementPoints.Count == 2)
        {
            ViewModel.AddDistanceMeasurementCommand.Execute(_placementPoints.ToArray());
            _placementPoints.Clear();
            ViewModel.CancelPlacementCommand.Execute(null);
        }
    }

    private void HandleAnglePlacement(Point position)
    {
        _placementPoints.Add(position);
        if (_placementPoints.Count == 3)
        {
            ViewModel.AddAngleMeasurementCommand.Execute(_placementPoints.ToArray());
            _placementPoints.Clear();
            ViewModel.CancelPlacementCommand.Execute(null);
        }
    }

    private void HandleRectanglePlacement(Point position)
    {
        _placementPoints.Add(position);
        if (_placementPoints.Count == 2)
        {
            ViewModel.AddRectangleMeasurementCommand.Execute(_placementPoints.ToArray());
            _placementPoints.Clear();
            ViewModel.CancelPlacementCommand.Execute(null);
        }
    }

    private void HandleCirclePlacement(Point position)
    {
        _placementPoints.Add(position);
        if (_placementPoints.Count == 2)
        {
            ViewModel.AddCircleMeasurementCommand.Execute(_placementPoints.ToArray());
            _placementPoints.Clear();
            ViewModel.CancelPlacementCommand.Execute(null);
        }
    }

    // Drag and drop handling (View concern)
    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) 
            ? DragDropEffects.Copy 
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                var file = files[0];
                var ext = Path.GetExtension(file).ToLowerInvariant();
                
                if (ext == ".mcm")
                {
                    await ViewModel.LoadProjectFromFileAsync(file);
                }
                else if (IsImageFile(ext))
                {
                    await ViewModel.LoadImageFromFileAsync(file);
                }
            }
        }
    }

    private static bool IsImageFile(string extension)
    {
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" 
            or ".gif" or ".tiff" or ".tif" or ".webp";
    }
}
```

### 5. Remove Obsolete RelayCommand<T>

The existing `Models/RelayCommand.cs` can be kept for backward compatibility or marked as obsolete:

**File: `Models/RelayCommand.cs`**

```csharp
using System.Windows.Input;

namespace MagickCrop.Models;

/// <summary>
/// Simple implementation of ICommand for backward compatibility.
/// Prefer using [RelayCommand] attribute from CommunityToolkit.Mvvm for new code.
/// </summary>
[Obsolete("Use [RelayCommand] attribute from CommunityToolkit.Mvvm instead")]
public class RelayCommand<T> : ICommand
{
    // ... existing implementation ...
}
```

---

## Command Patterns Summary

### 1. Simple Command
```csharp
[RelayCommand]
private void DoSomething()
{
    // Synchronous logic
}
```

### 2. Async Command (Basic)
```csharp
[RelayCommand]
private async Task DoSomethingAsync()
{
    // Async logic
}
```

### 3. Async Command (Advanced Options)
```csharp
// Prevent concurrent executions - useful for save/load operations
[RelayCommand(AllowConcurrentExecutions = false)]
private async Task SaveProjectAsync()
{
    IsLoading = true;
    try
    {
        await _projectService.SaveAsync(CurrentProject);
    }
    finally
    {
        IsLoading = false;
    }
}

// Include cancel command for long-running operations
[RelayCommand(IncludeCancelCommand = true)]
private async Task ProcessImageAsync(CancellationToken token)
{
    // Long-running operation with cancellation support
    await _imageProcessor.ProcessAsync(CurrentImage, token);
}
// Generated properties: ProcessImageCommand, ProcessImageCancelCommand

// Both options combined
[RelayCommand(AllowConcurrentExecutions = false, IncludeCancelCommand = true)]
private async Task LoadLargeFileAsync(CancellationToken token)
{
    await _fileService.LoadAsync(FilePath, token);
}
```

### 4. Command with Parameter
```csharp
[RelayCommand]
private void DoSomething(string parameter)
{
    // Use parameter
}
```

### 5. Command with CanExecute
```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(DoSomethingCommand))]
private bool _canDoSomething;

[RelayCommand(CanExecute = nameof(CanDoSomething))]
private void DoSomething()
{
    // Logic
}
```

### 6. Command with Property CanExecute
```csharp
// CanExecute based on computed property
public bool HasData => Data != null && Data.Count > 0;

[RelayCommand(CanExecute = nameof(HasData))]
private void ProcessData()
{
    // Logic
}
```

### 7. Async Command with Concurrent Execution Prevention and CanExecute
```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(SaveProjectCommand))]
[NotifyPropertyChangedFor(nameof(CanSave))]
private bool _hasImage;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(CanSave))]
private bool _isLoading;

public bool CanSave => HasImage && !IsLoading;

[RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanSave))]
private async Task SaveProjectAsync()
{
    IsLoading = true;
    try
    {
        await _projectService.SaveAsync(CurrentProject);
        IsDirty = false;
    }
    finally
    {
        IsLoading = false;
    }
}
```

---

## Async Command Options Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `AllowConcurrentExecutions` | `bool` | `true` | When `false`, prevents the command from executing while a previous execution is still running. The command's `CanExecute` returns `false` during execution. |
| `IncludeCancelCommand` | `bool` | `false` | When `true`, generates an additional cancel command (e.g., `DoSomethingCancelCommand`) that can cancel the ongoing async operation. Requires a `CancellationToken` parameter on the method. |

### Usage Guidelines

**Use `AllowConcurrentExecutions = false` when:**
- Saving/loading files (prevent double-save)
- Database operations (prevent race conditions)
- Network requests (prevent duplicate submissions)
- Any operation where concurrent execution could cause data corruption

**Use `IncludeCancelCommand = true` when:**
- Long-running operations (file processing, image conversion)
- Network operations that might timeout
- Any operation the user should be able to abort
- Operations with progress indicators

### Example: Complete Async Pattern

```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(ExportImageCommand))]
private bool _hasImage;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(CanExport))]
private bool _isExporting;

[ObservableProperty]
private double _exportProgress;

public bool CanExport => HasImage && !IsExporting;

[RelayCommand(
    AllowConcurrentExecutions = false, 
    IncludeCancelCommand = true,
    CanExecute = nameof(CanExport))]
private async Task ExportImageAsync(CancellationToken token)
{
    IsExporting = true;
    ExportProgress = 0;
    
    try
    {
        var progress = new Progress<double>(p => ExportProgress = p);
        await _imageService.ExportAsync(CurrentImage, ExportPath, progress, token);
        
        _navigationService.ShowMessage("Export complete!", "Success");
    }
    catch (OperationCanceledException)
    {
        _navigationService.ShowMessage("Export cancelled.", "Cancelled");
    }
    finally
    {
        IsExporting = false;
        ExportProgress = 0;
    }
}
```

**XAML binding:**
```xml
<Button Content="Export" Command="{Binding ExportImageCommand}"/>
<Button Content="Cancel" Command="{Binding ExportImageCancelCommand}"
        Visibility="{Binding IsExporting, Converter={StaticResource BoolToVisibilityConverter}}"/>
<ProgressBar Value="{Binding ExportProgress}" Maximum="1.0"/>
```

---

## Validation Checklist

- [ ] All event handlers reviewed
- [ ] Appropriate handlers converted to commands
- [ ] CanExecute properly implemented
- [ ] Async commands used for I/O operations
- [ ] View-specific handlers remain in code-behind
- [ ] Commands work correctly
- [ ] No regressions

---

## Files Changed

| File | Change Type |
|------|-------------|
| `ViewModels/MainWindowViewModel.cs` | Modified |
| `Messages/AppMessages.cs` | Modified |
| `MainWindow.xaml.cs` | Modified |
| `Models/RelayCommand.cs` | Modified (obsolete) |

---

## Notes

### View vs ViewModel Responsibility

**ViewModel handles:**
- Business logic
- State management
- Service coordination
- Data transformation

**View handles:**
- Mouse/keyboard events
- UI-specific calculations (viewport size, etc.)
- Animations
- Focus management

### CanExecute Refresh

CommunityToolkit.Mvvm automatically raises `CanExecuteChanged` when properties used in `CanExecute` change, as long as those properties use `[ObservableProperty]` or `SetProperty`.

### Command Naming

The `[RelayCommand]` attribute automatically adds "Command" suffix:
- Method `DoSomething()` → Property `DoSomethingCommand`
- Method `Save()` → Property `SaveCommand`

---

## Next Steps

Proceed to **Step 19: Final Integration and Testing** to complete the migration.
