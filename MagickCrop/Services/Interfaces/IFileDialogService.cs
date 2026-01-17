namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Service for showing file dialogs.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Shows an open file dialog.
    /// </summary>
    /// <param name="filter">File filter string (e.g., "Images|*.png;*.jpg")</param>
    /// <param name="title">Dialog title</param>
    /// <returns>Selected file path, or null if cancelled.</returns>
    string? ShowOpenFileDialog(string filter, string? title = null);

    /// <summary>
    /// Shows a save file dialog.
    /// </summary>
    /// <param name="filter">File filter string</param>
    /// <param name="defaultFileName">Default file name</param>
    /// <param name="title">Dialog title</param>
    /// <returns>Selected file path, or null if cancelled.</returns>
    string? ShowSaveFileDialog(string filter, string? defaultFileName = null, string? title = null);

    /// <summary>
    /// Shows a folder browser dialog.
    /// </summary>
    /// <param name="description">Dialog description</param>
    /// <returns>Selected folder path, or null if cancelled.</returns>
    string? ShowFolderBrowserDialog(string? description = null);
}
