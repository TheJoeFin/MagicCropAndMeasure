using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.ViewModels.Measurements;

namespace MagickCrop.Controls;

/// <summary>
/// Control for measuring rectangles using MVVM pattern.
/// </summary>
public partial class RectangleMeasurementControl : MeasurementControlBase
{
    private FrameworkElement? _clickedElement;
    private int _pointDraggingIndex = -1;
    private Point _clickedPoint;

    // Backward compatibility events for MainWindow
    public event MouseButtonEventHandler? MeasurementPointMouseDown;
    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    /// <summary>
    /// Gets the ViewModel for this control.
    /// </summary>
    public RectangleMeasurementViewModel? ViewModel => DataContext as RectangleMeasurementViewModel;

    /// <summary>
    /// Initializes a new instance of the RectangleMeasurementControl class.
    /// </summary>
    public RectangleMeasurementControl()
    {
        InitializeComponent();

        // Create or use injected ViewModel
        if (DataContext is not RectangleMeasurementViewModel)
        {
            try
            {
                DataContext = Ioc.Default.GetService<RectangleMeasurementViewModel>() ?? new RectangleMeasurementViewModel();
            }
            catch
            {
                DataContext = new RectangleMeasurementViewModel();
            }
        }
    }

    /// <summary>
    /// Initializes the measurement with reasonable starting positions.
    /// </summary>
    public void InitializePositions(double canvasWidth, double canvasHeight)
    {
        if (ViewModel is null)
            return;

        ViewModel.TopLeft = new Point(canvasWidth * 0.3, canvasHeight * 0.3);
        ViewModel.BottomRight = new Point(canvasWidth * 0.7, canvasHeight * 0.7);
    }

    /// <summary>
    /// Moves a measurement point to a new position.
    /// </summary>
    public void MovePoint(int pointIndex, Point newPosition)
    {
        if (ViewModel is null)
            return;

        if (pointIndex == 0)
            ViewModel.TopLeft = newPosition;
        else if (pointIndex == 1)
            ViewModel.BottomRight = newPosition;
    }

    /// <summary>
    /// Gets the index of the currently dragging point (-1 if none).
    /// </summary>
    public int GetActivePointIndex() => _pointDraggingIndex;

    /// <summary>
    /// Resets the active point index.
    /// </summary>
    public void ResetActivePoint()
    {
        _pointDraggingIndex = -1;
        _clickedElement = null;
    }

    /// <summary>
    /// Converts this control to a DTO for serialization.
    /// </summary>
    public RectangleMeasurementControlDto ToDto()
    {
        if (ViewModel is null)
            throw new InvalidOperationException("ViewModel is not initialized");

        return new RectangleMeasurementControlDto
        {
            TopLeft = ViewModel.TopLeft,
            BottomRight = ViewModel.BottomRight,
            ScaleFactor = ViewModel.ScaleFactor,
            Units = ViewModel.Units
        };
    }

    /// <summary>
    /// Loads data from a DTO for deserialization.
    /// </summary>
    public void FromDto(RectangleMeasurementControlDto dto)
    {
        if (ViewModel is null)
            throw new InvalidOperationException("ViewModel is not initialized");

        ViewModel.TopLeft = dto.TopLeft;
        ViewModel.BottomRight = dto.BottomRight;
        ViewModel.ScaleFactor = dto.ScaleFactor;
        ViewModel.Units = dto.Units;
    }

    private void MeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Shapes.Ellipse ellipse || ellipse.Tag is not string intAsString)
            return;

        _pointDraggingIndex = int.Parse(intAsString);
        _clickedElement = ellipse;
        _clickedPoint = e.GetPosition(MeasurementCanvas);

        MeasurementPointMouseDown?.Invoke(sender, e);
        RaiseMeasurementPointMouseDown(e);
    }

    private void CopyMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.DisplayText is not null)
        {
            Clipboard.SetText(ViewModel.DisplayText);
        }
    }

    private void MeasurementButton_Click(object sender, RoutedEventArgs e)
    {
        ContextMenu? contextMenu = MeasurementText.ContextMenu;
        if (contextMenu is not null)
        {
            contextMenu.PlacementTarget = MeasurementText;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void RemoveMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RemoveControlRequested?.Invoke(this, EventArgs.Empty);
        RaiseRemoveRequested();
    }
}
