using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagick;
using MagickCrop.Messages;
using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.Services.Interfaces;
using MagickCrop.ViewModels.Base;
using MagickCrop.ViewModels.Measurements;

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
    private readonly IWindowFactory _windowFactory;
    private string? _currentImagePath;
    private MagickImage? _magickImage;

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
    /// Gets or sets whether the welcome screen is visible.
    /// </summary>
    [ObservableProperty]
    private bool _isWelcomeVisible = true;

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

    #region File Operations State

    /// <summary>
    /// Gets or sets whether the application is currently saving a file.
    /// </summary>
    [ObservableProperty]
    private bool _isSaving;

    /// <summary>
    /// Gets or sets the path of the last saved file.
    /// </summary>
    [ObservableProperty]
    private string? _lastSavedPath;

    /// <summary>
    /// Gets or sets whether the application is currently loading a file.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPerformImageOperations))]
    private bool _isLoading;

    #endregion

    #region Save Commands

    /// <summary>
    /// Saves the current project to the last saved path or prompts for a new path if not previously saved.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task SaveProject()
    {
        if (string.IsNullOrEmpty(LastSavedPath))
        {
            await SaveProjectAs();
        }
        else
        {
            await SaveProjectToPathAsync(LastSavedPath);
        }
    }

    /// <summary>
    /// Saves the current project to a user-selected path.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task SaveProjectAs()
    {
        var result = _fileDialogService.ShowSaveFileDialog(
            "Magic Crop Project Files (*.mcm)|*.mcm",
            defaultFileName: "project.mcm");

        if (!string.IsNullOrEmpty(result))
        {
            await SaveProjectToPathAsync(result);
        }
    }

    /// <summary>
    /// Creates a new project, prompting to save if there are unsaved changes.
    /// </summary>
    [RelayCommand]
    private async Task NewProject()
    {
        if (IsDirty && HasImage)
        {
            var save = _navigationService.ShowConfirmation(
                "You have unsaved changes. Do you want to save before creating a new project?");
            if (save)
            {
                await SaveProject();
                if (IsDirty) // Save failed or was cancelled
                    return;
            }
        }

        ClearAllMeasurementsInternal();
        CurrentImage = null;
        HasImage = false;
        IsWelcomeVisible = true;
        CurrentFilePath = null;
        LastSavedPath = null;
        CurrentProjectId = Guid.NewGuid();
        IsDirty = false;
        ImageWidth = 0;
        ImageHeight = 0;
        _currentImagePath = null;
        _magickImage?.Dispose();
        _magickImage = null;
        ClearUndoHistory();
    }

    /// <summary>
    /// Core save logic that creates and saves the measurement package to the specified path.
    /// </summary>
    /// <param name="filePath">The full path where the project file should be saved.</param>
    private async Task SaveProjectToPathAsync(string filePath)
    {
        try
        {
            IsSaving = true;

            await Task.Run(() =>
            {
                var package = CreateMeasurementPackage();
                SavePackageToFile(package, filePath);
            });

            LastSavedPath = filePath;
            CurrentFilePath = filePath;
            IsDirty = false;

            await UpdateRecentProjectsAsync(filePath);

            WeakReferenceMessenger.Default.Send(new ProjectSavedMessage(filePath));
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

    /// <summary>
    /// Creates a measurement package from the current application state.
    /// </summary>
    /// <returns>A MagickCropMeasurementPackage containing current measurements and metadata.</returns>
    private MagickCropMeasurementPackage CreateMeasurementPackage()
    {
        var metadata = new PackageMetadata
        {
            CreationDate = DateTime.Now,
            LastModified = DateTime.Now,
            OriginalFilename = Path.GetFileName(CurrentFilePath ?? "Untitled.mcm"),
            OriginalImageSize = new System.Windows.Size(ImageWidth, ImageHeight),
            CurrentImageSize = new System.Windows.Size(ImageWidth, ImageHeight),
            ProjectId = CurrentProjectId.ToString()
        };

        var package = new MagickCropMeasurementPackage
        {
            Metadata = metadata,
            Measurements = ToMeasurementCollection(),
            ImagePath = _currentImagePath
        };

        return package;
    }

    /// <summary>
    /// Saves a measurement package to a .mcm file.
    /// </summary>
    /// <param name="package">The package to save.</param>
    /// <param name="filePath">The full path where the package should be saved.</param>
    private void SavePackageToFile(MagickCropMeasurementPackage package, string filePath)
    {
        if (!package.SaveToFileAsync(filePath))
        {
            throw new InvalidOperationException($"Failed to save package to {filePath}");
        }
    }

    #endregion

    #region Load Commands

    /// <summary>
    /// Opens a file dialog and loads a project from the selected .mcm file.
    /// </summary>
    [RelayCommand]
    private async Task OpenProject()
    {
        var filter = "Magic Crop Project (*.mcm)|*.mcm|All Files (*.*)|*.*";
        var filePath = _fileDialogService.ShowOpenFileDialog(filter, "Open Project");

        if (string.IsNullOrEmpty(filePath))
            return;

        await LoadProjectFromFileAsync(filePath);
    }

    /// <summary>
    /// Loads a project from the specified file path.
    /// Handles unsaved changes prompt, image loading, measurement loading, and UI updates.
    /// </summary>
    /// <param name="filePath">The full path to the .mcm project file to load.</param>
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
                _magickImage = new MagickImage(package.ImageData);
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

            // Update recent projects
            await UpdateRecentProjectsAsync(filePath);

            WeakReferenceMessenger.Default.Send(new ProjectOpenedMessage(filePath, CurrentProjectId));
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

    /// <summary>
    /// Loads a measurement package from a .mcm file.
    /// </summary>
    /// <param name="filePath">The full path to the .mcm file.</param>
    /// <returns>The loaded package or null if loading failed.</returns>
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
                package.Metadata = JsonSerializer.Deserialize<PackageMetadata>(json);
            }

            // Load measurements
            var measurementsEntry = archive.GetEntry("measurements.json");
            if (measurementsEntry != null)
            {
                using var measurementsStream = measurementsEntry.Open();
                using var reader = new StreamReader(measurementsStream);
                var json = reader.ReadToEnd();
                package.Measurements = JsonSerializer.Deserialize<MeasurementCollection>(json);
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

    /// <summary>
    /// Exports the currently loaded image to a file in the selected format.
    /// Supports JPEG, PNG, BMP, TIFF, and WebP formats.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task ExportImage()
    {
        const string filter = "JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                              "PNG Image (*.png)|*.png|" +
                              "BMP Image (*.bmp)|*.bmp|" +
                              "TIFF Image (*.tiff;*.tif)|*.tiff;*.tif|" +
                              "WebP Image (*.webp)|*.webp|" +
                              "All Files (*.*)|*.*";

        var filePath = _fileDialogService.ShowSaveFileDialog(filter, defaultFileName: "image.png");

        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            IsLoading = true;

            // Determine format from file extension
            var extension = Path.GetExtension(filePath).TrimStart('.').ToLower();
            var format = extension switch
            {
                "jpg" or "jpeg" => MagickFormat.Jpg,
                "png" => MagickFormat.Png,
                "bmp" => MagickFormat.Bmp,
                "tiff" or "tif" => MagickFormat.Tiff,
                "webp" => MagickFormat.WebP,
                _ => MagickFormat.Png
            };

            // Save the image
            if (_magickImage != null)
            {
                var success = await _imageProcessingService.SaveImageAsync(_magickImage, filePath, format);

                if (success)
                {
                    WeakReferenceMessenger.Default.Send(new ImageSavedMessage(filePath));
                    _navigationService.ShowMessage(
                        $"Image saved successfully to {Path.GetFileName(filePath)}",
                        "Export Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    _navigationService.ShowError("Failed to save image. Please check the file path and try again.");
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Opens a SaveWindow with export options for the current image.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task ShowSaveWindow()
    {
        try
        {
            IsLoading = true;

            // Save current image to a temporary file
            var tempFileName = $"magickcrop_{Guid.NewGuid()}.jpg";
            var tempPath = Path.Combine(Path.GetTempPath(), tempFileName);

            if (_magickImage != null)
            {
                var success = await _imageProcessingService.SaveImageAsync(_magickImage, tempPath, MagickFormat.Jpg);

                if (!success)
                {
                    _navigationService.ShowError("Failed to prepare image for export.");
                    return;
                }

                // Create SaveWindow and show as modal dialog
                var saveWindow = _windowFactory.CreateSaveWindow(tempPath);
                var activeWindow = _navigationService.GetActiveWindow();
                if (activeWindow != null)
                {
                    saveWindow.Owner = activeWindow;
                }

                saveWindow.ShowDialog();

                // Delete the temporary file after dialog closes
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't show to user - cleanup failure is not critical
                    System.Diagnostics.Debug.WriteLine($"Failed to delete temporary file: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _navigationService.ShowError($"Failed to open export dialog: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Recent Projects


    /// <summary>
    /// Updates the recent projects list with the specified file path.
    /// </summary>
    /// <param name="filePath">The file path of the project to add to recent projects.</param>
    private async Task UpdateRecentProjectsAsync(string filePath)
    {
        var projectInfo = new RecentProjectInfo
        {
            Id = CurrentProjectId.ToString(),
            Name = Path.GetFileNameWithoutExtension(filePath),
            PackagePath = filePath,
            LastModified = DateTime.Now
        };

        await _recentProjectsService.AddRecentProjectAsync(projectInfo);
        WeakReferenceMessenger.Default.Send(new RecentProjectsChangedMessage());
    }

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
        App.GetService<INavigationService>(),
        App.GetService<IWindowFactory>())
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
    /// <param name="windowFactory">Factory for creating windows.</param>
    public MainWindowViewModel(
        IRecentProjectsService recentProjectsService,
        IFileDialogService fileDialogService,
        IClipboardService clipboardService,
        IImageProcessingService imageProcessingService,
        INavigationService navigationService,
        IWindowFactory windowFactory)
    {
        _recentProjectsService = recentProjectsService ?? throw new ArgumentNullException(nameof(recentProjectsService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));

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

    /// <summary>
    /// Clears the undo/redo history.
    /// </summary>
    private void ClearUndoHistory()
    {
        _undoRedo?.Clear();
        UpdateUndoRedoState();
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
        InitializeMeasurementCollections();
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

    #region Measurement Collections

    /// <summary>
    /// Gets or sets the global scale factor for all measurements.
    /// </summary>
    [ObservableProperty]
    private double _globalScaleFactor = 1.0;

    /// <summary>
    /// Gets or sets the global unit of measurement for all measurements.
    /// </summary>
    [ObservableProperty]
    private string _globalUnits = "px";

    /// <summary>
    /// Collection of distance measurements.
    /// </summary>
    public ObservableCollection<DistanceMeasurementViewModel> DistanceMeasurements { get; } = [];

    /// <summary>
    /// Collection of angle measurements.
    /// </summary>
    public ObservableCollection<AngleMeasurementViewModel> AngleMeasurements { get; } = [];

    /// <summary>
    /// Collection of rectangle measurements.
    /// </summary>
    public ObservableCollection<RectangleMeasurementViewModel> RectangleMeasurements { get; } = [];

    /// <summary>
    /// Collection of circle measurements.
    /// </summary>
    public ObservableCollection<CircleMeasurementViewModel> CircleMeasurements { get; } = [];

    /// <summary>
    /// Collection of polygon measurements.
    /// </summary>
    public ObservableCollection<PolygonMeasurementViewModel> PolygonMeasurements { get; } = [];

    /// <summary>
    /// Collection of horizontal line guides.
    /// </summary>
    public ObservableCollection<HorizontalLineViewModel> HorizontalLines { get; } = [];

    /// <summary>
    /// Collection of vertical line guides.
    /// </summary>
    public ObservableCollection<VerticalLineViewModel> VerticalLines { get; } = [];

    /// <summary>
    /// Gets the total count of all measurements.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMeasurements))]
    private int _totalMeasurementCount;

    /// <summary>
    /// Gets whether there are any measurements.
    /// </summary>
    public bool HasMeasurements => TotalMeasurementCount > 0;

    #endregion

    #region Measurement Collection Initialization

    /// <summary>
    /// Initializes measurement collection event handlers and message subscriptions.
    /// </summary>
    private void InitializeMeasurementCollections()
    {
        // Subscribe to collection changes
        DistanceMeasurements.CollectionChanged += OnMeasurementCollectionChanged;
        AngleMeasurements.CollectionChanged += OnMeasurementCollectionChanged;
        RectangleMeasurements.CollectionChanged += OnMeasurementCollectionChanged;
        CircleMeasurements.CollectionChanged += OnMeasurementCollectionChanged;
        PolygonMeasurements.CollectionChanged += OnMeasurementCollectionChanged;
        HorizontalLines.CollectionChanged += OnMeasurementCollectionChanged;
        VerticalLines.CollectionChanged += OnMeasurementCollectionChanged;

        // Register for remove requests
        Register<RemoveMeasurementRequestMessage>(OnRemoveMeasurementRequested);
    }

    /// <summary>
    /// Handles collection changes by updating the total measurement count and marking as dirty.
    /// </summary>
    private void OnMeasurementCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateMeasurementCount();
        IsDirty = true;
    }

    /// <summary>
    /// Updates the total measurement count based on all collections.
    /// </summary>
    private void UpdateMeasurementCount()
    {
        TotalMeasurementCount =
            DistanceMeasurements.Count +
            AngleMeasurements.Count +
            RectangleMeasurements.Count +
            CircleMeasurements.Count +
            PolygonMeasurements.Count +
            HorizontalLines.Count +
            VerticalLines.Count;
    }

    #endregion

    #region Add Measurement Commands

    /// <summary>
    /// Adds a distance measurement to the collection.
    /// </summary>
    /// <param name="points">Array containing start and end points.</param>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddDistanceMeasurement(System.Windows.Point[] points)
    {
        if (points.Length < 2) return;

        var vm = new DistanceMeasurementViewModel
        {
            StartPoint = points[0],
            EndPoint = points[1],
            ScaleFactor = GlobalScaleFactor,
            Units = GlobalUnits
        };

        DistanceMeasurements.Add(vm);
        Send(new MeasurementAddedMessage("Distance", vm.Id));
    }

    /// <summary>
    /// Adds an angle measurement to the collection.
    /// </summary>
    /// <param name="points">Array containing three points: two rays and the vertex.</param>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddAngleMeasurement(System.Windows.Point[] points)
    {
        if (points.Length < 3) return;

        var vm = new AngleMeasurementViewModel
        {
            Point1 = points[0],
            Vertex = points[1],
            Point2 = points[2],
            ScaleFactor = GlobalScaleFactor,
            Units = GlobalUnits
        };

        AngleMeasurements.Add(vm);
        Send(new MeasurementAddedMessage("Angle", vm.Id));
    }

    /// <summary>
    /// Adds a rectangle measurement to the collection.
    /// </summary>
    /// <param name="points">Array containing top-left and bottom-right corners.</param>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddRectangleMeasurement(System.Windows.Point[] points)
    {
        if (points.Length < 2) return;

        var vm = new RectangleMeasurementViewModel
        {
            TopLeft = points[0],
            BottomRight = points[1],
            ScaleFactor = GlobalScaleFactor,
            Units = GlobalUnits
        };

        RectangleMeasurements.Add(vm);
        Send(new MeasurementAddedMessage("Rectangle", vm.Id));
    }

    /// <summary>
    /// Adds a circle measurement to the collection.
    /// </summary>
    /// <param name="points">Array containing center and edge point.</param>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddCircleMeasurement(System.Windows.Point[] points)
    {
        if (points.Length < 2) return;

        var vm = new CircleMeasurementViewModel
        {
            CenterPoint = points[0],
            EdgePoint = points[1],
            ScaleFactor = GlobalScaleFactor,
            Units = GlobalUnits
        };

        CircleMeasurements.Add(vm);
        Send(new MeasurementAddedMessage("Circle", vm.Id));
    }

    /// <summary>
    /// Adds a polygon measurement to the collection.
    /// </summary>
    /// <param name="points">Array of all vertices for the polygon.</param>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddPolygonMeasurement(System.Windows.Point[] points)
    {
        var vm = new PolygonMeasurementViewModel
        {
            ScaleFactor = GlobalScaleFactor,
            Units = GlobalUnits
        };

        foreach (var point in points)
        {
            vm.AddVertex(point);
        }

        PolygonMeasurements.Add(vm);
        Send(new MeasurementAddedMessage("Polygon", vm.Id));
    }

    /// <summary>
    /// Adds a horizontal line guide.
    /// </summary>
    /// <param name="y">The Y-coordinate of the line.</param>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddHorizontalLine(double y)
    {
        var vm = new HorizontalLineViewModel
        {
            Position = y,
            CanvasSize = ImageHeight
        };

        HorizontalLines.Add(vm);
        Send(new MeasurementAddedMessage("HorizontalLine", vm.Id));
    }

    /// <summary>
    /// Adds a vertical line guide.
    /// </summary>
    /// <param name="x">The X-coordinate of the line.</param>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddVerticalLine(double x)
    {
        var vm = new VerticalLineViewModel
        {
            Position = x,
            CanvasSize = ImageWidth
        };

        VerticalLines.Add(vm);
        Send(new MeasurementAddedMessage("VerticalLine", vm.Id));
    }

    #endregion

    #region Remove Measurements

    /// <summary>
    /// Handles remove measurement request messages.
    /// </summary>
    private void OnRemoveMeasurementRequested(RemoveMeasurementRequestMessage message)
    {
        RemoveMeasurementById(message.MeasurementId, message.MeasurementType);
    }

    /// <summary>
    /// Removes a measurement by its ID and type.
    /// </summary>
    /// <param name="id">The unique identifier of the measurement.</param>
    /// <param name="type">The type of measurement (e.g., "Distance", "Angle").</param>
    public void RemoveMeasurementById(Guid id, string type)
    {
        switch (type)
        {
            case "Distance":
                var distance = DistanceMeasurements.FirstOrDefault(m => m.Id == id);
                if (distance != null) DistanceMeasurements.Remove(distance);
                break;
            case "Angle":
                var angle = AngleMeasurements.FirstOrDefault(m => m.Id == id);
                if (angle != null) AngleMeasurements.Remove(angle);
                break;
            case "Rectangle":
                var rect = RectangleMeasurements.FirstOrDefault(m => m.Id == id);
                if (rect != null) RectangleMeasurements.Remove(rect);
                break;
            case "Circle":
                var circle = CircleMeasurements.FirstOrDefault(m => m.Id == id);
                if (circle != null) CircleMeasurements.Remove(circle);
                break;
            case "Polygon":
                var polygon = PolygonMeasurements.FirstOrDefault(m => m.Id == id);
                if (polygon != null) PolygonMeasurements.Remove(polygon);
                break;
            case "HorizontalLine":
                var hLine = HorizontalLines.FirstOrDefault(m => m.Id == id);
                if (hLine != null) HorizontalLines.Remove(hLine);
                break;
            case "VerticalLine":
                var vLine = VerticalLines.FirstOrDefault(m => m.Id == id);
                if (vLine != null) VerticalLines.Remove(vLine);
                break;
        }

        Send(new MeasurementRemovedMessage(id));
    }

    /// <summary>
    /// Clears all measurements after confirming with the user.
    /// </summary>
    [RelayCommand]
    private void ClearAllMeasurements()
    {
        if (!_navigationService.ShowConfirmation("Clear all measurements?"))
            return;

        ClearAllMeasurementsInternal();
    }

    #endregion

    #region Measurement Serialization

    /// <summary>
    /// Creates a MeasurementCollection from current ViewModels for serialization.
    /// </summary>
    /// <returns>A MeasurementCollection containing all current measurements as DTOs.</returns>
    public MeasurementCollection ToMeasurementCollection()
    {
        var collection = new MeasurementCollection
        {
            GlobalScaleFactor = GlobalScaleFactor,
            GlobalUnits = GlobalUnits
        };

        // Convert Distance measurements
        foreach (var vm in DistanceMeasurements)
        {
            collection.DistanceMeasurements.Add(new DistanceMeasurementControlDto
            {
                StartPosition = vm.StartPoint,
                EndPosition = vm.EndPoint,
                ScaleFactor = vm.ScaleFactor,
                Units = vm.Units
            });
        }

        // Convert Angle measurements
        foreach (var vm in AngleMeasurements)
        {
            collection.AngleMeasurements.Add(new AngleMeasurementControlDto
            {
                Point1Position = vm.Point1,
                VertexPosition = vm.Vertex,
                Point3Position = vm.Point2
            });
        }

        // Convert Rectangle measurements
        foreach (var vm in RectangleMeasurements)
        {
            collection.RectangleMeasurements.Add(new RectangleMeasurementControlDto
            {
                TopLeft = vm.TopLeft,
                BottomRight = vm.BottomRight,
                ScaleFactor = vm.ScaleFactor,
                Units = vm.Units
            });
        }

        // Convert Circle measurements
        foreach (var vm in CircleMeasurements)
        {
            collection.CircleMeasurements.Add(new CircleMeasurementControlDto
            {
                Center = vm.CenterPoint,
                EdgePoint = vm.EdgePoint,
                ScaleFactor = vm.ScaleFactor,
                Units = vm.Units
            });
        }

        // Convert Polygon measurements
        foreach (var vm in PolygonMeasurements)
        {
            var dto = new PolygonMeasurementControlDto
            {
                IsClosed = vm.IsClosed,
                ScaleFactor = vm.ScaleFactor,
                Units = vm.Units
            };
            foreach (var vertex in vm.Vertices)
            {
                dto.Vertices.Add(vertex);
            }
            collection.PolygonMeasurements.Add(dto);
        }

        // Convert line guides
        foreach (var vm in HorizontalLines)
        {
            collection.HorizontalLines.Add(new HorizontalLineControlDto
            {
                Position = vm.Position
            });
        }

        foreach (var vm in VerticalLines)
        {
            collection.VerticalLines.Add(new VerticalLineControlDto
            {
                Position = vm.Position
            });
        }

        return collection;
    }

    /// <summary>
    /// Loads measurements from a MeasurementCollection after clearing existing measurements.
    /// </summary>
    /// <param name="collection">The collection containing measurement DTOs to load.</param>
    public void LoadMeasurementCollection(MeasurementCollection collection)
    {
        ClearAllMeasurementsInternal();

        GlobalScaleFactor = collection.GlobalScaleFactor;
        GlobalUnits = collection.GlobalUnits;

        // Load Distance measurements
        foreach (var dto in collection.DistanceMeasurements)
        {
            var vm = new DistanceMeasurementViewModel
            {
                StartPoint = dto.StartPosition,
                EndPoint = dto.EndPosition,
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            DistanceMeasurements.Add(vm);
        }

        // Load Angle measurements
        foreach (var dto in collection.AngleMeasurements)
        {
            var vm = new AngleMeasurementViewModel
            {
                Point1 = dto.Point1Position,
                Vertex = dto.VertexPosition,
                Point2 = dto.Point3Position
            };
            AngleMeasurements.Add(vm);
        }

        // Load Rectangle measurements
        foreach (var dto in collection.RectangleMeasurements)
        {
            var vm = new RectangleMeasurementViewModel
            {
                TopLeft = dto.TopLeft,
                BottomRight = dto.BottomRight,
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            RectangleMeasurements.Add(vm);
        }

        // Load Circle measurements
        foreach (var dto in collection.CircleMeasurements)
        {
            var vm = new CircleMeasurementViewModel
            {
                CenterPoint = dto.Center,
                EdgePoint = dto.EdgePoint,
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            CircleMeasurements.Add(vm);
        }

        // Load Polygon measurements
        foreach (var dto in collection.PolygonMeasurements)
        {
            var vm = new PolygonMeasurementViewModel
            {
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            foreach (var point in dto.Vertices)
            {
                vm.AddVertex(point);
            }
            if (dto.IsClosed) vm.Close();
            PolygonMeasurements.Add(vm);
        }

        // Load line guides
        foreach (var dto in collection.HorizontalLines)
        {
            HorizontalLines.Add(new HorizontalLineViewModel { Position = dto.Position });
        }

        foreach (var dto in collection.VerticalLines)
        {
            VerticalLines.Add(new VerticalLineViewModel { Position = dto.Position });
        }
    }

    /// <summary>
    /// Clears all measurements without user confirmation.
    /// </summary>
    private void ClearAllMeasurementsInternal()
    {
        DistanceMeasurements.Clear();
        AngleMeasurements.Clear();
        RectangleMeasurements.Clear();
        CircleMeasurements.Clear();
        PolygonMeasurements.Clear();
        HorizontalLines.Clear();
        VerticalLines.Clear();
    }

    #endregion
}

