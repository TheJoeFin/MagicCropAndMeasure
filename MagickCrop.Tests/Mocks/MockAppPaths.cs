using MagickCrop.Services.Interfaces;
using System.IO;

namespace MagickCrop.Tests.Mocks;

/// <summary>
/// Mock implementation of IAppPaths for unit testing.
/// Provides test-specific folder paths instead of using actual LocalAppData.
/// </summary>
public class MockAppPaths : IAppPaths
{
    private readonly string _testRootFolder;

    public string AppDataRoot => _testRootFolder;

    public string ProjectsFolder => Path.Combine(_testRootFolder, "Projects");

    public string ThumbnailsFolder => Path.Combine(_testRootFolder, "Thumbnails");

    public string ProjectIndexFile => Path.Combine(_testRootFolder, "project_index.json");

    /// <summary>
    /// Creates a new MockAppPaths instance with the specified test folder as the root.
    /// </summary>
    /// <param name="testRootFolder">The root folder to use for test paths.</param>
    public MockAppPaths(string testRootFolder)
    {
        _testRootFolder = testRootFolder ?? throw new ArgumentNullException(nameof(testRootFolder));
    }

    public string GetPackageFilePath(string projectId)
    {
        return Path.Combine(ProjectsFolder, $"{projectId}.mcm");
    }

    public string GetThumbnailFilePath(string projectId)
    {
        return Path.Combine(ThumbnailsFolder, $"{projectId}_thumb.jpg");
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(ProjectsFolder);
        Directory.CreateDirectory(ThumbnailsFolder);
    }
}
