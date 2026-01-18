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
            return _provider!;
        }
    }

    public T GetService<T>() where T : class
    {
        return Services.GetRequiredService<T>();
    }

    public void BuildProvider()
    {
        // Create a new messenger instance for each test (not the global singleton)
        var messenger = new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger();
        _services.AddSingleton<CommunityToolkit.Mvvm.Messaging.IMessenger>(messenger);

        // Register concrete mocks
        _services.AddSingleton<IRecentProjectsService, MockRecentProjectsService>();
        _services.AddSingleton<IFileDialogService, MockFileDialogService>();
        _services.AddSingleton<IClipboardService, MockClipboardService>();
        _services.AddSingleton<IImageProcessingService, MockImageProcessingService>();
        _services.AddSingleton<INavigationService, MockNavigationService>();
        _services.AddSingleton<IWindowFactory, MockWindowFactory>();
        _services.AddSingleton<IThemeService, MockThemeService>();

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
