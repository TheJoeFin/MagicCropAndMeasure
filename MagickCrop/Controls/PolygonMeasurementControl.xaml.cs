using MagickCrop.Helpers;
using MagickCrop.Models.MeasurementControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MagickCrop.Controls;

public partial class PolygonMeasurementControl : UserControl
{
    private readonly List<Point> vertices = [];
    private readonly List<Ellipse> vertexPoints = [];
    private bool isClosed = false;
    private int pointDraggingIndex = -1;

    private double scaleFactor = 1.0;
    public double ScaleFactor
    {
        get => scaleFactor;
        set
        {
            scaleFactor = value;
            UpdateDisplay();
        }
    }

    private string units = "pixels";
    public string Units
    {
        get => units;
        set
        {
            units = value;
            UpdateDisplay();
        }
    }

    public bool IsClosed => isClosed;
    public int VertexCount => vertices.Count;

    public event MouseButtonEventHandler? MeasurementPointMouseDown;
    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    public PolygonMeasurementControl()
    {
        InitializeComponent();
    }

    public void AddVertex(Point vertex)
    {
        if (isClosed) return;

        System.Diagnostics.Debug.WriteLine($"AddVertex called: Point({vertex.X:F1}, {vertex.Y:F1}), Current vertex count: {vertices.Count}");

        // Check if clicking near first vertex to close polygon
        if (vertices.Count >= 3 && IsNearFirstVertex(vertex))
        {
            System.Diagnostics.Debug.WriteLine("Closing polygon - clicked near first vertex");
            ClosePolygon();
            return;
        }

        vertices.Add(vertex);
        CreateVertexPoint(vertex, vertices.Count - 1);
        UpdatePolygonPath();
        UpdateDisplay();

        // Highlight first vertex when we have enough vertices to close
        if (vertices.Count == 3)
        {
            UpdateFirstVertexAppearance();
        }

        System.Diagnostics.Debug.WriteLine($"Added vertex {vertices.Count}: Point({vertex.X:F1}, {vertex.Y:F1})");
    }

    public void UpdatePreviewLine(Point mousePosition)
    {
        if (isClosed || vertices.Count == 0)
        {
            PreviewLine.Visibility = Visibility.Collapsed;
            return;
        }

        Point lastVertex = vertices[^1];
        PreviewLine.X1 = lastVertex.X;
        PreviewLine.Y1 = lastVertex.Y;
        PreviewLine.X2 = mousePosition.X;
        PreviewLine.Y2 = mousePosition.Y;
        PreviewLine.Visibility = Visibility.Visible;
    }

    public void ClosePolygon()
    {
        if (vertices.Count < 3 || isClosed)
        {
            System.Diagnostics.Debug.WriteLine($"ClosePolygon early return: vertices.Count={vertices.Count}, isClosed={isClosed}");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"ClosePolygon: Closing polygon with {vertices.Count} vertices");
        isClosed = true;
        PreviewLine.Visibility = Visibility.Collapsed;

        // Re-enable hit testing on the polygon path now that it's closed
        PolygonPath.IsHitTestVisible = true;

        // Reset first vertex appearance back to normal
        ResetFirstVertexAppearance();

        UpdatePolygonPath();
        UpdateDisplay();

        System.Diagnostics.Debug.WriteLine("ClosePolygon: Polygon successfully closed");
    }

    private bool IsNearFirstVertex(Point point)
    {
        if (vertices.Count == 0) return false;
        Point firstVertex = vertices[0];
        double dx = point.X - firstVertex.X;
        double dy = point.Y - firstVertex.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        // Debug: Output distance for troubleshooting
        System.Diagnostics.Debug.WriteLine($"Distance from click to first vertex: {distance:F1}, Tolerance: {Defaults.VertexCloseTolerance}");

        return distance <= Defaults.VertexCloseTolerance;
    }

    private void CreateVertexPoint(Point position, int index)
    {
        Ellipse ellipse = new()
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0066FF")),
            Stroke = Brushes.White,
            StrokeThickness = 1,
            Opacity = 0.8,
            Cursor = Cursors.SizeAll,
            Tag = index.ToString()
        };

        // Normal appearance for all vertices initially

        ellipse.MouseDown += MeasurementPoint_MouseDown;

        Canvas.SetLeft(ellipse, position.X - ellipse.Width / 2);
        Canvas.SetTop(ellipse, position.Y - ellipse.Height / 2);

        vertexPoints.Add(ellipse);
        MeasurementCanvas.Children.Add(ellipse);
    }

    private void UpdatePolygonPath()
    {
        if (vertices.Count == 0)
        {
            PolygonPath.Data = null;
            return;
        }

        PathGeometry geometry = new();
        PathFigure figure = new() { StartPoint = vertices[0] };

        for (int i = 1; i < vertices.Count; i++)
        {
            figure.Segments.Add(new LineSegment(vertices[i], true));
        }

        if (isClosed && vertices.Count >= 3)
        {
            figure.IsClosed = true;
        }

        geometry.Figures.Add(figure);
        PolygonPath.Data = geometry;
    }

    private void UpdateVertexPositions()
    {
        for (int i = 0; i < vertexPoints.Count && i < vertices.Count; i++)
        {
            Ellipse ellipse = vertexPoints[i];
            Canvas.SetLeft(ellipse, vertices[i].X - ellipse.Width / 2);
            Canvas.SetTop(ellipse, vertices[i].Y - ellipse.Height / 2);
        }
    }

    private void UpdateFirstVertexAppearance()
    {
        if (vertexPoints.Count > 0 && !isClosed)
        {
            Ellipse firstVertex = vertexPoints[0];
            firstVertex.Width = 16;
            firstVertex.Height = 16;
            firstVertex.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6600"));
            firstVertex.StrokeThickness = 2;

            // Reposition after size change
            Canvas.SetLeft(firstVertex, vertices[0].X - firstVertex.Width / 2);
            Canvas.SetTop(firstVertex, vertices[0].Y - firstVertex.Height / 2);
        }
    }

    private void ResetFirstVertexAppearance()
    {
        if (vertexPoints.Count > 0)
        {
            Ellipse firstVertex = vertexPoints[0];
            // Reset to normal appearance
            firstVertex.Width = 12;
            firstVertex.Height = 12;
            firstVertex.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0066FF"));
            firstVertex.StrokeThickness = 1;

            // Reposition after size change
            Canvas.SetLeft(firstVertex, vertices[0].X - firstVertex.Width / 2);
            Canvas.SetTop(firstVertex, vertices[0].Y - firstVertex.Height / 2);
        }
    }

    private double CalculatePerimeter()
    {
        double p = GeometryMathHelper.PolygonPerimeter(vertices, isClosed);
        return p * ScaleFactor;
    }

    private double CalculateArea()
    {
        if (!isClosed) return 0;
        double a = GeometryMathHelper.PolygonArea(vertices);
        return a * ScaleFactor * ScaleFactor;
    }

    private void UpdateMeasurementText()
    {
        double perimeter = CalculatePerimeter();

        if (isClosed)
        {
            double area = CalculateArea();
            PolygonTextBlock.Text = MeasurementFormattingHelper.FormatPerimeterArea(perimeter, area, Units);
        }
        else
        {
            if (vertices.Count < 3)
            {
                PolygonTextBlock.Text = MeasurementFormattingHelper.FormatNeedMorePoints(perimeter, Units, 3 - vertices.Count);
            }
            else
            {
                PolygonTextBlock.Text = MeasurementFormattingHelper.FormatClickToClose(perimeter, Units);
            }
        }
    }

    private void PositionMeasurementText()
    {
        if (vertices.Count == 0) return;

        // Position text at the centroid of the polygon
        double centerX = vertices.Average(v => v.X);
        double centerY = vertices.Average(v => v.Y);

        Canvas.SetLeft(MeasurementText, centerX - (MeasurementText.ActualWidth / 2));
        Canvas.SetTop(MeasurementText, centerY - MeasurementText.ActualHeight - 10);
    }

    private void UpdateDisplay()
    {
        UpdateMeasurementText();
        PositionMeasurementText();
    }

    public void MovePoint(int pointIndex, Point newPosition)
    {
        if (pointIndex < 0 || pointIndex >= vertices.Count) return;

        vertices[pointIndex] = newPosition;
        UpdatePolygonPath();
        UpdateVertexPositions();
        UpdateDisplay();
    }

    public int GetActivePointIndex() => pointDraggingIndex;

    public void ResetActivePoint()
    {
        pointDraggingIndex = -1;
    }

    private void MeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse ellipse || ellipse.Tag is not string indexString)
            return;

        int vertexIndex = int.Parse(indexString);

        // Special handling: if clicking on first vertex and polygon is not closed and we have 3+ vertices, close the polygon
        if (!isClosed && vertexIndex == 0 && vertices.Count >= 3)
        {
            System.Diagnostics.Debug.WriteLine("Closing polygon via first vertex click");
            ClosePolygon();
            e.Handled = true;
            return;
        }

        // Normal dragging behavior for closed polygons
        if (isClosed)
        {
            pointDraggingIndex = vertexIndex;
            MeasurementPointMouseDown?.Invoke(sender, e);
        }

        e.Handled = true;
    }

    private void CopyMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string measurement = PolygonTextBlock.Text;
        Clipboard.SetText(measurement);
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

    public PolygonMeasurementControlDto ToDto()
    {
        return new PolygonMeasurementControlDto
        {
            Vertices = [.. vertices],
            ScaleFactor = ScaleFactor,
            Units = Units,
            IsClosed = isClosed
        };
    }

    public void FromDto(PolygonMeasurementControlDto dto)
    {
        System.Diagnostics.Debug.WriteLine($"FromDto: Restoring polygon with {dto.Vertices.Count} vertices, IsClosed: {dto.IsClosed}");

        // Clear existing vertices and points
        vertices.Clear();
        foreach (Ellipse ellipse in vertexPoints)
        {
            MeasurementCanvas.Children.Remove(ellipse);
        }
        vertexPoints.Clear();

        // Restore from DTO
        vertices.AddRange(dto.Vertices);
        isClosed = dto.IsClosed;
        ScaleFactor = dto.ScaleFactor;
        Units = dto.Units;

        // Recreate vertex points
        for (int i = 0; i < vertices.Count; i++)
        {
            CreateVertexPoint(vertices[i], i);
        }

        // Make sure hit testing is enabled for closed polygons
        if (isClosed)
        {
            PolygonPath.IsHitTestVisible = true;
        }

        UpdatePolygonPath();
        UpdateDisplay();

        System.Diagnostics.Debug.WriteLine($"FromDto: Polygon restoration complete");
    }
}