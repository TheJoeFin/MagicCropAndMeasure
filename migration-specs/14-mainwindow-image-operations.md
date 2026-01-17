# Step 14: MainWindow ViewModel - Image Operations

## Objective
Extract image loading, manipulation, and processing operations from MainWindow.xaml.cs into the ViewModel and services.

## Prerequisites
- Step 13 completed (State management)

---

## ⚠️ This step has been broken into smaller sub-steps for easier implementation

**Complete these sub-steps in order:**

| Sub-Step | Description | Estimated Effort |
|----------|-------------|-----------------|
| **14a** | Create ImageProcessingService.cs with LoadImageAsync method | 45 min |
| **14b** | Add rotate methods to ImageProcessingService (RotateClockwise, RotateCounterClockwise) | 30 min |
| **14c** | Add flip methods to ImageProcessingService (FlipHorizontal, FlipVertical) | 20 min |
| **14d** | Add crop method to ImageProcessingService | 30 min |
| **14e** | Add perspective correction method to ImageProcessingService | 30 min |
| **14f** | Register ImageProcessingService in DI | 15 min |
| **14g** | Add LoadImageCommand to MainWindowViewModel | 30 min |
| **14h** | Add PasteFromClipboardCommand to MainWindowViewModel | 30 min |
| **14i** | Add RotateCommands to MainWindowViewModel (RotateCW, RotateCCW) | 30 min |
| **14j** | Add FlipCommands to MainWindowViewModel (FlipH, FlipV) | 20 min |
| **14k** | Add CropCommand to MainWindowViewModel | 45 min |
| **14l** | Wire up drag-drop support through ViewModel | 30 min |
| **14m** | Update MainWindow.xaml bindings for image commands | 30 min |

Each sub-step should be its own commit with a working build.

---

## Current Image Operations in MainWindow

1. **Loading**: Open file dialog, load from clipboard, load from drag-drop
2. **Transforms**: Rotate, flip horizontal/vertical
3. **Cropping**: Interactive crop with aspect ratio
4. **Perspective**: Quadrilateral correction
5. **Export**: Save with format/quality options

## Changes Required

### 1. Create IImageProcessingService Implementation

**File: `Services/ImageProcessingService.cs`**

```csharp
using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;
using MagickCrop.Services.Interfaces;

namespace MagickCrop.Services;

/// <summary>
/// Service for image processing operations using ImageMagick.
/// </summary>
public class ImageProcessingService : IImageProcessingService
{
    public async Task<MagickImage?> LoadImageAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        return await Task.Run(() =>
        {
            try
            {
                var image = new MagickImage(filePath);
                image.AutoOrient(); // Apply EXIF orientation
                return image;
            }
            catch
            {
                return null;
            }
        });
    }

    public async Task<bool> SaveImageAsync(MagickImage image, string filePath, MagickFormat format, int quality = 90)
    {
        return await Task.Run(() =>
        {
            try
            {
                image.Format = format;
                image.Quality = (uint)quality;
                image.Write(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public MagickImage Rotate(MagickImage image, double degrees)
    {
        var clone = (MagickImage)image.Clone();
        clone.Rotate(degrees);
        return clone;
    }

    public MagickImage Crop(MagickImage image, int x, int y, int width, int height)
    {
        var clone = (MagickImage)image.Clone();
        clone.Crop(new MagickGeometry(x, y, width, height));
        clone.RePage();
        return clone;
    }

    public MagickImage Resize(MagickImage image, int width, int height)
    {
        var clone = (MagickImage)image.Clone();
        clone.Resize((uint)width, (uint)height);
        return clone;
    }

    public MagickImage ApplyPerspectiveCorrection(MagickImage image, double[] sourcePoints, double[] targetPoints)
    {
        var clone = (MagickImage)image.Clone();
        clone.Distort(DistortMethod.Perspective, sourcePoints.Concat(targetPoints).ToArray());
        return clone;
    }

    public BitmapSource ToBitmapSource(MagickImage image)
    {
        using var memoryStream = new MemoryStream();
        image.Write(memoryStream, MagickFormat.Png);
        memoryStream.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = memoryStream;
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }

    public MagickImage FromBitmapSource(BitmapSource bitmapSource)
    {
        using var memoryStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
        encoder.Save(memoryStream);
        memoryStream.Position = 0;
        
        return new MagickImage(memoryStream);
    }

    public void FlipHorizontal(MagickImage image)
    {
        image.Flop();
    }

    public void FlipVertical(MagickImage image)
    {
        image.Flip();
    }
}
```

### 2. Add Image Operations to MainWindowViewModel

**File: `ViewModels/MainWindowViewModel.cs`** (add to existing file)

```csharp
public partial class MainWindowViewModel : ViewModelBase
{
    // Add to existing fields
    private readonly IImageProcessingService _imageProcessingService;
    private MagickImage? _magickImage;

    // Add to constructor parameters and initialization
    public MainWindowViewModel(
        IRecentProjectsService recentProjectsService,
        IFileDialogService fileDialogService,
        IClipboardService clipboardService,
        INavigationService navigationService,
        IImageProcessingService imageProcessingService)
    {
        _recentProjectsService = recentProjectsService;
        _fileDialogService = fileDialogService;
        _clipboardService = clipboardService;
        _navigationService = navigationService;
        _imageProcessingService = imageProcessingService;
        
        // ... existing initialization ...
    }

    #region Image Loading Commands

    [RelayCommand]
    private async Task OpenImage()
    {
        var filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.webp|All Files|*.*";
        var filePath = _fileDialogService.ShowOpenFileDialog(filter, "Open Image");
        
        if (string.IsNullOrEmpty(filePath))
            return;

        await LoadImageFromFileAsync(filePath);
    }

    [RelayCommand]
    private async Task PasteFromClipboard()
    {
        if (!_clipboardService.ContainsImage())
            return;

        try
        {
            IsLoading = true;
            var bitmapSource = _clipboardService.GetImage();
            
            if (bitmapSource == null)
                return;

            _magickImage = _imageProcessingService.FromBitmapSource(bitmapSource);
            CurrentImage = bitmapSource;
            ImageWidth = (int)bitmapSource.Width;
            ImageHeight = (int)bitmapSource.Height;
            
            HasImage = true;
            IsWelcomeVisible = false;
            CurrentFilePath = null;
            IsDirty = true;
            
            Send(new ImageLoadedMessage("Clipboard", ImageWidth, ImageHeight));
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to paste image: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads an image from a file path.
    /// </summary>
    public async Task LoadImageFromFileAsync(string filePath)
    {
        try
        {
            IsLoading = true;
            
            _magickImage = await _imageProcessingService.LoadImageAsync(filePath);
            
            if (_magickImage == null)
            {
                _navigationService.ShowError("Failed to load image.");
                return;
            }

            CurrentImage = _imageProcessingService.ToBitmapSource(_magickImage);
            ImageWidth = (int)_magickImage.Width;
            ImageHeight = (int)_magickImage.Height;
            
            HasImage = true;
            IsWelcomeVisible = false;
            CurrentFilePath = filePath;
            IsDirty = false;
            ClearUndoHistory();
            
            Send(new ImageLoadedMessage(filePath, ImageWidth, ImageHeight));
            UpdateWindowTitle();
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to load image: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Image Transform Commands

    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task RotateClockwise()
    {
        await RotateImageAsync(90);
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task RotateCounterClockwise()
    {
        await RotateImageAsync(-90);
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task RotateByAngle(double degrees)
    {
        await RotateImageAsync(degrees);
    }

    private async Task RotateImageAsync(double degrees)
    {
        if (_magickImage == null)
            return;

        try
        {
            IsLoading = true;
            
            // Store for undo
            var previousImage = (MagickImage)_magickImage.Clone();
            
            await Task.Run(() =>
            {
                _magickImage = _imageProcessingService.Rotate(_magickImage, degrees);
            });

            CurrentImage = _imageProcessingService.ToBitmapSource(_magickImage);
            ImageWidth = (int)_magickImage.Width;
            ImageHeight = (int)_magickImage.Height;
            
            AddUndoItem(new MagickImageUndoRedoItem(previousImage, _magickImage, SetMagickImage));
            IsDirty = true;
            
            Send(new ImageModifiedMessage($"Rotated {degrees}°"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task FlipHorizontal()
    {
        if (_magickImage == null)
            return;

        try
        {
            IsLoading = true;
            
            var previousImage = (MagickImage)_magickImage.Clone();
            
            await Task.Run(() =>
            {
                _imageProcessingService.FlipHorizontal(_magickImage);
            });

            CurrentImage = _imageProcessingService.ToBitmapSource(_magickImage);
            
            AddUndoItem(new MagickImageUndoRedoItem(previousImage, _magickImage, SetMagickImage));
            IsDirty = true;
            
            Send(new ImageModifiedMessage("Flipped Horizontal"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task FlipVertical()
    {
        if (_magickImage == null)
            return;

        try
        {
            IsLoading = true;
            
            var previousImage = (MagickImage)_magickImage.Clone();
            
            await Task.Run(() =>
            {
                _imageProcessingService.FlipVertical(_magickImage);
            });

            CurrentImage = _imageProcessingService.ToBitmapSource(_magickImage);
            
            AddUndoItem(new MagickImageUndoRedoItem(previousImage, _magickImage, SetMagickImage));
            IsDirty = true;
            
            Send(new ImageModifiedMessage("Flipped Vertical"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Crop Commands

    [ObservableProperty]
    private bool _isCropping;

    [ObservableProperty]
    private System.Windows.Rect _cropRegion;

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void StartCrop()
    {
        IsCropping = true;
        CurrentTool = ToolMode.Crop;
    }

    [RelayCommand]
    private void CancelCrop()
    {
        IsCropping = false;
        CurrentTool = ToolMode.None;
        CropRegion = default;
    }

    [RelayCommand(CanExecute = nameof(CanApplyCrop))]
    private async Task ApplyCrop()
    {
        if (_magickImage == null || CropRegion.IsEmpty)
            return;

        try
        {
            IsLoading = true;
            
            var previousImage = (MagickImage)_magickImage.Clone();
            
            var x = (int)CropRegion.X;
            var y = (int)CropRegion.Y;
            var width = (int)CropRegion.Width;
            var height = (int)CropRegion.Height;

            await Task.Run(() =>
            {
                _magickImage = _imageProcessingService.Crop(_magickImage, x, y, width, height);
            });

            CurrentImage = _imageProcessingService.ToBitmapSource(_magickImage);
            ImageWidth = (int)_magickImage.Width;
            ImageHeight = (int)_magickImage.Height;
            
            AddUndoItem(new MagickImageUndoRedoItem(previousImage, _magickImage, SetMagickImage));
            IsDirty = true;
            IsCropping = false;
            CurrentTool = ToolMode.None;
            CropRegion = default;
            
            Send(new ImageModifiedMessage("Cropped"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanApplyCrop => IsCropping && !CropRegion.IsEmpty;

    #endregion

    #region Perspective Correction

    [ObservableProperty]
    private bool _isCorrectingPerspective;

    [ObservableProperty]
    private System.Windows.Point[]? _perspectivePoints;

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void StartPerspectiveCorrection()
    {
        IsCorrectingPerspective = true;
        CurrentTool = ToolMode.None; // Custom tool state
    }

    [RelayCommand]
    private void CancelPerspectiveCorrection()
    {
        IsCorrectingPerspective = false;
        PerspectivePoints = null;
    }

    [RelayCommand]
    private async Task ApplyPerspectiveCorrection()
    {
        if (_magickImage == null || PerspectivePoints == null || PerspectivePoints.Length != 4)
            return;

        try
        {
            IsLoading = true;
            
            var previousImage = (MagickImage)_magickImage.Clone();
            
            // Calculate source points (corners of quadrilateral)
            var sourcePoints = PerspectivePoints.SelectMany(p => new[] { p.X, p.Y }).ToArray();
            
            // Calculate target points (rectangle)
            var minX = PerspectivePoints.Min(p => p.X);
            var minY = PerspectivePoints.Min(p => p.Y);
            var maxX = PerspectivePoints.Max(p => p.X);
            var maxY = PerspectivePoints.Max(p => p.Y);
            
            var targetPoints = new double[]
            {
                minX, minY,  // Top-left
                maxX, minY,  // Top-right
                maxX, maxY,  // Bottom-right
                minX, maxY   // Bottom-left
            };

            await Task.Run(() =>
            {
                _magickImage = _imageProcessingService.ApplyPerspectiveCorrection(_magickImage, sourcePoints, targetPoints);
            });

            CurrentImage = _imageProcessingService.ToBitmapSource(_magickImage);
            ImageWidth = (int)_magickImage.Width;
            ImageHeight = (int)_magickImage.Height;
            
            AddUndoItem(new MagickImageUndoRedoItem(previousImage, _magickImage, SetMagickImage));
            IsDirty = true;
            IsCorrectingPerspective = false;
            PerspectivePoints = null;
            
            Send(new ImageModifiedMessage("Perspective Corrected"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Sets the current MagickImage (used by undo/redo).
    /// </summary>
    private void SetMagickImage(MagickImage image)
    {
        _magickImage = image;
        CurrentImage = _imageProcessingService.ToBitmapSource(image);
        ImageWidth = (int)image.Width;
        ImageHeight = (int)image.Height;
    }

    /// <summary>
    /// Gets the current MagickImage for external use.
    /// </summary>
    public MagickImage? GetMagickImage() => _magickImage;

    #endregion
}
```

### 3. Update UndoRedoItem for MagickImage

**File: `Models/UndoRedo.cs`** (add/update)

```csharp
/// <summary>
/// Undo/redo item for MagickImage changes.
/// </summary>
public class MagickImageUndoRedoItem : UndoRedoItem
{
    private readonly MagickImage _previousImage;
    private readonly MagickImage _newImage;
    private readonly Action<MagickImage> _applyAction;

    public MagickImageUndoRedoItem(
        MagickImage previousImage, 
        MagickImage newImage,
        Action<MagickImage> applyAction)
    {
        _previousImage = (MagickImage)previousImage.Clone();
        _newImage = (MagickImage)newImage.Clone();
        _applyAction = applyAction;
    }

    public override void Undo()
    {
        _applyAction((MagickImage)_previousImage.Clone());
    }

    public override void Redo()
    {
        _applyAction((MagickImage)_newImage.Clone());
    }

    public override void Dispose()
    {
        _previousImage.Dispose();
        _newImage.Dispose();
    }
}
```

### 4. Update DI Registration

**File: `App.xaml.cs`**

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // ... existing registrations ...
    
    // Register Image Processing Service
    services.AddSingleton<IImageProcessingService, ImageProcessingService>();
    
    // Update MainWindowViewModel registration
    services.AddTransient<MainWindowViewModel>();
}
```

### 5. Update MainWindow XAML Bindings

**File: `MainWindow.xaml`** (add bindings)

```xml
<!-- Image display -->
<Image Source="{Binding CurrentImage}" 
       Stretch="Uniform"/>

<!-- Loading indicator -->
<ui:ProgressRing IsIndeterminate="True"
                 Visibility="{Binding IsLoading, Converter={StaticResource BooleanToVisibilityConverter}}"/>

<!-- Crop tool buttons -->
<ui:Button Content="Crop" 
           Command="{Binding StartCropCommand}"
           IsEnabled="{Binding HasImage}"/>

<ui:Button Content="Apply Crop" 
           Command="{Binding ApplyCropCommand}"
           Visibility="{Binding IsCropping, Converter={StaticResource BooleanToVisibilityConverter}}"/>

<ui:Button Content="Cancel Crop" 
           Command="{Binding CancelCropCommand}"
           Visibility="{Binding IsCropping, Converter={StaticResource BooleanToVisibilityConverter}}"/>

<!-- Rotate buttons -->
<ui:Button Content="↻" 
           ToolTip="Rotate Clockwise"
           Command="{Binding RotateClockwiseCommand}"/>

<ui:Button Content="↺" 
           ToolTip="Rotate Counter-Clockwise"
           Command="{Binding RotateCounterClockwiseCommand}"/>

<!-- Flip buttons -->
<ui:Button Content="⇔" 
           ToolTip="Flip Horizontal"
           Command="{Binding FlipHorizontalCommand}"/>

<ui:Button Content="⇕" 
           ToolTip="Flip Vertical"
           Command="{Binding FlipVerticalCommand}"/>
```

---

## Implementation Steps

1. Create `Services/ImageProcessingService.cs`
2. Add image operation methods to `MainWindowViewModel`
3. Update `UndoRedo.cs` with `MagickImageUndoRedoItem`
4. Update DI registration
5. Add XAML bindings for image operations
6. Migrate existing code-behind image logic to use ViewModel commands
7. Test each operation

---

## Validation Checklist

- [ ] ImageProcessingService loads images correctly
- [ ] Open Image command works
- [ ] Paste from clipboard works
- [ ] Rotate 90° clockwise works
- [ ] Rotate 90° counter-clockwise works
- [ ] Flip horizontal works
- [ ] Flip vertical works
- [ ] Crop workflow works
- [ ] Undo restores previous image
- [ ] Redo re-applies change

---

## Files Changed/Created

| File | Change Type |
|------|-------------|
| `Services/ImageProcessingService.cs` | Created |
| `ViewModels/MainWindowViewModel.cs` | Modified |
| `Models/UndoRedo.cs` | Modified |
| `MainWindow.xaml` | Modified |
| `App.xaml.cs` | Modified |

---

## Notes

### Async Image Operations

All image processing runs on background threads:
```csharp
await Task.Run(() =>
{
    _magickImage = _imageProcessingService.Rotate(_magickImage, degrees);
});
```

This keeps the UI responsive during heavy operations.

### Undo/Redo with Images

Image undo/redo creates clones:
```csharp
_previousImage = (MagickImage)previousImage.Clone();
_newImage = (MagickImage)newImage.Clone();
```

This can be memory-intensive for large images. Consider:
- Limiting undo history size
- Storing diffs instead of full images (complex)
- Disposing old undo items when limit reached

### Service Abstraction Benefits

`IImageProcessingService` allows:
1. Unit testing with mock image operations
2. Potential for different backends (not just ImageMagick)
3. Clean separation between business logic and implementation

---

## Next Steps

Proceed to **Step 15: MainWindow ViewModel - Measurement Management** to extract measurement collection management.
