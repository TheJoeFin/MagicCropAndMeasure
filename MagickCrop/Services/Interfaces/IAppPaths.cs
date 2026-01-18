namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Provides centralized access to application-related file paths.
/// Eliminates duplicated path-building logic across the application.
/// </summary>
public interface IAppPaths
{
    /// <summary>
    /// Gets the application data root directory (typically %LocalApplicationData%/MagickCrop).
    /// </summary>
    string AppDataRoot { get; }

    /// <summary>
    /// Gets the projects folder path where .mcm files are stored.
    /// </summary>
    string ProjectsFolder { get; }

    /// <summary>
    /// Gets the thumbnails folder path for project thumbnails.
    /// </summary>
    string ThumbnailsFolder { get; }

    /// <summary>
    /// Gets the project index file path.
    /// </summary>
    string ProjectIndexFile { get; }

    /// <summary>
    /// Gets the full path for a package file with the given project ID.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>Full path to the .mcm package file.</returns>
    string GetPackageFilePath(string projectId);

    /// <summary>
    /// Gets the full path for a thumbnail file with the given project ID.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>Full path to the thumbnail file.</returns>
    string GetThumbnailFilePath(string projectId);

    /// <summary>
    /// Ensures all necessary directories exist.
    /// </summary>
    void EnsureDirectoriesExist();
}
