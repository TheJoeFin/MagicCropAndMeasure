using ImageMagick;
using System.Windows;

namespace MagickCrop.Helpers;

/// <summary>
/// Corrects perspective distortion for tri-folded paper images.
/// The paper is divided into three panels by two horizontal fold lines.
/// Eight control points define the geometry:
///   TL ──── TR       (outer corners)
///   │ Panel1 │
///  UFL ──── UFR      (upper fold)
///   │ Panel2 │
///  LFL ──── LFR      (lower fold)
///   │ Panel3 │
///   BL ──── BR       (outer corners)
/// Each panel is independently perspective-corrected and then stitched vertically.
/// </summary>
public static class TriFoldCorrector
{
    /// <summary>
    /// Corrects a tri-folded paper image using 8 control points.
    /// </summary>
    /// <param name="imagePath">Path to the source image file.</param>
    /// <param name="topLeft">Top-left corner of the paper (display coordinates).</param>
    /// <param name="topRight">Top-right corner of the paper (display coordinates).</param>
    /// <param name="upperFoldLeft">Left point of the upper fold line (display coordinates).</param>
    /// <param name="upperFoldRight">Right point of the upper fold line (display coordinates).</param>
    /// <param name="lowerFoldLeft">Left point of the lower fold line (display coordinates).</param>
    /// <param name="lowerFoldRight">Right point of the lower fold line (display coordinates).</param>
    /// <param name="bottomLeft">Bottom-left corner of the paper (display coordinates).</param>
    /// <param name="bottomRight">Bottom-right corner of the paper (display coordinates).</param>
    /// <param name="scaleFactor">Scale from display coordinates to image pixel coordinates.</param>
    /// <returns>A stitched <see cref="MagickImage"/> with the three corrected panels, or null on failure.</returns>
    public static async Task<MagickImage?> CorrectTriFoldAsync(
        string imagePath,
        Point topLeft, Point topRight,
        Point upperFoldLeft, Point upperFoldRight,
        Point lowerFoldLeft, Point lowerFoldRight,
        Point bottomLeft, Point bottomRight,
        double scaleFactor)
    {
        // Scale all points from display to image-pixel space
        Point sTL = Scale(topLeft, scaleFactor);
        Point sTR = Scale(topRight, scaleFactor);
        Point sUFL = Scale(upperFoldLeft, scaleFactor);
        Point sUFR = Scale(upperFoldRight, scaleFactor);
        Point sLFL = Scale(lowerFoldLeft, scaleFactor);
        Point sLFR = Scale(lowerFoldRight, scaleFactor);
        Point sBL = Scale(bottomLeft, scaleFactor);
        Point sBR = Scale(bottomRight, scaleFactor);

        // Determine output width from the widest panel edge
        double outputWidth = Math.Max(
            Math.Max(Distance(sTL, sTR), Distance(sUFL, sUFR)),
            Math.Max(Distance(sLFL, sLFR), Distance(sBL, sBR)));

        // Determine panel heights proportionally from the source geometry
        double panel1Height = (AverageY(sUFL, sUFR) - AverageY(sTL, sTR));
        double panel2Height = (AverageY(sLFL, sLFR) - AverageY(sUFL, sUFR));
        double panel3Height = (AverageY(sBL, sBR) - AverageY(sLFL, sLFR));

        // Ensure all heights are positive (in case points are mis-ordered)
        panel1Height = Math.Abs(panel1Height);
        panel2Height = Math.Abs(panel2Height);
        panel3Height = Math.Abs(panel3Height);

        // Enforce minimum panel height
        const double minHeight = 10;
        panel1Height = Math.Max(minHeight, panel1Height);
        panel2Height = Math.Max(minHeight, panel2Height);
        panel3Height = Math.Max(minHeight, panel3Height);

        int outW = (int)Math.Round(outputWidth);
        int outH1 = (int)Math.Round(panel1Height);
        int outH2 = (int)Math.Round(panel2Height);
        int outH3 = (int)Math.Round(panel3Height);

        return await Task.Run(() =>
        {
            using MagickImage source = new(imagePath);

            // Correct each panel independently
            using IMagickImage<ushort> panel1 = CorrectPanel(source, sTL, sTR, sUFR, sUFL, outW, outH1);
            using IMagickImage<ushort> panel2 = CorrectPanel(source, sUFL, sUFR, sLFR, sLFL, outW, outH2);
            using IMagickImage<ushort> panel3 = CorrectPanel(source, sLFL, sLFR, sBR, sBL, outW, outH3);

            // Stitch panels vertically
            MagickImage result = new(MagickColors.White, (uint)outW, (uint)(outH1 + outH2 + outH3))
            {
                Format = source.Format // Inherit format so WriteAsync can encode properly
            };
            result.Composite(panel1, 0, 0, CompositeOperator.Over);
            result.Composite(panel2, 0, outH1, CompositeOperator.Over);
            result.Composite(panel3, 0, outH1 + outH2, CompositeOperator.Over);

            return result;
        });
    }

    /// <summary>
    /// Applies a perspective distortion to map a quadrilateral region of the source image
    /// to a rectangular output of the given dimensions.
    /// </summary>
    private static IMagickImage<ushort> CorrectPanel(
        MagickImage source,
        Point tl, Point tr, Point br, Point bl,
        int outputWidth, int outputHeight)
    {
        IMagickImage<ushort> clone = source.Clone();

        double[] arguments =
        [
            // source TL → dest TL
            tl.X, tl.Y, 0, 0,
            // source TR → dest TR
            tr.X, tr.Y, outputWidth, 0,
            // source BR → dest BR
            br.X, br.Y, outputWidth, outputHeight,
            // source BL → dest BL
            bl.X, bl.Y, 0, outputHeight,
        ];

        // Distort without Bestfit so the output keeps source dimensions.
        // The perspective transform maps our source quad onto (0,0)→(outputWidth, outputHeight).
        clone.Distort(DistortMethod.Perspective, arguments);

        // Crop to extract only the corrected panel region
        clone.Crop(new MagickGeometry(0, 0, (uint)outputWidth, (uint)outputHeight));
        clone.ResetPage();

        return clone;
    }

    private static Point Scale(Point p, double factor) => new(p.X * factor, p.Y * factor);

    private static double Distance(Point a, Point b) =>
        Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    private static double AverageY(Point a, Point b) => (a.Y + b.Y) / 2.0;
}
