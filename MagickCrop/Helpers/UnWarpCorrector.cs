using ImageMagick;
using System.Diagnostics;
using System.Windows;

namespace MagickCrop.Helpers;

/// <summary>
/// Corrects barrel/pincushion distortion (curved edges) by mapping a region
/// defined by four corners and four edge midpoints (which can be pulled to
/// create arcs) back to a straight rectangle.
///
/// The boundary is defined by:
///   TL ─── MidTop ─── TR
///   │                   │
///  MidLeft            MidRight
///   │                   │
///   BL ── MidBottom ── BR
///
/// Each edge is a quadratic Bézier curve through the two corners and the
/// midpoint control handle. A dense grid of source↔destination point pairs
/// is computed via transfinite interpolation and fed to ImageMagick's
/// polynomial distortion in a single pass — no tiling or compositing.
/// </summary>
public static class UnWarpCorrector
{
    private const int GridDivisions = 16;

    /// <summary>
    /// Un-warps a curved region of the source image to a straight rectangle.
    /// </summary>
    /// <param name="imagePath">Path to the source image file.</param>
    /// <param name="topLeft">Top-left corner (display coordinates).</param>
    /// <param name="topRight">Top-right corner (display coordinates).</param>
    /// <param name="bottomLeft">Bottom-left corner (display coordinates).</param>
    /// <param name="bottomRight">Bottom-right corner (display coordinates).</param>
    /// <param name="midTop">Midpoint handle for the top edge (display coordinates).</param>
    /// <param name="midRight">Midpoint handle for the right edge (display coordinates).</param>
    /// <param name="midBottom">Midpoint handle for the bottom edge (display coordinates).</param>
    /// <param name="midLeft">Midpoint handle for the left edge (display coordinates).</param>
    /// <param name="scaleFactor">Scale from display coordinates to image pixel coordinates.</param>
    /// <returns>A corrected <see cref="MagickImage"/>, or null on failure.</returns>
    public static async Task<MagickImage?> CorrectUnWarpAsync(
        string imagePath,
        Point topLeft, Point topRight,
        Point bottomLeft, Point bottomRight,
        Point midTop, Point midRight,
        Point midBottom, Point midLeft,
        double scaleFactor)
    {
        Stopwatch sw = Stopwatch.StartNew();
        Debug.WriteLine($"[UnWarp] Starting. scaleFactor={scaleFactor:F2}");

        Point sTL = Scale(topLeft, scaleFactor);
        Point sTR = Scale(topRight, scaleFactor);
        Point sBL = Scale(bottomLeft, scaleFactor);
        Point sBR = Scale(bottomRight, scaleFactor);
        Point sMT = Scale(midTop, scaleFactor);
        Point sMR = Scale(midRight, scaleFactor);
        Point sMB = Scale(midBottom, scaleFactor);
        Point sML = Scale(midLeft, scaleFactor);

        // Determine output dimensions from the bounding box of the corners
        double outputWidth = Math.Max(Distance(sTL, sTR), Distance(sBL, sBR));
        double outputHeight = Math.Max(Distance(sTL, sBL), Distance(sTR, sBR));

        int outW = (int)Math.Round(outputWidth);
        int outH = (int)Math.Round(outputHeight);

        Debug.WriteLine($"[UnWarp] Output size: {outW}x{outH}, control grid: {GridDivisions + 1}x{GridDivisions + 1}");

        if (outW < 10 || outH < 10)
        {
            Debug.WriteLine("[UnWarp] Output too small, aborting.");
            return null;
        }

        return await Task.Run(() =>
        {
            MagickImage source = new(imagePath);
            Debug.WriteLine($"[UnWarp] Source image: {source.Width}x{source.Height}");

            // Sample points along each edge using quadratic Bézier
            Point[] topEdge = SampleQuadraticBezier(sTL, sMT, sTR, GridDivisions);
            Point[] bottomEdge = SampleQuadraticBezier(sBL, sMB, sBR, GridDivisions);
            Point[] leftEdge = SampleQuadraticBezier(sTL, sML, sBL, GridDivisions);
            Point[] rightEdge = SampleQuadraticBezier(sTR, sMR, sBR, GridDivisions);

            // Build control point pairs for polynomial distortion:
            // source (curved image coords) → destination (output rectangle coords)
            // Format: [order, sx1, sy1, dx1, dy1, sx2, sy2, dx2, dy2, ...]
            List<double> args = [3.0]; // 3rd-order polynomial (10 terms)
            double cellW = (double)outW / GridDivisions;
            double cellH = (double)outH / GridDivisions;

            for (int row = 0; row <= GridDivisions; row++)
            {
                double v = (double)row / GridDivisions;
                for (int col = 0; col <= GridDivisions; col++)
                {
                    double u = (double)col / GridDivisions;

                    // Transfinite interpolation: edges minus bilinear of corners
                    Point topPt = Lerp(topEdge[col], bottomEdge[col], v);
                    Point leftPt = Lerp(leftEdge[row], rightEdge[row], u);
                    Point topLeftPt = Lerp(Lerp(sTL, sTR, u), Lerp(sBL, sBR, u), v);

                    double srcX = topPt.X + leftPt.X - topLeftPt.X;
                    double srcY = topPt.Y + leftPt.Y - topLeftPt.Y;

                    double dstX = col * cellW;
                    double dstY = row * cellH;

                    args.Add(srcX);
                    args.Add(srcY);
                    args.Add(dstX);
                    args.Add(dstY);
                }
            }

            Debug.WriteLine($"[UnWarp] Built {(GridDivisions + 1) * (GridDivisions + 1)} control points in {sw.ElapsedMilliseconds}ms");

            // Use ImageMagick's polynomial distortion in a single pass — no tiling
            source.VirtualPixelMethod = VirtualPixelMethod.Transparent;
            source.SetArtifact("distort:viewport", $"{outW}x{outH}+0+0");
            source.Distort(DistortMethod.Polynomial, args.ToArray());
            source.ResetPage();

            Debug.WriteLine($"[UnWarp] Complete in {sw.ElapsedMilliseconds}ms");
            return source;
        });
    }

    /// <summary>
    /// Samples a quadratic Bézier curve defined by start, control, end.
    /// The control point is the midpoint handle which the user can drag.
    /// When the handle sits exactly at the midpoint of start–end the curve is a straight line.
    /// </summary>
    private static Point[] SampleQuadraticBezier(Point p0, Point control, Point p2, int divisions)
    {
        // Convert the "pass-through" midpoint to a true Bézier control point.
        // For a quadratic Bézier B(0.5) = 0.25*P0 + 0.5*C + 0.25*P2 = control
        // => C = 2*control - 0.5*P0 - 0.5*P2
        Point c = new(
            2 * control.X - 0.5 * p0.X - 0.5 * p2.X,
            2 * control.Y - 0.5 * p0.Y - 0.5 * p2.Y);

        Point[] points = new Point[divisions + 1];
        for (int i = 0; i <= divisions; i++)
        {
            double t = (double)i / divisions;
            double mt = 1 - t;
            points[i] = new Point(
                mt * mt * p0.X + 2 * mt * t * c.X + t * t * p2.X,
                mt * mt * p0.Y + 2 * mt * t * c.Y + t * t * p2.Y);
        }

        return points;
    }

    /// <summary>
    /// Samples points along an edge for visualization (display coordinates).
    /// Returns points that trace the quadratic Bézier through the three control points.
    /// </summary>
    public static Point[] SampleEdgeCurve(Point start, Point mid, Point end, int segments = 20)
    {
        return SampleQuadraticBezier(start, mid, end, segments);
    }

    private static Point Scale(Point p, double factor) => new(p.X * factor, p.Y * factor);

    private static double Distance(Point a, Point b) =>
        Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    private static Point Lerp(Point a, Point b, double t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}
