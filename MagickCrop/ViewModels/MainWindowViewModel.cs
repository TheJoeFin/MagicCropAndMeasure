using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MagickCrop.Messages;
using MagickCrop.Services.Interfaces;
using MagickCrop.ViewModels.Base;

namespace MagickCrop.ViewModels;

/// <summary>
/// Main ViewModel for the application.
/// Manages application state, tool selection, and user interactions.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IRecentProjectsService _recentProjectsService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IClipboardService _clipboardService;
    private readonly INavigationService _navigationService;
    private readonly IImageProcessingService _imageProcessingService;
    private string? _currentImagePath;

    #region Image State

    /// <summary>
    /// Gets or sets the currently loaded image.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPerformImageOperations))]
    private BitmapSource? _currentImage;

    /// <summary>
    /// Gets or sets whether an image is currently loaded.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPerformImageOperations))]
    private bool _hasImage;

    /// <summary>
    /// Gets or sets the width of the current image in pixels.
    /// </summary>
    [ObservableProperty]
    private int _imageWidth;

    /// <summary>
    /// Gets or sets the height of the current image in pixels.
    /// </summary>
    [ObservableProperty]
    private int _imageHeight;

    /// <summary>
    /// Gets whether image operations can be performed.
    /// Used by multiple commands as CanExecute condition.
    /// </summary>
    public bool CanPerformImageOperations => HasImage && !IsLoading;

    #endregion

    #region UI State

    /// <summary>
    /// Gets or sets whether the measurement panel is visible.
    /// </summary>
    [ObservableProperty]
    private bool _showMeasurementPanel = true;

    /// <summary>
    /// Gets or sets whether the toolbar is visible.
    /// </summary>
    [ObservableProperty]
    private bool _showToolbar = true;

    /// <summary>
    /// Gets the window title with optional unsaved changes indicator.
    /// </summary>
    public string WindowTitle => IsDirty && !string.IsNullOrEmpty(CurrentFilePath)
        ? $"Magic Crop & Measure - {Path.GetFileName(CurrentFilePath)}*"
        : (!string.IsNullOrEmpty(CurrentFilePath) ? $"Magic Crop & Measure - {Path.GetFileName(CurrentFilePath)}" : "Magic Crop & Measure");

    /// <summary>
    /// Gets or sets the zoom level (1.0 = 100%).
    /// </summary>
    [ObservableProperty]
    private double _zoomLevel = 1.0;

    #endregion

    #region Project State

    /// <summary>
    /// Gets or sets whether the current project has unsaved changes.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    /// <summary>
    /// Gets or sets the path to the current file.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string? _currentFilePath;

    /// <summary>
    /// Gets or sets the unique identifier of the current project.
    /// </summary>
    [ObservableProperty]
    private Guid _currentProjectId;

    #endregion

    #region Tool State

    /// <summary>
    /// Gets or sets the current tool/mode the application is in.
    /// </summary>
    [ObservableProperty]
    private DraggingMode _currentTool = DraggingMode.None;

    /// <summary>
    /// Gets or sets whether a measurement is currently being placed.
    /// </summary>
    [ObservableProperty]
    private bool _isPlacingMeasurement;

    /// <summary>
    /// Gets or sets the current state of measurement placement.
    /// </summary>
    [ObservableProperty]
    private PlacementState _placementState = PlacementState.NotPlacing;

    /// <summary>
    /// Gets or sets the current step in the measurement placement process.
    /// </summary>
    [ObservableProperty]
    private int _placementStep;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new instance of MainWindowViewModel with services from the DI container.
    /// </summary>
    public MainWindowViewModel() : this(
        App.GetService<IRecentProjectsService>(),
        App.GetService<IFileDialogService>(),
        App.GetService<IClipboardService>(),
        App.GetService<IImageProcessingService>(),
        App.GetService<INavigationService>())
    {
    }

    /// <summary>
    /// Creates a new instance of MainWindowViewModel with explicit service dependencies.
    /// </summary>
    /// <param name="recentProjectsService">Service for managing recent projects.</param>
    /// <param name="fileDialogService">Service for file dialog operations.</param>
    /// <param name="clipboardService">Service for clipboard operations.</param>
    /// <param name="imageProcessingService">Service for image processing operations.</param>
    /// <param name="navigationService">Service for window navigation.</param>
    public MainWindowViewModel(
        IRecentProjectsService recentProjectsService,
        IFileDialogService fileDialogService,
        IClipboardService clipboardService,
        IImageProcessingService imageProcessingService,
        INavigationService navigationService)
    {
        _recentProjectsService = recentProjectsService ?? throw new ArgumentNullException(nameof(recentProjectsService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

        Title = "Magic Crop & Measure";
    }

    #endregion

    #region Undo/Redo

    /// <summary>
    /// Gets or sets the undo/redo manager for the application.
    /// </summary>
    private UndoRedo? _undoRedo;

    /// <summary>
    /// Gets whether undo operations are available.
    /// </summary>
    [ObservableProperty]
    private bool _canUndo;

    /// <summary>
    /// Gets whether redo operations are available.
    /// </summary>
    [ObservableProperty]
    private bool _canRedo;

    /// <summary>
    /// Executes an undo operation if available.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _undoRedo?.Undo();
        UpdateUndoRedoState();
    }

    /// <summary>
    /// Executes a redo operation if available.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _undoRedo?.Redo();
        UpdateUndoRedoState();
    }

    /// <summary>
    /// Sets up callbacks for undo/redo state changes.
    /// </summary>
    private void SetupUndoRedoCallbacks()
    {
        if (_undoRedo != null)
        {
            UpdateUndoRedoState();
        }
    }

    /// <summary>
    /// Updates the CanUndo and CanRedo properties based on current undo/redo state.
    /// </summary>
    private void UpdateUndoRedoState()
    {
        if (_undoRedo != null)
        {
            CanUndo = _undoRedo.CanUndo;
            CanRedo = _undoRedo.CanRedo;
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the ViewModel asynchronously.
    /// </summary>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _undoRedo = new UndoRedo();
        SetupUndoRedoCallbacks();
    }

    /// <summary>
    /// Cleans up resources when the ViewModel is no longer needed.
    /// </summary>
    public override void Cleanup()
    {
        base.Cleanup();
    }

    #endregion

    #region Tool Commands

    /// <summary>
    /// Selects a specific tool/mode.
    /// Can only execute when an image is loaded.
    /// </summary>
    /// <param name="tool">The tool mode to select.</param>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private void SelectTool(DraggingMode tool)
    {
        // Cancel any in-progress placement
        if (IsPlacingMeasurement)
        {
            CancelPlacement();
        }

        CurrentTool = tool;
        Send(new ActiveToolChangedMessage(tool.ToString()));
    }

    /// <summary>
    /// Starts the measurement placement process for the specified measurement type.
    /// Can only execute when image operations are possible.
    /// </summary>
    /// <param name="measurementType">The type of measurement to place (e.g., "Distance", "Angle").</param>
    [RelayCommand(CanExecute = nameof(CanPerformImageOperations))]
    private void StartMeasurementPlacement(string measurementType)
    {
        IsPlacingMeasurement = true;
        PlacementState = PlacementState.WaitingForFirstPoint;
        PlacementStep = 0;
        
        // Set tool based on measurement type
        CurrentTool = measurementType switch
        {
            "Distance" => DraggingMode.MeasureDistance,
            "Angle" => DraggingMode.MeasureAngle,
            "Rectangle" => DraggingMode.MeasureRectangle,
            "Circle" => DraggingMode.MeasureCircle,
            "Polygon" => DraggingMode.MeasurePolygon,
            _ => DraggingMode.None
        };

        Send(new ActiveToolChangedMessage(CurrentTool.ToString()));
    }

    /// <summary>
    /// Cancels the current measurement placement.
    /// Can only execute when a measurement is being placed.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsPlacingMeasurement))]
    private void CancelPlacement()
    {
        IsPlacingMeasurement = false;
        PlacementState = PlacementState.NotPlacing;
        PlacementStep = 0;
        CurrentTool = DraggingMode.None;
    }

    /// <summary>
    /// Advances the measurement placement to the next step.
    /// Updates PlacementState based on the current tool and step count.
    /// </summary>
    [RelayCommand]
    private void AdvancePlacementStep()
    {
        PlacementStep++;
        
        PlacementState = CurrentTool switch
        {
            DraggingMode.MeasureDistance => PlacementStep >= 1 ? PlacementState.Complete : PlacementState.WaitingForSecondPoint,
            DraggingMode.MeasureAngle => PlacementStep >= 2 ? PlacementState.Complete : PlacementState.WaitingForSecondPoint,
            DraggingMode.MeasureRectangle => PlacementStep >= 1 ? PlacementState.Complete : PlacementState.WaitingForSecondPoint,
            DraggingMode.MeasureCircle => PlacementStep >= 1 ? PlacementState.Complete : PlacementState.WaitingForSecondPoint,
            _ => PlacementState.WaitingForMorePoints
        };

        if (PlacementState == PlacementState.Complete)
        {
            IsPlacingMeasurement = false;
            CurrentTool = DraggingMode.None;
        }
    }

    #endregion

    #region Image Operations

    /// <summary>
    /// Sets the current image and stores its file path.
    /// </summary>
    /// <param name="imagePath">The file path to the current image.</param>
    /// <param name="bitmapSource">The BitmapSource representation of the image.</param>
    public void SetCurrentImage(string imagePath, BitmapSource bitmapSource)
    {
        _currentImagePath = imagePath;
        CurrentImage = bitmapSource;
        ImageWidth = bitmapSource.PixelWidth;
        ImageHeight = bitmapSource.PixelHeight;
        HasImage = true;
    }

    /// <summary>
    /// Loads an image from a file path and displays it.
    /// </summary>
    [RelayCommand]
    private async Task LoadImage()
    {
        var filePath = _fileDialogService.ShowOpenFileDialog("Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.heic|All files (*.*)|*.*");
        if (string.IsNullOrEmpty(filePath))
            return;

        IsLoading = true;
        try
        {
            var magickImage = await _imageProcessingService.LoadImageAsync(filePath);
            if (magickImage != null)
            {
                _currentImagePath = filePath;
                CurrentImage = _imageProcessingService.ToBitmapSource(magickImage);
                ImageWidth = (int)magickImage.Width;
                ImageHeight = (int)magickImage.Height;
                HasImage = true;
                IsDirty = false;
                magickImage.Dispose();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Pastes an image from the clipboard and displays it.
    /// </summary>
    [RelayCommand]
    private async Task PasteFromClipboard()
    {
        var clipboardImage = _clipboardService.GetImage();
        if (clipboardImage == null)
            return;

        IsLoading = true;
        try
        {
            var magickImage = _imageProcessingService.FromBitmapSource(clipboardImage);
            if (magickImage != null)
            {
                var tempPath = Path.GetTempFileName();
                await _imageProcessingService.SaveImageAsync(magickImage, tempPath, ImageMagick.MagickFormat.Png);
                
                _currentImagePath = tempPath;
                CurrentImage = _imageProcessingService.ToBitmapSource(magickImage);
                ImageWidth = (int)magickImage.Width;
                ImageHeight = (int)magickImage.Height;
                HasImage = true;
                IsDirty = false;
                magickImage.Dispose();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Rotates the current image 90 degrees clockwise.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPerformImageOperations))]
    private async Task RotateClockwise()
    {
        if (string.IsNullOrEmpty(_currentImagePath))
            return;

        IsLoading = true;
        try
        {
            var magickImage = await _imageProcessingService.LoadImageAsync(_currentImagePath);
            if (magickImage != null)
            {
                var rotated = _imageProcessingService.Rotate(magickImage, 90);
                var tempPath = Path.GetTempFileName();
                await _imageProcessingService.SaveImageAsync(rotated, tempPath, ImageMagick.MagickFormat.Png);
                
                _currentImagePath = tempPath;
                CurrentImage = _imageProcessingService.ToBitmapSource(rotated);
                ImageWidth = (int)rotated.Width;
                ImageHeight = (int)rotated.Height;
                IsDirty = true;
                
                magickImage.Dispose();
                rotated.Dispose();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Rotates the current image 90 degrees counter-clockwise.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPerformImageOperations))]
    private async Task RotateCounterClockwise()
    {
        if (string.IsNullOrEmpty(_currentImagePath))
            return;

        IsLoading = true;
        try
        {
            var magickImage = await _imageProcessingService.LoadImageAsync(_currentImagePath);
            if (magickImage != null)
            {
                var rotated = _imageProcessingService.Rotate(magickImage, -90);
                var tempPath = Path.GetTempFileName();
                await _imageProcessingService.SaveImageAsync(rotated, tempPath, ImageMagick.MagickFormat.Png);
                
                _currentImagePath = tempPath;
                CurrentImage = _imageProcessingService.ToBitmapSource(rotated);
                ImageWidth = (int)rotated.Width;
                ImageHeight = (int)rotated.Height;
                IsDirty = true;
                
                magickImage.Dispose();
                rotated.Dispose();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Flips the current image horizontally.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPerformImageOperations))]
    private async Task FlipHorizontal()
    {
        if (string.IsNullOrEmpty(_currentImagePath))
            return;

        IsLoading = true;
        try
        {
            var magickImage = await _imageProcessingService.LoadImageAsync(_currentImagePath);
            if (magickImage != null)
            {
                _imageProcessingService.FlipHorizontal(magickImage);
                var tempPath = Path.GetTempFileName();
                await _imageProcessingService.SaveImageAsync(magickImage, tempPath, ImageMagick.MagickFormat.Png);
                
                _currentImagePath = tempPath;
                CurrentImage = _imageProcessingService.ToBitmapSource(magickImage);
                IsDirty = true;
                
                magickImage.Dispose();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Flips the current image vertically.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPerformImageOperations))]
    private async Task FlipVertical()
    {
        if (string.IsNullOrEmpty(_currentImagePath))
            return;

        IsLoading = true;
        try
        {
            var magickImage = await _imageProcessingService.LoadImageAsync(_currentImagePath);
            if (magickImage != null)
            {
                _imageProcessingService.FlipVertical(magickImage);
                var tempPath = Path.GetTempFileName();
                await _imageProcessingService.SaveImageAsync(magickImage, tempPath, ImageMagick.MagickFormat.Png);
                
                _currentImagePath = tempPath;
                CurrentImage = _imageProcessingService.ToBitmapSource(magickImage);
                IsDirty = true;
                
                magickImage.Dispose();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Crops the current image to the specified region.
    /// Call this method from code-behind when crop region is determined.
    /// </summary>
    public async Task CropImage(int x, int y, int width, int height)
    {
        if (string.IsNullOrEmpty(_currentImagePath))
            return;

        IsLoading = true;
        try
        {
            var magickImage = await _imageProcessingService.LoadImageAsync(_currentImagePath);
            if (magickImage != null)
            {
                var cropped = _imageProcessingService.Crop(magickImage, x, y, width, height);
                var tempPath = Path.GetTempFileName();
                await _imageProcessingService.SaveImageAsync(cropped, tempPath, ImageMagick.MagickFormat.Png);
                
                _currentImagePath = tempPath;
                CurrentImage = _imageProcessingService.ToBitmapSource(cropped);
                ImageWidth = (int)cropped.Width;
                ImageHeight = (int)cropped.Height;
                IsDirty = true;
                
                magickImage.Dispose();
                cropped.Dispose();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion
}
