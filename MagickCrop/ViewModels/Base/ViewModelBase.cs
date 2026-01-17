using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MagickCrop.ViewModels.Base;

/// <summary>
/// Base class for all ViewModels in the application.
/// Inherits from ObservableObject which provides INotifyPropertyChanged implementation.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Indicates whether the ViewModel is currently performing a loading operation.
    /// </summary>
    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Indicates whether the ViewModel is busy with any operation.
    /// </summary>
    [ObservableProperty]
    private bool isBusy;

    /// <summary>
    /// Gets or sets the title for the ViewModel/View.
    /// </summary>
    [ObservableProperty]
    private string title = string.Empty;

    /// <summary>
    /// Called when the ViewModel is first loaded.
    /// Override to perform initialization.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the associated View is being closed.
    /// Override to perform cleanup.
    /// </summary>
    public virtual void Cleanup()
    {
    }
}
