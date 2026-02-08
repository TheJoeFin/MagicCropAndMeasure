using MagickCrop.Models.MeasurementControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MagickCrop.Controls;

public partial class CircleMeasurementControl : UserControl
{
    private Point center = new(200, 200);
    private Point edgePoint = new(300, 200);
    private FrameworkElement? clickedElement;
    private int pointDraggingIndex = -1;
    private Point clickedPoint;
    private bool isPlacingCenter = true;

    private double scaleFactor = 1.0;
    public double ScaleFactor
    {
        get => scaleFactor;
        set
        {
            scaleFactor = value;
            UpdateMeasurementText();
        }
    }

    private string units = "pixels";
    public string Units
    {
        get => units;
        set
        {
            units = value;
            UpdateMeasurementText();
        }
    }

    public event MouseButtonEventHandler? MeasurementPointMouseDown;
    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    public CircleMeasurementControl()
    {
        InitializeComponent();
        UpdatePositions();
    }

    public void InitializePositions(double canvasWidth, double canvasHeight)
    {
        center = new Point(canvasWidth * 0.5, canvasHeight * 0.5);
        edgePoint = new Point(canvasWidth * 0.5 + canvasWidth * 0.2, canvasHeight * 0.5);
        UpdatePositions();
    }

    private void UpdatePositions()
    {
        // Calculate radius
        double radius = CalculateRadius();

        // Update circle
        Canvas.SetLeft(MeasurementCircle, center.X - radius);
        Canvas.SetTop(MeasurementCircle, center.Y - radius);
        MeasurementCircle.Width = radius * 2;
        MeasurementCircle.Height = radius * 2;

        // Update center point
        Canvas.SetLeft(CenterPoint, center.X - (CenterPoint.Width / 2));
        Canvas.SetTop(CenterPoint, center.Y - (CenterPoint.Height / 2));

        // Update edge point
        Canvas.SetLeft(EdgePoint, edgePoint.X - (EdgePoint.Width / 2));
        Canvas.SetTop(EdgePoint, edgePoint.Y - (EdgePoint.Height / 2));

        // Update measurement text
        UpdateMeasurementText();

        // Position the measurement text above the circle
        Canvas.SetLeft(MeasurementText, center.X - (MeasurementText.ActualWidth / 2));
        Canvas.SetTop(MeasurementText, center.Y - radius - MeasurementText.ActualHeight - 5);
    }
    public void UpdatePreview(Point mousePosition)
    {
        if (!isPlacingCenter)
        {
            // Second click - update edge point for preview
            edgePoint = mousePosition;
            UpdatePositions();
        }
    }

    private double CalculateRadius()
    {
        double deltaX = edgePoint.X - center.X;
        double deltaY = edgePoint.Y - center.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private void UpdateMeasurementText()
    {
        double radius = CalculateRadius();
        double circumference = 2 * Math.PI * radius;
        double area = Math.PI * radius * radius;

        double scaledRadius = radius * ScaleFactor;
        double scaledCircumference = circumference * ScaleFactor;
        double scaledArea = area * ScaleFactor * ScaleFactor; // Area scales by factor squared

        CircleTextBlock.Text = $"r: {scaledRadius:N2} {Units}, C: {scaledCircumference:N2} {Units}, A: {scaledArea:N2} {Units}²";
    }

    private void MeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Shapes.Ellipse ellipse || ellipse.Tag is not string intAsString)
            return;
        pointDraggingIndex = int.Parse(intAsString);
        clickedElement = ellipse;
        clickedPoint = e.GetPosition(MeasurementCanvas);
        MeasurementPointMouseDown?.Invoke(sender, e);
    }

    public void MovePoint(int pointIndex, Point newPosition)
    {
        if (pointIndex == 0)
            center = newPosition;
        else if (pointIndex == 1)
            edgePoint = newPosition;
        UpdatePositions();
    }

    public int GetActivePointIndex() => pointDraggingIndex;

    public void ResetActivePoint()
    {
        pointDraggingIndex = -1;
        clickedElement = null;
    }

    private void CopyMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string circle = CircleTextBlock.Text;
        Clipboard.SetText(circle);
    }

    private void MeasurementButton_Click(object sender, RoutedEventArgs e)
    {
        ContextMenu? contextMenu = MeasurementText.ContextMenu;
        if (contextMenu != null)
        {
            contextMenu.PlacementTarget = MeasurementText;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void RemoveMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RemoveControlRequested?.Invoke(this, EventArgs.Empty);
    }

    public CircleMeasurementControlDto ToDto()
    {
        return new CircleMeasurementControlDto
        {
            Center = center,
            EdgePoint = edgePoint,
            ScaleFactor = ScaleFactor,
            Units = Units
        };
    }

    /// <summary>
    /// Apply data from a DTO to this control
    /// </summary>
    public void FromDto(CircleMeasurementControlDto dto)
    {
        center = dto.Center;
        edgePoint = dto.EdgePoint;
        ScaleFactor = dto.ScaleFactor; // This will use the property setter
        Units = dto.Units;             // This will use the property setter
        UpdatePositions();
    }
}
