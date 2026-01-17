using MagickCrop.ViewModels;
using Wpf.Ui.Controls;

namespace MagickCrop.Windows;

/// <summary>
/// About window displaying application information.
/// </summary>
public partial class AboutWindow : FluentWindow
{
    public AboutWindow() : this(new AboutWindowViewModel())
    {
    }

    public AboutWindow(AboutWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }
}
