using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
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
    /// Auto-saves the current project state and returns the project info.
    /// </summary>
    /// <param name="package">The measurement package to save.</param>
    /// <param name="imageSource">The current image source for thumbnail generation.</param>
    /// <returns>The project info or null if save failed.</returns>
    Task<RecentProjectInfo?> AutosaveProject(MagickCropMeasurementPackage package, BitmapSource? imageSource);

    /// <summary>
    /// Auto-saves the current project state asynchronously.
    /// </summary>
    Task AutosaveProjectAsync(MagickCropMeasurementPackage package, RecentProjectInfo projectInfo);

    /// <summary>
    /// Clears all recent projects.
    /// </summary>
    Task ClearRecentProjectsAsync();
}
