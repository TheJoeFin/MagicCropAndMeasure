using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using MagickCrop.Services;
using MagickCrop.Services.Interfaces;
using MagickCrop.ViewModels;
using MagickCrop.ViewModels.Measurements;
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

        // Initialize theme service (load saved preference)
        var themeService = GetService<IThemeService>();
        
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
        
        // Set MVVM Toolkit service provider for Ioc.Default usage
        Ioc.Default.ConfigureServices(
            new ServiceCollection()
                .AddSingleton<IMessenger>(WeakReferenceMessenger.Default)
                .BuildServiceProvider());
        
        // Register Infrastructure Services
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IThumbnailService, ThumbnailService>();
        services.AddSingleton<IThemeService, ThemeService>();
        
        // Register Service Interfaces
        services.AddSingleton<IRecentProjectsService, RecentProjectsManager>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IImageProcessingService, ImageProcessingService>();
        services.AddSingleton<INavigationService, NavigationService>();
        
        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<WelcomeViewModel>();
        services.AddTransient<AboutWindowViewModel>();
        services.AddTransient<SaveWindowViewModel>();
        
        // Register Measurement ViewModels
        services.AddTransient<DistanceMeasurementViewModel>();
        services.AddTransient<AngleMeasurementViewModel>();
        services.AddTransient<CircleMeasurementViewModel>();
        services.AddTransient<RectangleMeasurementViewModel>();
        services.AddTransient<PolygonMeasurementViewModel>();
        services.AddTransient<HorizontalLineViewModel>();
        services.AddTransient<VerticalLineViewModel>();

        // Register Windows/Views
        services.AddTransient<MainWindow>(sp =>
            new MainWindow(
                sp.GetRequiredService<MainWindowViewModel>(),
                sp.GetRequiredService<IRecentProjectsService>()));
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


