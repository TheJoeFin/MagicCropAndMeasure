using ImageMagick;
using System.Diagnostics;
using System.Windows;

namespace MagickCrop.Helpers;

/// <summary>
/// Straightens a skewed or distorted image using a user-adjustable grid.
///
/// The user overlays an NxM grid on the image and drags intersection points
/// to align them with features in the distorted image. Each moved point
/// defines a source↔destination mapping: the user-placed position is where
/// that grid intersection currently appears in the image (source), and the
/// regular grid position is where it should be (destination).
///
/// The grid of control points is fed to ImageMagick's polynomial distortion
/// in a single pass, producing a smooth correction across the entire image.
/// </summary>
public static class GridStraightenHelper
{
    /// <summary>
    /// Applies grid-based straightening to the image.
    /// </summary>
    /// <param name="imagePath">Path to the source image file.</param>
    /// <param name="gridPoints">
    /// User-adjusted grid intersection points in display coordinates,
    /// stored row-major: [row * cols + col].
    /// </param>
    /// <param name="gridRows">Number of rows in the grid (including edges).</param>
    /// <param name="gridCols">Number of columns in the grid (including edges).</param>
    /// <param name="imageDisplayWidth">Display width of the image.</param>
    /// <param name="imageDisplayHeight">Display height of the image.</param>
    /// <param name="scaleFactor">Scale from display coordinates to image pixel coordinates.</param>
    /// <returns>A corrected <see cref="MagickImage"/>, or null on failure.</returns>
    public static async Task<MagickImage?> StraightenAsync(
        string imagePath,
        IReadOnlyList<Point> gridPoints,
        int gridRows,
        int gridCols,
        double imageDisplayWidth,
        double imageDisplayHeight,
        double scaleFactor)
    {
        Stopwatch sw = Stopwatch.StartNew();
        Debug.WriteLine($"[GridStraighten] Starting with {gridRows}x{gridCols} grid. scaleFactor={scaleFactor:F2}");

        if (gridPoints.Count != gridRows * gridCols)
        {
            Debug.WriteLine($"[GridStraighten] Grid point count mismatch: expected {gridRows * gridCols}, got {gridPoints.Count}");
            return null;
        }

        return await Task.Run(() =>
        {
            MagickImage source = new(imagePath);
            Debug.WriteLine($"[GridStraighten] Source image: {source.Width}x{source.Height}");

            int outW = (int)source.Width;
            int outH = (int)source.Height;

            double cellW = imageDisplayWidth / (gridCols - 1);
            double cellH = imageDisplayHeight / (gridRows - 1);

            // Build control point pairs for polynomial distortion:
            // source (user-placed position in image) → destination (regular grid position)
            // Format: [order, sx1, sy1, dx1, dy1, ...]
            int polyOrder = Math.Min(3, Math.Min(gridRows, gridCols));
            List<double> args = [polyOrder];

            for (int row = 0; row < gridRows; row++)
            {
                for (int col = 0; col < gridCols; col++)
                {
                    Point userPoint = gridPoints[row * gridCols + col];

                    // Source: where the point is in the image (user-dragged position)
                    double srcX = userPoint.X * scaleFactor;
                    double srcY = userPoint.Y * scaleFactor;

                    // Destination: where it should be in a regular grid
                    double dstX = col * cellW * scaleFactor;
                    double dstY = row * cellH * scaleFactor;

                    args.Add(srcX);
                    args.Add(srcY);
                    args.Add(dstX);
                    args.Add(dstY);
                }
            }

            Debug.WriteLine($"[GridStraighten] Built {gridRows * gridCols} control points in {sw.ElapsedMilliseconds}ms");

            source.VirtualPixelMethod = VirtualPixelMethod.Edge;
            source.SetArtifact("distort:viewport", $"{outW}x{outH}+0+0");
            source.Distort(DistortMethod.Polynomial, args.ToArray());
            source.ResetPage();

            Debug.WriteLine($"[GridStraighten] Complete in {sw.ElapsedMilliseconds}ms");
            return source;
        });
    }

    /// <summary>
    /// Generates the initial regular grid points in display coordinates.
    /// </summary>
    /// <param name="imageWidth">Display width of the image.</param>
    /// <param name="imageHeight">Display height of the image.</param>
    /// <param name="rows">Number of grid rows (including edges).</param>
    /// <param name="cols">Number of grid columns (including edges).</param>
    /// <returns>Grid points in row-major order.</returns>
    public static List<Point> GenerateRegularGrid(double imageWidth, double imageHeight, int rows, int cols)
    {
        List<Point> points = new(rows * cols);

        for (int row = 0; row < rows; row++)
        {
            double y = row * imageHeight / (rows - 1);
            for (int col = 0; col < cols; col++)
            {
                double x = col * imageWidth / (cols - 1);
                points.Add(new Point(x, y));
            }
        }

        return points;
    }
}
