using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace MagickCrop.Tests.Mocks;

/// <summary>
/// Mock implementation of IClipboardService for testing
/// </summary>
public class MockClipboardService : IClipboardService
{
    private BitmapSource? _image;
    private List<string> _fileDropList = [];
    private string? _text;

    public bool ContainsImage()
    {
        return _image != null;
    }

    public bool ContainsFileDropList()
    {
        return _fileDropList.Count > 0;
    }

    public BitmapSource? GetImage()
    {
        return _image;
    }

    public IReadOnlyList<string> GetFileDropList()
    {
        return _fileDropList.AsReadOnly();
    }

    public void SetImage(BitmapSource image)
    {
        _image = image;
    }

    public void SetText(string text)
    {
        _text = text;
    }

    /// <summary>
    /// Test helper to set file drop list
    /// </summary>
    public void SetFileDropList(params string[] files)
    {
        _fileDropList = files.ToList();
    }

    /// <summary>
    /// Test helper to clear all clipboard data
    /// </summary>
    public void Clear()
    {
        _image = null;
        _fileDropList.Clear();
        _text = null;
    }
}
