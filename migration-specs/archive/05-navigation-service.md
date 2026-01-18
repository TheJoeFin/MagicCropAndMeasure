# Step 05: Navigation Service

## Objective
Create a navigation service to handle window/dialog navigation in a testable, MVVM-friendly way.

## Prerequisites
- Step 04 completed (Messaging setup)

## Why a Navigation Service?

Current problems:
```csharp
// In MainWindow.xaml.cs - direct window creation
AboutWindow aboutWindow = new();
aboutWindow.ShowDialog();

SaveWindow saveWindow = new(imagePath);
saveWindow.ShowDialog();
```

Issues:
1. Not testable (can't mock window creation)
2. Tight coupling to concrete types
3. No central place to manage window lifecycle

## Changes Required

### 1. Create INavigationService Interface

**File: `Services/Interfaces/INavigationService.cs`**

```csharp
namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Service for navigating between windows and showing dialogs.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Shows a window as a dialog (modal).
    /// </summary>
    /// <typeparam name="TWindow">The type of window to show.</typeparam>
    /// <returns>True if dialog result was true, false otherwise.</returns>
    bool? ShowDialog<TWindow>() where TWindow : System.Windows.Window;

    /// <summary>
    /// Shows a window as a dialog with a parameter.
    /// </summary>
    /// <typeparam name="TWindow">The type of window to show.</typeparam>
    /// <param name="parameter">Parameter to pass to the window.</param>
    /// <returns>True if dialog result was true, false otherwise.</returns>
    bool? ShowDialog<TWindow>(object parameter) where TWindow : System.Windows.Window;

    /// <summary>
    /// Shows a window (non-modal).
    /// </summary>
    /// <typeparam name="TWindow">The type of window to show.</typeparam>
    void ShowWindow<TWindow>() where TWindow : System.Windows.Window;

    /// <summary>
    /// Shows a window with a parameter (non-modal).
    /// </summary>
    /// <typeparam name="TWindow">The type of window to show.</typeparam>
    /// <param name="parameter">Parameter to pass to the window.</param>
    void ShowWindow<TWindow>(object parameter) where TWindow : System.Windows.Window;

    /// <summary>
    /// Shows a message box.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The title of the message box.</param>
    /// <param name="buttons">The buttons to show.</param>
    /// <param name="icon">The icon to display.</param>
    /// <returns>The result of the message box.</returns>
    System.Windows.MessageBoxResult ShowMessage(
        string message, 
        string title = "", 
        System.Windows.MessageBoxButton buttons = System.Windows.MessageBoxButton.OK,
        System.Windows.MessageBoxImage icon = System.Windows.MessageBoxImage.Information);

    /// <summary>
    /// Shows an error message box.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="title">The title (defaults to "Error").</param>
    void ShowError(string message, string title = "Error");

    /// <summary>
    /// Shows a confirmation dialog.
    /// </summary>
    /// <param name="message">The confirmation message.</param>
    /// <param name="title">The title.</param>
    /// <returns>True if user confirmed, false otherwise.</returns>
    bool ShowConfirmation(string message, string title = "Confirm");

    /// <summary>
    /// Gets the current active window.
    /// </summary>
    System.Windows.Window? GetActiveWindow();
}
```

### 2. Create Navigation Service Implementation

**File: `Services/NavigationService.cs`**

```csharp
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
```

### 3. Create Window Factory for Complex Cases

**File: `Services/IWindowFactory.cs`**

```csharp
namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Factory for creating windows with complex initialization.
/// </summary>
public interface IWindowFactory
{
    /// <summary>
    /// Creates a SaveWindow with the specified image path.
    /// </summary>
    SaveWindow CreateSaveWindow(string imagePath);
}
```

**File: `Services/WindowFactory.cs`**

```csharp
using MagickCrop.Services.Interfaces;

namespace MagickCrop.Services;

/// <summary>
/// Factory for creating windows that require special initialization.
/// </summary>
public class WindowFactory : IWindowFactory
{
    public SaveWindow CreateSaveWindow(string imagePath)
    {
        return new SaveWindow(imagePath);
    }
}
```

### 4. Update DI Registration

**File: `App.xaml.cs`**

```csharp
private static void ConfigureServices(IServiceCollection services)
{
    // ... existing registrations ...

    // Register Navigation Service
    services.AddSingleton<INavigationService, NavigationService>();
    services.AddSingleton<IWindowFactory, WindowFactory>();
    
    // ... rest of registrations ...
}
```

### 5. Example Usage

**Before (in code-behind):**
```csharp
private void ShowAboutClick(object sender, RoutedEventArgs e)
{
    AboutWindow aboutWindow = new();
    aboutWindow.ShowDialog();
}

private void ShowSaveWindow(string imagePath)
{
    SaveWindow saveWindow = new(imagePath);
    saveWindow.ShowDialog();
}
```

**After (in ViewModel):**
```csharp
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IWindowFactory _windowFactory;

    public MainWindowViewModel(
        INavigationService navigationService,
        IWindowFactory windowFactory)
    {
        _navigationService = navigationService;
        _windowFactory = windowFactory;
    }

    [RelayCommand]
    private void ShowAbout()
    {
        _navigationService.ShowDialog<Windows.AboutWindow>();
    }

    [RelayCommand]
    private void ShowSaveWindow()
    {
        if (string.IsNullOrEmpty(CurrentImagePath))
            return;
            
        var saveWindow = _windowFactory.CreateSaveWindow(CurrentImagePath);
        saveWindow.Owner = _navigationService.GetActiveWindow();
        saveWindow.ShowDialog();
    }

    [RelayCommand]
    private void DeleteItem()
    {
        if (_navigationService.ShowConfirmation("Are you sure you want to delete this item?"))
        {
            // Perform deletion
        }
    }
}
```

---

## Implementation Steps

1. Create `INavigationService` interface
2. Create `NavigationService` implementation
3. Create `IWindowFactory` interface and implementation
4. Update DI registration
5. Build and verify

---

## Validation Checklist

- [ ] INavigationService interface created
- [ ] NavigationService implementation works
- [ ] WindowFactory creates SaveWindow correctly
- [ ] DI registration complete
- [ ] Application builds and runs

---

## Files Changed/Created

| File | Change Type |
|------|-------------|
| `Services/Interfaces/INavigationService.cs` | Created |
| `Services/NavigationService.cs` | Created |
| `Services/Interfaces/IWindowFactory.cs` | Created |
| `Services/WindowFactory.cs` | Created |
| `App.xaml.cs` | Modified |

---

## Notes

### Why Both NavigationService and WindowFactory?

- **NavigationService**: For standard window navigation (parameterless or simple parameters)
- **WindowFactory**: For windows with complex initialization requirements

### Testing Navigation

In unit tests, mock `INavigationService`:
```csharp
var mockNav = new Mock<INavigationService>();
mockNav.Setup(n => n.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()))
       .Returns(true);

var viewModel = new MainWindowViewModel(mockNav.Object, ...);
viewModel.DeleteItemCommand.Execute(null);

mockNav.Verify(n => n.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
```

### Owner Window Pattern

Always set `Owner` on dialogs:
```csharp
window.Owner = GetActiveWindow();
```

This ensures:
- Dialog appears centered on parent
- Dialog stays on top of parent
- Alt+Tab behavior is correct

---

## Next Steps

Proceed to **Step 06: ObservableObject for Models** to add property change notification to data models.
