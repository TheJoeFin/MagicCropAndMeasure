using MagickCrop.Services.Interfaces;
using MagickCrop.Windows;

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
