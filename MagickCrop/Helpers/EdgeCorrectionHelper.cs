using ImageMagick;
using System.Diagnostics;
using System.Windows;

namespace MagickCrop.Helpers;

/// <summary>
/// Corrects wavy/curved edges of a document after perspective correction.
/// 
/// The user places edge-correction points near the boundary of the image.
/// Each point is snapped to the nearest edge (top/right/bottom/left) along a
/// perpendicular path. These points define the actual curved boundary, and
/// the corrector uses the same transfinite-interpolation unwarp technique as
/// <see cref="UnWarpCorrector"/> to straighten the edges.
///
/// Workflow:
///   1. After perspective correction the image is roughly rectangular.
///   2. The user places points along any wavy edges.
///   3. Each point is assigned to the nearest rectangle edge.
///   4. Points on each edge are sorted and turned into a piecewise-linear
///      boundary curve (with the rectangle corners as endpoints).
///   5. Transfinite interpolation maps the curved boundary back to a
///      perfect rectangle.
/// </summary>
public static class EdgeCorrectionHelper
{
    private const int GridDivisions = 20;

    private enum Edge { Top, Right, Bottom, Left }

    /// <summary>
    /// Applies edge correction to straighten wavy edges of an already-cropped image.
    /// </summary>
    /// <param name="imagePath">Path to the source image file.</param>
    /// <param name="edgePoints">
    /// Points placed by the user near the edges of the image (display coordinates).
    /// Each point will be assigned to the nearest edge automatically.
    /// </param>
    /// <param name="imageDisplayWidth">Display width of the image.</param>
    /// <param name="imageDisplayHeight">Display height of the image.</param>
    /// <param name="scaleFactor">Scale from display coordinates to image pixel coordinates.</param>
    /// <returns>A corrected <see cref="MagickImage"/>, or null on failure.</returns>
    public static async Task<MagickImage?> CorrectEdgesAsync(
        string imagePath,
        IReadOnlyList<Point> edgePoints,
        double imageDisplayWidth,
        double imageDisplayHeight,
        double scaleFactor)
    {
        Stopwatch sw = Stopwatch.StartNew();
        Debug.WriteLine($"[EdgeCorrection] Starting with {edgePoints.Count} user points. scaleFactor={scaleFactor:F2}");

        if (edgePoints.Count == 0)
        {
            Debug.WriteLine("[EdgeCorrection] No edge points provided, aborting.");
            return null;
        }

        // Define the ideal rectangle corners in display coordinates
        Point dispTL = new(0, 0);
        Point dispTR = new(imageDisplayWidth, 0);
        Point dispBR = new(imageDisplayWidth, imageDisplayHeight);
        Point dispBL = new(0, imageDisplayHeight);

        // Assign each user point to the nearest edge
        List<Point> topPoints = [];
        List<Point> rightPoints = [];
        List<Point> bottomPoints = [];
        List<Point> leftPoints = [];

        foreach (Point pt in edgePoints)
        {
            Edge edge = ClassifyPointToEdge(pt, imageDisplayWidth, imageDisplayHeight);
            switch (edge)
            {
                case Edge.Top: topPoints.Add(pt); break;
                case Edge.Right: rightPoints.Add(pt); break;
                case Edge.Bottom: bottomPoints.Add(pt); break;
                case Edge.Left: leftPoints.Add(pt); break;
            }
        }

        Debug.WriteLine($"[EdgeCorrection] Points per edge: Top={topPoints.Count}, Right={rightPoints.Count}, Bottom={bottomPoints.Count}, Left={leftPoints.Count}");

        // Sort points along each edge
        topPoints.Sort((a, b) => a.X.CompareTo(b.X));
        rightPoints.Sort((a, b) => a.Y.CompareTo(b.Y));
        bottomPoints.Sort((a, b) => a.X.CompareTo(b.X));
        leftPoints.Sort((a, b) => a.Y.CompareTo(b.Y));

        // Build the source boundary curves (with corners as endpoints)
        // These represent where the actual image edge IS (the curved reality)
        Point[] topEdge = BuildEdgeCurve(dispTL, dispTR, topPoints, isHorizontal: true);
        Point[] rightEdge = BuildEdgeCurve(dispTR, dispBR, rightPoints, isHorizontal: false);
        Point[] bottomEdge = BuildEdgeCurve(dispBL, dispBR, bottomPoints, isHorizontal: true);
        Point[] leftEdge = BuildEdgeCurve(dispTL, dispBL, leftPoints, isHorizontal: false);

        // Scale everything to image pixel coordinates
        Point sTL = Scale(dispTL, scaleFactor);
        Point sTR = Scale(dispTR, scaleFactor);
        Point sBL = Scale(dispBL, scaleFactor);
        Point sBR = Scale(dispBR, scaleFactor);

        Point[] sTopEdge = ScaleArray(topEdge, scaleFactor);
        Point[] sRightEdge = ScaleArray(rightEdge, scaleFactor);
        Point[] sBottomEdge = ScaleArray(bottomEdge, scaleFactor);
        Point[] sLeftEdge = ScaleArray(leftEdge, scaleFactor);

        int outW = (int)Math.Round(sTR.X - sTL.X);
        int outH = (int)Math.Round(sBL.Y - sTL.Y);

        Debug.WriteLine($"[EdgeCorrection] Output size: {outW}x{outH}");

        if (outW < 10 || outH < 10)
        {
            Debug.WriteLine("[EdgeCorrection] Output too small, aborting.");
            return null;
        }

        return await Task.Run(() =>
        {
            MagickImage source = new(imagePath);
            Debug.WriteLine($"[EdgeCorrection] Source image: {source.Width}x{source.Height}");

            // Use the actual image size for the output
            outW = (int)source.Width;
            outH = (int)source.Height;

            // Build control point pairs using transfinite interpolation:
            // source (curved actual edge) → destination (perfect rectangle)
            List<double> args = [3.0]; // 3rd-order polynomial
            double cellW = (double)outW / GridDivisions;
            double cellH = (double)outH / GridDivisions;

            for (int row = 0; row <= GridDivisions; row++)
            {
                double v = (double)row / GridDivisions;
                for (int col = 0; col <= GridDivisions; col++)
                {
                    double u = (double)col / GridDivisions;

                    // Source point: transfinite interpolation of the curved edges
                    // Top/bottom edges indexed by col, left/right edges indexed by row
                    Point topPt = Lerp(sTopEdge[col], sBottomEdge[col], v);
                    Point leftPt = Lerp(sLeftEdge[row], sRightEdge[row], u);
                    Point bilinearPt = Lerp(
                        Lerp(sTopEdge[0], sTopEdge[GridDivisions], u),
                        Lerp(sBottomEdge[0], sBottomEdge[GridDivisions], u), v);

                    double srcX = topPt.X + leftPt.X - bilinearPt.X;
                    double srcY = topPt.Y + leftPt.Y - bilinearPt.Y;

                    // Destination: perfect rectangle grid
                    double dstX = col * cellW;
                    double dstY = row * cellH;

                    args.Add(srcX);
                    args.Add(srcY);
                    args.Add(dstX);
                    args.Add(dstY);
                }
            }

            Debug.WriteLine($"[EdgeCorrection] Built {(GridDivisions + 1) * (GridDivisions + 1)} control points in {sw.ElapsedMilliseconds}ms");

            source.VirtualPixelMethod = VirtualPixelMethod.Edge;
            source.SetArtifact("distort:viewport", $"{outW}x{outH}+0+0");
            source.Distort(DistortMethod.Polynomial, args.ToArray());
            source.ResetPage();

            Debug.WriteLine($"[EdgeCorrection] Complete in {sw.ElapsedMilliseconds}ms");
            return source;
        });
    }

    /// <summary>
    /// Classifies a point to the nearest rectangle edge based on perpendicular distance.
    /// </summary>
    private static Edge ClassifyPointToEdge(Point pt, double width, double height)
    {
        double distTop = pt.Y;
        double distBottom = height - pt.Y;
        double distLeft = pt.X;
        double distRight = width - pt.X;

        double min = Math.Min(Math.Min(distTop, distBottom), Math.Min(distLeft, distRight));

        if (min == distTop) return Edge.Top;
        if (min == distRight) return Edge.Right;
        if (min == distBottom) return Edge.Bottom;
        return Edge.Left;
    }

    /// <summary>
    /// Builds a sampled edge curve from corner to corner, incorporating user-placed
    /// edge points. The result has (GridDivisions + 1) evenly-spaced samples along
    /// the primary axis of the edge.
    /// </summary>
    /// <param name="start">Start corner of the edge.</param>
    /// <param name="end">End corner of the edge.</param>
    /// <param name="userPoints">User-placed points on this edge (sorted along the edge).</param>
    /// <param name="isHorizontal">True for top/bottom edges, false for left/right edges.</param>
    private static Point[] BuildEdgeCurve(Point start, Point end, List<Point> userPoints, bool isHorizontal)
    {
        // Build the full point list: start + user points + end
        List<Point> allPoints = [start, .. userPoints, end];

        Point[] result = new Point[GridDivisions + 1];

        for (int i = 0; i <= GridDivisions; i++)
        {
            double t = (double)i / GridDivisions;

            if (isHorizontal)
            {
                double x = start.X + t * (end.X - start.X);
                double y = InterpolateAlongEdge(allPoints, x, isHorizontal);
                result[i] = new Point(x, y);
            }
            else
            {
                double y = start.Y + t * (end.Y - start.Y);
                double x = InterpolateAlongEdge(allPoints, y, isHorizontal);
                result[i] = new Point(x, y);
            }
        }

        return result;
    }

    /// <summary>
    /// Linearly interpolates the perpendicular coordinate at a given position
    /// along the edge, using the sorted control points.
    /// </summary>
    private static double InterpolateAlongEdge(List<Point> sortedPoints, double position, bool isHorizontal)
    {
        if (sortedPoints.Count == 0)
            return position;

        // Find the two points that bracket the position
        for (int i = 0; i < sortedPoints.Count - 1; i++)
        {
            double pos0 = isHorizontal ? sortedPoints[i].X : sortedPoints[i].Y;
            double pos1 = isHorizontal ? sortedPoints[i + 1].X : sortedPoints[i + 1].Y;

            if (position >= pos0 && position <= pos1)
            {
                double range = pos1 - pos0;
                if (range < 0.001)
                {
                    double val0 = isHorizontal ? sortedPoints[i].Y : sortedPoints[i].X;
                    return val0;
                }

                double localT = (position - pos0) / range;
                double v0 = isHorizontal ? sortedPoints[i].Y : sortedPoints[i].X;
                double v1 = isHorizontal ? sortedPoints[i + 1].Y : sortedPoints[i + 1].X;
                return v0 + localT * (v1 - v0);
            }
        }

        // If position is outside range, clamp to nearest endpoint
        double firstPos = isHorizontal ? sortedPoints[0].X : sortedPoints[0].Y;
        double lastPos = isHorizontal ? sortedPoints[^1].X : sortedPoints[^1].Y;
        if (position <= firstPos)
            return isHorizontal ? sortedPoints[0].Y : sortedPoints[0].X;
        return isHorizontal ? sortedPoints[^1].Y : sortedPoints[^1].X;
    }

    /// <summary>
    /// Gets the edge assignment and snap information for a single point, useful
    /// for providing visual feedback as the user places points.
    /// </summary>
    /// <param name="point">The user-placed point in display coordinates.</param>
    /// <param name="imageWidth">Display width of the image.</param>
    /// <param name="imageHeight">Display height of the image.</param>
    /// <returns>
    /// A tuple of (edgeName, snappedPoint) where snappedPoint is the point projected
    /// perpendicularly onto the nearest edge.
    /// </returns>
    public static (string EdgeName, Point SnappedPoint) GetEdgeSnapInfo(Point point, double imageWidth, double imageHeight)
    {
        Edge edge = ClassifyPointToEdge(point, imageWidth, imageHeight);

        Point snapped = edge switch
        {
            Edge.Top => new Point(point.X, 0),
            Edge.Right => new Point(imageWidth, point.Y),
            Edge.Bottom => new Point(point.X, imageHeight),
            Edge.Left => new Point(0, point.Y),
            _ => point
        };

        string name = edge switch
        {
            Edge.Top => "Top",
            Edge.Right => "Right",
            Edge.Bottom => "Bottom",
            Edge.Left => "Left",
            _ => "Unknown"
        };

        return (name, snapped);
    }

    private static Point Scale(Point p, double factor) => new(p.X * factor, p.Y * factor);

    private static Point[] ScaleArray(Point[] points, double factor)
    {
        Point[] scaled = new Point[points.Length];
        for (int i = 0; i < points.Length; i++)
            scaled[i] = Scale(points[i], factor);
        return scaled;
    }

    private static Point Lerp(Point a, Point b, double t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}
