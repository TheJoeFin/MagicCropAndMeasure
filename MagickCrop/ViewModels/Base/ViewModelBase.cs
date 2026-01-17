using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace MagickCrop.ViewModels.Base;

/// <summary>
/// Base class for all ViewModels in the application.
/// Inherits from ObservableObject which provides INotifyPropertyChanged implementation.
/// Includes support for the MVVM Toolkit Messenger for decoupled communication.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Gets the messenger instance for sending and receiving messages.
    /// </summary>
    protected IMessenger Messenger { get; }

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
    /// Protected constructor with default messenger.
    /// </summary>
    protected ViewModelBase() : this(WeakReferenceMessenger.Default)
    {
    }

    /// <summary>
    /// Protected constructor allowing dependency injection of messenger (for testing).
    /// </summary>
    protected ViewModelBase(IMessenger messenger)
    {
        Messenger = messenger;
        
        // Auto-register if this ViewModel implements any IRecipient<T> interfaces
        if (GetType().GetInterfaces().Any(i => 
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRecipient<>)))
        {
            Messenger.RegisterAll(this);
        }
    }

    /// <summary>
    /// Called when the ViewModel is first loaded.
    /// Override to perform initialization and register message handlers.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the associated View is being closed.
    /// Override to perform cleanup. Base implementation unregisters all messages.
    /// </summary>
    public virtual void Cleanup()
    {
        Messenger.UnregisterAll(this);
    }

    /// <summary>
    /// Sends a message to all registered recipients.
    /// </summary>
    protected void Send<TMessage>(TMessage message) where TMessage : class
    {
        Messenger.Send(message);
    }

    /// <summary>
    /// Registers this ViewModel to receive messages of type TMessage.
    /// </summary>
    protected void Register<TMessage>(Action<TMessage> handler) where TMessage : class
    {
        Messenger.Register<TMessage>(this, (r, m) => handler(m));
    }
}
