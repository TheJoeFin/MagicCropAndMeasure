using System.Windows;
using System.Windows.Media.Imaging;
using MagickCrop.Helpers;
using MagickCrop.Services.Interfaces;

namespace MagickCrop.Services;

/// <summary>
/// Implementation of IClipboardService using WPF Clipboard.
/// </summary>
public class ClipboardService : IClipboardService
{
    public bool ContainsImage()
    {
        return Clipboard.ContainsImage() || 
               Clipboard.ContainsData(DataFormats.Dib) ||
               Clipboard.ContainsFileDropList();
    }

    public bool ContainsFileDropList()
    {
        return Clipboard.ContainsFileDropList();
    }

    public BitmapSource? GetImage()
    {
        return ClipboardHelper.GetImageFromClipboard();
    }

    public IReadOnlyList<string> GetFileDropList()
    {
        if (Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList();
            return files?.Cast<string>().ToList() ?? [];
        }
        return [];
    }

    public void SetImage(BitmapSource image)
    {
        Clipboard.SetImage(image);
    }

    public void SetText(string text)
    {
        Clipboard.SetText(text);
    }
}
