using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System.Drawing;
using System.IO;
using System.Windows;

namespace MagickCrop.Helpers;

/// <summary>
/// Helper class for detecting quadrilaterals in images using OpenCV
/// </summary>
public static class QuadrilateralDetector
{
    // Detection parameters
    private const double DefaultMinArea = 0.05;
    private const int DefaultMaxResults = 5;
    
    // Confidence calculation weights
    private const double SizeWeight = 0.6;
    private const double RectangularityWeight = 0.4;
    
    /// <summary>
    /// Represents a detected quadrilateral with its corner points
    /// </summary>
    public class DetectedQuadrilateral
    {
        public System.Windows.Point TopLeft { get; set; }
        public System.Windows.Point TopRight { get; set; }
        public System.Windows.Point BottomRight { get; set; }
        public System.Windows.Point BottomLeft { get; set; }
        public double Area { get; set; }
        public double Confidence { get; set; }

        public DetectedQuadrilateral(System.Windows.Point[] points, double area, double confidence)
        {
            if (points.Length != 4)
                throw new ArgumentException("Quadrilateral must have exactly 4 points");

            // Order points: top-left, top-right, bottom-right, bottom-left
            var orderedPoints = OrderPoints(points);
            TopLeft = orderedPoints[0];
            TopRight = orderedPoints[1];
            BottomRight = orderedPoints[2];
            BottomLeft = orderedPoints[3];
            Area = area;
            Confidence = confidence;
        }

        private static System.Windows.Point[] OrderPoints(System.Windows.Point[] points)
        {
            // Sort by sum (x + y) to find top-left (smallest) and bottom-right (largest)
            var ordered = points.OrderBy(p => p.X + p.Y).ToArray();
            System.Windows.Point topLeft = ordered[0];
            System.Windows.Point bottomRight = ordered[3];

            // Sort remaining two by difference (x - y) to find top-right and bottom-left
            var remaining = new[] { ordered[1], ordered[2] };
            var orderedRemaining = remaining.OrderBy(p => p.X - p.Y).ToArray();
            System.Windows.Point bottomLeft = orderedRemaining[0];
            System.Windows.Point topRight = orderedRemaining[1];

            return new[] { topLeft, topRight, bottomRight, bottomLeft };
        }
    }

    /// <summary>
    /// Result of quadrilateral detection including image dimensions
    /// </summary>
    public class DetectionResult
    {
        public List<DetectedQuadrilateral> Quadrilaterals { get; set; } = [];
        public double ImageWidth { get; set; }
        public double ImageHeight { get; set; }
    }

    /// <summary>
    /// Detects quadrilaterals in the given image
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="minArea">Minimum area of quadrilaterals to detect (relative to image size, 0-1)</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <returns>List of detected quadrilaterals, sorted by confidence (highest first)</returns>
    public static List<DetectedQuadrilateral> DetectQuadrilaterals(string imagePath, double minArea = 0.05, int maxResults = 5)
    {
        return DetectQuadrilaterals(imagePath, out _, out _, minArea, maxResults);
    }

    /// <summary>
    /// Detects quadrilaterals in the given image and returns image dimensions
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="imageWidth">Output: width of the image</param>
    /// <param name="imageHeight">Output: height of the image</param>
    /// <param name="minArea">Minimum area of quadrilaterals to detect (relative to image size, 0-1)</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <returns>List of detected quadrilaterals, sorted by confidence (highest first)</returns>
    public static List<DetectedQuadrilateral> DetectQuadrilaterals(string imagePath, out double imageWidth, out double imageHeight, double minArea = 0.05, int maxResults = 5)
    {
        var result = DetectQuadrilateralsWithDimensions(imagePath, minArea, maxResults);
        imageWidth = result.ImageWidth;
        imageHeight = result.ImageHeight;
        return result.Quadrilaterals;
    }

    /// <summary>
    /// Detects quadrilaterals in the given image and returns result with image dimensions
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="minArea">Minimum area of quadrilaterals to detect (relative to image size, 0-1)</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <returns>Detection result including quadrilaterals and image dimensions</returns>
    public static DetectionResult DetectQuadrilateralsWithDimensions(string imagePath, double minArea = 0.05, int maxResults = 5)
    {
        var result = new DetectionResult();

        try
        {
            using var image = CvInvoke.Imread(imagePath, ImreadModes.Color);
            if (image.IsEmpty)
                return result;

            result.ImageWidth = image.Width;
            result.ImageHeight = image.Height;
            double imageArea = result.ImageWidth * result.ImageHeight;
            double minAreaPixels = imageArea * minArea;

            // Convert to grayscale
            using var gray = new Mat();
            CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);

            // Apply Gaussian blur to reduce noise
            using var blurred = new Mat();
            CvInvoke.GaussianBlur(gray, blurred, new System.Drawing.Size(5, 5), 0);

            // Apply Canny edge detection
            using var edges = new Mat();
            CvInvoke.Canny(blurred, edges, 50, 150);

            // Dilate to connect broken edges
            using var kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new System.Drawing.Size(3, 3), new System.Drawing.Point(-1, -1));
            using var dilated = new Mat();
            CvInvoke.Dilate(edges, dilated, kernel, new System.Drawing.Point(-1, -1), 1, BorderType.Constant, new MCvScalar());

            // Find contours
            using var contours = new VectorOfVectorOfPoint();
            using var hierarchy = new Mat();
            CvInvoke.FindContours(dilated, contours, hierarchy, RetrType.List, ChainApproxMethod.ChainApproxSimple);

            // Process contours to find quadrilaterals
            for (int i = 0; i < contours.Size; i++)
            {
                using var contour = contours[i];
                double area = CvInvoke.ContourArea(contour);

                // Skip small contours
                if (area < minAreaPixels)
                    continue;

                // Approximate contour to polygon
                double perimeter = CvInvoke.ArcLength(contour, true);
                using var approx = new VectorOfPoint();
                CvInvoke.ApproxPolyDP(contour, approx, 0.02 * perimeter, true);

                // Check if it's a quadrilateral (4 points)
                if (approx.Size == 4)
                {
                    var points = new System.Windows.Point[4];
                    for (int j = 0; j < 4; j++)
                    {
                        var pt = approx[j];
                        points[j] = new System.Windows.Point(pt.X, pt.Y);
                    }

                    // Check if quadrilateral is convex (to filter out invalid shapes)
                    if (IsConvex(points))
                    {
                        // Calculate confidence based on area ratio and shape regularity
                        double confidence = CalculateConfidence(points, area, imageArea);
                        result.Quadrilaterals.Add(new DetectedQuadrilateral(points, area, confidence));
                    }
                }
            }

            // Sort by confidence (highest first) and take top results
            result.Quadrilaterals = result.Quadrilaterals.OrderByDescending(q => q.Confidence).Take(maxResults).ToList();
        }
        catch (FileNotFoundException)
        {
            // Image file not found - return empty result
        }
        catch (Exception)
        {
            // OpenCV error or other exception - return empty result
            // Caller will handle empty result appropriately
        }

        return result;
    }

    /// <summary>
    /// Check if quadrilateral is convex
    /// </summary>
    private static bool IsConvex(System.Windows.Point[] points)
    {
        if (points.Length != 4)
            return false;

        // Check cross products to ensure all turns are in the same direction
        bool? firstSign = null;
        for (int i = 0; i < 4; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % 4];
            var p3 = points[(i + 2) % 4];

            double cross = (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
            bool currentSign = cross > 0;

            if (firstSign == null)
                firstSign = currentSign;
            else if (firstSign != currentSign)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Calculate confidence score for a quadrilateral
    /// </summary>
    private static double CalculateConfidence(System.Windows.Point[] points, double area, double imageArea)
    {
        // Confidence based on:
        // 1. Relative size (larger quadrilaterals are typically more relevant)
        // 2. How close to rectangular it is

        double sizeScore = Math.Min(area / imageArea, 1.0);

        // Calculate how "rectangular" the shape is by comparing angles to 90 degrees
        double angleScore = CalculateRectangularityScore(points);

        // Weighted combination
        return (SizeWeight * sizeScore) + (RectangularityWeight * angleScore);
    }

    /// <summary>
    /// Calculate how close the quadrilateral is to being rectangular
    /// </summary>
    private static double CalculateRectangularityScore(System.Windows.Point[] points)
    {
        if (points.Length != 4)
            return 0;

        double totalDeviation = 0;
        for (int i = 0; i < 4; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % 4];
            var p3 = points[(i + 2) % 4];

            // Calculate angle at p2
            double angle = CalculateAngle(p1, p2, p3);
            double deviation = Math.Abs(angle - 90.0);
            totalDeviation += deviation;
        }

        // Average deviation from 90 degrees
        double avgDeviation = totalDeviation / 4.0;

        // Convert to score (0-1, where 1 is perfect rectangle)
        // Max realistic deviation is about 45 degrees
        return Math.Max(0, 1.0 - (avgDeviation / 45.0));
    }

    /// <summary>
    /// Calculate angle between three points in degrees
    /// </summary>
    private static double CalculateAngle(System.Windows.Point p1, System.Windows.Point p2, System.Windows.Point p3)
    {
        double dx1 = p1.X - p2.X;
        double dy1 = p1.Y - p2.Y;
        double dx2 = p3.X - p2.X;
        double dy2 = p3.Y - p2.Y;

        double angle1 = Math.Atan2(dy1, dx1);
        double angle2 = Math.Atan2(dy2, dx2);

        double angleDiff = Math.Abs(angle1 - angle2) * 180.0 / Math.PI;

        // Normalize to 0-180 range
        if (angleDiff > 180)
            angleDiff = 360 - angleDiff;

        return angleDiff;
    }

    /// <summary>
    /// Scale detected quadrilateral points to fit display dimensions
    /// </summary>
    /// <param name="quad">Detected quadrilateral with coordinates in original image space</param>
    /// <param name="originalWidth">Width of the original image</param>
    /// <param name="originalHeight">Height of the original image</param>
    /// <param name="displayWidth">Width of the display area</param>
    /// <param name="displayHeight">Height of the display area</param>
    /// <returns>New quadrilateral with scaled coordinates</returns>
    public static DetectedQuadrilateral ScaleToDisplay(DetectedQuadrilateral quad, double originalWidth, double originalHeight, double displayWidth, double displayHeight)
    {
        double scaleX = displayWidth / originalWidth;
        double scaleY = displayHeight / originalHeight;

        var scaledPoints = new System.Windows.Point[]
        {
            new System.Windows.Point(quad.TopLeft.X * scaleX, quad.TopLeft.Y * scaleY),
            new System.Windows.Point(quad.TopRight.X * scaleX, quad.TopRight.Y * scaleY),
            new System.Windows.Point(quad.BottomRight.X * scaleX, quad.BottomRight.Y * scaleY),
            new System.Windows.Point(quad.BottomLeft.X * scaleX, quad.BottomLeft.Y * scaleY)
        };

        return new DetectedQuadrilateral(scaledPoints, quad.Area * scaleX * scaleY, quad.Confidence);
    }
}
