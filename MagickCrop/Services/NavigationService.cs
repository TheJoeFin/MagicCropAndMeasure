using System.Windows;
using MagickCrop.Services.Interfaces;

namespace MagickCrop.Services;

/// <summary>
/// Implementation of INavigationService using WPF windows.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool? ShowDialog<TWindow>() where TWindow : Window
    {
        var window = CreateWindow<TWindow>();
        window.Owner = GetActiveWindow();
        return window.ShowDialog();
    }

    public bool? ShowDialog<TWindow>(object parameter) where TWindow : Window
    {
        var window = CreateWindow<TWindow>(parameter);
        window.Owner = GetActiveWindow();
        return window.ShowDialog();
    }

    public void ShowWindow<TWindow>() where TWindow : Window
    {
        var window = CreateWindow<TWindow>();
        window.Show();
    }

    public void ShowWindow<TWindow>(object parameter) where TWindow : Window
    {
        var window = CreateWindow<TWindow>(parameter);
        window.Show();
    }

    public MessageBoxResult ShowMessage(
        string message,
        string title = "",
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.Information)
    {
        var owner = GetActiveWindow();
        return owner != null
            ? MessageBox.Show(owner, message, title, buttons, icon)
            : MessageBox.Show(message, title, buttons, icon);
    }

    public void ShowError(string message, string title = "Error")
    {
        ShowMessage(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool ShowConfirmation(string message, string title = "Confirm")
    {
        var result = ShowMessage(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public Window? GetActiveWindow()
    {
        return Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive) 
            ?? Application.Current?.MainWindow;
    }

    private TWindow CreateWindow<TWindow>() where TWindow : Window
    {
        // Try to resolve from DI container first
        var window = _serviceProvider.GetService(typeof(TWindow)) as TWindow;
        
        if (window == null)
        {
            // Fallback to Activator if not registered
            window = Activator.CreateInstance<TWindow>();
        }

        return window;
    }

    private TWindow CreateWindow<TWindow>(object parameter) where TWindow : Window
    {
        // For windows that need parameters, we need a different approach
        // Try to find a constructor that accepts the parameter type
        var windowType = typeof(TWindow);
        var parameterType = parameter.GetType();

        // Look for constructor with matching parameter
        var constructor = windowType.GetConstructor([parameterType]);
        
        if (constructor != null)
        {
            return (TWindow)constructor.Invoke([parameter]);
        }

        // Try to find constructor with object parameter
        constructor = windowType.GetConstructor([typeof(object)]);
        if (constructor != null)
        {
            return (TWindow)constructor.Invoke([parameter]);
        }

        // Fallback: Create window and try to set parameter via property/method
        var window = CreateWindow<TWindow>();
        
        // Try to set a "Parameter" property if it exists
        var parameterProperty = windowType.GetProperty("Parameter");
        if (parameterProperty != null && parameterProperty.CanWrite)
        {
            parameterProperty.SetValue(window, parameter);
        }
        
        // Try to call Initialize method if it exists
        var initMethod = windowType.GetMethod("Initialize", [parameterType]);
        initMethod?.Invoke(window, [parameter]);

        return window;
    }
}
