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

## Next Steps
- Step 04: Messaging setup (needed for loose coupling)
- Step 05: Navigation service (needed for Step 07-08)
- Step 06: Observable models (needed for Step 09-16)

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
