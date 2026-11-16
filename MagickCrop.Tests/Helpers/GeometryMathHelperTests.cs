using System.Windows;
using MagickCrop.Helpers;

namespace MagickCrop.Tests.Helpers;

[TestClass]
public class GeometryMathHelperTests
{
    [TestMethod]
    public void MidPoint_ReturnsAverageOfCoordinates()
    {
        Point result = GeometryMathHelper.MidPoint(new Point(0, 0), new Point(10, 20));

        Assert.AreEqual(5, result.X);
        Assert.AreEqual(10, result.Y);
    }

    [TestMethod]
    public void Distance_ForHorizontalSegment_ReturnsDeltaX()
    {
        double result = GeometryMathHelper.Distance(new Point(0, 0), new Point(3, 0));

        Assert.AreEqual(3, result, 1e-9);
    }

    [TestMethod]
    public void Distance_For3_4_5Triangle_ReturnsFive()
    {
        double result = GeometryMathHelper.Distance(new Point(0, 0), new Point(3, 4));

        Assert.AreEqual(5, result, 1e-9);
    }

    [TestMethod]
    public void PolygonPerimeter_ClosedTriangle_SumsAllThreeEdges()
    {
        Point[] triangle = [new(0, 0), new(4, 0), new(0, 3)];

        double result = GeometryMathHelper.PolygonPerimeter(triangle, isClosed: true);

        // 4 + 5 + 3 = 12
        Assert.AreEqual(12, result, 1e-9);
    }

    [TestMethod]
    public void PolygonPerimeter_OpenPolyline_ExcludesClosingEdge()
    {
        Point[] triangle = [new(0, 0), new(4, 0), new(0, 3)];

        double result = GeometryMathHelper.PolygonPerimeter(triangle, isClosed: false);

        // 4 + 5 = 9 (no closing edge back to the start)
        Assert.AreEqual(9, result, 1e-9);
    }

    [TestMethod]
    public void PolygonPerimeter_FewerThanTwoVertices_ReturnsZero()
    {
        Assert.AreEqual(0, GeometryMathHelper.PolygonPerimeter([], isClosed: true));
        Assert.AreEqual(0, GeometryMathHelper.PolygonPerimeter([new Point(1, 1)], isClosed: true));
    }

    [TestMethod]
    public void PolygonArea_UnitSquare_ReturnsOne()
    {
        Point[] square = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

        double result = GeometryMathHelper.PolygonArea(square);

        Assert.AreEqual(1, result, 1e-9);
    }

    [TestMethod]
    public void PolygonArea_FewerThanThreeVertices_ReturnsZero()
    {
        Point[] segment = [new(0, 0), new(1, 1)];

        Assert.AreEqual(0, GeometryMathHelper.PolygonArea(segment));
    }

    [TestMethod]
    public void TryGetCircumcircle_ForPointsOnUnitCircle_FindsOriginAndRadiusOne()
    {
        bool found = GeometryMathHelper.TryGetCircumcircle(
            new Point(1, 0), new Point(0, 1), new Point(-1, 0), out Point center, out double radius);

        Assert.IsTrue(found);
        Assert.AreEqual(0, center.X, 1e-9);
        Assert.AreEqual(0, center.Y, 1e-9);
        Assert.AreEqual(1, radius, 1e-9);
    }

    [TestMethod]
    public void TryGetCircumcircle_ForCollinearPoints_ReturnsFalse()
    {
        bool found = GeometryMathHelper.TryGetCircumcircle(
            new Point(0, 0), new Point(1, 1), new Point(2, 2), out _, out _);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void BezierControlFromPassThrough_ForMidpointOnStraightLine_ReturnsThatSamePoint()
    {
        Point start = new(0, 0);
        Point end = new(10, 0);
        Point mid = new(5, 0);

        Point control = GeometryMathHelper.BezierControlFromPassThrough(start, mid, end);

        Assert.AreEqual(5, control.X, 1e-9);
        Assert.AreEqual(0, control.Y, 1e-9);
    }
}
