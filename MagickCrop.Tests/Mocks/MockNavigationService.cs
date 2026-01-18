using System.Collections.Generic;

namespace MagickCrop.Tests.Mocks;

/// <summary>
/// Mock implementation of INavigationService for testing
/// </summary>
public class MockNavigationService : INavigationService
{
    public List<string> ShowedDialogs { get; } = [];
    public List<string> ShowedWindows { get; } = [];
    public List<(string Message, string Title)> ShowedMessages { get; } = [];
    public List<(string Message, string Title)> ShowedErrors { get; } = [];
    public List<(string Message, string Title)> ShowedConfirmations { get; } = [];

    public System.Windows.Window? ActiveWindow { get; set; }

    public bool? DialogResult { get; set; } = true;
    public System.Windows.MessageBoxResult MessageBoxResult { get; set; } = System.Windows.MessageBoxResult.OK;
    public bool ConfirmationResult { get; set; } = true;

    public bool? ShowDialog<TWindow>() where TWindow : System.Windows.Window
    {
        ShowedDialogs.Add(typeof(TWindow).Name);
        return DialogResult;
    }

    public bool? ShowDialog<TWindow>(object parameter) where TWindow : System.Windows.Window
    {
        ShowedDialogs.Add($"{typeof(TWindow).Name} (with parameter)");
        return DialogResult;
    }

    public void ShowWindow<TWindow>() where TWindow : System.Windows.Window
    {
        ShowedWindows.Add(typeof(TWindow).Name);
    }

    public void ShowWindow<TWindow>(object parameter) where TWindow : System.Windows.Window
    {
        ShowedWindows.Add($"{typeof(TWindow).Name} (with parameter)");
    }

    public System.Windows.MessageBoxResult ShowMessage(
        string message,
        string title = "",
        System.Windows.MessageBoxButton buttons = System.Windows.MessageBoxButton.OK,
        System.Windows.MessageBoxImage icon = System.Windows.MessageBoxImage.Information)
    {
        ShowedMessages.Add((message, title));
        return MessageBoxResult;
    }

    public void ShowError(string message, string title = "Error")
    {
        ShowedErrors.Add((message, title));
    }

    public bool ShowConfirmation(string message, string title = "Confirm")
    {
        ShowedConfirmations.Add((message, title));
        return ConfirmationResult;
    }

    public System.Windows.Window? GetActiveWindow()
    {
        return ActiveWindow;
    }
}
