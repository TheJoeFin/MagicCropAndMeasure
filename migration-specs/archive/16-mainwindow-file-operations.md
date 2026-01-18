# Step 16: MainWindow ViewModel - File Operations

## Objective
Extract file save/load operations from MainWindow.xaml.cs into the ViewModel, including project file management and recent projects integration.

## Prerequisites
- Step 15 completed (Measurement management)

---

## ⚠️ This step has been broken into smaller sub-steps for easier implementation

**Complete these sub-steps in order:**

| Sub-Step | Description | Estimated Effort |
|----------|-------------|-----------------|
| **16a** | Add file operation state properties (IsSaving, IsLoading, LastSavedPath, CurrentFilePath) | 20 min |
| **16b** | Create SaveProjectCommand with basic .mcm save logic | 45 min |
| **16c** | Create SaveAsCommand (always prompts for file location) | 30 min |
| **16d** | Create LoadProjectCommand with .mcm load logic | 45 min |
| **16e** | Add LoadMeasurementsFromDto helper method | 30 min |
| **16f** | Create ExportImageCommand | 30 min |
| **16g** | Add ExportWithOptionsCommand (opens SaveWindow) | 30 min |
| **16h** | Integrate with IRecentProjectsService for tracking | 30 min |
| **16i** | Add NewProjectCommand (clears current project) | 20 min |
| **16j** | Add confirmation dialog for unsaved changes | 30 min |
| **16k** | Wire file menu commands in MainWindow.xaml | 30 min |
| **16l** | Handle file association opening (.mcm double-click) | 30 min |
| **16m** | Test save/load cycle with all measurement types | 30 min |

Each sub-step should be its own commit with a working build.

---

## Current File Operations

1. **Save Project**: Save as .mcm (zip with image + metadata + measurements)
2. **Load Project**: Open .mcm files
3. **Export Image**: Save image in various formats
4. **Recent Projects**: Track and display recent files
5. **Auto-save**: Background saving of current state

## Changes Required

### 1. Add File Operations to MainWindowViewModel

**File: `ViewModels/MainWindowViewModel.cs`** (add to existing)

```csharp
using System.IO;
using System.IO.Compression;
using MagickCrop.Models.MeasurementControls;

public partial class MainWindowViewModel : ViewModelBase
{
    #region File Operations State

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _lastSavedPath;

    #endregion

    #region Save Commands

    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task SaveProject()
    {
        if (string.IsNullOrEmpty(LastSavedPath))
        {
            await SaveProjectAs();
            return;
        }

        await SaveProjectToPathAsync(LastSavedPath);
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task SaveProjectAs()
    {
        var filter = "MagickCrop Project|*.mcm";
        var defaultName = string.IsNullOrEmpty(CurrentFilePath) 
            ? "Untitled.mcm" 
            : Path.ChangeExtension(Path.GetFileName(CurrentFilePath), ".mcm");
        
        var savePath = _fileDialogService.ShowSaveFileDialog(filter, defaultName, "Save Project");
        
        if (string.IsNullOrEmpty(savePath))
            return;

        await SaveProjectToPathAsync(savePath);
    }

    private async Task SaveProjectToPathAsync(string filePath)
    {
        if (_magickImage == null)
            return;

        try
        {
            IsSaving = true;
            
            await Task.Run(() =>
            {
                // Create package
                var package = CreateMeasurementPackage();
                
                // Save to file
                SavePackageToFile(package, filePath);
            });

            LastSavedPath = filePath;
            CurrentFilePath = filePath;
            IsDirty = false;
            UpdateWindowTitle();
            
            // Update recent projects
            await UpdateRecentProjectsAsync(filePath);
            
            Send(new ProjectSavedMessage(filePath));
            _navigationService.ShowMessage($"Project saved to:\n{filePath}", "Saved");
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to save project: {ex.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private MagickCropMeasurementPackage CreateMeasurementPackage()
    {
        var metadata = new PackageMetadata
        {
            FormatVersion = "1.1",
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            OriginalFileName = Path.GetFileName(CurrentFilePath ?? "Untitled"),
            OriginalWidth = ImageWidth,
            OriginalHeight = ImageHeight,
            CurrentWidth = ImageWidth,
            CurrentHeight = ImageHeight
        };

        return new MagickCropMeasurementPackage
        {
            Metadata = metadata,
            Measurements = ToMeasurementCollection()
        };
    }

    private void SavePackageToFile(MagickCropMeasurementPackage package, string filePath)
    {
        // Delete existing file if present
        if (File.Exists(filePath))
            File.Delete(filePath);

        using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);
        
        // Save image
        var imageEntry = archive.CreateEntry("image.jpg");
        using (var imageStream = imageEntry.Open())
        {
            _magickImage!.Write(imageStream, ImageMagick.MagickFormat.Jpeg);
        }

        // Save metadata
        var metadataEntry = archive.CreateEntry("metadata.json");
        using (var metadataStream = metadataEntry.Open())
        using (var writer = new StreamWriter(metadataStream))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(package.Metadata, 
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            writer.Write(json);
        }

        // Save measurements
        var measurementsEntry = archive.CreateEntry("measurements.json");
        using (var measurementsStream = measurementsEntry.Open())
        using (var writer = new StreamWriter(measurementsStream))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(package.Measurements,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            writer.Write(json);
        }
    }

    #endregion

    #region Load Commands

    [RelayCommand]
    private async Task OpenProject()
    {
        var filter = "MagickCrop Project|*.mcm|All Files|*.*";
        var filePath = _fileDialogService.ShowOpenFileDialog(filter, "Open Project");
        
        if (string.IsNullOrEmpty(filePath))
            return;

        await LoadProjectFromFileAsync(filePath);
    }

    /// <summary>
    /// Loads a project from a file path.
    /// </summary>
    public async Task LoadProjectFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _navigationService.ShowError("File not found.");
            return;
        }

        // Check for unsaved changes
        if (IsDirty)
        {
            var save = _navigationService.ShowConfirmation(
                "You have unsaved changes. Do you want to save before opening a new project?");
            if (save)
            {
                await SaveProject();
            }
        }

        try
        {
            IsLoading = true;

            var package = await Task.Run(() => LoadPackageFromFile(filePath));
            
            if (package == null)
            {
                _navigationService.ShowError("Failed to load project file.");
                return;
            }

            // Load image
            if (package.ImageData != null)
            {
                _magickImage = new ImageMagick.MagickImage(package.ImageData);
                CurrentImage = _imageProcessingService.ToBitmapSource(_magickImage);
                ImageWidth = (int)_magickImage.Width;
                ImageHeight = (int)_magickImage.Height;
            }

            // Load measurements
            if (package.Measurements != null)
            {
                LoadMeasurementCollection(package.Measurements);
            }

            // Update state
            HasImage = true;
            IsWelcomeVisible = false;
            CurrentFilePath = filePath;
            LastSavedPath = filePath;
            CurrentProjectId = Guid.NewGuid();
            IsDirty = false;
            ClearUndoHistory();
            UpdateWindowTitle();

            // Update recent projects
            await UpdateRecentProjectsAsync(filePath);

            Send(new ProjectOpenedMessage(filePath, CurrentProjectId));
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to load project: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private MagickCropMeasurementPackage? LoadPackageFromFile(string filePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            var package = new MagickCropMeasurementPackage();

            // Load image
            var imageEntry = archive.GetEntry("image.jpg") ?? archive.GetEntry("image.png");
            if (imageEntry != null)
            {
                using var imageStream = imageEntry.Open();
                using var memoryStream = new MemoryStream();
                imageStream.CopyTo(memoryStream);
                package.ImageData = memoryStream.ToArray();
            }

            // Load metadata
            var metadataEntry = archive.GetEntry("metadata.json");
            if (metadataEntry != null)
            {
                using var metadataStream = metadataEntry.Open();
                using var reader = new StreamReader(metadataStream);
                var json = reader.ReadToEnd();
                package.Metadata = System.Text.Json.JsonSerializer.Deserialize<PackageMetadata>(json);
            }

            // Load measurements
            var measurementsEntry = archive.GetEntry("measurements.json");
            if (measurementsEntry != null)
            {
                using var measurementsStream = measurementsEntry.Open();
                using var reader = new StreamReader(measurementsStream);
                var json = reader.ReadToEnd();
                package.Measurements = System.Text.Json.JsonSerializer.Deserialize<MeasurementCollection>(json);
            }

            return package;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Export Commands

    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task ExportImage()
    {
        if (_magickImage == null)
            return;

        var filter = "JPEG|*.jpg|PNG|*.png|BMP|*.bmp|TIFF|*.tiff|WebP|*.webp";
        var defaultName = Path.GetFileNameWithoutExtension(CurrentFilePath ?? "image") + ".jpg";
        
        var savePath = _fileDialogService.ShowSaveFileDialog(filter, defaultName, "Export Image");
        
        if (string.IsNullOrEmpty(savePath))
            return;

        try
        {
            IsLoading = true;

            var extension = Path.GetExtension(savePath).ToLowerInvariant();
            var format = extension switch
            {
                ".jpg" or ".jpeg" => ImageMagick.MagickFormat.Jpeg,
                ".png" => ImageMagick.MagickFormat.Png,
                ".bmp" => ImageMagick.MagickFormat.Bmp,
                ".tiff" or ".tif" => ImageMagick.MagickFormat.Tiff,
                ".webp" => ImageMagick.MagickFormat.WebP,
                _ => ImageMagick.MagickFormat.Jpeg
            };

            var success = await _imageProcessingService.SaveImageAsync(_magickImage, savePath, format);
            
            if (success)
            {
                Send(new ImageSavedMessage(savePath));
                _navigationService.ShowMessage($"Image exported to:\n{savePath}", "Exported");
            }
            else
            {
                _navigationService.ShowError("Failed to export image.");
            }
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to export image: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void ShowSaveWindow()
    {
        if (_magickImage == null || string.IsNullOrEmpty(CurrentFilePath))
            return;

        // Save current image to temp file for SaveWindow
        var tempPath = Path.Combine(Path.GetTempPath(), $"magickcrop_{Guid.NewGuid()}.jpg");
        _magickImage.Write(tempPath);

        var saveWindow = App.GetService<IWindowFactory>().CreateSaveWindow(tempPath);
        saveWindow.Owner = _navigationService.GetActiveWindow();
        saveWindow.ShowDialog();

        // Cleanup temp file
        try { File.Delete(tempPath); } catch { }
    }

    #endregion

    #region Recent Projects

    private async Task UpdateRecentProjectsAsync(string filePath)
    {
        var projectInfo = new RecentProjectInfo
        {
            Id = CurrentProjectId != Guid.Empty ? CurrentProjectId : Guid.NewGuid(),
            Name = Path.GetFileNameWithoutExtension(filePath),
            FilePath = filePath,
            Created = DateTime.Now,
            LastOpened = DateTime.Now
        };

        await _recentProjectsService.AddRecentProjectAsync(projectInfo);
        Send(new RecentProjectsChangedMessage());
    }

    [RelayCommand]
    private async Task OpenRecentProject(RecentProjectInfo? project)
    {
        if (project == null || string.IsNullOrEmpty(project.FilePath))
            return;

        if (!File.Exists(project.FilePath))
        {
            var remove = _navigationService.ShowConfirmation(
                "This file no longer exists. Remove from recent projects?");
            if (remove)
            {
                await _recentProjectsService.RemoveRecentProjectAsync(project.Id);
            }
            return;
        }

        await LoadProjectFromFileAsync(project.FilePath);
    }

    #endregion

    #region New Document

    [RelayCommand]
    private async Task NewDocument()
    {
        if (IsDirty)
        {
            var save = _navigationService.ShowConfirmation(
                "You have unsaved changes. Do you want to save before creating a new document?");
            if (save)
            {
                await SaveProject();
            }
        }

        ResetForNewDocument();
    }

    #endregion

    #region Auto-Save

    private System.Timers.Timer? _autoSaveTimer;

    /// <summary>
    /// Starts auto-save timer.
    /// </summary>
    public void StartAutoSave(int intervalSeconds = 60)
    {
        _autoSaveTimer?.Stop();
        _autoSaveTimer = new System.Timers.Timer(intervalSeconds * 1000);
        _autoSaveTimer.Elapsed += async (_, _) => await AutoSaveAsync();
        _autoSaveTimer.Start();
    }

    /// <summary>
    /// Stops auto-save timer.
    /// </summary>
    public void StopAutoSave()
    {
        _autoSaveTimer?.Stop();
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;
    }

    private async Task AutoSaveAsync()
    {
        if (!HasImage || !IsDirty || _magickImage == null)
            return;

        try
        {
            var autoSavePath = _recentProjectsService.GetAutosavePath();
            var package = CreateMeasurementPackage();
            
            await Task.Run(() => SavePackageToFile(package, autoSavePath));
            
            var projectInfo = new RecentProjectInfo
            {
                Id = CurrentProjectId != Guid.Empty ? CurrentProjectId : Guid.NewGuid(),
                Name = Path.GetFileNameWithoutExtension(CurrentFilePath ?? "Untitled"),
                FilePath = autoSavePath,
                LastOpened = DateTime.Now
            };
            
            await _recentProjectsService.AutosaveProjectAsync(package, projectInfo);
        }
        catch
        {
            // Silently fail auto-save
        }
    }

    public override void Cleanup()
    {
        StopAutoSave();
        base.Cleanup();
    }

    #endregion
}
```

### 2. Update MagickCropMeasurementPackage

**File: `Models/MeasurementControls/MagickCropMeasurementPackage.cs`**

```csharp
namespace MagickCrop.Models.MeasurementControls;

/// <summary>
/// Complete measurement package including image and data.
/// </summary>
public class MagickCropMeasurementPackage
{
    /// <summary>
    /// Package metadata.
    /// </summary>
    public PackageMetadata? Metadata { get; set; }

    /// <summary>
    /// Collection of all measurements.
    /// </summary>
    public MeasurementCollection? Measurements { get; set; }

    /// <summary>
    /// Raw image data (for loading).
    /// </summary>
    public byte[]? ImageData { get; set; }

    /// <summary>
    /// Path to the image file (for reference).
    /// </summary>
    public string? ImagePath { get; set; }
}
```

### 3. Update MainWindow XAML File Menu

**File: `MainWindow.xaml`** (update menu)

```xml
<ui:TitleBar.Menu>
    <Menu>
        <MenuItem Header="_File">
            <MenuItem Header="_New" Command="{Binding NewDocumentCommand}" InputGestureText="Ctrl+N"/>
            <MenuItem Header="_Open Image..." Command="{Binding OpenImageCommand}" InputGestureText="Ctrl+O"/>
            <MenuItem Header="Open _Project..." Command="{Binding OpenProjectCommand}" InputGestureText="Ctrl+Shift+O"/>
            <Separator/>
            <MenuItem Header="_Save Project" Command="{Binding SaveProjectCommand}" InputGestureText="Ctrl+S"/>
            <MenuItem Header="Save Project _As..." Command="{Binding SaveProjectAsCommand}" InputGestureText="Ctrl+Shift+S"/>
            <Separator/>
            <MenuItem Header="_Export Image..." Command="{Binding ExportImageCommand}" InputGestureText="Ctrl+E"/>
            <MenuItem Header="Show in Save Window" Command="{Binding ShowSaveWindowCommand}"/>
            <Separator/>
            <MenuItem Header="E_xit" Click="Exit_Click" InputGestureText="Alt+F4"/>
        </MenuItem>
        
        <MenuItem Header="_Edit">
            <MenuItem Header="_Undo" Command="{Binding UndoCommand}" InputGestureText="Ctrl+Z"/>
            <MenuItem Header="_Redo" Command="{Binding RedoCommand}" InputGestureText="Ctrl+Y"/>
            <Separator/>
            <MenuItem Header="_Paste" Command="{Binding PasteFromClipboardCommand}" InputGestureText="Ctrl+V"/>
        </MenuItem>
        
        <MenuItem Header="_View">
            <MenuItem Header="Reset View" Command="{Binding ResetViewCommand}"/>
            <MenuItem Header="Toggle Measurements" Command="{Binding ToggleMeasurementPanelCommand}"/>
        </MenuItem>
        
        <MenuItem Header="_Help">
            <MenuItem Header="_About" Command="{Binding ShowAboutCommand}"/>
        </MenuItem>
    </Menu>
</ui:TitleBar.Menu>
```

### 4. Add Keyboard Shortcuts

**File: `MainWindow.xaml`** (add input bindings)

```xml
<Window.InputBindings>
    <!-- File -->
    <KeyBinding Key="N" Modifiers="Control" Command="{Binding NewDocumentCommand}"/>
    <KeyBinding Key="O" Modifiers="Control" Command="{Binding OpenImageCommand}"/>
    <KeyBinding Key="O" Modifiers="Control+Shift" Command="{Binding OpenProjectCommand}"/>
    <KeyBinding Key="S" Modifiers="Control" Command="{Binding SaveProjectCommand}"/>
    <KeyBinding Key="S" Modifiers="Control+Shift" Command="{Binding SaveProjectAsCommand}"/>
    <KeyBinding Key="E" Modifiers="Control" Command="{Binding ExportImageCommand}"/>
    
    <!-- Edit -->
    <KeyBinding Key="Z" Modifiers="Control" Command="{Binding UndoCommand}"/>
    <KeyBinding Key="Y" Modifiers="Control" Command="{Binding RedoCommand}"/>
    <KeyBinding Key="V" Modifiers="Control" Command="{Binding PasteFromClipboardCommand}"/>
    
    <!-- Escape to cancel -->
    <KeyBinding Key="Escape" Command="{Binding CancelPlacementCommand}"/>
</Window.InputBindings>
```

### 5. Update MainWindow Code-Behind for Window Events

**File: `MainWindow.xaml.cs`** (add window event handlers)

```csharp
public partial class MainWindow : FluentWindow
{
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
        ViewModel.StartAutoSave(60); // Auto-save every 60 seconds
    }

    private async void OnClosing(object sender, CancelEventArgs e)
    {
        if (ViewModel.IsDirty)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    e.Cancel = true;
                    await ViewModel.SaveProjectCommand.ExecuteAsync(null);
                    if (!ViewModel.IsDirty) // Save succeeded
                    {
                        Close();
                    }
                    break;
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;
                // MessageBoxResult.No - proceed with closing
            }
        }
        
        ViewModel.Cleanup();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
```

---

## Implementation Steps

1. Add file operation methods to `MainWindowViewModel`
2. Update `MagickCropMeasurementPackage` if needed
3. Update `MainWindow.xaml` with menu and keyboard shortcuts
4. Update `MainWindow.xaml.cs` with window event handlers
5. Test save/load cycle
6. Test export functionality
7. Test recent projects integration
8. Test auto-save
9. Test unsaved changes prompt on close

---

## Validation Checklist

- [ ] Save Project command works
- [ ] Save Project As command works
- [ ] Open Project command works
- [ ] Export Image works for all formats
- [ ] Recent projects are updated after save/open
- [ ] Auto-save runs in background
- [ ] Keyboard shortcuts work
- [ ] Unsaved changes prompt on close
- [ ] Menu items enable/disable correctly

---

## Files Changed/Created

| File | Change Type |
|------|-------------|
| `ViewModels/MainWindowViewModel.cs` | Modified |
| `Models/MeasurementControls/MagickCropMeasurementPackage.cs` | Modified |
| `MainWindow.xaml` | Modified |
| `MainWindow.xaml.cs` | Modified |

---

## Notes

### File Format (.mcm)

The .mcm file is a ZIP archive containing:
- `image.jpg` - The working image
- `metadata.json` - Project metadata
- `measurements.json` - All measurements

### Auto-Save Strategy

Auto-save:
- Runs every 60 seconds (configurable)
- Only saves if document is dirty
- Saves to a temp location
- Doesn't affect the "dirty" state
- Silent failure (non-disruptive)

### Unsaved Changes Flow

1. User tries to close/new
2. If dirty, prompt to save
3. If Yes: save, then proceed
4. If No: proceed without saving
5. If Cancel: abort the operation

---

## Next Steps

Proceed to **Step 17: Value Converters** to create commonly needed converters.
