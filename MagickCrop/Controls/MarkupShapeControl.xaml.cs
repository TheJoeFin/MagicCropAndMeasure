using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MagickCrop.Controls;

public partial class MarkupShapeControl : UserControl
{
    private Point point1 = new(100, 100);
    private Point point2 = new(300, 300);
    private int pointDraggingIndex = -1;

    public event MouseButtonEventHandler? MeasurementPointMouseDown;

    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    private MarkupShapeType shapeType = MarkupShapeType.Rectangle;
    public MarkupShapeType ShapeType
    {
        get => shapeType;
        set
        {
            shapeType = value;
            UpdateShapeVisibility();
            UpdatePositions();
        }
    }

    private Color strokeColor = Colors.Red;
    public Color StrokeColor
    {
        get => strokeColor;
        set
        {
            strokeColor = value;
            UpdateColors();
        }
    }

    private double strokeThickness = 3.0;
    public double StrokeThickness
    {
        get => strokeThickness;
        set
        {
            strokeThickness = value;
            UpdateThickness();
            UpdatePositions();
        }
    }

    public bool IsDragGizmoVisible
    {
        get => Point1Handle.Visibility == Visibility.Visible;
        set
        {
            Visibility visibility = value ? Visibility.Visible : Visibility.Collapsed;
            Point1Handle.Visibility = visibility;
            Point2Handle.Visibility = visibility;
        }
    }

    public MarkupShapeControl()
    {
        InitializeComponent();
        UpdateShapeVisibility();
        UpdatePositions();
        UpdateColors();
    }

    private void UpdateShapeVisibility()
    {
        ShapeLine.Visibility = shapeType is MarkupShapeType.Line or MarkupShapeType.Arrow
            ? Visibility.Visible : Visibility.Collapsed;
        ArrowHead.Visibility = shapeType == MarkupShapeType.Arrow
            ? Visibility.Visible : Visibility.Collapsed;
        ShapeRectangle.Visibility = shapeType == MarkupShapeType.Rectangle
            ? Visibility.Visible : Visibility.Collapsed;
        ShapeEllipse.Visibility = shapeType == MarkupShapeType.Ellipse
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateColors()
    {
        SolidColorBrush brush = new(strokeColor);
        ShapeLine.Stroke = brush;
        ArrowHead.Fill = new SolidColorBrush(strokeColor);
        ShapeRectangle.Stroke = new SolidColorBrush(strokeColor);
        ShapeEllipse.Stroke = new SolidColorBrush(strokeColor);
        Point1Handle.Fill = new SolidColorBrush(strokeColor);
        Point2Handle.Fill = new SolidColorBrush(strokeColor);
    }

    private void UpdateThickness()
    {
        ShapeLine.StrokeThickness = strokeThickness;
        ShapeRectangle.StrokeThickness = strokeThickness;
        ShapeEllipse.StrokeThickness = strokeThickness;
    }

    private void UpdatePositions()
    {
        Canvas.SetLeft(Point1Handle, point1.X - Point1Handle.Width / 2);
        Canvas.SetTop(Point1Handle, point1.Y - Point1Handle.Height / 2);
        Canvas.SetLeft(Point2Handle, point2.X - Point2Handle.Width / 2);
        Canvas.SetTop(Point2Handle, point2.Y - Point2Handle.Height / 2);

        switch (shapeType)
        {
            case MarkupShapeType.Line:
            case MarkupShapeType.Arrow:
                ShapeLine.X1 = point1.X;
                ShapeLine.Y1 = point1.Y;
                ShapeLine.X2 = point2.X;
                ShapeLine.Y2 = point2.Y;
                if (shapeType == MarkupShapeType.Arrow)
                    UpdateArrowHead();
                break;

            case MarkupShapeType.Rectangle:
                double rx = Math.Min(point1.X, point2.X);
                double ry = Math.Min(point1.Y, point2.Y);
                Canvas.SetLeft(ShapeRectangle, rx);
                Canvas.SetTop(ShapeRectangle, ry);
                ShapeRectangle.Width = Math.Max(1, Math.Abs(point2.X - point1.X));
                ShapeRectangle.Height = Math.Max(1, Math.Abs(point2.Y - point1.Y));
                break;

            case MarkupShapeType.Ellipse:
                double ex = Math.Min(point1.X, point2.X);
                double ey = Math.Min(point1.Y, point2.Y);
                Canvas.SetLeft(ShapeEllipse, ex);
                Canvas.SetTop(ShapeEllipse, ey);
                ShapeEllipse.Width = Math.Max(1, Math.Abs(point2.X - point1.X));
                ShapeEllipse.Height = Math.Max(1, Math.Abs(point2.Y - point1.Y));
                break;
        }
    }

    private void UpdateArrowHead()
    {
        double dx = point2.X - point1.X;
        double dy = point2.Y - point1.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) return;

        double arrowSize = Math.Max(14, strokeThickness * 4);
        double angle = Math.Atan2(dy, dx);

        Point tip = point2;
        Point b1 = new(
            tip.X - arrowSize * Math.Cos(angle - Math.PI / 6),
            tip.Y - arrowSize * Math.Sin(angle - Math.PI / 6));
        Point b2 = new(
            tip.X - arrowSize * Math.Cos(angle + Math.PI / 6),
            tip.Y - arrowSize * Math.Sin(angle + Math.PI / 6));

        ArrowHead.Points = [tip, b1, b2];
    }

    private void HandlePoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse ellipse || ellipse.Tag is not string s)
            return;
        pointDraggingIndex = int.Parse(s);
        MeasurementPointMouseDown?.Invoke(sender, e);
    }

    public void MovePoint(int pointIndex, Point newPosition)
    {
        if (pointIndex == 0) point1 = newPosition;
        else if (pointIndex == 1) point2 = newPosition;
        UpdatePositions();
    }

    public void StartDraggingPoint(int pointIndex)
    {
        pointDraggingIndex = pointIndex;
        MeasurementPointMouseDown?.Invoke(
            pointIndex == 0 ? Point1Handle : Point2Handle, null!);
    }

    public int GetActivePointIndex() => pointDraggingIndex;

    public void ResetActivePoint() => pointDraggingIndex = -1;

    public (Point Point1, Point Point2) GetPoints() => (point1, point2);

    private void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RemoveControlRequested?.Invoke(this, EventArgs.Empty);
    }

    public MarkupShapeDto ToDto()
    {
        return new MarkupShapeDto
        {
            ShapeType = shapeType,
            Point1 = point1,
            Point2 = point2,
            StrokeColor = strokeColor.ToString(),
            StrokeThickness = strokeThickness
        };
    }

    public void FromDto(MarkupShapeDto dto)
    {
        shapeType = dto.ShapeType;
        point1 = dto.Point1;
        point2 = dto.Point2;

        try { strokeColor = (Color)ColorConverter.ConvertFromString(dto.StrokeColor); }
        catch { strokeColor = Colors.Red; }

        strokeThickness = dto.StrokeThickness;
        UpdateShapeVisibility();
        UpdateColors();
        UpdateThickness();
        UpdatePositions();
    }
}
