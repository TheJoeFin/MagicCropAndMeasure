using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.DependencyInjection;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.ViewModels.Measurements;

namespace MagickCrop.Controls;

/// <summary>
/// Control for measuring angles between two lines using MVVM pattern.
/// </summary>
public partial class AngleMeasurementControl : MeasurementControlBase
{
    private FrameworkElement? _clickedElement;
    private int _pointDraggingIndex = -1;
    private Point _clickedPoint;

    // Backward compatibility events for MainWindow
    public new event MouseButtonEventHandler? MeasurementPointMouseDown;
#pragma warning disable CS0067
    public event MouseEventHandler? MeasurementPointMouseMove;
#pragma warning restore CS0067
    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    /// <summary>
    /// Gets the ViewModel for this control.
    /// </summary>
    public AngleMeasurementViewModel? ViewModel => DataContext as AngleMeasurementViewModel;

    /// <summary>
    /// Initializes a new instance of the AngleMeasurementControl class.
    /// </summary>
    public AngleMeasurementControl()
    {
        InitializeComponent();

        // Create or use injected ViewModel
        if (DataContext is not AngleMeasurementViewModel)
        {
            try
            {
                DataContext = Ioc.Default.GetService<AngleMeasurementViewModel>() ?? new AngleMeasurementViewModel();
            }
            catch
            {
                DataContext = new AngleMeasurementViewModel();
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

        ViewModel.Vertex = new Point(canvasWidth * 0.5, canvasHeight * 0.5);
        ViewModel.Point1 = new Point(canvasWidth * 0.3, canvasHeight * 0.3);
        ViewModel.Point2 = new Point(canvasWidth * 0.7, canvasHeight * 0.3);
    }

    /// <summary>
    /// Set hit testing for Point3 during placement operations
    /// </summary>
    /// <param name="isEnabled">Whether Point3 should be hit testable</param>
    public void SetPoint3HitTestable(bool isEnabled)
    {
        Point3.IsHitTestVisible = isEnabled;
    }

    /// <summary>
    /// Starts dragging a measurement point.
    /// </summary>
    public void StartDraggingPoint(int pointIndex)
    {
        _pointDraggingIndex = pointIndex;
        _clickedElement = pointIndex switch
        {
            0 => Point1,
            1 => VertexPoint,
            _ => Point3
        };
        MeasurementPointMouseDown?.Invoke(_clickedElement, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseDownEvent });
    }

    /// <summary>
    /// Moves a measurement point to a new position.
    /// </summary>
    public void MovePoint(int pointIndex, Point newPosition)
    {
        if (ViewModel is null)
            return;

        switch (pointIndex)
        {
            case 0:
                ViewModel.Point1 = newPosition;
                break;
            case 1:
                ViewModel.Vertex = newPosition;
                break;
            case 2:
                ViewModel.Point2 = newPosition;
                break;
        }
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
    public AngleMeasurementControlDto ToDto()
    {
        if (ViewModel is null)
            throw new InvalidOperationException("ViewModel is not initialized");

        return new AngleMeasurementControlDto
        {
            Point1Position = ViewModel.Point1,
            VertexPosition = ViewModel.Vertex,
            Point3Position = ViewModel.Point2
        };
    }

    /// <summary>
    /// Loads data from a DTO for deserialization.
    /// </summary>
    public void FromDto(AngleMeasurementControlDto dto)
    {
        if (ViewModel is null)
            throw new InvalidOperationException("ViewModel is not initialized");

        ViewModel.Point1 = dto.Point1Position;
        ViewModel.Vertex = dto.VertexPosition;
        ViewModel.Point2 = dto.Point3Position;
    }

    private void MeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse ellipse || ellipse.Tag is not string intAsString)
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
