# Step 02: Dependency Injection Setup

## Objective
Configure Microsoft.Extensions.DependencyInjection in App.xaml.cs to enable constructor injection throughout the application.

## Prerequisites
- Step 01 completed (MVVM packages installed)

## Changes Required

### 1. Update App.xaml.cs

**File: `App.xaml.cs`**

Replace the current content with:

```csharp
using System.IO;
using System.Windows;
using MagickCrop.Services;
using MagickCrop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MagickCrop;

public partial class App : Application
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Gets the current service provider instance.
    /// </summary>
    public static IServiceProvider ServiceProvider => _serviceProvider 
        ?? throw new InvalidOperationException("Service provider not initialized");

    /// <summary>
    /// Gets a service of type T from the DI container.
    /// </summary>
    public static T GetService<T>() where T : class
        => ServiceProvider.GetRequiredService<T>();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Handle .mcm file association
        if (e.Args.Length > 0 && File.Exists(e.Args[0])
            && Path.GetExtension(e.Args[0]).Equals(".mcm", StringComparison.OrdinalIgnoreCase))
        {
            var mainWindow = GetService<MainWindow>();
            mainWindow.LoadMeasurementsPackageFromFile(e.Args[0]);
            mainWindow.Show();
            return;
        }

        // Normal startup
        var normalMainWindow = GetService<MainWindow>();
        normalMainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register Services
        services.AddSingleton<RecentProjectsManager>();
        
        // Register ViewModels (to be added in future steps)
        // services.AddTransient<MainWindowViewModel>();
        // services.AddTransient<SaveWindowViewModel>();
        // services.AddTransient<AboutWindowViewModel>();

        // Register Windows/Views
        services.AddTransient<MainWindow>();
        services.AddTransient<SaveWindow>();
        services.AddTransient<Windows.AboutWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        
        // Dispose of the service provider if it implements IDisposable
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
```

### 2. Update MainWindow Constructor

**File: `MainWindow.xaml.cs`**

Update the constructor to accept dependencies:

```csharp
// Add field
private readonly RecentProjectsManager _recentProjectsManager;

// Update constructor
public MainWindow() : this(Singleton<RecentProjectsManager>.Instance)
{
}

public MainWindow(RecentProjectsManager recentProjectsManager)
{
    _recentProjectsManager = recentProjectsManager;
    InitializeComponent();
    // ... rest of existing initialization
}
```

**Explanation:** We keep the parameterless constructor as a fallback during the transition period. The DI container will use the constructor with the most parameters it can satisfy.

### 3. Update SaveWindow Constructor

**File: `SaveWindow.xaml.cs`**

For now, SaveWindow takes an `imagePath` parameter which is dynamic. We'll handle this with a factory pattern:

```csharp
// Keep existing constructor for now
// Will be refactored in Step 08
```

### 4. Update AboutWindow Constructor

**File: `Windows/AboutWindow.xaml.cs`**

```csharp
public AboutWindow()
{
    InitializeComponent();
    // ... rest of existing initialization
}
```

**Note:** AboutWindow has no dependencies, so no changes needed yet.

---

## Implementation Steps

### Step 1: Backup Current App.xaml.cs
Keep a copy of the original for reference.

### Step 2: Update App.xaml.cs
Replace with the new DI-enabled version.

### Step 3: Update MainWindow Constructor
Add the dependency injection-friendly constructor.

### Step 4: Verify Build and Test

```powershell
dotnet build MagickCrop.sln
```

Then manually test:
1. Application launches normally
2. Can open images
3. Recent projects still work
4. File association (.mcm) still works

---

## Validation Checklist

- [ ] Solution builds without errors
- [ ] Application launches normally
- [ ] Recent projects load correctly
- [ ] Opening a .mcm file from Explorer works
- [ ] All existing functionality works

---

## Files Changed

| File | Change Type |
|------|-------------|
| `App.xaml.cs` | Modified - Added DI setup |
| `MainWindow.xaml.cs` | Modified - Added DI constructor |

---

## Notes

### Service Lifetime Choices

| Registration | Meaning | Use For |
|--------------|---------|---------|
| `AddSingleton<T>()` | One instance for app lifetime | Services with state (RecentProjectsManager) |
| `AddTransient<T>()` | New instance each request | ViewModels, Windows |
| `AddScoped<T>()` | One instance per scope | Not commonly used in WPF |

### Transition Strategy

During the migration, we maintain backward compatibility by:
1. Keeping parameterless constructors that call the DI-enabled constructor
2. Using `Singleton<T>` as the fallback parameter value
3. Gradually removing `Singleton<T>` usage as we complete the migration

### App.GetService<T>() Pattern

The static `App.GetService<T>()` method allows service resolution from anywhere. This is an anti-pattern in purist DI, but useful during migration. Eventually, all dependencies should come through constructors.

### Why Not Use a Locator?

Some MVVM frameworks use a `ViewModelLocator`. We prefer direct DI because:
1. Explicit dependencies in constructors
2. Better testability
3. No hidden service location

---

## Next Steps

Proceed to **Step 03: Service Interface Extraction** to create interfaces for the existing services.
