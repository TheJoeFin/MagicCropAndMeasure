using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.DependencyInjection;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.ViewModels.Measurements;

namespace MagickCrop.Controls;

/// <summary>
/// Control for measuring circles using MVVM pattern.
/// </summary>
public partial class CircleMeasurementControl : MeasurementControlBase
{
    private FrameworkElement? _clickedElement;
    private int _pointDraggingIndex = -1;
    private Point _clickedPoint;

    // Backward compatibility events for MainWindow
    public new event MouseButtonEventHandler? MeasurementPointMouseDown;
    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    /// <summary>
    /// Gets the ViewModel for this control.
    /// </summary>
    public CircleMeasurementViewModel? ViewModel => DataContext as CircleMeasurementViewModel;

    /// <summary>
    /// Initializes a new instance of the CircleMeasurementControl class.
    /// </summary>
    public CircleMeasurementControl()
    {
        InitializeComponent();

        // Create or use injected ViewModel
        if (DataContext is not CircleMeasurementViewModel)
        {
            try
            {
                DataContext = Ioc.Default.GetService<CircleMeasurementViewModel>() ?? new CircleMeasurementViewModel();
            }
            catch
            {
                DataContext = new CircleMeasurementViewModel();
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

        ViewModel.CenterPoint = new Point(canvasWidth * 0.5, canvasHeight * 0.5);
        ViewModel.EdgePoint = new Point(canvasWidth * 0.5 + canvasWidth * 0.2, canvasHeight * 0.5);
    }

    /// <summary>
    /// Updates preview while placing the measurement.
    /// </summary>
    public void UpdatePreview(Point mousePosition)
    {
        if (ViewModel is null)
            return;

        ViewModel.EdgePoint = mousePosition;
    }

    /// <summary>
    /// Moves a measurement point to a new position.
    /// </summary>
    public void MovePoint(int pointIndex, Point newPosition)
    {
        if (ViewModel is null)
            return;

        if (pointIndex == 0)
            ViewModel.CenterPoint = newPosition;
        else if (pointIndex == 1)
            ViewModel.EdgePoint = newPosition;
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
    public CircleMeasurementControlDto ToDto()
    {
        if (ViewModel is null)
            throw new InvalidOperationException("ViewModel is not initialized");

        return new CircleMeasurementControlDto
        {
            Center = ViewModel.CenterPoint,
            EdgePoint = ViewModel.EdgePoint,
            ScaleFactor = ScaleFactor,
            Units = Units
        };
    }

    /// <summary>
    /// Loads data from a DTO for deserialization.
    /// </summary>
    public void FromDto(CircleMeasurementControlDto dto)
    {
        if (ViewModel is null)
            throw new InvalidOperationException("ViewModel is not initialized");

        ViewModel.CenterPoint = dto.Center;
        ViewModel.EdgePoint = dto.EdgePoint;
        ScaleFactor = dto.ScaleFactor;
        Units = dto.Units;
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
