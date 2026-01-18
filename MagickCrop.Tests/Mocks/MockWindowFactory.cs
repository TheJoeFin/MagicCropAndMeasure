using System.Collections.Generic;
using MagickCrop.Windows;

namespace MagickCrop.Tests.Mocks;

/// <summary>
/// Mock implementation of IWindowFactory for testing
/// </summary>
public class MockWindowFactory : IWindowFactory
{
    public List<string> CreatedSaveWindowsWithImages { get; } = [];

    public SaveWindow CreateSaveWindow(string imagePath)
    {
        CreatedSaveWindowsWithImages.Add(imagePath);
        // Return a new instance - tests can verify it was called with the right parameters
        return new SaveWindow();
    }
}
