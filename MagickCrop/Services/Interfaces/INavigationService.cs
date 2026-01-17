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
