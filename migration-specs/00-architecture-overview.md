# Step 00: Architecture Overview and Target State

## Current State Analysis

### Overview
MagickCrop is a WPF desktop application that evolved organically from an MVP. The current architecture follows a **code-behind-heavy pattern** with minimal separation of concerns, making it difficult to maintain, test, and extend.

### Key Metrics
| Component | Current State |
|-----------|---------------|
| MainWindow.xaml.cs | ~3,500+ lines, 157+ methods |
| Data Binding | Minimal (~9 bindings in MainWindow) |
| INotifyPropertyChanged | Not implemented on models |
| Dependency Injection | None (uses static Singleton<T>) |
| Unit Test Coverage | Not feasible with current design |
| ViewModels | None exist |

### Current Architecture Diagram
```
┌─────────────────────────────────────────────────────────────┐
│                        App.xaml.cs                          │
│                    (Manual MainWindow creation)              │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      MainWindow.xaml.cs                      │
│  • 3500+ lines of code-behind                               │
│  • ALL business logic                                        │
│  • ALL UI logic                                              │
│  • ALL state management                                      │
│  • Direct service instantiation via Singleton<T>            │
└─────────────────────────────────────────────────────────────┘
          │                    │                    │
          ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│   Controls/     │  │    Services/    │  │    Models/      │
│  (UserControls  │  │ RecentProjects  │  │  (POCOs, DTOs)  │
│  with code-     │  │   Manager       │  │  No INPC        │
│  behind logic)  │  │ (Singleton)     │  │                 │
└─────────────────┘  └─────────────────┘  └─────────────────┘
```

### Pain Points

1. **Untestable Business Logic**
   - All logic embedded in code-behind
   - Cannot unit test without UI instantiation

2. **Tight Coupling**
   - Views directly create/access services
   - No abstraction layers between components
   - Hard-coded dependencies everywhere

3. **State Management Chaos**
   - State scattered across code-behind fields
   - No centralized state management
   - Difficult to track data flow

4. **Code Duplication**
   - Similar patterns repeated across controls
   - No shared base infrastructure

5. **Difficult Maintenance**
   - Single 3,500+ line file for main functionality
   - Changes risk breaking unrelated features

---

## Target State Architecture

### MVVM Pattern Implementation
```
┌─────────────────────────────────────────────────────────────┐
│                        App.xaml.cs                          │
│            (DI Container + Service Registration)             │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│    Services     │  │   ViewModels    │  │     Views       │
│  (Interfaces +  │  │  (INPC, Cmds)   │  │  (XAML + thin   │
│  Implementations)│  │                 │  │   code-behind)  │
└─────────────────┘  └─────────────────┘  └─────────────────┘
         │                    │                    │
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────────────────────────────────────────────────────┐
│                         Models                               │
│              (INPC-enabled, Observable)                      │
└─────────────────────────────────────────────────────────────┘
```

### Target Folder Structure
```
MagickCrop/
├── App.xaml(.cs)              # DI setup, app bootstrapping
├── ViewModels/                # NEW: All ViewModels
│   ├── Base/
│   │   ├── ViewModelBase.cs
│   │   └── ObservableObject.cs
│   ├── MainWindowViewModel.cs
│   ├── SaveWindowViewModel.cs
│   ├── AboutWindowViewModel.cs
│   ├── WelcomeViewModel.cs
│   └── Measurements/
│       ├── MeasurementViewModelBase.cs
│       ├── DistanceMeasurementViewModel.cs
│       ├── AngleMeasurementViewModel.cs
│       └── ...
├── Views/                     # RENAMED: Windows → Views
│   ├── MainWindow.xaml(.cs)
│   ├── SaveWindow.xaml(.cs)
│   └── AboutWindow.xaml(.cs)
├── Controls/                  # Simplified UserControls
├── Models/                    # ENHANCED: With INPC
├── Services/                  # ENHANCED: Interfaces + Implementations
│   ├── Interfaces/
│   │   ├── IRecentProjectsService.cs
│   │   ├── IImageProcessingService.cs
│   │   ├── IFileDialogService.cs
│   │   ├── INavigationService.cs
│   │   └── IMessenger.cs
│   └── Implementations/
├── Helpers/                   # Static utilities (unchanged)
├── Behaviors/                 # WPF Behaviors (unchanged)
├── Converters/                # NEW: Value converters
└── Commands/                  # NEW: Reusable commands (optional)
```

### Key Components in Target Architecture

#### 1. ViewModelBase
- Implements `INotifyPropertyChanged`
- Provides `SetProperty<T>()` helper
- Provides command creation helpers

#### 2. Dependency Injection
- Use `Microsoft.Extensions.DependencyInjection`
- Register all services, ViewModels, Views
- Resolve dependencies via constructor injection

#### 3. Service Interfaces
- `IRecentProjectsService` - Project history management
- `IImageProcessingService` - ImageMagick operations
- `IFileDialogService` - Open/Save dialogs
- `INavigationService` - Window navigation
- `IClipboardService` - Clipboard operations
- `IMessenger` - Cross-component communication

#### 4. Messaging System
- Decoupled event communication
- Replace direct method calls between components
- Support for request/response patterns

---

## Migration Strategy

### Guiding Principles

1. **Incremental Changes** - Each step should be independently deployable
2. **No Breaking Changes** - App should work after each migration step
3. **Test Along the Way** - Add tests as testable components emerge
4. **Preserve Behavior** - Focus on refactoring, not feature changes

### Migration Phases

| Phase | Steps | Focus |
|-------|-------|-------|
| **Phase 1: Foundation** | 01-06 | Infrastructure setup (DI, base classes, services) |
| **Phase 2: Simple Windows** | 07-08 | Migrate AboutWindow, SaveWindow |
| **Phase 3: Controls** | 09-13 | Migrate UserControls to MVVM |
| **Phase 4: MainWindow** | 14-17 | Extract MainWindow logic to ViewModel |
| **Phase 5: Polish** | 18-20 | Commands, converters, cleanup |

### Risk Mitigation

1. **Feature Flags** - Keep old code paths available during transition
2. **Integration Testing** - Manual smoke tests after each step
3. **Git Branches** - Separate branch per migration step
4. **Rollback Plan** - Clear revert points at each phase boundary

---

## Success Criteria

### Quantitative Goals
- MainWindow.xaml.cs reduced to < 200 lines
- 80%+ of business logic in testable ViewModels
- Zero direct service instantiation in Views
- All Models implement INotifyPropertyChanged

### Qualitative Goals
- Clear separation between UI and business logic
- Easy to add new measurement tools
- Straightforward to test business logic
- New developers can understand architecture quickly

---

## Dependencies and Prerequisites

### NuGet Packages to Add
```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
```

### Why CommunityToolkit.Mvvm?
- Source generators for `[ObservableProperty]` and `[RelayCommand]`
- Built-in `IMessenger` for event aggregation
- Well-maintained, widely adopted
- Reduces boilerplate significantly

---

## Next Steps

Proceed to **Step 01: MVVM Infrastructure Setup** to begin adding the foundational packages and base classes.
