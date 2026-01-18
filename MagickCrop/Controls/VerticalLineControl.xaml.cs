using MagickCrop.Models.MeasurementControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using MagickCrop.ViewModels.Measurements;
using Wpf.Ui.Controls;

namespace MagickCrop.Controls;

public partial class VerticalLineControl : MeasurementControlBase
{
    private bool isDragging = false;
    private Point initialMousePosition;

    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    /// <summary>
    /// Gets the ViewModel for this control.
    /// </summary>
    public VerticalLineViewModel? ViewModel => DataContext as VerticalLineViewModel;

    public VerticalLineControl()
    {
        InitializeComponent();

        // Create or use injected ViewModel
        if (DataContext is not VerticalLineViewModel)
        {
            try
            {
                DataContext = Ioc.Default.GetService<VerticalLineViewModel>() ?? new VerticalLineViewModel();
            }
            catch
            {
                DataContext = new VerticalLineViewModel();
            }
        }
    }

    public void Initialize(double canvasWidth, double canvasHeight, double xPosition = 40)
    {
        if (ViewModel is VerticalLineViewModel vm)
        {
            vm.Position = xPosition;
            vm.CanvasSize = canvasHeight;
        }
    }

    public void Resize(double canvasHeight)
    {
        if (ViewModel is VerticalLineViewModel vm)
        {
            vm.CanvasSize = canvasHeight;
        }
    }

    private void VerticalLine_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            isDragging = true;
            initialMousePosition = e.GetPosition(LineCanvas);
            VerticalLine.CaptureMouse();
            e.Handled = true;
        }
        else if (e.RightButton == MouseButtonState.Pressed && this.ContextMenu is ContextMenu contextMenu)
        {
            contextMenu.IsOpen = true;
        }
    }

    private void VerticalLine_MouseMove(object sender, MouseEventArgs e)
    {
        if (isDragging && ViewModel is VerticalLineViewModel vm)
        {
            Point currentMousePosition = e.GetPosition(LineCanvas);
            double deltaX = currentMousePosition.X - initialMousePosition.X;

            vm.Position += deltaX;

            initialMousePosition = currentMousePosition;
            e.Handled = true;
        }
    }

    private void VerticalLine_MouseUp(object sender, MouseButtonEventArgs e)
    {
        isDragging = false;
        VerticalLine.ReleaseMouseCapture();
        e.Handled = true;
    }

    private async void ChangeColorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Create a simple color picker dialog
        ContentDialog colorDialog = new()
        {
            Title = "Select Line Color",
            CloseButtonText = "Cancel",
            PrimaryButtonText = "Apply"
        };

        // ColorPicker colorPicker = new();
        // if (VerticalLine.Stroke is SolidColorBrush brush)
        // {
        //     colorPicker.Color = brush.Color;
        // }
        // 
        // colorDialog.Content = colorPicker;

        // Show dialog
        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        ContentDialogResult result = await colorDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // VerticalLine.Stroke = new SolidColorBrush(colorPicker.Color);
        }
    }

    private async void ChangeThicknessMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Create a thickness selector dialog
        ContentDialog thicknessDialog = new()
        {
            Title = "Select Line Thickness",
            CloseButtonText = "Cancel",
            PrimaryButtonText = "Apply"
        };

        NumberBox thicknessSlider = new()
        {
            Value = ViewModel?.StrokeThickness ?? 2.0,
            Minimum = 0.5,
            Maximum = 5.0,
            SmallChange = 0.5,
            PlaceholderText = "Line Thickness"
        };

        thicknessDialog.Content = thicknessSlider;

        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        // Show dialog
        ContentDialogResult result = await thicknessDialog.ShowAsync();
        if (result == ContentDialogResult.Primary && thicknessSlider.Value.HasValue && ViewModel is VerticalLineViewModel vm)
        {
            vm.StrokeThickness = thicknessSlider.Value.Value;
        }
    }

    private void RemoveLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RemoveControlRequested?.Invoke(this, EventArgs.Empty);
    }

    public VerticalLineControlDto ToDto()
    {
        return new VerticalLineControlDto
        {
            Position = ViewModel?.Position ?? 0,
            StrokeColor = ViewModel?.Color.ToString() ?? "#800080",
            StrokeThickness = ViewModel?.StrokeThickness ?? 2.0
        };
    }

    public void FromDto(VerticalLineControlDto dto)
    {
        if (ViewModel is VerticalLineViewModel vm)
        {
            vm.Position = dto.Position;

            // Parse color from stored string
            if (dto.StrokeColor != null)
            {
                try
                {
                    Color color = (Color)ColorConverter.ConvertFromString(dto.StrokeColor);
                    vm.Color = color;
                }
                catch
                {
                    // Fallback to default color if parsing fails
                    vm.Color = Colors.Purple;
                }
            }

            vm.StrokeThickness = dto.StrokeThickness;
        }
    }
}
