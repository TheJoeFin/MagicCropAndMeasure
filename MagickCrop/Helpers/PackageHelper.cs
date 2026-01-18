using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;
using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;

namespace MagickCrop.Helpers;

/// <summary>
/// Helper methods for creating and managing MagickCropMeasurementPackage instances
/// </summary>
public static class PackageHelper
{
    /// <summary>
    /// Creates a new MagickCropMeasurementPackage from current state
    /// </summary>
    /// <param name="currentImage">The current BitmapSource image</param>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="measurements">Current measurement collection</param>
    /// <param name="metadata">Package metadata</param>
    /// <returns>A new MagickCropMeasurementPackage with the provided data</returns>
    public static MagickCropMeasurementPackage CreateMeasurementPackage(
        BitmapSource? currentImage,
        string? imagePath,
        MeasurementCollection? measurements,
        PackageMetadata? metadata)
    {
        var package = new MagickCropMeasurementPackage
        {
            Metadata = metadata ?? new PackageMetadata(),
            Measurements = measurements ?? new MeasurementCollection(),
            ImagePath = imagePath
        };

        // Extract image data if BitmapSource is available
        if (currentImage != null)
        {
            package.ImageData = BitmapSourceToByteArray(currentImage);
        }
        else if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            // Fall back to reading from file
            package.ImageData = File.ReadAllBytes(imagePath);
        }

        return package;
    }

    /// <summary>
    /// Saves a package to a .mcm file (zip archive)
    /// </summary>
    /// <param name="package">The package to save</param>
    /// <param name="outputPath">Path to save the .mcm file</param>
    /// <returns>True if successful, false otherwise</returns>
    public static async Task<bool> SavePackageToFile(MagickCropMeasurementPackage package, string outputPath)
    {
        return await package.SaveToFileAsync(outputPath);
    }

    /// <summary>
    /// Loads a package from a .mcm file (zip archive)
    /// </summary>
    /// <param name="packagePath">Path to the .mcm file</param>
    /// <returns>The loaded MagickCropMeasurementPackage, or null if loading fails</returns>
    public static async Task<MagickCropMeasurementPackage?> LoadPackageFromFile(string packagePath)
    {
        return await MagickCropMeasurementPackage.LoadFromFileAsync(packagePath);
    }

    /// <summary>
    /// Converts current measurement state to a MeasurementCollection DTO for serialization
    /// </summary>
    /// <param name="distanceMeasurements">List of distance measurement data</param>
    /// <param name="angleMeasurements">List of angle measurement data</param>
    /// <param name="verticalLines">List of vertical line data</param>
    /// <param name="horizontalLines">List of horizontal line data</param>
    /// <param name="rectangleMeasurements">List of rectangle measurement data</param>
    /// <param name="circleMeasurements">List of circle measurement data</param>
    /// <param name="polygonMeasurements">List of polygon measurement data</param>
    /// <param name="inkStrokes">List of ink stroke data</param>
    /// <param name="strokeInfos">List of stroke info data</param>
    /// <param name="globalScaleFactor">Global scale factor for measurements</param>
    /// <param name="globalUnits">Global units for measurements</param>
    /// <returns>A MeasurementCollection DTO ready for serialization</returns>
    public static MeasurementCollection ToMeasurementCollection(
        List<DistanceMeasurementControlDto>? distanceMeasurements = null,
        List<AngleMeasurementControlDto>? angleMeasurements = null,
        List<VerticalLineControlDto>? verticalLines = null,
        List<HorizontalLineControlDto>? horizontalLines = null,
        List<RectangleMeasurementControlDto>? rectangleMeasurements = null,
        List<CircleMeasurementControlDto>? circleMeasurements = null,
        List<PolygonMeasurementControlDto>? polygonMeasurements = null,
        List<StrokeDto>? inkStrokes = null,
        List<StrokeInfoDto>? strokeInfos = null,
        double globalScaleFactor = 1.0,
        string globalUnits = "pixels")
    {
        return new MeasurementCollection
        {
            DistanceMeasurements = distanceMeasurements ?? [],
            AngleMeasurements = angleMeasurements ?? [],
            VerticalLines = verticalLines ?? [],
            HorizontalLines = horizontalLines ?? [],
            RectangleMeasurements = rectangleMeasurements ?? [],
            CircleMeasurements = circleMeasurements ?? [],
            PolygonMeasurements = polygonMeasurements ?? [],
            InkStrokes = inkStrokes ?? [],
            StrokeInfos = strokeInfos ?? [],
            GlobalScaleFactor = globalScaleFactor,
            GlobalUnits = globalUnits
        };
    }

    /// <summary>
    /// Loads measurements from a MeasurementCollection DTO
    /// </summary>
    /// <param name="collection">The MeasurementCollection to load from</param>
    /// <returns>The same MeasurementCollection (for method chaining)</returns>
    public static MeasurementCollection LoadMeasurementCollection(MeasurementCollection collection)
    {
        // This is mainly a pass-through for consistency, but ensures all properties are accessible
        return collection ?? new MeasurementCollection();
    }

    /// <summary>
    /// Converts a BitmapSource to a byte array in JPEG format
    /// </summary>
    /// <param name="bitmapSource">The BitmapSource to convert</param>
    /// <returns>Byte array of the JPEG-encoded image</returns>
    private static byte[] BitmapSourceToByteArray(BitmapSource bitmapSource)
    {
        using var memoryStream = new MemoryStream();
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
        encoder.Save(memoryStream);
        return memoryStream.ToArray();
    }
}
