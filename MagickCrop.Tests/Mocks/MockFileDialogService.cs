using System.Collections.Generic;

namespace MagickCrop.Tests.Mocks;

/// <summary>
/// Mock implementation of IFileDialogService for testing
/// </summary>
public class MockFileDialogService : IFileDialogService
{
    public string? LastOpenFilter { get; private set; }
    public string? LastOpenTitle { get; private set; }
    public string? LastSaveFilter { get; private set; }
    public string? LastSaveFileName { get; private set; }
    public string? LastSaveTitle { get; private set; }
    public string? LastFolderDescription { get; private set; }

    public string? SelectedOpenFile { get; set; }
    public string? SelectedSaveFile { get; set; }
    public string? SelectedFolder { get; set; }

    public string? ShowOpenFileDialog(string filter, string? title = null)
    {
        LastOpenFilter = filter;
        LastOpenTitle = title;
        return SelectedOpenFile;
    }

    public string? ShowSaveFileDialog(string filter, string? defaultFileName = null, string? title = null)
    {
        LastSaveFilter = filter;
        LastSaveFileName = defaultFileName;
        LastSaveTitle = title;
        return SelectedSaveFile ?? defaultFileName;
    }

    public string? ShowFolderBrowserDialog(string? description = null)
    {
        LastFolderDescription = description;
        return SelectedFolder;
    }
}
