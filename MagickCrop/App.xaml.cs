using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using MagickCrop.Services;
using MagickCrop.Services.Interfaces;
using MagickCrop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MagickCrop;

public partial class App : Application
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Gets the current service provider instance.
    /// </summary>
    public static IServiceProvider ServiceProvider => _serviceProvider 
        ?? throw new InvalidOperationException("Service provider not initialized");

    /// <summary>
    /// Gets a service of type T from the DI container.
    /// </summary>
    public static T GetService<T>() where T : class
        => ServiceProvider.GetRequiredService<T>();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Handle .mcm file association
        if (e.Args.Length > 0 && File.Exists(e.Args[0])
            && Path.GetExtension(e.Args[0]).Equals(".mcm", StringComparison.OrdinalIgnoreCase))
        {
            var mainWindow = GetService<MainWindow>();
            mainWindow.LoadMeasurementsPackageFromFile(e.Args[0]);
            mainWindow.Show();
            return;
        }

        // Normal startup
        var normalMainWindow = GetService<MainWindow>();
        normalMainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register Messenger as singleton (uses weak references, thread-safe)
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        
        // Register Service Interfaces
        services.AddSingleton<IRecentProjectsService, RecentProjectsManager>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<INavigationService, NavigationService>();
        // services.AddSingleton<IImageProcessingService, ImageProcessingService>(); // To be implemented in Step 14
        // services.AddSingleton<IThemeService, ThemeService>(); // To be implemented in future step

        // Keep backward compatibility during migration
        services.AddSingleton<RecentProjectsManager>(sp => 
            (RecentProjectsManager)sp.GetRequiredService<IRecentProjectsService>());
        
        // Register ViewModels
        services.AddTransient<AboutWindowViewModel>();
        services.AddTransient<SaveWindowViewModel>();
        // services.AddTransient<MainWindowViewModel>();

        // Register Windows/Views
        services.AddTransient<MainWindow>();
        services.AddTransient<Windows.AboutWindow>();
        services.AddTransient<SaveWindow>();

        // Register Factory
        services.AddSingleton<IWindowFactory, WindowFactory>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        
        // Dispose of the service provider if it implements IDisposable
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}


