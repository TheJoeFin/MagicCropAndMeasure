using MagickCrop.Models;
using MagickCrop.Tests.Fixtures;
using MagickCrop.Tests.Mocks;

namespace MagickCrop.Tests;

/// <summary>
/// Basic tests to verify test infrastructure is working
/// </summary>
[TestClass]
public class TestInfrastructureTests
{
    private TestServiceFixture? _fixture;

    [TestInitialize]
    public void Setup()
    {
        _fixture = new TestServiceFixture();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _fixture?.Dispose();
    }

    [TestMethod]
    public void TestFixture_CanBuildServices()
    {
        // Arrange & Act
        var recentProjects = _fixture!.GetService<IRecentProjectsService>();

        // Assert
        Assert.IsNotNull(recentProjects);
        Assert.IsInstanceOfType(recentProjects, typeof(MockRecentProjectsService));
    }

    [TestMethod]
    public async Task MockRecentProjectsService_CanAddProject()
    {
        // Arrange
        var service = new MockRecentProjectsService();
        var projectId = Guid.NewGuid().ToString();
        var projectInfo = new RecentProjectInfo
        {
            Id = projectId,
            Name = "test",
            PackagePath = Path.Combine(Path.GetTempPath(), "test.mcm"),
            LastModified = DateTime.Now
        };

        // Act
        await service.AddRecentProjectAsync(projectInfo);

        // Assert
        Assert.AreEqual(1, service.RecentProjects.Count);
        Assert.AreEqual(projectInfo.PackagePath, service.RecentProjects[0].PackagePath);
    }

    [TestMethod]
    public async Task MockRecentProjectsService_CanRemoveProject()
    {
        // Arrange
        var service = new MockRecentProjectsService();
        var projectId = Guid.NewGuid();
        var projectInfo = new RecentProjectInfo
        {
            Id = projectId.ToString(),
            Name = "test",
            PackagePath = Path.Combine(Path.GetTempPath(), "test.mcm"),
            LastModified = DateTime.Now
        };
        await service.AddRecentProjectAsync(projectInfo);

        // Act
        await service.RemoveRecentProjectAsync(projectId);

        // Assert
        Assert.AreEqual(0, service.RecentProjects.Count);
    }

    [TestMethod]
    public async Task MockRecentProjectsService_CanClearProjects()
    {
        // Arrange
        var service = new MockRecentProjectsService();
        var project1 = new RecentProjectInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "test1",
            PackagePath = Path.Combine(Path.GetTempPath(), "test1.mcm"),
            LastModified = DateTime.Now
        };
        var project2 = new RecentProjectInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = "test2",
            PackagePath = Path.Combine(Path.GetTempPath(), "test2.mcm"),
            LastModified = DateTime.Now
        };
        await service.AddRecentProjectAsync(project1);
        await service.AddRecentProjectAsync(project2);
        Assert.AreEqual(2, service.RecentProjects.Count);

        // Act
        await service.ClearRecentProjectsAsync();

        // Assert
        Assert.AreEqual(0, service.RecentProjects.Count);
    }

    [TestMethod]
    public void MockRecentProjectsService_GetAutosavePath()
    {
        // Arrange
        var service = new MockRecentProjectsService();

        // Act
        var path = service.GetAutosavePath();

        // Assert
        Assert.IsNotNull(path);
        Assert.IsTrue(path.Contains("MagickCrop"));
        Assert.IsTrue(path.Contains("autosave"));
    }

    [TestMethod]
    public void MockFileDialogService_TracksOpenDialogCalls()
    {
        // Arrange
        var service = new MockFileDialogService();
        service.SelectedOpenFile = "/path/to/file.png";

        // Act
        var result = service.ShowOpenFileDialog("Images|*.png", "Open Image");

        // Assert
        Assert.AreEqual("/path/to/file.png", result);
        Assert.AreEqual("Images|*.png", service.LastOpenFilter);
        Assert.AreEqual("Open Image", service.LastOpenTitle);
    }

    [TestMethod]
    public void MockFileDialogService_TracksSaveDialogCalls()
    {
        // Arrange
        var service = new MockFileDialogService();
        service.SelectedSaveFile = "/path/to/saved.png";

        // Act
        var result = service.ShowSaveFileDialog("PNG Files|*.png", "image.png", "Save Image");

        // Assert
        Assert.AreEqual("/path/to/saved.png", result);
        Assert.AreEqual("PNG Files|*.png", service.LastSaveFilter);
        Assert.AreEqual("image.png", service.LastSaveFileName);
        Assert.AreEqual("Save Image", service.LastSaveTitle);
    }

    [TestMethod]
    public void MockClipboardService_CanStoreAndRetrieveFileList()
    {
        // Arrange
        var service = new MockClipboardService();
        var files = new[] { "/path/to/file1.png", "/path/to/file2.png" };

        // Act
        service.SetFileDropList(files);

        // Assert
        Assert.IsTrue(service.ContainsFileDropList());
        var retrieved = service.GetFileDropList();
        Assert.AreEqual(2, retrieved.Count);
        Assert.AreEqual("/path/to/file1.png", retrieved[0]);
    }

    [TestMethod]
    public void MockClipboardService_CanClearData()
    {
        // Arrange
        var service = new MockClipboardService();
        service.SetFileDropList("/path/to/file.png");
        service.SetText("test");

        // Act
        service.Clear();

        // Assert
        Assert.IsFalse(service.ContainsFileDropList());
        Assert.IsFalse(service.ContainsImage());
    }

    [TestMethod]
    public async Task MockImageProcessingService_TracksLoadedFiles()
    {
        // Arrange
        var service = new MockImageProcessingService();

        // Act
        await service.LoadImageAsync("/path/to/image.png");

        // Assert
        Assert.AreEqual(1, service.LoadedFiles.Count);
        Assert.AreEqual("/path/to/image.png", service.LoadedFiles[0]);
    }

    [TestMethod]
    public async Task MockImageProcessingService_TracksSavedFiles()
    {
        // Arrange
        var service = new MockImageProcessingService();
        using (var image = new ImageMagick.MagickImage(ImageMagick.MagickColors.White, 100, 100))
        {
            // Act
            await service.SaveImageAsync(image, "/path/to/saved.png", ImageMagick.MagickFormat.Png, 90);

            // Assert
            Assert.AreEqual(1, service.SavedFiles.Count);
            var (path, format, quality) = service.SavedFiles[0];
            Assert.AreEqual("/path/to/saved.png", path);
            Assert.AreEqual(ImageMagick.MagickFormat.Png, format);
            Assert.AreEqual(90, quality);
        }
    }

    [TestMethod]
    public void MockNavigationService_TracksDialogCalls()
    {
        // Arrange
        var service = new MockNavigationService();

        // Act
        var result = service.ShowDialog<System.Windows.Window>();

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(1, service.ShowedDialogs.Count);
        Assert.IsTrue(service.ShowedDialogs[0].Contains("Window"));
    }

    [TestMethod]
    public void MockNavigationService_TracksMessageCalls()
    {
        // Arrange
        var service = new MockNavigationService();

        // Act
        var result = service.ShowMessage("Test message", "Test Title");

        // Assert
        Assert.AreEqual(System.Windows.MessageBoxResult.OK, result);
        Assert.AreEqual(1, service.ShowedMessages.Count);
        Assert.AreEqual("Test message", service.ShowedMessages[0].Message);
    }

    [TestMethod]
    public void MockNavigationService_TracksConfirmationCalls()
    {
        // Arrange
        var service = new MockNavigationService();
        service.ConfirmationResult = true;

        // Act
        var result = service.ShowConfirmation("Are you sure?", "Confirm");

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(1, service.ShowedConfirmations.Count);
        Assert.AreEqual("Are you sure?", service.ShowedConfirmations[0].Message);
    }

    [TestMethod]
    public void MockThemeService_CanChangeTheme()
    {
        // Arrange
        var service = new MockThemeService();
        Assert.IsFalse(service.IsDarkTheme);

        // Act
        service.SetDarkTheme();

        // Assert
        Assert.IsTrue(service.IsDarkTheme);

        // Act
        service.SetLightTheme();

        // Assert
        Assert.IsFalse(service.IsDarkTheme);
    }
}

