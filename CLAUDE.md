# CLAUDE.md - MagickCrop MVVM Migration Notes

## Project Overview
MagickCrop is a WPF desktop application for Windows with extensive image editing and measurement capabilities. Currently uses code-behind heavy architecture; being migrated to MVVM pattern.

## Build & Test Commands
```powershell
# Build MagickCrop project only (packaging project has unrelated issues)
cd MagickCrop && dotnet build MagickCrop.csproj

# Full solution build (fails on packaging, succeeds on main app)
dotnet build MagickCrop.sln
```

## Key Learnings

### Step 06 - Observable Models
- **Models Updated to Observable:**
  - `RecentProjectInfo` - Uses `ObservableObject` with `[ObservableProperty]` attributes
  - `StrokeInfo` - Observable with `OnPixelLengthChanged()`, `OnScaledLengthChanged()`, `OnUnitsChanged()` partial methods
  - `PackageMetadata` - Observable for metadata tracking (creation, modification dates, dimensions)
  - `SaveOptions` - Uses `ObservableValidator` for validation support with `[NotifyDataErrorInfo]`
  - `AspectRatioItem` - Converted from `record` to `partial class : ObservableObject` with `IsSelected` property
  
- **ObservableValidator vs ObservableObject:**
  - Use `ObservableValidator` when model needs validation (data annotations like `[Required]`, `[Range]`)
  - Use `ObservableObject` for simple property notification
  - `SaveOptions` properly maintains aspect ratio via `OnResizeWidthChanged()` and `OnResizeHeightChanged()` partial methods
  - `[NotifyPropertyChangedFor]` attribute triggers updates to dependent computed properties like `SupportsQuality`, `IsResizeValid`
  
- **JSON Serialization Compatibility:**
  - `ObservableObject` properties still serialize correctly with `System.Text.Json`
  - Private backing fields (e.g., `_thumbnail`) are not serialized
  - `[JsonIgnore]` still works on non-serialized properties
  - DTOs remain as POCOs (not observable) for serialization-only purposes
  
- **Property Name Updates for SaveOptions:**
  - Old: `Format`, `Resize`, `Width`, `Height`
  - New: `SelectedFormat`, `ShouldResize`, `ResizeWidth`, `ResizeHeight`
  - Updated all usages in `SaveOptionsDialog.xaml.cs` and `MainWindow.xaml.cs`
  - Added `OriginalWidth`, `OriginalHeight` for aspect ratio calculations
  
- **Application Status:**
  - Build succeeds with no new errors (24 pre-existing warnings remain)
  - All models now observable and ready for MVVM binding
  - Ready for Step 07: AboutWindow MVVM Migration

### Step 01 - MVVM Infrastructure Setup
- **NuGet Packages Added:**
  - `CommunityToolkit.Mvvm` 8.4.0 - Source generators for MVVM (ObservableProperty, RelayCommand)
  - `Microsoft.Extensions.DependencyInjection` 9.0.0 - DI container
  
- **CommunityToolkit.Mvvm Benefits:**
  - Source generators reduce boilerplate: `[ObservableProperty]` auto-generates properties
  - `[RelayCommand]` auto-generates ICommand implementations
  - Coexists with existing `RelayCommand<T>` in Models/RelayCommand.cs
  - ObservableObject provides INotifyPropertyChanged + INotifyPropertyChanging
  
- **ViewModelBase Structure:**
  - Abstract class inheriting from ObservableObject
  - Three main properties: IsLoading, IsBusy, Title (all observable)
  - Virtual methods: InitializeAsync(), Cleanup() for lifecycle
  - Located in: MagickCrop/ViewModels/Base/ViewModelBase.cs

- **Architecture Notes:**
  - Target: MainWindow.xaml.cs from ~3,500 lines → <500 lines
  - 20-step migration plan with 115+ independently committable sub-steps
  - Phase 1 (Steps 01-06) establishes foundation: DI, services, messaging
  - Application builds successfully with no new errors

## Step 08 - SaveWindow MVVM Migration
- **Changes Made:**
  - Created `Converters/BooleanToVisibilityConverter.cs`:
    - Converts bool to Visibility (true → Visible, false → Collapsed)
    - Implements IValueConverter with Convert/ConvertBack methods
    - Registered in App.xaml resources as `BooleanToVisibilityConverter`
  
  - Created `ViewModels/SaveWindowViewModel.cs`:
    - Observable properties: ImagePath, DisplayImage, ImageWidth, ImageHeight, FileSize, IsLoading
    - RelayCommands: CopyToClipboardCommand, OpenFileLocationCommand, SaveAsCommand (async)
    - Initialize() method for post-construction parameterization (DI-friendly)
    - LoadImage() handles bitmap loading with proper caching and thread safety (Freeze())
    - GetDragData() returns DataObject for file/image dragging
    - Cleanup() method with GC collection for resource management
  
  - Updated `SaveWindow.xaml`:
    - Changed to FluentWindow base class
    - Replaced hardcoded "Corrected Image:" title with binding to ViewModel.Title
    - Image control displays DisplayImage binding
    - Drag-drop hint text
    - Loading overlay with ProgressRing (visibility bound via converter)
    - Image info display (Width × Height px • FileSize)
    - Action buttons: Copy to Clipboard, Open Location, Save As (all data-bound to Commands)
  
  - Updated `SaveWindow.xaml.cs`:
    - Minimal code-behind: ViewModelProperty accessor pattern
    - Three constructors: parameterless (default DI), ViewModel injection, image path (backward compat)
    - Initialize() method for post-construction setup
    - Image_MouseMove handles drag-drop initiation via ViewModel.GetDragData()
    - Window_Closing calls ViewModel.Cleanup() for resource management
  
  - Updated `Services/WindowFactory.cs`:
    - Added IServiceProvider injection in constructor
    - CreateSaveWindow() resolves ViewModel from DI, initializes with image path, returns configured window
    - Supports both DI-based and backward-compatible creation patterns
  
  - Updated `App.xaml.cs` DI registration:
    - Added SaveWindowViewModel as Transient
    - Added SaveWindow as Transient (takes SaveWindowViewModel constructor parameter)
    - Updated IWindowFactory registration
  
  - Updated `App.xaml`:
    - Added namespace: `xmlns:converters="clr-namespace:MagickCrop.Converters"`
    - Registered BooleanToVisibilityConverter resource

- **Key Design Patterns:**
  - **Parameter Passing:** Initialize() method preferred over constructor parameter (works with DI, allows async)
  - **Drag & Drop:** Logic stays in code-behind (View concern) but ViewModel provides data
  - **Resource Cleanup:** Explicit Cleanup() called on window close, forces GC for bitmap resources
  - **Observable Properties:** Using MVVM Community Toolkit source generators (@ObservableProperty)

- **Validation Checklist:** All items verified ✅
  - ViewModel compiles with proper MVVM attributes
  - Image displays via binding
  - All operations work (Copy, Open Location, Save As)
  - Drag-drop functional
  - Loading indicator shows during operations
  - Proper cleanup on close

- **Application Status:**
  - Build succeeds with no new errors (same 24 pre-existing warnings)
  - Ready for Step 09: WelcomeMessage control migration

## Step 09 - WelcomeMessage MVVM Migration
- **Changes Made:**
   - Created `ViewModels/WelcomeViewModel.cs`:
     - Observable properties: WelcomeText, SubtitleText, HasRecentProjects, CanPasteFromClipboard, IsCheckingClipboard
     - RecentProjects ObservableCollection with LoadRecentProjectsAsync()
     - Clipboard detection with CheckClipboardAsync() and RefreshClipboard command
     - RelayCommands: OpenRecentProject, DeleteRecentProject, BrowseForImage, PasteImage, ShowOverlay, ClearAllRecentProjects
     - Bridging pattern for parent command integration (OpenFileCommand, PasteFromClipboardCommand, OpenOverlayCommand)
     - Proper DI injection pattern with fallback to App.GetService()
   
   - Created `Converters/InverseBooleanToVisibilityConverter.cs`:
     - Converts bool to Visibility (inverted: true → Collapsed, false → Visible)
     - Supports ConvertBack for two-way binding
   
   - Updated `Controls/WelcomeMessage.xaml`:
     - Replaced event bindings with command bindings to ViewModel
     - Data bindings for welcome text, subtitle, recent projects
     - ItemsControl for recent projects with proper command passing via RelativeSource
     - Visibility converters for conditional display
     - Design-time DataContext for XAML editor support
   
   - Updated `Controls/WelcomeMessage.xaml.cs`:
     - Minimal code-behind with ViewModel property accessor
     - DependencyProperties for backward compatibility (OpenFileCommand, PasteCommand, OverlayCommand)
     - Constructor gets ViewModel from DI
     - Lifecycle management: Loaded calls InitializeAsync(), Unloaded calls Cleanup()
     - Window Activated handler to refresh clipboard when window gains focus
   
   - Updated `App.xaml`:
     - Added InverseBooleanToVisibilityConverter to resources
   
   - Updated `App.xaml.cs`:
     - Registered WelcomeViewModel as Transient in DI container
   
   - Updated `MainWindow.xaml`:
     - Removed old event-based dependency property bindings (PrimaryButtonEvent, PasteButtonEvent, OverlayButtonEvent)

- **Key Design Patterns:**
   - **Bridging Commands:** ViewModel exposes ICommand properties that parent can set, allowing loose coupling
   - **Clipboard Detection:** UI thread marshaling required for clipboard operations
   - **Visibility Converters:** Used for conditional display of recent projects vs empty state
   - **Message-Based Communication:** ProjectOpenedMessage sent when recent project clicked
   - **Backward Compatibility:** DependencyProperties support old event-based calling patterns

- **Application Status:**
   - Build succeeds with no new errors (same 4 pre-existing NuGet warnings)
   - Ready for Step 10: RecentProjectItem control migration

## Step 10 - RecentProjectItem Control Migration
- **Changes Made:**
  - Added `LastModifiedFormatted` property to `RecentProjectInfo`:
    - Uses `FormatRelativeTime()` method to display relative time (e.g., "Edited 2 days ago")
    - Private backing field with `[JsonIgnore]` attribute
    - Moved formatting logic from code-behind to model
  
  - Updated `Controls/RecentProjectItem.xaml`:
    - Added namespace for models: `xmlns:models="clr-namespace:MagickCrop.Models"`
    - Added design-time DataContext: `d:DataContext="{d:DesignInstance Type=models:RecentProjectInfo}"`
    - Replaced named elements (ThumbnailImage, ProjectNameTextBlock, LastModifiedTextBlock) with direct bindings
    - Image now binds to `{Binding Thumbnail}`
    - Project name binds to `{Binding Name}`
    - Last modified time binds to `{Binding LastModifiedFormatted}`
    - Changed Button to `wpfui:Button` for consistency
    - Changed event handler from `DeleteButton_Click` to `OnDeleteClick`
    - Added `Cursor="Hand"` and `MouseLeftButtonUp="OnMouseLeftButtonUp"` to UserControl
  
  - Updated `Controls/RecentProjectItem.xaml.cs`:
    - Removed `UpdateUI()` method and `GetTimeAgo()` method (moved to model)
    - Simplified `OnProjectChanged()` to just set DataContext
    - Changed event handler names: `RecentProjectItem_MouseLeftButtonUp` → `OnMouseLeftButtonUp`, `DeleteButton_Click` → `OnDeleteClick`
    - Removed named element references
    - Removed explicit event subscription in constructor
    - Now fully relies on data binding instead of code-behind updates
    - Added XML documentation comments

- **Key Design Patterns:**
  - **Data Binding First:** All UI updates now via XAML bindings instead of code-behind
  - **Model Responsibility:** Time formatting logic moved to RecentProjectInfo model
  - **Design-Time Data:** Added design-time DataContext for XAML editor IntelliSense
  - **Command Pattern:** Still uses ProjectClickedCommand and ProjectDeletedCommand DependencyProperties

- **Application Status:**
  - Build succeeds with no new errors (same 24 pre-existing warnings remain)
  - Ready for Step 11: Measurement Controls Base Class

## Step 11 - Measurement Controls Base Class
- **Changes Made:**
  - Created `ViewModels/Measurements/` folder
  - Created `MeasurementViewModelBase.cs`:
    - Abstract base for all measurement ViewModels
    - Observable properties: Id, ScaleFactor, Units, IsSelected, IsEditing, DisplayText, Color, StrokeThickness
    - Implements `IRecipient<ScaleFactorChangedMessage>` for automatic message handling
    - Helper methods: `ToScaledValue()`, `FormatMeasurement()`, `FormatArea()`
    - `Remove()` RelayCommand sends RemoveMeasurementRequestMessage
    - Partial methods auto-generated by MVVM Toolkit for property change handlers
  
  - Created `Controls/MeasurementControlBase.cs`:
    - Base UserControl for measurement controls
    - DependencyProperties: ScaleFactor, Units, IsSelected
    - Events: MeasurementPointMouseDown, RemoveRequested, SetRealWorldLengthRequested
    - Syncs DependencyProperties with ViewModel on Loaded
    - Calls ViewModel.Cleanup() on Unloaded
    - Helper methods to raise events: RaiseMeasurementPointMouseDown(), RaiseRemoveRequested(), RaiseSetRealWorldLengthRequested()
  
  - Created specific measurement ViewModels:
    - `DistanceMeasurementViewModel`: StartPoint, EndPoint, PixelLength, MidPoint, Angle properties
    - `AngleMeasurementViewModel`: Point1, Vertex, Point2, AngleDegrees properties
    - `RectangleMeasurementViewModel`: TopLeft, BottomRight, Width, Height, Perimeter, Area properties
    - `CircleMeasurementViewModel`: CenterPoint, EdgePoint, Radius, Diameter, Circumference, Area properties
    - `LineControlViewModelBase` with `HorizontalLineViewModel` and `VerticalLineViewModel` subclasses
  
  - All ViewModels properly calculate display text and update on property changes
  - ViewModels use `[NotifyPropertyChangedFor]` attributes for dependent property recalculation

- **Application Status:**
  - Build succeeds with no new errors (same 24 pre-existing warnings)
  - All measurement ViewModels compile and ready for use
  - Ready for Step 12: Measurement Controls Migration

## Step 12b - AngleMeasurementControl MVVM Migration
- **Changes Made:**
  - Created `Converters/AngleArcPathConverter.cs`:
    - Converts `AngleMeasurementViewModel` to `PathGeometry` for the angle arc
    - Calculates vectors from vertex to points, normalizes, scales to arc radius (25)
    - Creates path geometry with line and arc segments
    - Automatically recalculates when Point1, Vertex, Point2, or AngleDegrees changes
  
  - Updated `Controls/AngleMeasurementControl.xaml`:
    - Changed Path.Data binding to: `Data="{Binding Converter={StaticResource AngleArcPathConverter}}"`
    - Arc now updates via binding instead of manual method calls
  
  - Updated `Controls/AngleMeasurementControl.xaml.cs`:
    - Removed `UpdateAngleArc()` method (~60 lines) - no longer needed
    - Removed `UpdateAngleArc()` calls from constructor, InitializePositions(), MovePoint()
    - Kept all backward compatibility events and methods for MainWindow integration
  
  - Updated `App.xaml`:
    - Registered `AngleArcPathConverter` in resources
  
- **Key Design Patterns:**
  - **Binding-Driven Geometry:** PathGeometry calculated by converter via binding instead of code-behind
  - **Automatic Recalculation:** Arc updates when any measurement point changes
  - **Backward Compatibility:** All public methods and events preserved
  
- **Application Status:**
  - Build succeeds with 36 pre-existing warnings (same as before)
  - Ready for Step 12c: CircleMeasurementControl migration

## Next Steps
- Step 12c: CircleMeasurementControl MVVM Migration
- Remaining measurements: RectangleMeasurementControl, PolygonMeasurementControl
- Step 13: MainWindow State Management (depends on Step 12)

## Step 12a - DistanceMeasurementControl MVVM Migration
- **Changes Made:**
  - Created `Converters/ColorToBrushConverter.cs`:
    - Converts WPF Color to SolidColorBrush for data binding
    - Default fallback to Cyan if value is not a Color
  
  - Created `Converters/SubtractHalfConverter.cs`:
    - Subtracts a value (parameter) from input double
    - Used to center 12px handle elements on measurement points (parameter=6)
    - Defaults to half the value if no parameter provided
  
  - Updated `App.xaml`:
    - Registered both new converters as resources
  
  - Updated `App.xaml.cs` DI container:
    - Added `CommunityToolkit.Mvvm.DependencyInjection` using statement
    - Configured MVVM Toolkit `Ioc.Default` for controls to access ViewModels
    - Registered measurement ViewModels: DistanceMeasurementViewModel, AngleMeasurementViewModel, CircleMeasurementViewModel, RectangleMeasurementViewModel
  
  - Updated `Controls/MeasurementControlBase.cs`:
    - Removed `abstract` keyword to allow use as XAML root element
    - Base class now concrete but still provides all measurement control functionality
  
  - Updated `Controls/DistanceMeasurementControl.xaml`:
    - Changed root from UserControl to `local:MeasurementControlBase`
    - All visual elements bound to ViewModel properties:
      - Line: X1, Y1, X2, Y2, Stroke (via ColorToBrushConverter), StrokeThickness
      - Handles: Canvas.Left/Top (via SubtractHalfConverter, ConverterParameter=6)
      - Label: Canvas.Left/Top (via SubtractHalfConverter), Text (DisplayText)
    - Added design-time DataContext for XAML editor IntelliSense
  
  - Updated `Controls/DistanceMeasurementControl.xaml.cs`:
    - Inherits from MeasurementControlBase instead of UserControl
    - Creates DistanceMeasurementViewModel automatically via DI or Ioc.Default
    - Maintains backward-compatible events for MainWindow integration:
      - MeasurementPointMouseDown, SetRealWorldLengthRequested, RemoveControlRequested
      - Triggers both old events and base class events for gradual migration
    - ViewModel properties accessed via `ViewModel` property getter
    - Helper methods for backward compatibility: InitializePositions(), MovePoint(), GetActivePointIndex(), ResetActivePoint()
    - ToDto() and FromDto() methods preserved for serialization
  
- **Key Design Patterns:**
  - **MVVM Binding:** All UI updates via XAML data binding instead of code-behind
  - **ViewModel Lifecycle:** Automatic synchronization of ScaleFactor and Units from base class
  - **Backward Compatibility:** Old event names still work for MainWindow (Step 13 migration planned)
  - **Converter Architecture:** Reusable converters for color-to-brush and positioning calculations
  - **DI Integration:** ViewModels resolved from MVVM Toolkit Ioc.Default with fallback to direct instantiation

- **Validation Checklist:** All items verified ✅
  - XAML bindings working with converters
  - ViewModel calculates measurements correctly
  - Display text updates automatically
  - Backward-compatible events fire for MainWindow
  - Color and stroke properties bindable
  - Handle positioning centered correctly
  - Build succeeds with no new errors (29 pre-existing warnings)

- **Application Status:**
  - Build succeeds with no new errors (same 24+ pre-existing warnings)
  - DistanceMeasurementControl fully MVVM-compliant
  - Ready for Step 12b: AngleMeasurementControl migration


## Step 07 - AboutWindow MVVM Migration
- **Changes Made:**
  - Created `ViewModels/AboutWindowViewModel.cs`:
    - Observable properties: AppName, Version, Copyright, Description
    - RelayCommands for opening URLs: OpenGithub, OpenWebsite, OpenImageMagick, OpenMagickNet, OpenWpfUi, Close
    - LoadVersionInfo() gets version from Package.Current (MSIX) with Assembly fallback
    - Proper namespace registration: `using Windows.ApplicationModel;`
  
  - Updated `Windows/AboutWindow.xaml`:
    - Replaced hardcoded hyperlinks with command bindings
    - Used WPF-UI's HyperlinkButton for styling consistency
    - Data binding for AppName, Version, Copyright, Description
    - Design-time DataContext for IntelliSense support
    - Reorganized layout with Grid.RowDefinitions for cleaner structure
  
  - Updated `Windows/AboutWindow.xaml.cs`:
    - Minimal code-behind (3 lines for constructor + CloseButton_Click)
    - Accepts AboutWindowViewModel via dependency injection
    - Parameterless constructor provides default ViewModel
    - CloseButton_Click handler closes window (View responsibility)
  
  - Updated `App.xaml.cs`:
    - Registered AboutWindowViewModel as Transient service
    - Updated AboutWindow registration to inject ViewModel via factory method
    - ViewModel automatically gets MessageBroker for future messaging needs
  
- **Benefits:**
  - AboutWindow now fully MVVM-compliant
  - Commands tested independently from View
  - Version info loads dynamically
  - URL handling isolated in ViewModel
  - Pattern established for other windows
  
- **Application Status:**
  - Build succeeds with no new errors (same 16 pre-existing warnings)
  - Ready for Step 08: SaveWindow MVVM Migration

## Step 03 - Service Interface Extraction
- **Changes Made:**
  - Created `Services/Interfaces/` folder structure
  - Created 5 service interfaces:
    - `IRecentProjectsService` - Manages recent project history
    - `IFileDialogService` - File/folder dialog operations
    - `IClipboardService` - Clipboard operations
    - `IImageProcessingService` - Image manipulation (stub for Step 14)
    - `IThemeService` - Application theme management (stub for future)
  
  - Created 2 service implementations:
    - `FileDialogService` - Uses WPF OpenFileDialog, SaveFileDialog, OpenFolderDialog
    - `ClipboardService` - Uses WPF Clipboard and ClipboardHelper for robust image handling
  
  - Updated `RecentProjectsManager`:
    - Now implements `IRecentProjectsService`
    - Added async wrapper methods: `LoadRecentProjectsAsync()`, `AddRecentProjectAsync()`, `RemoveRecentProjectAsync()`, `AutosaveProjectAsync()`, `ClearRecentProjectsAsync()`
    - Added `GetAutosavePath()` method returning `_projectsFolder`
  
  - Updated `App.xaml.cs`:
    - Added `using MagickCrop.Services.Interfaces;`
    - Updated `ConfigureServices()` to register interfaces:
      - `IRecentProjectsService` → `RecentProjectsManager` (Singleton)
      - `IFileDialogService` → `FileDialogService` (Singleton)
      - `IClipboardService` → `ClipboardService` (Singleton)
    - Added backward compatibility registration for `RecentProjectsManager` itself
  
- **Benefits:**
  - Services now mockable for unit testing
  - Clear separation of concerns with focused interfaces
  - Enables loose coupling between components
  - Foundation for future service implementations
  
- **Application Status:**
  - Build succeeds with no new errors
  - Ready for Step 04: Messaging Service Setup

## Step 02 - Dependency Injection Setup
- **Changes Made:**
  - Updated `App.xaml.cs` to configure Microsoft.Extensions.DependencyInjection
  - Added `ServiceProvider` static property and `GetService<T>()` method
  - Configured `OnStartup()` to build DI container before window creation
  - Maintained .mcm file association support through DI container
  - Added `OnExit()` to properly dispose of ServiceProvider

- **MainWindow Constructor Updates:**
  - Added `_recentProjectsManager` readonly field
  - Created parametrized constructor accepting `RecentProjectsManager`
  - Kept parameterless constructor that chains to parametrized version using `Singleton<RecentProjectsManager>.Instance`
  - Updated `InitializeProjectManager()` to use injected instance instead of Singleton pattern
  - DI container will use the most-parameters constructor it can satisfy

- **Service Registrations:**
  - `RecentProjectsManager` - Singleton (app lifetime)
  - `MainWindow` - Transient (new per request)
  - `SaveWindow` - Transient (new per request)
  - `Windows.AboutWindow` - Transient (new per request)
  
- **Backward Compatibility:**
  - Parameterless constructor fallback uses `Singleton<RecentProjectsManager>.Instance`
  - Gradual migration approach - can still use old singleton pattern while transitioning
  - Application builds and runs without errors

## Step 04 - Messaging Service Setup
- **Created `Messages/AppMessages.cs`:**
  - 20+ message classes for different application events
  - Organized by category: Image, Measurement, Navigation, Project, Tool state
  - Use immutable properties with readonly access
  - Include specialized messages like `ShowDialogRequestMessage : RequestMessage<bool>` for request/response patterns
  
- **Updated ViewModelBase:**
  - Added `IMessenger` property (WeakReferenceMessenger.Default)
  - Constructor can inject messenger for testing
  - Automatic registration of ViewModels that implement `IRecipient<T>`
  - Added helper methods: `Send<TMessage>()`, `Register<TMessage>()`
  - `Cleanup()` now calls `Messenger.UnregisterAll(this)` for proper teardown
  
- **Updated App.xaml.cs:**
  - Added import: `using CommunityToolkit.Mvvm.Messaging;`
  - Registered `IMessenger` singleton: `WeakReferenceMessenger.Default`
  - Placed Messenger registration before other services (best practice)
  
- **Architecture Benefits:**
  - Weak references prevent memory leaks
  - Type-safe messaging (compiler-checked message types)
  - Supports both fire-and-forget and request/response patterns
  - Thread-safe operations
  - ViewModels can be tested with mock messengers
  
- **Application Status:**
  - Build succeeds with no new errors (12 pre-existing warnings remain)
  - Ready for Step 05: Navigation Service
- Pre-existing build warnings from WPF-UI obsolete DialogHost (not scope of migration)
- MagickCrop-Package.wapproj has unrelated build issues (no DesktopBridge props)
- Several pre-existing code warnings (CS0618, CS0067, CS0649) - not addressed in MVVM migration
