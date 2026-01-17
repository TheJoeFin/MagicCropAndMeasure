using Microsoft.Win32;
using MagickCrop.Services.Interfaces;

namespace MagickCrop.Services;

/// <summary>
/// Implementation of IFileDialogService using WPF dialogs.
/// </summary>
public class FileDialogService : IFileDialogService
{
    public string? ShowOpenFileDialog(string filter, string? title = null)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title ?? "Open File"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string filter, string? defaultFileName = null, string? title = null)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName ?? string.Empty,
            Title = title ?? "Save File"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowFolderBrowserDialog(string? description = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = description ?? "Select Folder"
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
