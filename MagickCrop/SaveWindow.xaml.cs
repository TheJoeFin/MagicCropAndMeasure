using System.Windows;
using System.Windows.Input;
using MagickCrop.ViewModels;
using Wpf.Ui.Controls;

namespace MagickCrop;

/// <summary>
/// Window for displaying and saving processed images.
/// </summary>
public partial class SaveWindow : FluentWindow
{
    private SaveWindowViewModel ViewModel => (SaveWindowViewModel)DataContext;

    /// <summary>
    /// Parameterless constructor for DI.
    /// </summary>
    public SaveWindow() : this(new SaveWindowViewModel())
    {
    }

    /// <summary>
    /// Constructor with ViewModel injection.
    /// </summary>
    public SaveWindow(SaveWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>
    /// Constructor with image path (backward compatibility).
    /// </summary>
    public SaveWindow(string imagePath) : this(new SaveWindowViewModel())
    {
        ViewModel.Initialize(imagePath);
    }

    /// <summary>
    /// Initialize with an image path after construction.
    /// </summary>
    public void Initialize(string imagePath)
    {
        ViewModel.Initialize(imagePath);
    }

    private void Image_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var data = ViewModel.GetDragData();
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
            }
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        ViewModel.Cleanup();
    }
}
