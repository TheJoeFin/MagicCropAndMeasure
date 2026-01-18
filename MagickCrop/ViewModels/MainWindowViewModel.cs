using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
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
        App.GetService<INavigationService>())
    {
    }

    /// <summary>
    /// Creates a new instance of MainWindowViewModel with explicit service dependencies.
    /// </summary>
    /// <param name="recentProjectsService">Service for managing recent projects.</param>
    /// <param name="fileDialogService">Service for file dialog operations.</param>
    /// <param name="clipboardService">Service for clipboard operations.</param>
    /// <param name="navigationService">Service for window navigation.</param>
    public MainWindowViewModel(
        IRecentProjectsService recentProjectsService,
        IFileDialogService fileDialogService,
        IClipboardService clipboardService,
        INavigationService navigationService)
    {
        _recentProjectsService = recentProjectsService ?? throw new ArgumentNullException(nameof(recentProjectsService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

        Title = "Magic Crop & Measure";
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the ViewModel asynchronously.
    /// </summary>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
    }

    /// <summary>
    /// Cleans up resources when the ViewModel is no longer needed.
    /// </summary>
    public override void Cleanup()
    {
        base.Cleanup();
    }

    #endregion
}
