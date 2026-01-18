using System.IO;
using MagickCrop.Services.Interfaces;

namespace MagickCrop.Services;

/// <summary>
/// Provides centralized access to application-related file paths.
/// </summary>
public class AppPaths : IAppPaths
{
    private const string AppFolderName = "MagickCrop";
    private const string ProjectsFolderName = "Projects";
    private const string ThumbnailsFolderName = "Thumbnails";
    private const string ProjectIndexFileName = "project_index.json";
    private const string ThumbnailSuffix = "_thumb.jpg";
    private const string PackageExtension = ".mcm";

    private readonly string _appDataRoot;

    public string AppDataRoot => _appDataRoot;

    public string ProjectsFolder => Path.Combine(_appDataRoot, ProjectsFolderName);

    public string ThumbnailsFolder => Path.Combine(_appDataRoot, ThumbnailsFolderName);

    public string ProjectIndexFile => Path.Combine(_appDataRoot, ProjectIndexFileName);

    public AppPaths()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _appDataRoot = Path.Combine(appDataPath, AppFolderName);
    }

    public string GetPackageFilePath(string projectId)
    {
        return Path.Combine(ProjectsFolder, $"{projectId}{PackageExtension}");
    }

    public string GetThumbnailFilePath(string projectId)
    {
        return Path.Combine(ThumbnailsFolder, $"{projectId}{ThumbnailSuffix}");
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(ProjectsFolder);
        Directory.CreateDirectory(ThumbnailsFolder);
    }
}
