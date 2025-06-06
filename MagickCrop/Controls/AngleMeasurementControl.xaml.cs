using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MagickCrop.Models.MeasurementControls;

namespace MagickCrop.Controls;

public partial class AngleMeasurementControl : UserControl
{
    private Point point1Position = new(100, 100);
    private Point vertexPosition = new(300, 300);
    private Point point3Position = new(500, 100);
    private FrameworkElement? clickedElement;
    private int pointDraggingIndex = -1;
    private Point clickedPoint;

    public event MouseButtonEventHandler? MeasurementPointMouseDown;
    public event MouseEventHandler? MeasurementPointMouseMove;
    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    public AngleMeasurementControl()
    {
        InitializeComponent();
        UpdatePositions();
    }

    public void InitializePositions(double canvasWidth, double canvasHeight)
    {
        // Place points at reasonable starting positions
        vertexPosition = new Point(canvasWidth * 0.5, canvasHeight * 0.5);
        point1Position = new Point(canvasWidth * 0.3, canvasHeight * 0.3);
        point3Position = new Point(canvasWidth * 0.7, canvasHeight * 0.3);
        UpdatePositions();
    }

    /// <summary>
    /// Set hit testing for Point3 during placement operations
    /// </summary>
    /// <param name="isEnabled">Whether Point3 should be hit testable</param>
    public void SetPoint3HitTestable(bool isEnabled)
    {
        Point3.IsHitTestVisible = isEnabled;
    }

    private void UpdatePositions()
    {
        // Update the visual elements
        Canvas.SetLeft(Point1, point1Position.X - (Point1.Width / 2));
        Canvas.SetTop(Point1, point1Position.Y - (Point1.Height / 2));

        Canvas.SetLeft(VertexPoint, vertexPosition.X - (VertexPoint.Width / 2));
        Canvas.SetTop(VertexPoint, vertexPosition.Y - (VertexPoint.Height / 2));

        Canvas.SetLeft(Point3, point3Position.X - (Point3.Width / 2));
        Canvas.SetTop(Point3, point3Position.Y - (Point3.Height / 2));

        // Update lines
        Line1.X1 = point1Position.X;
        Line1.Y1 = point1Position.Y;
        Line1.X2 = vertexPosition.X;
        Line1.Y2 = vertexPosition.Y;

        Line2.X1 = vertexPosition.X;
        Line2.Y1 = vertexPosition.Y;
        Line2.X2 = point3Position.X;
        Line2.Y2 = point3Position.Y;

        // Calculate angle and update the arc and text
        double angle = CalculateAngle();
        UpdateAngleArc();

        AngleTextBlock.Text = $"{angle:F1}�";

        // Position the measurement text near the vertex
        Canvas.SetLeft(MeasurementText, vertexPosition.X + 15);
        Canvas.SetTop(MeasurementText, vertexPosition.Y - MeasurementText.ActualHeight - 5);
    }

    private double CalculateAngle()
    {
        // Calculate vectors from vertex to the two points
        Vector vector1 = new(point1Position.X - vertexPosition.X, point1Position.Y - vertexPosition.Y);
        Vector vector2 = new(point3Position.X - vertexPosition.X, point3Position.Y - vertexPosition.Y);

        // Normalize vectors
        vector1.Normalize();
        vector2.Normalize();

        // Calculate angle using dot product (gives angle between 0-180 degrees)
        double dotProduct = Vector.Multiply(vector1, vector2);
        dotProduct = Math.Clamp(dotProduct, -1.0, 1.0); // Clamp to prevent floating point errors
        double angleInRadians = Math.Acos(dotProduct);

        // Convert to degrees
        double angleInDegrees = angleInRadians * (180 / Math.PI);

        // Use cross product to determine the direction/orientation of the angle
        // In 2D, the cross product's Z component tells us if we're going clockwise or counterclockwise
        double crossProductZ = (vector1.X * vector2.Y) - (vector1.Y * vector2.X);

        // If cross product is negative, we need to use 360 - angle to get the correct angle in the 0-360 range
        if (crossProductZ < 0)
        {
            angleInDegrees = 360 - angleInDegrees;
        }

        return angleInDegrees;
    }

    private void UpdateAngleArc()
    {
        double arcRadius = 25;

        // Calculate vectors from vertex to points
        Vector vector1 = new(point1Position.X - vertexPosition.X, point1Position.Y - vertexPosition.Y);
        Vector vector2 = new(point3Position.X - vertexPosition.X, point3Position.Y - vertexPosition.Y);

        // Normalize vectors and scale to arc radius
        vector1.Normalize();
        vector2.Normalize();
        vector1 *= arcRadius;
        vector2 *= arcRadius;

        // Calculate arc points
        Point arcStart = new(vertexPosition.X + vector1.X, vertexPosition.Y + vector1.Y);
        Point arcEnd = new(vertexPosition.X + vector2.X, vertexPosition.Y + vector2.Y);

        // Calculate angle to determine arc size and direction
        double angle = CalculateAngle();
        bool isLargeArc = angle > 180;

        // Create path geometry for the arc
        PathGeometry pathGeometry = new();
        PathFigure pathFigure = new()
        {
            StartPoint = vertexPosition,
            IsClosed = true
        };

        // Add line segments to outer arc points
        pathFigure.Segments.Add(new LineSegment(arcStart, true));

        // Add arc segment
        ArcSegment arcSegment = new()
        {
            Point = arcEnd,
            Size = new Size(arcRadius, arcRadius),
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Clockwise,
        };
        pathFigure.Segments.Add(arcSegment);

        // Close path back to center
        pathFigure.Segments.Add(new LineSegment(vertexPosition, true));

        pathGeometry.Figures.Add(pathFigure);
        AngleArc.Data = pathGeometry;
    }

    private void MeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse ellipse || ellipse.Tag is not string intAsString)
            return;

        pointDraggingIndex = int.Parse(intAsString);
        clickedElement = ellipse;
        clickedPoint = e.GetPosition(MeasurementCanvas);

        MeasurementPointMouseDown?.Invoke(sender, e);
    }

    public void MovePoint(int pointIndex, Point newPosition)
    {
        switch (pointIndex)
        {
            case 0:
                point1Position = newPosition;
                break;
            case 1:
                vertexPosition = newPosition;
                break;
            case 2:
                point3Position = newPosition;
                break;
        }

        UpdatePositions();
    }

    public int GetActivePointIndex()
    {
        return pointDraggingIndex;
    }

    public void ResetActivePoint()
    {
        pointDraggingIndex = -1;
        clickedElement = null;
    }

    private void CopyMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string angle = AngleTextBlock.Text;
        Clipboard.SetText(angle);
    }

    private void MeasurementButton_Click(object sender, RoutedEventArgs e)
    {
        // Show context menu when button is clicked
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
        // Trigger the removal event so the parent canvas can remove this control
        RemoveControlRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Convert this control to a data transfer object
    /// </summary>
    public AngleMeasurementControlDto ToDto()
    {
        return new AngleMeasurementControlDto
        {
            Point1Position = point1Position,
            VertexPosition = vertexPosition,
            Point3Position = point3Position
        };
    }

    /// <summary>
    /// Apply data from a DTO to this control
    /// </summary>
    public void FromDto(AngleMeasurementControlDto dto)
    {
        point1Position = dto.Point1Position;
        vertexPosition = dto.VertexPosition;
        point3Position = dto.Point3Position;
        UpdatePositions();
    }
}
