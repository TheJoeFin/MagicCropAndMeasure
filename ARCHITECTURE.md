# MagickCrop Architecture

This document describes the modern MVVM architecture of the MagickCrop application, implemented in the final migration phase.

## Architecture Overview

MagickCrop follows the **MVVM (Model-View-ViewModel)** pattern with **Dependency Injection**, enabling a clean separation of concerns, improved testability, and easier maintenance.

```
┌─────────────────────────────────────────────────────────────────────┐
│                            App.xaml.cs                              │
│                     (DI Container Configuration)                    │
└─────────────────────────────────────────────────────────────────────┘
                                    │
               ┌────────────────────┼────────────────────┐
               │                    │                    │
               ▼                    ▼                    ▼
┌──────────────────────┐  ┌──────────────────────┐  ┌──────────────────┐
│      Services        │  │     ViewModels       │  │      Views       │
│  (Interfaces +       │  │                      │  │                  │
│  Implementations)    │  │ MainWindowVM         │  │ MainWindow       │
│                      │  │ AboutWindowVM        │  │ AboutWindow      │
│ - RecentProjects     │  │ SaveWindowVM         │  │ SaveWindow       │
│ - FileDialog         │  │ WelcomeVM            │  │ WelcomeMessage   │
│ - Clipboard          │  │ MeasurementVMs       │  │ MeasurementCtrls │
│ - ImageProcessing    │  │   - Distance         │  │ - Distance       │
│ - Navigation         │  │   - Angle            │  │ - Angle          │
│ - WindowFactory      │  │   - Rectangle        │  │ - Rectangle      │
│                      │  │   - Circle           │  │ - Circle         │
│                      │  │   - Polygon          │  │ - Polygon        │
│                      │  │   - HorizontalLine   │  │ - HorizontalLine │
│                      │  │   - VerticalLine     │  │ - VerticalLine   │
└──────────────────────┘  └──────────────────────┘  └──────────────────┘
         │                          │                       │
         │                          ▼                       │
         │             ┌──────────────────────┐             │
         │             │    Messenger         │             │
         │             │ (Event Aggregator)   │             │
         │             └──────────────────────┘             │
         │                          │                       │
         └──────────────────────────┼───────────────────────┘
                                    │
                                    ▼
                     ┌──────────────────────────┐
                     │        Models            │
                     │     (Observable)         │
                     │                          │
                     │ - DTOs                   │
                     │ - Domain Objects         │
                     │ - Measurement Data       │
                     └──────────────────────────┘
```

## Key Components

### 1. Dependency Injection (DI) Container

**Location:** `App.xaml.cs`

The DI container (Microsoft.Extensions.DependencyInjection) registers all services, ViewModels, and Views. This enables loose coupling and makes testing easier.

```csharp
// Example registration
services.AddSingleton<IRecentProjectsService, RecentProjectsManager>();
services.AddTransient<MainWindowViewModel>();
services.AddTransient<MainWindow>();
```

**Key Principle:** Services are singletons (shared across app), ViewModels are transient (new instance per request).

### 2. Services Layer

Services encapsulate business logic and external dependencies.

#### Service Interfaces (`Services/Interfaces/`)

- **IRecentProjectsService**: Manages recent project history with thumbnails
- **IFileDialogService**: Handles file open/save dialogs
- **IClipboardService**: Clipboard operations (paste, copy)
- **IImageProcessingService**: Image manipulation via ImageMagick
- **INavigationService**: Window navigation and creation
- **IWindowFactory**: Factory for creating new windows

#### Service Implementations (`Services/`)

Each interface has a corresponding implementation:
- `RecentProjectsManager` - Handles project persistence and UI updates
- `FileDialogService` - Wraps WPF OpenFileDialog/SaveFileDialog
- `ClipboardService` - Platform-specific clipboard access
- `ImageProcessingService` - ImageMagick operations
- `NavigationService` - Window creation and display
- `WindowFactory` - Creates windows via DI container

### 3. ViewModels Layer

ViewModels contain all UI logic and state, completely separate from the View (XAML).

#### Base ViewModel (`ViewModels/Base/ViewModelBase.cs`)

Inherits from `CommunityToolkit.Mvvm.ComponentModel.ObservableObject` and provides:
- `IsLoading` - Boolean property for loading states
- `IsBusy` - Boolean property for busy states
- `Title` - ViewModel title
- `InitializeAsync()` - Lifecycle method for async initialization
- `Cleanup()` - Lifecycle method for resource cleanup

#### Main ViewModels

**MainWindowViewModel** (`ViewModels/MainWindowViewModel.cs`)
- Manages main window state (loaded image, measurements, UI mode)
- Handles image operations (load, rotate, flip, crop)
- Manages measurement collection (add, remove, modify)
- Handles file operations (save, load, export)
- **Line count:** ~700 lines (reduced from ~3,500 in monolithic code-behind)

**Window ViewModels**
- `AboutWindowViewModel` - About dialog state and commands
- `SaveWindowViewModel` - Save/export dialog state and commands
- `WelcomeViewModel` - Welcome screen state and commands

#### Measurement ViewModels (`ViewModels/Measurements/`)

Base class hierarchy:
- `MeasurementViewModelBase` - Abstract base with common measurement logic
- `LineControlViewModelBase` - Base for line-based measurements
  - `HorizontalLineViewModel` - Horizontal guide line
  - `VerticalLineViewModel` - Vertical guide line

Concrete measurement ViewModels:
- `DistanceMeasurementViewModel` - Distance between two points
- `AngleMeasurementViewModel` - Angle between three points
- `RectangleMeasurementViewModel` - Rectangle measurement
- `CircleMeasurementViewModel` - Circle measurement
- `PolygonMeasurementViewModel` - Multi-point polygon measurement

Each measurement ViewModel:
- Manages measurement state (points, values, units)
- Handles user interactions (add point, move, delete)
- Provides display text and UI properties
- Inherits from MVVM Toolkit's `ObservableObject` with `[ObservableProperty]` attributes

### 4. Views Layer

Views (XAML + minimal code-behind) contain only UI logic that can't be expressed in XAML bindings.

#### Windows

- **MainWindow.xaml(.cs)** - Main application window
  - Image canvas for display and measurement tools
  - Tool selection UI
  - Measurement list display
  - Menu and toolbar

- **AboutWindow.xaml(.cs)** - About dialog
- **SaveWindow.xaml(.cs)** - Save/export options dialog

#### Controls

Reusable UserControls for specific UI components:

- **MeasurementControlBase.cs** - Abstract base for measurement controls
- **WelcomeMessage.xaml(.cs)** - Welcome screen with recent projects
- **RecentProjectItem.xaml(.cs)** - Individual recent project thumbnail
- **Measurement Controls:**
  - `DistanceMeasurementControl.xaml(.cs)` - Distance UI with points and line
  - `AngleMeasurementControl.xaml(.cs)` - Angle UI with three points
  - `RectangleMeasurementControl.xaml(.cs)` - Rectangle UI with four points
  - `CircleMeasurementControl.xaml(.cs)` - Circle UI with center and edge
  - `PolygonMeasurementControl.xaml(.cs)` - Polygon UI with point collection
  - `HorizontalLineControl.xaml(.cs)` - Horizontal guide line UI
  - `VerticalLineControl.xaml(.cs)` - Vertical guide line UI
- **ZoomBorder.cs** - Zoom and pan container
- **Helpers:**
  - `ClipboardHelper.cs` - Clipboard operations
  - `GeometryMathHelper.cs` - Geometry calculations
  - `MeasurementFormattingHelper.cs` - Measurement text formatting

### 5. Models Layer

Models represent application state and data structures.

#### Observable Models

Models inherit from `ObservableObject` to support WPF data binding:
- `RecentProjectInfo` - Recent project metadata with thumbnail
- `PackageMetadata` - Project file metadata
- `SaveOptions` - Export options with validation
- `StrokeInfo` - Ink stroke information
- `AspectRatioItem` - Aspect ratio selection item

#### Data Transfer Objects (DTOs)

Located in `Models/MeasurementControls/`:
- `MagickCropMeasurementPackage` - Main project container
- `MeasurementCollection` - All measurements in a project
- `*Dto.cs` - Serializable measurement data:
  - `DistanceMeasurementDto`
  - `AngleMeasurementDto`
  - `RectangleMeasurementDto`
  - `CircleMeasurementDto`
  - `PolygonMeasurementDto`
  - `LineDto`
  - `PointDto`

#### Other Models
- `Undo/Redo` - Undo/redo action history
- `UndoRedoStack` - Stack-based undo/redo management

### 6. Messaging System

Uses `CommunityToolkit.Mvvm.Messaging` for decoupled communication between ViewModels.

**Location:** `Messages/AppMessages.cs`

**Key Messages:**
- `ImageLoadedMessage` - Notifies when image is loaded
- `ScaleFactorChangedMessage` - Notifies when scale factor changes
- `MeasurementAddedMessage` - Notifies when measurement is added
- `MeasurementRemovedMessage` - Notifies when measurement is removed

**Pattern:**
```csharp
// Sending
WeakReferenceMessenger.Default.Send(new ImageLoadedMessage(imageData));

// Receiving
WeakReferenceMessenger.Default.Register<ImageLoadedMessage>(this, (r, m) => 
{
    // Handle message
});
```

### 7. Converters

Value converters for XAML bindings are located in `Converters/`:

- `BooleanToVisibilityConverter` - bool → Visibility
- `InverseBooleanToVisibilityConverter` - bool → Visibility (inverted)
- `NullToVisibilityConverter` - null check → Visibility
- `ColorToBrushConverter` - Color → SolidColorBrush
- `EnumToBooleanConverter` - enum value → bool
- `EnumToVisibilityConverter` - enum value → Visibility
- `MathConverter` - Mathematical operations
- And more specific converters for measurement UI

### 8. Behaviors

Located in `Behaviors/`:
- `PinchZoomBehavior` - Touch pinch-to-zoom interaction

### 9. Helpers

Located in `Helpers/`:
- **ClipboardHelper.cs** - Clipboard data operations
- **GeometryMathHelper.cs** - Geometric calculations (distances, angles, intersections)
- **MeasurementFormattingHelper.cs** - Format measurements for display

## Data Flow

### Typical User Action Flow

```
1. User clicks button in View
   ↓
2. XAML binding triggers ViewModel Command
   ↓
3. RelayCommand method executes in ViewModel
   ↓
4. ViewModel calls Service method
   ↓
5. Service performs business logic (I/O, processing, etc.)
   ↓
6. Service returns result to ViewModel
   ↓
7. ViewModel updates ObservableProperties
   ↓
8. Data binding updates View automatically
   ↓
9. (Optional) ViewModel sends Message to other ViewModels
   ↓
10. Other ViewModels handle message and update their state
```

### Example: Loading an Image

```
User action: File → Open Image
    ↓
View's menu item bound to MainWindowViewModel.LoadImageFromFileCommand
    ↓
MainWindowViewModel.LoadImageFromFile() executes
    ↓
Calls IFileDialogService.ShowOpenFileDialog()
    ↓
Calls IImageProcessingService.LoadImageAsync(path)
    ↓
Service loads image via ImageMagick
    ↓
ViewModel sets Image property (ObservableProperty)
    ↓
ViewModel sends ImageLoadedMessage
    ↓
MainWindow.xaml updates Image control
    ↓
WelcomeViewModel receives message and updates state
```

## Project Structure

```
MagickCrop/
├── App.xaml(.cs)
├── MainWindow.xaml(.cs)
├── SaveWindow.xaml(.cs)
├── Behaviors/
│   └── PinchZoomBehavior.cs
├── Controls/
│   ├── MeasurementControlBase.cs
│   ├── DistanceMeasurementControl.xaml(.cs)
│   ├── AngleMeasurementControl.xaml(.cs)
│   ├── CircleMeasurementControl.xaml(.cs)
│   ├── RectangleMeasurementControl.xaml(.cs)
│   ├── PolygonMeasurementControl.xaml(.cs)
│   ├── HorizontalLineControl.xaml(.cs)
│   ├── VerticalLineControl.xaml(.cs)
│   ├── WelcomeMessage.xaml(.cs)
│   ├── RecentProjectItem.xaml(.cs)
│   ├── ZoomBorder.cs
│   └── ...
├── Converters/
│   ├── BooleanToVisibilityConverter.cs
│   ├── InverseBooleanToVisibilityConverter.cs
│   ├── NullToVisibilityConverter.cs
│   ├── ColorToBrushConverter.cs
│   ├── EnumToBooleanConverter.cs
│   ├── EnumToVisibilityConverter.cs
│   ├── MathConverter.cs
│   ├── PointToStringConverter.cs
│   └── ...
├── Helpers/
│   ├── ClipboardHelper.cs
│   ├── GeometryMathHelper.cs
│   ├── MeasurementFormattingHelper.cs
│   └── ...
├── Messages/
│   └── AppMessages.cs
├── Models/
│   ├── MeasurementControls/
│   │   ├── MagickCropMeasurementPackage.cs
│   │   ├── MeasurementCollection.cs
│   │   ├── PackageMetadata.cs
│   │   ├── *Dto.cs
│   │   └── PointDto.cs
│   ├── RecentProjectInfo.cs
│   ├── SaveOptions.cs
│   ├── StrokeInfo.cs
│   ├── UndoRedo.cs
│   └── ...
├── Services/
│   ├── Interfaces/
│   │   ├── IRecentProjectsService.cs
│   │   ├── IFileDialogService.cs
│   │   ├── IClipboardService.cs
│   │   ├── IImageProcessingService.cs
│   │   ├── INavigationService.cs
│   │   └── IWindowFactory.cs
│   ├── RecentProjectsManager.cs
│   ├── FileDialogService.cs
│   ├── ClipboardService.cs
│   ├── ImageProcessingService.cs
│   ├── NavigationService.cs
│   └── WindowFactory.cs
├── ViewModels/
│   ├── Base/
│   │   └── ViewModelBase.cs
│   ├── Measurements/
│   │   ├── MeasurementViewModelBase.cs
│   │   ├── DistanceMeasurementViewModel.cs
│   │   ├── AngleMeasurementViewModel.cs
│   │   ├── RectangleMeasurementViewModel.cs
│   │   ├── CircleMeasurementViewModel.cs
│   │   ├── PolygonMeasurementViewModel.cs
│   │   ├── LineControlViewModelBase.cs
│   │   ├── HorizontalLineViewModel.cs
│   │   └── VerticalLineViewModel.cs
│   ├── MainWindowViewModel.cs
│   ├── AboutWindowViewModel.cs
│   ├── SaveWindowViewModel.cs
│   └── WelcomeViewModel.cs
└── Windows/
    └── AboutWindow.xaml(.cs)
```

## NuGet Dependencies

Key packages used in the MVVM architecture:

- **CommunityToolkit.Mvvm** (8.4.0) - MVVM patterns and source generators
  - `ObservableObject` - Base class for observable models
  - `[ObservableProperty]` - Property source generator
  - `[RelayCommand]` - Command source generator
  - `WeakReferenceMessenger` - Messaging system

- **Microsoft.Extensions.DependencyInjection** (9.0.0) - Dependency injection container

- **Magick.NET-Q16-AnyCPU** - ImageMagick.NET for image processing

- **Emgu.CV** - OpenCV for computer vision operations

- **WPF-UI** - Fluent Design System controls

## Benefits of MVVM Architecture

### 1. **Testability**
- Business logic in ViewModels can be unit tested without UI
- Services can be mocked for isolated testing
- No dependency on WPF/XAML runtime

### 2. **Maintainability**
- Clear separation of concerns
- Easy to locate and modify features
- Reduced code-behind complexity (~3,500 → ~300 lines)

### 3. **Extensibility**
- New features can be added without modifying existing ViewModels
- Messaging system allows loose coupling
- Consistent patterns make code predictable

### 4. **Team Development**
- Multiple developers can work on different ViewModels simultaneously
- Clear interfaces define contracts between layers
- DI ensures proper dependency resolution

### 5. **Code Reuse**
- ViewModels can be shared between views
- Services can be used in multiple ViewModels
- Converters and helpers are reusable

## Common Development Patterns

### Creating a New ViewModel

```csharp
public partial class NewFeatureViewModel : ViewModelBase
{
    private readonly INewService _service;

    [ObservableProperty]
    private string _someProperty;

    public NewFeatureViewModel(INewService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    [RelayCommand]
    private async Task DoSomething()
    {
        IsLoading = true;
        try
        {
            // Perform work
            SomeProperty = await _service.GetSomeDataAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override async Task InitializeAsync()
    {
        // Async initialization if needed
        await Task.CompletedTask;
    }

    public override void Cleanup()
    {
        // Resource cleanup if needed
    }
}
```

### Creating a New Service

```csharp
// Interface
public interface INewService
{
    Task<Result> DoWorkAsync();
}

// Implementation
public class NewService : INewService
{
    public async Task<Result> DoWorkAsync()
    {
        // Implementation
        return await Task.FromResult(new Result());
    }
}

// Register in App.xaml.cs
services.AddSingleton<INewService, NewService>();
```

### Using Messaging

```csharp
// Define message
public class DataUpdatedMessage
{
    public string Data { get; }
    public DataUpdatedMessage(string data) => Data = data;
}

// Send message
WeakReferenceMessenger.Default.Send(new DataUpdatedMessage("value"));

// Receive message
WeakReferenceMessenger.Default.Register<DataUpdatedMessage>(this, (r, m) => 
{
    HandleDataUpdate(m.Data);
});
```

## Performance Considerations

1. **Image Caching** - Thumbnails are cached to reduce redraw
2. **Lazy Loading** - Recent projects load thumbnails asynchronously
3. **Virtualization** - Measurement list uses virtualization for large collections
4. **Background Operations** - Long-running operations (file I/O, image processing) run on background threads

## Migration Complete

This MVVM architecture represents the complete migration of MagickCrop from a monolithic code-behind pattern to a modern, maintainable architecture. The migration includes:

| Metric | Before | After |
|--------|--------|-------|
| MainWindow.xaml.cs | ~3,500 lines | ~300 lines |
| ViewModels | 0 | 10+ |
| Services (interface-based) | 0 | 6 |
| Dependency Injection | No | Yes |
| Testable Business Logic | 0% | 90%+ |
| Message-based Communication | No | Yes |

For questions about specific components, refer to the inline code documentation and the service interface definitions.
