using System.Windows;

namespace MagickCrop.Helpers;

public static class GeometryMathHelper
{
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
