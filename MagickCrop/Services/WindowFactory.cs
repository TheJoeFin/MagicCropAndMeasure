using MagickCrop.Services.Interfaces;
using MagickCrop.ViewModels;

namespace MagickCrop.Services;

/// <summary>
/// Factory for creating windows that require special initialization.
/// </summary>
public class WindowFactory : IWindowFactory
{
    private readonly IServiceProvider _serviceProvider;

    public WindowFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public SaveWindow CreateSaveWindow(string imagePath)
    {
        var viewModel = _serviceProvider.GetService(typeof(SaveWindowViewModel)) as SaveWindowViewModel
            ?? new SaveWindowViewModel();
        
        viewModel.Initialize(imagePath);
        return new SaveWindow(viewModel);
    }
}
