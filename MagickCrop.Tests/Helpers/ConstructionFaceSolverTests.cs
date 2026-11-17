using MagickCrop.Helpers;
using MagickCrop.Models.Construction;
using System.Windows;

namespace MagickCrop.Tests.Helpers;

[TestClass]
public class ConstructionFaceSolverTests
{
    [TestMethod]
    public void UnionFaces_SelectionWithHole_IsRejected()
    {
        List<ConstructionFace> faces = BuildGridFaces();
        int centerIndex = faces.FindIndex(face =>
        {
            Point center = Centroid(face.Ring);
            return Math.Abs(center.X - 25) < 0.1 && Math.Abs(center.Y - 25) < 0.1;
        });

        Assert.IsTrue(centerIndex >= 0);

        List<int> selected = [.. Enumerable.Range(0, faces.Count).Where(index => index != centerIndex)];

        bool canUnion = ConstructionFaceSolver.TryUnionFaces(
            faces,
            selected,
            out List<List<Point>> rings,
            out bool containsHole);

        Assert.IsFalse(canUnion);
        Assert.IsTrue(containsHole);
        Assert.AreEqual(0, rings.Count);
        Assert.AreEqual(0, ConstructionFaceSolver.UnionFaces(faces, selected).Count);
    }

    [TestMethod]
    public void UnionFaces_DisconnectedSimpleSelection_ReturnsEachBoundary()
    {
        List<ConstructionFace> faces = BuildGridFaces();
        int leftIndex = faces.FindIndex(face => Centroid(face.Ring).X < 20);
        int rightIndex = faces.FindIndex(face => Centroid(face.Ring).X > 30);

        Assert.IsTrue(leftIndex >= 0);
        Assert.IsTrue(rightIndex >= 0);

        bool canUnion = ConstructionFaceSolver.TryUnionFaces(
            faces,
            [leftIndex, rightIndex],
            out List<List<Point>> rings,
            out bool containsHole);

        Assert.IsTrue(canUnion);
        Assert.IsFalse(containsHole);
        Assert.AreEqual(2, rings.Count);
    }

    private static List<ConstructionFace> BuildGridFaces()
    {
        List<(Guid Id, Point Start, Point End)> lines = [];

        foreach (double coordinate in new[] { 10d, 20d, 30d, 40d })
        {
            lines.Add((Guid.NewGuid(), new Point(coordinate, 5), new Point(coordinate, 45)));
            lines.Add((Guid.NewGuid(), new Point(5, coordinate), new Point(45, coordinate)));
        }

        List<ConstructionFace> faces = ConstructionFaceSolver.SolveFaces(lines, new Rect(0, 0, 50, 50));
        Assert.AreEqual(9, faces.Count);
        return faces;
    }

    private static Point Centroid(IReadOnlyList<Point> ring) =>
        new(ring.Average(point => point.X), ring.Average(point => point.Y));
}
