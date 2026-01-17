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
- Step 03: Service interface extraction (7 sub-steps)
- Create service interfaces (7 sub-steps)
- Set up messaging system using CommunityToolkit.Mvvm's IMessenger

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

## Known Issues
- Pre-existing build warnings from WPF-UI obsolete DialogHost (not scope of migration)
- MagickCrop-Package.wapproj has unrelated build issues (no DesktopBridge props)
- Several pre-existing code warnings (CS0618, CS0067, CS0649) - not addressed in MVVM migration
