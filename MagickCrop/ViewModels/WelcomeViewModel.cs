using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MagickCrop.Messages;
using MagickCrop.Models;
using MagickCrop.Services.Interfaces;
using MagickCrop.ViewModels.Base;

namespace MagickCrop.ViewModels;

/// <summary>
/// ViewModel for the Welcome message control.
/// </summary>
public partial class WelcomeViewModel : ViewModelBase
{
    private readonly IRecentProjectsService _recentProjectsService;
    private readonly IClipboardService _clipboardService;
    private readonly IFileDialogService _fileDialogService;

    [ObservableProperty]
    private string _welcomeText = "Welcome to Magic Crop & Measure";

    [ObservableProperty]
    private string _subtitleText = "Open an image to get started";

    [ObservableProperty]
    private bool _hasRecentProjects;

    [ObservableProperty]
    private bool _canPasteFromClipboard;

    [ObservableProperty]
    private bool _isCheckingClipboard;

    /// <summary>
    /// Collection of recent projects to display.
    /// </summary>
    public ObservableCollection<RecentProjectInfo> RecentProjects { get; } = [];

    // Commands that parent can bind to
    public ICommand? OpenFileCommand { get; set; }
    public ICommand? PasteFromClipboardCommand { get; set; }
    public ICommand? OpenOverlayCommand { get; set; }

    public WelcomeViewModel() : this(
        App.GetService<IRecentProjectsService>(),
        App.GetService<IClipboardService>(),
        App.GetService<IFileDialogService>())
    {
    }

    public WelcomeViewModel(
        IRecentProjectsService recentProjectsService,
        IClipboardService clipboardService,
        IFileDialogService fileDialogService)
    {
        _recentProjectsService = recentProjectsService;
        _clipboardService = clipboardService;
        _fileDialogService = fileDialogService;
        
        Title = "Welcome";
    }

    public override async Task InitializeAsync()
    {
        await LoadRecentProjectsAsync();
        await CheckClipboardAsync();
        
        // Register for project changes
        Register<RecentProjectsChangedMessage>(m => _ = LoadRecentProjectsAsync());
    }

    private async Task LoadRecentProjectsAsync()
    {
        try
        {
            await _recentProjectsService.LoadRecentProjectsAsync();
            
            RecentProjects.Clear();
            foreach (var project in _recentProjectsService.RecentProjects.Take(10))
            {
                RecentProjects.Add(project);
            }
            
            HasRecentProjects = RecentProjects.Count > 0;
        }
        catch
        {
            HasRecentProjects = false;
        }
    }

    private async Task CheckClipboardAsync()
    {
        try
        {
            IsCheckingClipboard = true;
            
            // Run on UI thread since Clipboard requires it
            await Task.Run(() =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    CanPasteFromClipboard = _clipboardService.ContainsImage();
                });
            });
        }
        catch
        {
            CanPasteFromClipboard = false;
        }
        finally
        {
            IsCheckingClipboard = false;
        }
    }

    /// <summary>
    /// Refresh clipboard state (call when window gains focus).
    /// </summary>
    [RelayCommand]
    private async Task RefreshClipboard()
    {
        await CheckClipboardAsync();
    }

    /// <summary>
    /// Called when a recent project is clicked.
    /// </summary>
    [RelayCommand]
    private void OpenRecentProject(RecentProjectInfo? project)
    {
        if (project == null)
            return;

        Send(new ProjectOpenedMessage(project.PackagePath, Guid.Parse(project.Id)));
    }

    /// <summary>
    /// Called when a recent project delete is requested.
    /// </summary>
    [RelayCommand]
    private async Task DeleteRecentProject(RecentProjectInfo? project)
    {
        if (project == null)
            return;

        await _recentProjectsService.RemoveRecentProjectAsync(Guid.Parse(project.Id));
        RecentProjects.Remove(project);
        HasRecentProjects = RecentProjects.Count > 0;
    }

    /// <summary>
    /// Opens file browser to select an image.
    /// </summary>
    [RelayCommand]
    private void BrowseForImage()
    {
        // Execute the parent's command if set
        if (OpenFileCommand?.CanExecute(null) == true)
        {
            OpenFileCommand.Execute(null);
            return;
        }

        // Fallback: Use file dialog directly
        var filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.mcm|All Files|*.*";
        var filePath = _fileDialogService.ShowOpenFileDialog(filter, "Open Image");
        
        if (!string.IsNullOrEmpty(filePath))
        {
            Send(new ImageLoadedMessage(filePath, 0, 0));
        }
    }

    /// <summary>
    /// Pastes image from clipboard.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPasteFromClipboard))]
    private void PasteImage()
    {
        if (PasteFromClipboardCommand?.CanExecute(null) == true)
        {
            PasteFromClipboardCommand.Execute(null);
        }
    }

    /// <summary>
    /// Opens the overlay/welcome screen.
    /// </summary>
    [RelayCommand]
    private void ShowOverlay()
    {
        if (OpenOverlayCommand?.CanExecute(null) == true)
        {
            OpenOverlayCommand.Execute(null);
        }
    }

    /// <summary>
    /// Clears all recent projects.
    /// </summary>
    [RelayCommand]
    private async Task ClearAllRecentProjects()
    {
        await _recentProjectsService.ClearRecentProjectsAsync();
        RecentProjects.Clear();
        HasRecentProjects = false;
    }
}
