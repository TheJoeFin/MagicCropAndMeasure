using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MagickCrop.Models.MeasurementControls;

namespace MagickCrop.Controls;

public partial class RectangleMeasurementControl : UserControl
{
    private Point topLeft = new(100, 100);
    private Point bottomRight = new(300, 300);
    private FrameworkElement? clickedElement;
    private int pointDraggingIndex = -1;
    private Point clickedPoint;

    public event MouseButtonEventHandler? MeasurementPointMouseDown;
    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    public RectangleMeasurementControl()
    {
        InitializeComponent();
        UpdatePositions();
    }

    public void InitializePositions(double canvasWidth, double canvasHeight)
    {
        topLeft = new Point(canvasWidth * 0.3, canvasHeight * 0.3);
        bottomRight = new Point(canvasWidth * 0.7, canvasHeight * 0.7);
        UpdatePositions();
    }

    private void UpdatePositions()
    {
        // Update rectangle
        double x = Math.Min(topLeft.X, bottomRight.X);
        double y = Math.Min(topLeft.Y, bottomRight.Y);
        double width = Math.Abs(bottomRight.X - topLeft.X);
        double height = Math.Abs(bottomRight.Y - topLeft.Y);
        Canvas.SetLeft(MeasurementRectangle, x);
        Canvas.SetTop(MeasurementRectangle, y);
        MeasurementRectangle.Width = width;
        MeasurementRectangle.Height = height;

        // Update handles
        Canvas.SetLeft(TopLeftPoint, topLeft.X - (TopLeftPoint.Width / 2));
        Canvas.SetTop(TopLeftPoint, topLeft.Y - (TopLeftPoint.Height / 2));
        Canvas.SetLeft(BottomRightPoint, bottomRight.X - (BottomRightPoint.Width / 2));
        Canvas.SetTop(BottomRightPoint, bottomRight.Y - (BottomRightPoint.Height / 2));

        // Update measurement text
        RectangleTextBlock.Text = $"{width:F1} × {height:F1} px";
        Canvas.SetLeft(MeasurementText, x + width / 2 - (MeasurementText.ActualWidth / 2));
        Canvas.SetTop(MeasurementText, y - MeasurementText.ActualHeight - 5);
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
            topLeft = newPosition;
        else if (pointIndex == 1)
            bottomRight = newPosition;
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
        string rect = RectangleTextBlock.Text;
        Clipboard.SetText(rect);
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

    public RectangleMeasurementControlDto ToDto()
    {
        return new RectangleMeasurementControlDto
        {
            TopLeft = topLeft,
            BottomRight = bottomRight
        };
    }

    public void FromDto(RectangleMeasurementControlDto dto)
    {
        topLeft = dto.TopLeft;
        bottomRight = dto.BottomRight;
        UpdatePositions();
    }
}
