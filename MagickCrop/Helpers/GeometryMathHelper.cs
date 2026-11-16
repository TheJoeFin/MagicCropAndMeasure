using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MagickCrop.Helpers;

public static class GeometryMathHelper
{
    public static Point MidPoint(Point a, Point b) =>
        new((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);

    public static double Distance(Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public static Point GetEllipseCenter(Ellipse ellipse) =>
        new(Canvas.GetLeft(ellipse) + (ellipse.Width / 2),
            Canvas.GetTop(ellipse) + (ellipse.Height / 2));

    public static Point BezierControlFromPassThrough(Point start, Point passThrough, Point end) =>
        new(2 * passThrough.X - 0.5 * start.X - 0.5 * end.X,
            2 * passThrough.Y - 0.5 * start.Y - 0.5 * end.Y);

    public static PathGeometry BuildUnWarpPathGeometry(
        Point tl, Point tr, Point bl, Point br,
        Point mt, Point mr, Point mb, Point ml)
    {
        Point ctrlTop = BezierControlFromPassThrough(tl, mt, tr);
        Point ctrlRight = BezierControlFromPassThrough(tr, mr, br);

        PathFigure figure = new()
        {
            StartPoint = tl,
            IsClosed = true,
            IsFilled = false
        };

        // Top edge: TL → TR
        figure.Segments.Add(new QuadraticBezierSegment(ctrlTop, tr, true));
        // Right edge: TR → BR
        figure.Segments.Add(new QuadraticBezierSegment(ctrlRight, br, true));
        // Bottom edge: BR → BL (reverse direction)
        Point ctrlBottomRev = BezierControlFromPassThrough(br, mb, bl);
        figure.Segments.Add(new QuadraticBezierSegment(ctrlBottomRev, bl, true));
        // Left edge: BL → TL (reverse direction)
        Point ctrlLeftRev = BezierControlFromPassThrough(bl, ml, tl);
        figure.Segments.Add(new QuadraticBezierSegment(ctrlLeftRev, tl, true));

        PathGeometry geometry = new();
        geometry.Figures.Add(figure);
        return geometry;
    }

    public static double PolygonPerimeter(IReadOnlyList<Point> vertices, bool isClosed)
    {
        if (vertices is null || vertices.Count < 2) return 0;
        double perimeter = 0;
        for (int i = 0; i < vertices.Count; i++)
        {
            if (!isClosed && i == vertices.Count - 1) break;
            Point current = vertices[i];
            Point next = vertices[(i + 1) % vertices.Count];
            double dx = next.X - current.X;
            double dy = next.Y - current.Y;
            perimeter += Math.Sqrt(dx * dx + dy * dy);
        }
        return perimeter;
    }

    /// <summary>
    /// The unique circle through three points. Returns false when they are collinear
    /// (or two coincide), where no finite circle exists.
    /// </summary>
    public static bool TryGetCircumcircle(Point a, Point b, Point c, out Point center, out double radius)
    {
        center = default;
        radius = 0;

        // Twice the signed area of the triangle: zero exactly when the points are
        // collinear, and the denominator of the circumcenter either way.
        double d = 2 * ((a.X * (b.Y - c.Y)) + (b.X * (c.Y - a.Y)) + (c.X * (a.Y - b.Y)));

        if (Math.Abs(d) < 1e-9) return false;

        double aSquared = (a.X * a.X) + (a.Y * a.Y);
        double bSquared = (b.X * b.X) + (b.Y * b.Y);
        double cSquared = (c.X * c.X) + (c.Y * c.Y);

        double x = ((aSquared * (b.Y - c.Y)) + (bSquared * (c.Y - a.Y)) + (cSquared * (a.Y - b.Y))) / d;
        double y = ((aSquared * (c.X - b.X)) + (bSquared * (a.X - c.X)) + (cSquared * (b.X - a.X))) / d;

        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
            return false;

        center = new Point(x, y);
        radius = Distance(center, a);

        return !double.IsNaN(radius) && !double.IsInfinity(radius) && radius > 0;
    }

    public static double PolygonArea(IReadOnlyList<Point> vertices)
    {
        if (vertices is null || vertices.Count < 3) return 0;
        double area = 0;
        for (int i = 0; i < vertices.Count; i++)
        {
            Point a = vertices[i];
            Point b = vertices[(i + 1) % vertices.Count];
            area += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(area) * 0.5;
    }
}
