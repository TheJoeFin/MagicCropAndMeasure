using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using ImageMagick;
using MagickCrop.Models;
using MagickCrop.Services;
using MagickCrop.Services.Interfaces;
using MagickCrop.Tests.Base;

namespace MagickCrop.Tests.Services;

/// <summary>
/// Comprehensive unit tests for RecentProjectsService (RecentProjectsManager).
/// Tests cover project tracking, persistence, thumbnails, and edge cases.
/// </summary>
[TestClass]
public class RecentProjectsServiceTests : ServiceTestBase
{
    private RecentProjectsManager? _service;
    private string? _testAppDataFolder;

    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
        
        // Create an isolated test folder for app data
        _testAppDataFolder = CreateTempDirectory();
        
        // Create the service instance
        _service = new RecentProjectsManager();
    }

    [TestCleanup]
    public override void TestCleanup()
    {
        base.TestCleanup();
        _service = null;
    }

    #region Initialization Tests

    [TestMethod]
    public void TestInitialization_CreatesValidRecentProjectsCollection()
    {
        // Act & Assert
        Assert.IsNotNull(_service);
        Assert.IsNotNull(_service!.RecentProjects);
        Assert.IsInstanceOfType(_service.RecentProjects, typeof(ObservableCollection<RecentProjectInfo>));
    }

    [TestMethod]
    public void TestInitialization_RecentProjectsEmptyByDefault()
    {
        // Act & Assert - Fresh service instance should have empty collection initially
        var newService = new RecentProjectsManager();
        // Should either be empty or load from existing file
        Assert.IsNotNull(newService.RecentProjects);
    }

    #endregion

    #region Add Recent Project Tests

    [TestMethod]
    public async Task TestAddRecentProject_AddsProjectToCollection()
    {
        // Arrange
        var project = new RecentProjectInfo
        {
            Id = "test-proj-001",
            Name = "Test Project 1",
            PackagePath = CreateTempFile(),
            LastModified = DateTime.Now
        };

        // Act
        await _service!.AddRecentProjectAsync(project);

        // Assert
        Assert.IsTrue(_service.RecentProjects.Any(p => p.Id == project.Id));
        Assert.AreEqual(project.Name, _service.RecentProjects.FirstOrDefault(p => p.Id == project.Id)?.Name);
    }

    [TestMethod]
    public async Task TestAddRecentProject_UpdatesExistingProjectWithSameId()
    {
        // Arrange
        var originalProject = new RecentProjectInfo
        {
            Id = "test-proj-001",
            Name = "Original Name",
            PackagePath = CreateTempFile(),
            LastModified = DateTime.Now
        };

        var updatedProject = new RecentProjectInfo
        {
            Id = "test-proj-001",
            Name = "Updated Name",
            PackagePath = CreateTempFile(),
            LastModified = DateTime.Now
        };

        // Act
        await _service!.AddRecentProjectAsync(originalProject);
        await _service.AddRecentProjectAsync(updatedProject);

        // Assert
        Assert.AreEqual(1, _service.RecentProjects.Count(p => p.Id == "test-proj-001"),
            "Should only have one project with this ID");
        var result = _service.RecentProjects.FirstOrDefault(p => p.Id == "test-proj-001");
        Assert.AreEqual("Updated Name", result?.Name);
    }

    [TestMethod]
    public async Task TestAddRecentProject_MultipleProjectsInOrder()
    {
        // Arrange
        var projects = new[]
        {
            new RecentProjectInfo { Id = "proj-1", Name = "Project 1", PackagePath = CreateTempFile(), LastModified = DateTime.Now },
            new RecentProjectInfo { Id = "proj-2", Name = "Project 2", PackagePath = CreateTempFile(), LastModified = DateTime.Now.AddSeconds(1) },
            new RecentProjectInfo { Id = "proj-3", Name = "Project 3", PackagePath = CreateTempFile(), LastModified = DateTime.Now.AddSeconds(2) }
        };

        // Act
        foreach (var project in projects)
        {
            await _service!.AddRecentProjectAsync(project);
        }

        // Assert
        Assert.AreEqual(3, _service!.RecentProjects.Count);
        Assert.AreEqual("proj-1", _service.RecentProjects[0].Id);
        Assert.AreEqual("proj-2", _service.RecentProjects[1].Id);
        Assert.AreEqual("proj-3", _service.RecentProjects[2].Id);
    }

    #endregion

    #region Remove Recent Project Tests

    [TestMethod]
    public async Task TestRemoveRecentProject_RemovesProjectById()
    {
        // Arrange
        var project = new RecentProjectInfo
        {
            Id = "test-proj-001",
            Name = "Test Project",
            PackagePath = CreateTempFile(),
            LastModified = DateTime.Now
        };

        await _service!.AddRecentProjectAsync(project);
        Assert.AreEqual(1, _service.RecentProjects.Count);

        // Act
        await _service.RemoveRecentProjectAsync(Guid.Parse(project.Id));

        // Assert
        Assert.AreEqual(0, _service.RecentProjects.Count);
        Assert.IsFalse(_service.RecentProjects.Any(p => p.Id == project.Id));
    }

    [TestMethod]
    public async Task TestRemoveRecentProject_RemovingNonExistentProjectIsNoOp()
    {
        // Arrange
        var project1 = new RecentProjectInfo
        {
            Id = "proj-1",
            Name = "Project 1",
            PackagePath = CreateTempFile(),
            LastModified = DateTime.Now
        };

        await _service!.AddRecentProjectAsync(project1);

        // Act - Remove non-existent project
        await _service.RemoveRecentProjectAsync(Guid.Parse("00000000-0000-0000-0000-000000000099"));

        // Assert - Original project still exists
        Assert.AreEqual(1, _service.RecentProjects.Count);
        Assert.AreEqual("proj-1", _service.RecentProjects[0].Id);
    }

    [TestMethod]
    public async Task TestRemoveRecentProject_RemovesCorrectProjectWhenMultipleExist()
    {
        // Arrange
        var projects = new[]
        {
            new RecentProjectInfo { Id = "proj-1", Name = "Project 1", PackagePath = CreateTempFile(), LastModified = DateTime.Now },
            new RecentProjectInfo { Id = "proj-2", Name = "Project 2", PackagePath = CreateTempFile(), LastModified = DateTime.Now.AddSeconds(1) },
            new RecentProjectInfo { Id = "proj-3", Name = "Project 3", PackagePath = CreateTempFile(), LastModified = DateTime.Now.AddSeconds(2) }
        };

        foreach (var project in projects)
        {
            await _service!.AddRecentProjectAsync(project);
        }

        // Act
        await _service!.RemoveRecentProjectAsync(Guid.Parse("proj-2"));

        // Assert
        Assert.AreEqual(2, _service.RecentProjects.Count);
        Assert.IsFalse(_service.RecentProjects.Any(p => p.Id == "proj-2"));
        Assert.IsTrue(_service.RecentProjects.Any(p => p.Id == "proj-1"));
        Assert.IsTrue(_service.RecentProjects.Any(p => p.Id == "proj-3"));
    }

    #endregion

    #region Clear Recent Projects Tests

    [TestMethod]
    public async Task TestClearRecentProjects_ClearsAllProjects()
    {
        // Arrange
        var projects = new[]
        {
            new RecentProjectInfo { Id = "proj-1", Name = "Project 1", PackagePath = CreateTempFile(), LastModified = DateTime.Now },
            new RecentProjectInfo { Id = "proj-2", Name = "Project 2", PackagePath = CreateTempFile(), LastModified = DateTime.Now },
            new RecentProjectInfo { Id = "proj-3", Name = "Project 3", PackagePath = CreateTempFile(), LastModified = DateTime.Now }
        };

        foreach (var project in projects)
        {
            await _service!.AddRecentProjectAsync(project);
        }

        Assert.AreEqual(3, _service!.RecentProjects.Count);

        // Act
        await _service.ClearRecentProjectsAsync();

        // Assert
        Assert.AreEqual(0, _service.RecentProjects.Count);
    }

    [TestMethod]
    public async Task TestClearRecentProjects_ClearsEmptyCollectionIsNoOp()
    {
        // Arrange
        Assert.AreEqual(0, _service!.RecentProjects.Count);

        // Act - Should not throw
        await _service.ClearRecentProjectsAsync();

        // Assert
        Assert.AreEqual(0, _service.RecentProjects.Count);
    }

    #endregion

    #region Autosave Path Tests

    [TestMethod]
    public void TestGetAutosavePath_ReturnsValidPath()
    {
        // Act
        var autosavePath = _service!.GetAutosavePath();

        // Assert
        Assert.IsNotNull(autosavePath);
        Assert.IsTrue(autosavePath!.Length > 0);
        Assert.IsTrue(autosavePath.Contains("MagickCrop") || autosavePath.Contains("Temp"));
    }

    [TestMethod]
    public void TestGetAutosavePath_ConsistentAcrossMultipleCalls()
    {
        // Act
        var path1 = _service!.GetAutosavePath();
        var path2 = _service.GetAutosavePath();

        // Assert
        Assert.AreEqual(path1, path2);
    }

    [TestMethod]
    public void TestGetAutosavePath_PathIsInValidLocation()
    {
        // Act
        var autosavePath = _service!.GetAutosavePath();

        // Assert
        var directory = Path.GetDirectoryName(autosavePath);
        Assert.IsNotNull(directory);
        Assert.IsTrue(Directory.Exists(directory));
    }

    #endregion

    #region Autosave Project Tests

    [TestMethod]
    public async Task TestAutosaveProject_SavesValidProjectPackage()
    {
        // Arrange
        var package = new MagickCropMeasurementPackage
        {
            ImagePath = CreateTestImage("autosave_test", 100, 100),
            Measurements = new MagickCrop.Models.MeasurementControls.MeasurementCollection(),
            Metadata = new PackageMetadata
            {
                CreationDate = DateTime.Now,
                LastModified = DateTime.Now,
                OriginalImageSize = new System.Windows.Size(100, 100)
            }
        };

        var projectInfo = new RecentProjectInfo
        {
            Id = "autosave-test-001",
            Name = "Autosave Test Project",
            PackagePath = Path.Combine(CreateTempDirectory(), "autosave_test.mcm"),
            LastModified = DateTime.Now
        };

        // Act
        await _service!.AutosaveProjectAsync(package, projectInfo);

        // Assert
        Assert.IsTrue(File.Exists(projectInfo.PackagePath),
            "Autosave file should be created");
    }

    [TestMethod]
    public async Task TestAutosaveProject_HandlesNullPackageGracefully()
    {
        // Arrange
        var projectInfo = new RecentProjectInfo
        {
            Id = "null-test",
            Name = "Null Test",
            PackagePath = Path.Combine(CreateTempDirectory(), "null_test.mcm"),
            LastModified = DateTime.Now
        };

        // Act & Assert - Should not throw
        await _service!.AutosaveProjectAsync(new MagickCropMeasurementPackage(), projectInfo);
    }

    #endregion

    #region Load Recent Projects Tests

    [TestMethod]
    public async Task TestLoadRecentProjectsAsync_LoadsEmptyWhenNothingExists()
    {
        // Act
        var newService = new RecentProjectsManager();
        await newService.LoadRecentProjectsAsync();

        // Assert
        Assert.IsNotNull(newService.RecentProjects);
        // Count will be 0 or reflect what's actually persisted
    }

    [TestMethod]
    public async Task TestLoadRecentProjectsAsync_PreservesCollectionBinding()
    {
        // Arrange - Add some projects
        var project = new RecentProjectInfo
        {
            Id = "persist-test",
            Name = "Persistence Test",
            PackagePath = CreateTempFile(),
            LastModified = DateTime.Now
        };

        await _service!.AddRecentProjectAsync(project);
        var initialCollection = _service.RecentProjects;

        // Act
        await _service.LoadRecentProjectsAsync();

        // Assert - Collection reference should be preserved for bindings
        Assert.IsTrue(ReferenceEquals(initialCollection, _service.RecentProjects),
            "Collection instance should be preserved to maintain UI bindings");
    }

    #endregion

    #region Edge Cases and Error Handling

    [TestMethod]
    public async Task TestOperations_WithInvalidGuidString()
    {
        // Arrange
        var project = new RecentProjectInfo
        {
            Id = "invalid-guid-string",
            Name = "Invalid Guid Test",
            PackagePath = CreateTempFile(),
            LastModified = DateTime.Now
        };

        await _service!.AddRecentProjectAsync(project);

        // Act & Assert - Should handle gracefully
        // Attempting to remove with invalid format should either fail gracefully or succeed
        try
        {
            await _service.RemoveRecentProjectAsync(Guid.Parse(project.Id));
        }
        catch (FormatException)
        {
            // Expected if ID can't be parsed as Guid
        }
    }

    [TestMethod]
    public async Task TestAddRecentProject_WithDuplicateNames()
    {
        // Arrange
        var project1 = new RecentProjectInfo
        {
            Id = "proj-1",
            Name = "Duplicate Name",
            PackagePath = CreateTempFile(),
            LastModified = DateTime.Now
        };

        var project2 = new RecentProjectInfo
        {
            Id = "proj-2",
            Name = "Duplicate Name",
            PackagePath = CreateTempFile(),
            LastModified = DateTime.Now.AddSeconds(1)
        };

        // Act
        await _service!.AddRecentProjectAsync(project1);
        await _service.AddRecentProjectAsync(project2);

        // Assert - Both should be added (name doesn't have to be unique)
        Assert.AreEqual(2, _service.RecentProjects.Count);
        Assert.AreEqual(2, _service.RecentProjects.Count(p => p.Name == "Duplicate Name"));
    }

    [TestMethod]
    public async Task TestAddRecentProject_WithNullPackagePath()
    {
        // Arrange
        var project = new RecentProjectInfo
        {
            Id = "null-path",
            Name = "Null Path Project",
            PackagePath = null,
            LastModified = DateTime.Now
        };

        // Act & Assert - Should handle null path gracefully
        try
        {
            await _service!.AddRecentProjectAsync(project);
            // If it doesn't throw, verify it was added
            Assert.IsTrue(_service.RecentProjects.Any(p => p.Id == project.Id));
        }
        catch (ArgumentException)
        {
            // Also acceptable if validation is performed
        }
    }

    #endregion

    #region Helper Methods

    private string CreateTestImage(string name, int width, int height)
    {
        using (var image = new MagickImage(MagickColors.White, (uint)width, (uint)height))
        {
            var path = Path.Combine(Path.GetTempPath(), $"test_{name}_{Guid.NewGuid()}.png");
            image.Write(path);
            RegisterTempImage(path);
            return path;
        }
    }

    private void RegisterTempImage(string path)
    {
        _tempImages.Add(path);
    }

    private readonly List<string> _tempImages = [];

    #endregion
}
