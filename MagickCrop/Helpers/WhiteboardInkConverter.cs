using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Ink;
using System.Windows.Input;
using WpfColor = System.Windows.Media.Color;

namespace MagickCrop.Helpers;

public static class WhiteboardInkConverter
{
    private const int MinComponentPixels = 50;
    private const double SimplifyEpsilon = 2.0;
    private const int DefaultSpeckleMinArea = 250;

    // HSV saturation threshold: pixels with S > this are considered colored ink
    // (blue/red/green markers on white; white background has S ≈ 0)
    private const int SaturationThreshold = 30;

    public static async Task<List<Stroke>> ConvertToStrokesAsync(
        string imagePath, double displayWidth, double displayHeight)
    {
        return await Task.Run(() => Convert(imagePath, displayWidth, displayHeight));
    }

    /// <summary>
    /// Removes small isolated marks (speckles/dirt) from a whiteboard image.
    /// Detects marks via HSV saturation (colored ink) and grayscale darkness.
    /// Returns the path to a cleaned temp PNG, or null if no speckles were found.
    /// </summary>
    public static async Task<string?> RemoveSpecklesAsync(string imagePath, int minArea = DefaultSpeckleMinArea)
    {
        return await Task.Run(() => RemoveSpeckles(imagePath, minArea));
    }

    /// <summary>
    /// Estimates the dominant stroke width in image pixels using the distance transform
    /// at skeleton pixels (75th-percentile diameter). Useful for diagnosing why conversion
    /// produces strokes that are too thick or too thin.
    /// </summary>
    public static async Task<double> DetectStrokeWidthAsync(string imagePath)
    {
        return await Task.Run(() => DetectStrokeWidth(imagePath));
    }

    /// <summary>
    /// Fills hollow interiors of thick whiteboard marker strokes so each stroke is a solid
    /// blob before skeletonization. Thick markers often produce "ring" shaped regions in the
    /// binary image; this preprocessing step corrects that so the centerline is found cleanly.
    /// Returns the path to a modified temp PNG, or null if no hollow regions were found.
    /// </summary>
    public static async Task<string?> FillHollowStrokesAsync(string imagePath)
    {
        return await Task.Run(() => FillHollowStrokes(imagePath));
    }

    // Builds a binary mask where white = ink stroke, black = background.
    // Detects BOTH colored markers (via HSV saturation) and dark markers (via adaptive
    // grayscale threshold), then OR-combines them. This is critical for colored markers
    // (blue, red, etc.) which are not "dark" in grayscale and are missed by the gray-only path.
    private static Mat CreateStrokeMask(Mat bgr)
    {
        using Mat hsv = new();
        CvInvoke.CvtColor(bgr, hsv, ColorConversion.Bgr2Hsv);

        Mat[] hsvChannels = hsv.Split();
        using Mat satChannel = hsvChannels[1]; // S: 0=gray/white, 255=fully saturated color
        using Mat valChannel = hsvChannels[2]; // V: brightness
        hsvChannels[0].Dispose();

        // Colored strokes: pixels with meaningful saturation
        using Mat satMask = new();
        CvInvoke.Threshold(satChannel, satMask, SaturationThreshold, 255, ThresholdType.Binary);

        // Exclude pixels that are near-pure-white (background bleeds through at S boundary)
        using Mat whiteMask = new();
        CvInvoke.Threshold(valChannel, whiteMask, 245, 255, ThresholdType.Binary);
        using Mat notWhite = new();
        CvInvoke.BitwiseNot(whiteMask, notWhite);

        using Mat coloredStrokes = new();
        CvInvoke.BitwiseAnd(satMask, notWhite, coloredStrokes);

        // Dark strokes: adaptive threshold on grayscale (catches black markers and edges)
        using Mat gray = new();
        CvInvoke.CvtColor(bgr, gray, ColorConversion.Bgr2Gray);
        using Mat darkMask = new();
        CvInvoke.AdaptiveThreshold(gray, darkMask, 255,
            AdaptiveThresholdType.GaussianC, ThresholdType.BinaryInv, 11, 5);

        // Combined: colored OR dark
        Mat combined = new();
        CvInvoke.BitwiseOr(coloredStrokes, darkMask, combined);
        return combined;
    }

    private static string? RemoveSpeckles(string imagePath, int minArea)
    {
        using Mat bgr = CvInvoke.Imread(imagePath, ImreadModes.AnyColor);
        if (bgr.IsEmpty) return null;

        using Mat binary = CreateStrokeMask(bgr);

        using Mat labels = new();
        int numLabels = CvInvoke.ConnectedComponents(binary, labels, LineType.EightConnected, DepthType.Cv32S);

        int rows = labels.Rows;
        int cols = labels.Cols;
        int labelStep = labels.Step;
        byte[] labelsData = new byte[rows * labelStep];
        Marshal.Copy(labels.DataPointer, labelsData, 0, labelsData.Length);

        int[] areaCounts = new int[numLabels];
        for (int y = 0; y < rows; y++)
        {
            int ry = y * labelStep;
            for (int x = 0; x < cols; x++)
            {
                int lbl = BitConverter.ToInt32(labelsData, ry + x * 4);
                if (lbl > 0) areaCounts[lbl]++;
            }
        }

        bool[] isSpeckle = new bool[numLabels];
        bool anySpeckle = false;
        for (int i = 1; i < numLabels; i++)
        {
            if (areaCounts[i] < minArea)
            {
                isSpeckle[i] = true;
                anySpeckle = true;
            }
        }

        if (!anySpeckle) return null;

        // Estimate background color from near-white pixels
        using Mat gray = new();
        CvInvoke.CvtColor(bgr, gray, ColorConversion.Bgr2Gray);
        using Mat lightMask = new();
        CvInvoke.Threshold(gray, lightMask, 200, 255, ThresholdType.Binary);
        MCvScalar bgColor = CvInvoke.Mean(bgr, lightMask);

        using Mat speckleMask = new(bgr.Size, DepthType.Cv8U, 1);
        speckleMask.SetTo(new MCvScalar(0));
        int mStep = speckleMask.Step;
        byte[] maskData = new byte[rows * mStep];

        for (int y = 0; y < rows; y++)
        {
            int ry = y * labelStep;
            int my = y * mStep;
            for (int x = 0; x < cols; x++)
            {
                int lbl = BitConverter.ToInt32(labelsData, ry + x * 4);
                if (lbl > 0 && isSpeckle[lbl])
                    maskData[my + x] = 255;
            }
        }
        Marshal.Copy(maskData, 0, speckleMask.DataPointer, maskData.Length);

        using Mat result = bgr.Clone();
        result.SetTo(bgColor, speckleMask);

        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
        CvInvoke.Imwrite(tempPath, result);
        return tempPath;
    }

    private static double DetectStrokeWidth(string imagePath)
    {
        using Mat bgr = CvInvoke.Imread(imagePath, ImreadModes.AnyColor);
        if (bgr.IsEmpty) return 0;

        using Mat binary = CreateStrokeMask(bgr);

        using Mat morphKernel = CvInvoke.GetStructuringElement(
            MorphShapes.Ellipse, new Size(3, 3), new Point(-1, -1));
        using Mat closed = new();
        CvInvoke.MorphologyEx(binary, closed, MorphOp.Close, morphKernel,
            new Point(-1, -1), 1, BorderType.Default, new MCvScalar());

        using Mat skeleton = Skeletonize(closed);
        using Mat dist = new();
        CvInvoke.DistanceTransform(closed, dist, null, DistType.L2, 5);

        int rows = skeleton.Rows, cols = skeleton.Cols;
        int skelStep = skeleton.Step, distStep = dist.Step;

        byte[] skelData = new byte[rows * skelStep];
        Marshal.Copy(skeleton.DataPointer, skelData, 0, skelData.Length);
        byte[] distData = new byte[rows * distStep];
        Marshal.Copy(dist.DataPointer, distData, 0, distData.Length);

        List<float> radii = [];
        for (int y = 0; y < rows; y++)
        {
            int sy = y * skelStep, dy = y * distStep;
            for (int x = 0; x < cols; x++)
            {
                if (skelData[sy + x] == 0) continue;
                float r = BitConverter.ToSingle(distData, dy + x * 4);
                if (r > 0.5f) radii.Add(r);
            }
        }

        if (radii.Count == 0) return 3.0;
        radii.Sort();
        return Math.Round(radii[(int)(radii.Count * 0.75)] * 2.0, 1);
    }

    private static string? FillHollowStrokes(string imagePath)
    {
        using Mat bgr = CvInvoke.Imread(imagePath, ImreadModes.AnyColor);
        if (bgr.IsEmpty) return null;

        using Mat binary = CreateStrokeMask(bgr);

        using Mat morphKernel = CvInvoke.GetStructuringElement(
            MorphShapes.Ellipse, new Size(3, 3), new Point(-1, -1));
        using Mat closed = new();
        CvInvoke.MorphologyEx(binary, closed, MorphOp.Close, morphKernel,
            new Point(-1, -1), 1, BorderType.Default, new MCvScalar());

        // RETR_EXTERNAL finds only outermost contours; filling them covers hollow interiors
        using VectorOfVectorOfPoint contours = new();
        using Mat hierarchy = new();
        CvInvoke.FindContours(closed, contours, hierarchy, RetrType.External, ChainApproxMethod.ChainApproxSimple);

        using Mat filledMask = new(bgr.Size, DepthType.Cv8U, 1);
        filledMask.SetTo(new MCvScalar(0));
        CvInvoke.DrawContours(filledMask, contours, -1, new MCvScalar(255), -1);

        // Hollow pixels: inside a filled contour but not in the original mask
        using Mat notClosed = new();
        CvInvoke.BitwiseNot(closed, notClosed);
        using Mat hollowMask = new();
        CvInvoke.BitwiseAnd(filledMask, notClosed, hollowMask);

        if (CvInvoke.CountNonZero(hollowMask) == 0) return null;

        MCvScalar inkColor = CvInvoke.Mean(bgr, closed);

        using Mat result = bgr.Clone();
        result.SetTo(inkColor, hollowMask);

        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
        CvInvoke.Imwrite(tempPath, result);
        return tempPath;
    }

    private static List<Stroke> Convert(string imagePath, double displayWidth, double displayHeight)
    {
        using Mat bgr = CvInvoke.Imread(imagePath, ImreadModes.AnyColor);
        if (bgr.IsEmpty) return [];

        WpfColor penColor = EstimatePenColor(bgr);

        using Mat binary = CreateStrokeMask(bgr);

        using Mat morphKernel = CvInvoke.GetStructuringElement(
            MorphShapes.Ellipse, new Size(3, 3), new Point(-1, -1));
        using Mat closed = new();
        CvInvoke.MorphologyEx(binary, closed, MorphOp.Close, morphKernel,
            new Point(-1, -1), 1, BorderType.Default, new MCvScalar());

        using Mat skeleton = Skeletonize(closed);

        using Mat dist = new();
        CvInvoke.DistanceTransform(closed, dist, null, DistType.L2, 5);

        double strokeThickness = EstimateStrokeThickness(dist, skeleton, displayWidth / bgr.Width);

        using Mat labels = new();
        int numLabels = CvInvoke.ConnectedComponents(
            skeleton, labels, LineType.EightConnected, DepthType.Cv32S);

        double scaleX = displayWidth / bgr.Width;
        double scaleY = displayHeight / bgr.Height;

        Dictionary<int, List<Point>> components = CollectComponents(labels, numLabels);
        List<Stroke> result = [];

        foreach ((int _, List<Point>? pixels) in components)
        {
            if (pixels.Count < MinComponentPixels) continue;

            foreach (List<Point> path in ExtractPathsFromSkeleton(pixels))
            {
                List<Point> simplified = SimplifyPath(path, SimplifyEpsilon);
                if (simplified.Count < 2) continue;

                StylusPointCollection spc = new(
                    simplified.Select(p => new StylusPoint(p.X * scaleX, p.Y * scaleY, 0.5f)));

                DrawingAttributes attrs = new()
                {
                    Color = penColor,
                    Width = strokeThickness,
                    Height = strokeThickness,
                    StylusTip = StylusTip.Ellipse,
                };

                result.Add(new Stroke(spc, attrs));
            }
        }

        return result;
    }

    // Samples colored (high-saturation) pixels to estimate ink color.
    // Falls back to dark-pixel detection for black/gray markers.
    private static WpfColor EstimatePenColor(Mat bgr)
    {
        using Mat hsv = new();
        CvInvoke.CvtColor(bgr, hsv, ColorConversion.Bgr2Hsv);

        Mat[] hsvChannels = hsv.Split();
        using Mat satChannel = hsvChannels[1];
        using Mat valChannel = hsvChannels[2];
        hsvChannels[0].Dispose();

        using Mat satMask = new();
        CvInvoke.Threshold(satChannel, satMask, SaturationThreshold + 10, 255, ThresholdType.Binary);

        // Exclude very bright pixels (background bleed-through near edges)
        using Mat whiteMask = new();
        CvInvoke.Threshold(valChannel, whiteMask, 240, 255, ThresholdType.Binary);
        using Mat notWhite = new();
        CvInvoke.BitwiseNot(whiteMask, notWhite);
        using Mat strokeMask = new();
        CvInvoke.BitwiseAnd(satMask, notWhite, strokeMask);

        if (CvInvoke.CountNonZero(strokeMask) >= 500)
        {
            MCvScalar mean = CvInvoke.Mean(bgr, strokeMask);
            return WpfColor.FromRgb(
                (byte)Math.Clamp(mean.V2, 0, 255),
                (byte)Math.Clamp(mean.V1, 0, 255),
                (byte)Math.Clamp(mean.V0, 0, 255));
        }

        // Fallback for black/gray markers: sample dark pixels
        using Mat gray = new();
        CvInvoke.CvtColor(bgr, gray, ColorConversion.Bgr2Gray);
        using Mat darkMask = new();
        CvInvoke.Threshold(gray, darkMask, 76, 255, ThresholdType.BinaryInv);
        MCvScalar darkMean = CvInvoke.Mean(bgr, darkMask);
        return WpfColor.FromRgb(
            (byte)Math.Clamp(darkMean.V2, 0, 255),
            (byte)Math.Clamp(darkMean.V1, 0, 255),
            (byte)Math.Clamp(darkMean.V0, 0, 255));
    }

    // Collects the distance-transform value at every skeleton pixel, sorts them, and returns
    // the 75th-percentile value × 2 as the stroke diameter.
    private static double EstimateStrokeThickness(Mat dist, Mat skeleton, double imageToDisplayScale)
    {
        int rows = skeleton.Rows;
        int cols = skeleton.Cols;
        int skelStep = skeleton.Step;
        int distStep = dist.Step;

        byte[] skelData = new byte[rows * skelStep];
        Marshal.Copy(skeleton.DataPointer, skelData, 0, skelData.Length);

        byte[] distData = new byte[rows * distStep];
        Marshal.Copy(dist.DataPointer, distData, 0, distData.Length);

        List<float> radii = [];
        for (int y = 0; y < rows; y++)
        {
            int sy = y * skelStep;
            int dy = y * distStep;
            for (int x = 0; x < cols; x++)
            {
                if (skelData[sy + x] == 0) continue;
                float r = BitConverter.ToSingle(distData, dy + x * 4);
                if (r > 0.5f) radii.Add(r);
            }
        }

        if (radii.Count == 0) return 3.0 * imageToDisplayScale;

        radii.Sort();
        float p75 = radii[(int)(radii.Count * 0.75)];

        return Math.Clamp(p75 * 2.0 * imageToDisplayScale, 1.5, 50.0);
    }

    private static Mat Skeletonize(Mat binary)
    {
        using Mat crossKernel = CvInvoke.GetStructuringElement(
            MorphShapes.Cross, new Size(3, 3), new Point(1, 1));

        Mat skeleton = new(binary.Size, DepthType.Cv8U, 1);
        skeleton.SetTo(new MCvScalar(0));

        Mat remaining = binary.Clone();
        using Mat eroded = new();
        using Mat temp = new();

        try
        {
            while (CvInvoke.CountNonZero(remaining) > 0)
            {
                CvInvoke.Erode(remaining, eroded, crossKernel,
                    new Point(-1, -1), 1, BorderType.Default, new MCvScalar());
                CvInvoke.Dilate(eroded, temp, crossKernel,
                    new Point(-1, -1), 1, BorderType.Default, new MCvScalar());
                CvInvoke.Subtract(remaining, temp, temp);
                CvInvoke.BitwiseOr(skeleton, temp, skeleton);
                eroded.CopyTo(remaining);
            }
        }
        finally
        {
            remaining.Dispose();
        }

        return skeleton;
    }

    private static Dictionary<int, List<Point>> CollectComponents(Mat labels, int numLabels)
    {
        int rows = labels.Rows;
        int cols = labels.Cols;
        int step = labels.Step;

        byte[] rawData = new byte[rows * step];
        Marshal.Copy(labels.DataPointer, rawData, 0, rawData.Length);

        Dictionary<int, List<Point>> components = new(numLabels);
        for (int y = 0; y < rows; y++)
        {
            int rowOffset = y * step;
            for (int x = 0; x < cols; x++)
            {
                int lbl = BitConverter.ToInt32(rawData, rowOffset + x * 4);
                if (lbl <= 0) continue;
                if (!components.TryGetValue(lbl, out List<Point>? pts))
                {
                    pts = [];
                    components[lbl] = pts;
                }
                pts.Add(new Point(x, y));
            }
        }

        return components;
    }

    // Extracts ordered path segments from a skeleton connected component by traversing
    // the pixel adjacency graph. Splits at junction pixels (3+ neighbors) so each
    // returned list is a smooth, non-branching sequence of pixels suitable for a stroke.
    private static List<List<Point>> ExtractPathsFromSkeleton(List<Point> pixels)
    {
        if (pixels.Count < 2) return [];

        HashSet<Point> pixelSet = [.. pixels];

        List<Point> GetNeighbors(Point p)
        {
            List<Point> ns = [];
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Point n = new(p.X + dx, p.Y + dy);
                    if (pixelSet.Contains(n)) ns.Add(n);
                }
            return ns;
        }

        Dictionary<Point, List<Point>> adj = new(pixels.Count);
        foreach (Point p in pixels)
            adj[p] = GetNeighbors(p);

        List<Point> endpoints = [.. pixels.Where(p => adj[p].Count == 1)];
        HashSet<Point> junctions = [.. pixels.Where(p => adj[p].Count >= 3)];

        HashSet<(Point, Point)> used = [];
        List<List<Point>> paths = [];

        void Follow(Point start, Point first)
        {
            if (used.Contains((start, first))) return;

            List<Point> path = [start, first];
            used.Add((start, first));
            used.Add((first, start));

            Point prev = start;
            Point cur = first;

            while (adj[cur].Count == 2)
            {
                Point next = default;
                bool found = false;
                foreach (Point n in adj[cur])
                {
                    if (n == prev || used.Contains((cur, n))) continue;
                    next = n;
                    found = true;
                    break;
                }
                if (!found) break;

                used.Add((cur, next));
                used.Add((next, cur));
                path.Add(next);
                prev = cur;
                cur = next;
            }

            if (path.Count >= 3)
                paths.Add(path);
        }

        foreach (Point ep in endpoints)
            foreach (Point n in adj[ep])
                Follow(ep, n);

        foreach (Point junc in junctions)
            foreach (Point n in adj[junc])
                Follow(junc, n);

        // Catch isolated loops (all pixels degree 2, no endpoints or junctions)
        foreach (Point p in pixels)
            foreach (Point n in adj[p])
                if (!used.Contains((p, n)))
                    Follow(p, n);

        return paths;
    }

    private static List<Point> SimplifyPath(List<Point> points, double epsilon)
    {
        if (points.Count <= 2) return points;

        using VectorOfPoint input = new([.. points]);
        using VectorOfPoint approx = new();
        CvInvoke.ApproxPolyDP(input, approx, epsilon, false);
        return [.. approx.ToArray()];
    }
}
