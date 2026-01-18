using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;

namespace MagickCrop.Tests.Mocks;

/// <summary>
/// Mock implementation of IRecentProjectsService for testing
/// </summary>
public class MockRecentProjectsService : IRecentProjectsService
{
    private readonly ObservableCollection<RecentProjectInfo> _projects = [];

    public ObservableCollection<RecentProjectInfo> RecentProjects => _projects;

    public async Task<RecentProjectInfo?> AutosaveProject(MagickCropMeasurementPackage package, BitmapSource? imageSource)
    {
        return null;
    }

    public Task LoadRecentProjectsAsync()
    {
        return Task.CompletedTask;
    }

    public Task AddRecentProjectAsync(RecentProjectInfo project)
    {
        _projects.Insert(0, project);
        return Task.CompletedTask;
    }

    public Task RemoveRecentProjectAsync(Guid projectId)
    {
        var project = _projects.FirstOrDefault(p => p.Id == projectId.ToString());
        if (project != null)
            _projects.Remove(project);
        return Task.CompletedTask;
    }

    public string GetAutosavePath()
    {
        return Path.Combine(Path.GetTempPath(), "MagickCrop", "autosave");
    }

    public Task AutosaveProjectAsync(MagickCropMeasurementPackage package, RecentProjectInfo projectInfo)
    {
        return Task.CompletedTask;
    }

    public Task ClearRecentProjectsAsync()
    {
        _projects.Clear();
        return Task.CompletedTask;
    }
}
