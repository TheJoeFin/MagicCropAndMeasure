# Step 03: Service Interface Extraction

## Objective
Extract interfaces from existing services to enable loose coupling, testability, and easier mocking.

## Prerequisites
- Step 02 completed (DI setup)

---

## ⚠️ This step has been broken into smaller sub-steps for easier implementation

Complete these sub-steps in order:

| Sub-Step | Description | Estimated Effort |
|----------|-------------|-----------------|
| **03a** | Create Services/Interfaces folder and IRecentProjectsService | 30 min |
| **03b** | Create IFileDialogService interface and FileDialogService implementation | 30 min |
| **03c** | Create IClipboardService interface and ClipboardService implementation | 30 min |
| **03d** | Create IImageProcessingService interface (implementation in Step 14) | 20 min |
| **03e** | Create IThemeService interface | 15 min |
| **03f** | Update RecentProjectsManager to implement IRecentProjectsService | 45 min |
| **03g** | Update DI registration in App.xaml.cs | 30 min |

Each sub-step should be its own commit with a working build.

---

## Changes Required

### 1. Create Services Folder Structure

```
MagickCrop/
└── Services/
    ├── Interfaces/
    │   ├── IRecentProjectsService.cs
    │   ├── IImageProcessingService.cs
    │   ├── IFileDialogService.cs
    │   ├── IClipboardService.cs
    │   └── IThemeService.cs
    └── (existing files remain)
```

### 2. Create IRecentProjectsService Interface

**File: `Services/Interfaces/IRecentProjectsService.cs`**

```csharp
using System.Collections.ObjectModel;
using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;

namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Service for managing recent project history.
/// </summary>
public interface IRecentProjectsService
{
    /// <summary>
    /// Gets the collection of recent projects.
    /// </summary>
    ObservableCollection<RecentProjectInfo> RecentProjects { get; }

    /// <summary>
    /// Loads the recent projects from storage.
    /// </summary>
    Task LoadRecentProjectsAsync();

    /// <summary>
    /// Adds or updates a project in the recent projects list.
    /// </summary>
    /// <param name="project">The project info to add.</param>
    Task AddRecentProjectAsync(RecentProjectInfo project);

    /// <summary>
    /// Removes a project from the recent projects list.
    /// </summary>
    /// <param name="projectId">The project ID to remove.</param>
    Task RemoveRecentProjectAsync(Guid projectId);

    /// <summary>
    /// Gets the auto-save path for the current project.
    /// </summary>
    string GetAutosavePath();

    /// <summary>
    /// Auto-saves the current project state.
    /// </summary>
    /// <param name="package">The measurement package to save.</param>
    /// <param name="projectInfo">The project info.</param>
    Task AutosaveProjectAsync(MagickCropMeasurementPackage package, RecentProjectInfo projectInfo);

    /// <summary>
    /// Clears all recent projects.
    /// </summary>
    Task ClearRecentProjectsAsync();
}
```

### 3. Create IFileDialogService Interface

**File: `Services/Interfaces/IFileDialogService.cs`**

```csharp
namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Service for showing file dialogs.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Shows an open file dialog.
    /// </summary>
    /// <param name="filter">File filter string (e.g., "Images|*.png;*.jpg")</param>
    /// <param name="title">Dialog title</param>
    /// <returns>Selected file path, or null if cancelled.</returns>
    string? ShowOpenFileDialog(string filter, string? title = null);

    /// <summary>
    /// Shows a save file dialog.
    /// </summary>
    /// <param name="filter">File filter string</param>
    /// <param name="defaultFileName">Default file name</param>
    /// <param name="title">Dialog title</param>
    /// <returns>Selected file path, or null if cancelled.</returns>
    string? ShowSaveFileDialog(string filter, string? defaultFileName = null, string? title = null);

    /// <summary>
    /// Shows a folder browser dialog.
    /// </summary>
    /// <param name="description">Dialog description</param>
    /// <returns>Selected folder path, or null if cancelled.</returns>
    string? ShowFolderBrowserDialog(string? description = null);
}
```

### 4. Create IClipboardService Interface

**File: `Services/Interfaces/IClipboardService.cs`**

```csharp
using System.Windows.Media.Imaging;

namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Service for clipboard operations.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Gets whether an image is available on the clipboard.
    /// </summary>
    bool ContainsImage();

    /// <summary>
    /// Gets whether a file drop list is available on the clipboard.
    /// </summary>
    bool ContainsFileDropList();

    /// <summary>
    /// Gets an image from the clipboard.
    /// </summary>
    /// <returns>The image, or null if not available.</returns>
    BitmapSource? GetImage();

    /// <summary>
    /// Gets file paths from the clipboard.
    /// </summary>
    /// <returns>List of file paths, or empty if not available.</returns>
    IReadOnlyList<string> GetFileDropList();

    /// <summary>
    /// Sets an image to the clipboard.
    /// </summary>
    /// <param name="image">The image to set.</param>
    void SetImage(BitmapSource image);

    /// <summary>
    /// Copies text to the clipboard.
    /// </summary>
    /// <param name="text">The text to copy.</param>
    void SetText(string text);
}
```

### 5. Create IImageProcessingService Interface

**File: `Services/Interfaces/IImageProcessingService.cs`**

```csharp
using System.Windows.Media.Imaging;
using ImageMagick;

namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Service for image processing operations.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Loads an image from a file path.
    /// </summary>
    Task<MagickImage?> LoadImageAsync(string filePath);

    /// <summary>
    /// Saves an image to a file.
    /// </summary>
    Task<bool> SaveImageAsync(MagickImage image, string filePath, MagickFormat format, int quality = 90);

    /// <summary>
    /// Rotates an image by the specified degrees.
    /// </summary>
    MagickImage Rotate(MagickImage image, double degrees);

    /// <summary>
    /// Crops an image to the specified region.
    /// </summary>
    MagickImage Crop(MagickImage image, int x, int y, int width, int height);

    /// <summary>
    /// Resizes an image to the specified dimensions.
    /// </summary>
    MagickImage Resize(MagickImage image, int width, int height);

    /// <summary>
    /// Applies perspective correction to an image.
    /// </summary>
    MagickImage ApplyPerspectiveCorrection(MagickImage image, double[] sourcePoints, double[] targetPoints);

    /// <summary>
    /// Converts a MagickImage to a WPF BitmapSource.
    /// </summary>
    BitmapSource ToBitmapSource(MagickImage image);

    /// <summary>
    /// Converts a WPF BitmapSource to a MagickImage.
    /// </summary>
    MagickImage FromBitmapSource(BitmapSource bitmapSource);

    /// <summary>
    /// Flips an image horizontally.
    /// </summary>
    void FlipHorizontal(MagickImage image);

    /// <summary>
    /// Flips an image vertically.
    /// </summary>
    void FlipVertical(MagickImage image);
}
```

### 6. Create IThemeService Interface

**File: `Services/Interfaces/IThemeService.cs`**

```csharp
namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Service for managing application theme.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Sets the application theme to dark mode.
    /// </summary>
    void SetDarkTheme();

    /// <summary>
    /// Sets the application theme to light mode.
    /// </summary>
    void SetLightTheme();

    /// <summary>
    /// Gets the current theme.
    /// </summary>
    bool IsDarkTheme { get; }
}
```

### 7. Update RecentProjectsManager to Implement Interface

**File: `Services/RecentProjectsManager.cs`**

Add interface implementation:

```csharp
using MagickCrop.Services.Interfaces;

// Change class declaration to:
public class RecentProjectsManager : IRecentProjectsService
{
    // Existing implementation remains
    // Add any missing interface methods with async versions
    
    // Example: Add async wrapper if sync version exists
    public async Task LoadRecentProjectsAsync()
    {
        await Task.Run(() => LoadRecentProjects());
    }
    
    // ... implement other interface methods
}
```

### 8. Create FileDialogService Implementation

**File: `Services/FileDialogService.cs`**

```csharp
using Microsoft.Win32;
using MagickCrop.Services.Interfaces;

namespace MagickCrop.Services;

/// <summary>
/// Implementation of IFileDialogService using WPF dialogs.
/// </summary>
public class FileDialogService : IFileDialogService
{
    public string? ShowOpenFileDialog(string filter, string? title = null)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title ?? "Open File"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string filter, string? defaultFileName = null, string? title = null)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName ?? string.Empty,
            Title = title ?? "Save File"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowFolderBrowserDialog(string? description = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = description ?? "Select Folder"
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
```

### 9. Create ClipboardService Implementation

**File: `Services/ClipboardService.cs`**

```csharp
using System.Windows;
using System.Windows.Media.Imaging;
using MagickCrop.Helpers;
using MagickCrop.Services.Interfaces;

namespace MagickCrop.Services;

/// <summary>
/// Implementation of IClipboardService using WPF Clipboard.
/// </summary>
public class ClipboardService : IClipboardService
{
    public bool ContainsImage()
    {
        return Clipboard.ContainsImage() || 
               Clipboard.ContainsData(DataFormats.Dib) ||
               Clipboard.ContainsFileDropList();
    }

    public bool ContainsFileDropList()
    {
        return Clipboard.ContainsFileDropList();
    }

    public BitmapSource? GetImage()
    {
        // Use existing ClipboardHelper logic
        return ClipboardHelper.TryGetClipboardImage();
    }

    public IReadOnlyList<string> GetFileDropList()
    {
        if (Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList();
            return files?.Cast<string>().ToList() ?? [];
        }
        return [];
    }

    public void SetImage(BitmapSource image)
    {
        Clipboard.SetImage(image);
    }

    public void SetText(string text)
    {
        Clipboard.SetText(text);
    }
}
```

### 10. Update DI Registration in App.xaml.cs

**File: `App.xaml.cs`**

Update `ConfigureServices`:

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // Register Service Interfaces
    services.AddSingleton<IRecentProjectsService, RecentProjectsManager>();
    services.AddSingleton<IFileDialogService, FileDialogService>();
    services.AddSingleton<IClipboardService, ClipboardService>();
    // services.AddSingleton<IImageProcessingService, ImageProcessingService>(); // To be implemented
    // services.AddSingleton<IThemeService, ThemeService>(); // To be implemented

    // Keep backward compatibility during migration
    services.AddSingleton<RecentProjectsManager>(sp => 
        (RecentProjectsManager)sp.GetRequiredService<IRecentProjectsService>());

    // Register ViewModels
    // ...

    // Register Windows/Views
    services.AddTransient<MainWindow>();
    services.AddTransient<SaveWindow>();
    services.AddTransient<Windows.AboutWindow>();
}
```

---

## Implementation Steps

1. Create `Services/Interfaces` folder
2. Create all interface files
3. Create `FileDialogService.cs` and `ClipboardService.cs`
4. Update `RecentProjectsManager` to implement interface
5. Update DI registration in `App.xaml.cs`
6. Build and test

---

## Validation Checklist

- [ ] All interfaces created and compile
- [ ] FileDialogService implementation works
- [ ] ClipboardService implementation works
- [ ] RecentProjectsManager implements IRecentProjectsService
- [ ] DI registration updated
- [ ] Application works normally

---

## Files Changed/Created

| File | Change Type |
|------|-------------|
| `Services/Interfaces/IRecentProjectsService.cs` | Created |
| `Services/Interfaces/IFileDialogService.cs` | Created |
| `Services/Interfaces/IClipboardService.cs` | Created |
| `Services/Interfaces/IImageProcessingService.cs` | Created |
| `Services/Interfaces/IThemeService.cs` | Created |
| `Services/FileDialogService.cs` | Created |
| `Services/ClipboardService.cs` | Created |
| `Services/RecentProjectsManager.cs` | Modified |
| `App.xaml.cs` | Modified |

---

## Notes

### Interface Segregation
Each interface is focused on a single responsibility:
- **IRecentProjectsService**: Project history only
- **IFileDialogService**: File/folder dialogs only
- **IClipboardService**: Clipboard operations only
- **IImageProcessingService**: Image manipulation only

### Testing Benefits
With interfaces, we can now:
1. Mock services in unit tests
2. Create test doubles for integration tests
3. Swap implementations without changing consumers

### Migration Approach
We implement interfaces incrementally:
1. Create interface for existing service
2. Make existing service implement the interface
3. Update DI registration to use interface
4. Update consumers one at a time

---

## Next Steps

Proceed to **Step 04: Messaging Service Setup** to add inter-component communication.
