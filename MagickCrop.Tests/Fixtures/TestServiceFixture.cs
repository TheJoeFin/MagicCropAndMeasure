using MagickCrop.Services;
using MagickCrop.Services.Interfaces;
using MagickCrop.Tests.Mocks;
using MagickCrop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MagickCrop.Tests.Fixtures;

/// <summary>
/// Provides test setup and teardown for tests requiring service instances
/// </summary>
public class TestServiceFixture : IDisposable
{
    private readonly ServiceCollection _services = new();
    private ServiceProvider? _provider;

    public IServiceProvider Services
    {
        get
        {
            if (_provider == null)
            {
                BuildProvider();
            }
            return _provider;
        }
    }

    public T GetService<T>() where T : class
    {
        return Services.GetRequiredService<T>();
    }

    public void BuildProvider()
    {
        // Register mocks
        _services.AddSingleton<IRecentProjectsService, MockRecentProjectsService>();
        _services.AddSingleton<IFileDialogService>(new Mock<IFileDialogService>().Object);
        _services.AddSingleton<IClipboardService>(new Mock<IClipboardService>().Object);
        _services.AddSingleton<IImageProcessingService>(new Mock<IImageProcessingService>().Object);
        _services.AddSingleton<INavigationService>(new Mock<INavigationService>().Object);
        _services.AddSingleton<IWindowFactory>(new Mock<IWindowFactory>().Object);

        _provider = _services.BuildServiceProvider();
    }

    public void Reset()
    {
        _provider?.Dispose();
        _provider = null;
        _services.Clear();
    }

    public void Dispose()
    {
        Reset();
        GC.SuppressFinalize(this);
    }
}
