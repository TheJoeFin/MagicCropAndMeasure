using MagickCrop.Helpers;
using MagickCrop.Models.MeasurementControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MagickCrop.Controls;

public partial class RectangleMeasurementControl : UserControl
{
    private Point topLeft = new(100, 100);
    private Point bottomRight = new(300, 300);
    private FrameworkElement? clickedElement;
    private int pointDraggingIndex = -1;
    private Point clickedPoint;

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

    private Color strokeColor = (Color)ColorConverter.ConvertFromString("#0066FF");
    public Color StrokeColor
    {
        get => strokeColor;
        set
        {
            strokeColor = value;
            UpdateColors();
        }
    }

    public RectangleMeasurementControl()
    {
        InitializeComponent();
        UpdatePositions();
    }

    private void UpdateColors()
    {
        MeasurementRectangle.Stroke = new SolidColorBrush(strokeColor);
        MeasurementRectangle.Fill = new SolidColorBrush(Color.FromArgb(0x20, strokeColor.R, strokeColor.G, strokeColor.B));
        SolidColorBrush pointBrush = new(strokeColor);
        TopLeftPoint.Fill = pointBrush;
        BottomRightPoint.Fill = pointBrush;
    }

    public bool IsDragGizmoVisible
    {
        get => TopLeftPoint.Visibility == Visibility.Visible;
        set
        {
            Visibility visibility = value ? Visibility.Visible : Visibility.Collapsed;
            TopLeftPoint.Visibility = visibility;
            BottomRightPoint.Visibility = visibility;
        }
    }

    public bool IsEndpointCapVisible
    {
        set
        {
            double size = value ? 6 : 12;
            TopLeftPoint.Width = size;
            TopLeftPoint.Height = size;
            BottomRightPoint.Width = size;
            BottomRightPoint.Height = size;
            UpdatePositions();
        }
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
        UpdateMeasurementText();

        // Position the measurement text above the rectangle
        Canvas.SetLeft(MeasurementText, x + width / 2 - (MeasurementText.ActualWidth / 2));
        Canvas.SetTop(MeasurementText, y - MeasurementText.ActualHeight - 5);
    }

    private void UpdateMeasurementText()
    {
        double width = Math.Abs(bottomRight.X - topLeft.X);
        double height = Math.Abs(bottomRight.Y - topLeft.Y);
        double area = width * height;

        double scaledWidth = width * ScaleFactor;
        double scaledHeight = height * ScaleFactor;
        double scaledArea = area * ScaleFactor * ScaleFactor; // Area scales by factor squared

        RectangleTextBlock.Text = $"{scaledWidth:N2} \u00D7 {scaledHeight:N2} {Units} (A: {scaledArea:N2} {Units}\u00B2)";
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

    private async void ChangeColorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        Color? picked = await ColorPickerDialog.PickColorAsync(mainWindow, strokeColor, "Change Measurement Color");
        if (picked is Color color)
            StrokeColor = color;
    }

    public RectangleMeasurementControlDto ToDto()
    {
        return new RectangleMeasurementControlDto
        {
            TopLeft = topLeft,
            BottomRight = bottomRight,
            ScaleFactor = ScaleFactor,
            Units = Units,
            StrokeColor = strokeColor.ToString()
        };
    }

    /// <summary>
    /// Apply data from a DTO to this control
    /// </summary>
    public void FromDto(RectangleMeasurementControlDto dto)
    {
        topLeft = dto.TopLeft;
        bottomRight = dto.BottomRight;
        ScaleFactor = dto.ScaleFactor; // This will use the property setter
        Units = dto.Units;             // This will use the property setter

        try { strokeColor = (Color)ColorConverter.ConvertFromString(dto.StrokeColor); }
        catch { strokeColor = (Color)ColorConverter.ConvertFromString("#0066FF"); }
        UpdateColors();

        UpdatePositions();
    }
}
