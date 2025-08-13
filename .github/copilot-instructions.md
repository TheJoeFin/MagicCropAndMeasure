# MagickCrop - GitHub Copilot Instructions

## Project Overview

MagickCrop is a WPF desktop application for Windows that provides image editing and measurement capabilities. The application allows users to crop, edit, and take precise measurements on images using various tools including distance measurements, angle measurements, geometric shapes, and annotation features.

## Key Features

- **Image Processing**: Crop, resize, rotate, flip, and apply various image effects using ImageMagick.NET
- **Measurement Tools**: Distance, angle, rectangle, circle, and polygon measurements with real-world unit conversion
- **Annotation Tools**: Drawing capabilities with ink strokes and line overlays
- **Project Management**: Save/load measurement projects as `.mcm` files (zip archives containing image + metadata + measurements)
- **Recent Projects**: Automatic project history with thumbnail previews
- **File Association**: Supports opening `.mcm` files directly from Windows Explorer

## Technology Stack

- **.NET 9** targeting Windows 10 (version 20348.0+)
- **WPF** for the user interface
- **C# 13.0** with nullable reference types enabled
- **ImageMagick.NET** for image processing
- **WPF-UI** library for modern Fluent Design controls
- **System.Text.Json** for serialization

## Project Structure

```
MagickCrop/
??? Controls/               # Reusable WPF UserControls
?   ??? WelcomeMessage.xaml.cs     # Welcome screen with recent projects
?   ??? *MeasurementControl.xaml.cs # Measurement tool controls
?   ??? RecentProjectItem.xaml.cs  # Recent project display item
?   ??? ...
??? Models/                 # Data models and DTOs
?   ??? MeasurementControls/       # Measurement-related models
?   ?   ??? MagickCropMeasurementPackage.cs  # Main package container
?   ?   ??? MeasurementCollection.cs         # Collection of all measurements
?   ?   ??? *Dto.cs                          # Data transfer objects
?   ??? RecentProjectInfo.cs       # Recent project metadata
?   ??? ...
??? Services/               # Business logic services
?   ??? RecentProjectsManager.cs   # Manages recent project history
??? Windows/                # Application windows
?   ??? AboutWindow.xaml.cs        # About dialog
??? Helpers/                # Utility classes and extensions
??? MainWindow.xaml.cs      # Primary application window
```

## Coding Standards & Conventions

### General Guidelines
- Use modern C# features (pattern matching, nullable reference types, collection expressions)
- Follow Microsoft's C# coding conventions
- Use XML documentation comments for public APIs
- Prefer `async/await` for I/O operations
- Use dependency injection pattern where appropriate

### WPF-Specific Guidelines
- Use MVVM pattern where beneficial, but code-behind is acceptable for simple UI logic
- Use data binding for dynamic UI updates
- Implement proper disposal for resources (images, file streams)
- Use `ObservableCollection` for data-bound collections
- Handle UI thread marshaling for async operations

### File Handling
- All measurement data is serialized to JSON using `System.Text.Json`
- Project files use `.mcm` extension (zip archives)
- Temporary files should be properly cleaned up
- Support both manual saves and auto-save functionality

## Key Classes & Responsibilities

### Core Models
- **`MagickCropMeasurementPackage`**: Main container for project data (image + measurements + metadata)
- **`MeasurementCollection`**: Holds all measurement data for serialization
- **`PackageMetadata`**: Project metadata (creation date, filename, dimensions, etc.)
- **`RecentProjectInfo`**: Recent project metadata with thumbnail support

### Services
- **`RecentProjectsManager`**: Manages project history, thumbnails, and auto-save
  - Stores data in `%LocalAppData%\MagickCrop\`
  - Maintains thumbnails and project index
  - Handles project cleanup and limits

### UI Controls
- **Measurement Controls**: Each measurement type has its own UserControl
  - `DistanceMeasurementControl`, `AngleMeasurementControl`, etc.
  - Support interactive editing and real-world unit conversion
- **`WelcomeMessage`**: Start screen with recent projects and quick actions

## Common Patterns

### File Operations
```csharp
// Always use async methods for file I/O
public async Task<bool> LoadFileAsync(string filePath)
{
    try
    {
        // File operations
        return true;
    }
    catch (Exception)
    {
        return false;
    }
}
```

### Error Handling
- Use try-catch blocks for file I/O and external API calls
- Log errors but continue gracefully where possible
- Show user-friendly error messages via MessageBox controls

### Data Binding
```csharp
// Use ObservableCollection for UI-bound data
public ObservableCollection<RecentProjectInfo> RecentProjects { get; private set; } = [];

// Use dependency properties for custom controls
public static readonly DependencyProperty SomePropertyProperty =
    DependencyProperty.Register(nameof(SomeProperty), typeof(Type), typeof(ClassName), new PropertyMetadata(defaultValue));
```

### Resource Management
```csharp
// Properly dispose of resources
using (var fileStream = new FileStream(path, FileMode.Create))
{
    // File operations
}
```

## Integration Points

### ImageMagick.NET
- Used for all image processing operations
- Convert between WPF `BitmapSource` and ImageMagick `MagickImage`
- Handle color profiles and format conversions

### Windows Integration
- File association for `.mcm` files via `App.xaml.cs`
- Clipboard integration for paste operations
- Windows Explorer integration for thumbnails

## Testing Considerations

- Test file I/O operations with various file states (missing, locked, corrupted)
- Verify measurement accuracy across different image sizes and scales
- Test project save/load cycles for data integrity
- Validate UI responsiveness during long-running operations

## Performance Notes

- Use image thumbnails for UI performance
- Implement lazy loading for recent projects
- Cache converted images to avoid repeated processing
- Use background threads for file operations

## Security Considerations

- Validate file paths and extensions
- Handle malformed project files gracefully
- Sanitize user input for measurements and metadata
- Use safe temporary file handling

## Common Tasks

When working on this codebase, you'll commonly need to:

1. **Add new measurement tools**: Create UserControl + DTO + update MeasurementCollection
2. **Modify file format**: Update serialization logic in MagickCropMeasurementPackage
3. **Enhance UI**: Work with WPF-UI controls and maintain Fluent Design consistency
4. **Improve image processing**: Add new ImageMagick operations to Helpers
5. **Extend project management**: Modify RecentProjectsManager for new features

Always consider backward compatibility when modifying the `.mcm` file format or measurement data structures.