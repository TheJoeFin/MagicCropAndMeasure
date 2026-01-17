using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MagickCrop.ViewModels;

namespace MagickCrop.Controls;

/// <summary>
/// Welcome message control displayed when no image is loaded.
/// </summary>
public partial class WelcomeMessage : UserControl
{
    public WelcomeViewModel ViewModel => (WelcomeViewModel)DataContext;

    #region Dependency Properties (for backward compatibility)

    public static readonly DependencyProperty OpenFileCommandProperty =
        DependencyProperty.Register(nameof(OpenFileCommand), typeof(ICommand), typeof(WelcomeMessage),
            new PropertyMetadata(null, OnOpenFileCommandChanged));

    public static readonly DependencyProperty PasteCommandProperty =
        DependencyProperty.Register(nameof(PasteCommand), typeof(ICommand), typeof(WelcomeMessage),
            new PropertyMetadata(null, OnPasteCommandChanged));

    public static readonly DependencyProperty OverlayCommandProperty =
        DependencyProperty.Register(nameof(OverlayCommand), typeof(ICommand), typeof(WelcomeMessage),
            new PropertyMetadata(null, OnOverlayCommandChanged));

    public ICommand? OpenFileCommand
    {
        get => (ICommand?)GetValue(OpenFileCommandProperty);
        set => SetValue(OpenFileCommandProperty, value);
    }

    public ICommand? PasteCommand
    {
        get => (ICommand?)GetValue(PasteCommandProperty);
        set => SetValue(PasteCommandProperty, value);
    }

    public ICommand? OverlayCommand
    {
        get => (ICommand?)GetValue(OverlayCommandProperty);
        set => SetValue(OverlayCommandProperty, value);
    }

    private static void OnOpenFileCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WelcomeMessage control && e.NewValue is ICommand command)
        {
            control.ViewModel.OpenFileCommand = command;
        }
    }

    private static void OnPasteCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WelcomeMessage control && e.NewValue is ICommand command)
        {
            control.ViewModel.PasteFromClipboardCommand = command;
        }
    }

    private static void OnOverlayCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WelcomeMessage control && e.NewValue is ICommand command)
        {
            control.ViewModel.OpenOverlayCommand = command;
        }
    }

    #endregion

    public WelcomeMessage()
    {
        var viewModel = App.GetService<WelcomeViewModel>();
        DataContext = viewModel;
        InitializeComponent();
        
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        
        // Refresh clipboard when control gains focus
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.Activated += Window_Activated;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Cleanup();
        
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.Activated -= Window_Activated;
        }
    }

    private async void Window_Activated(object? sender, EventArgs e)
    {
        // Refresh clipboard state when window gains focus
        await ViewModel.RefreshClipboardCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Updates the recent projects list by refreshing from the ViewModel.
    /// Called by MainWindow when recent projects change.
    /// </summary>
    internal async void UpdateRecentProjects()
    {
        await ViewModel.InitializeAsync();
    }
}
