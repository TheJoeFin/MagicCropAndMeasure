using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.ViewModels.Measurements;

namespace MagickCrop.Controls;

/// <summary>
/// Control for measuring distance between two points using MVVM pattern.
/// </summary>
public partial class DistanceMeasurementControl : MeasurementControlBase
{
    private FrameworkElement? _clickedElement;
    private int _pointDraggingIndex = -1;
    private Point _clickedPoint;

    // Backward compatibility events for MainWindow
    public event MouseButtonEventHandler? MeasurementPointMouseDown;
    public event MouseEventHandler? MeasurementPointMouseMove;
    public delegate void SetRealWorldLengthRequestedEventHandler(object sender, double pixelDistance);
    public event SetRealWorldLengthRequestedEventHandler? SetRealWorldLengthRequested;
    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    /// <summary>
    /// Gets the ViewModel for this control.
    /// </summary>
    public DistanceMeasurementViewModel? ViewModel => DataContext as DistanceMeasurementViewModel;

    /// <summary>
    /// Initializes a new instance of the DistanceMeasurementControl class.
    /// </summary>
    public DistanceMeasurementControl()
    {
        InitializeComponent();

        // Create or use injected ViewModel
        if (DataContext is not DistanceMeasurementViewModel)
        {
            try
            {
                DataContext = Ioc.Default.GetService<DistanceMeasurementViewModel>() ?? new DistanceMeasurementViewModel();
            }
            catch
            {
                DataContext = new DistanceMeasurementViewModel();
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

        ViewModel.StartPoint = new Point(canvasWidth * 0.3, canvasHeight * 0.4);
        ViewModel.EndPoint = new Point(canvasWidth * 0.7, canvasHeight * 0.6);
    }

    /// <summary>
    /// Starts dragging a measurement point.
    /// </summary>
    public void StartDraggingPoint(int pointIndex)
    {
        _pointDraggingIndex = pointIndex;
        _clickedElement = pointIndex == 0 ? StartPoint : EndPoint;
        MeasurementPointMouseDown?.Invoke(_clickedElement, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseDownEvent });
    }

    /// <summary>
    /// Moves a measurement point to a new position.
    /// </summary>
    public void MovePoint(int pointIndex, Point newPosition)
    {
        if (ViewModel is null)
            return;

        if (pointIndex == 0)
            ViewModel.StartPoint = newPosition;
        else if (pointIndex == 1)
            ViewModel.EndPoint = newPosition;
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
    public DistanceMeasurementControlDto ToDto()
    {
        if (ViewModel is null)
            throw new InvalidOperationException("ViewModel is not initialized");

        return new DistanceMeasurementControlDto
        {
            StartPosition = ViewModel.StartPoint,
            EndPosition = ViewModel.EndPoint,
            ScaleFactor = ViewModel.ScaleFactor,
            Units = ViewModel.Units
        };
    }

    /// <summary>
    /// Loads data from a DTO for deserialization.
    /// </summary>
    public void FromDto(DistanceMeasurementControlDto dto)
    {
        if (ViewModel is null)
            throw new InvalidOperationException("ViewModel is not initialized");

        ViewModel.StartPoint = dto.StartPosition;
        ViewModel.EndPoint = dto.EndPosition;
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

    private void SetRealWorldLengthMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            double pixelLength = ViewModel.PixelLength;
            SetRealWorldLengthRequested?.Invoke(this, pixelLength);
            RaiseSetRealWorldLengthRequested(pixelLength);
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

