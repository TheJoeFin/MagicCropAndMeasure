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
}
