using MagickCrop.Windows;

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
