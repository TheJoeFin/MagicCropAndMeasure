using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace MagickCrop.Helpers;

/// <summary>
/// Provides robust clipboard image retrieval supporting multiple formats
/// </summary>
public static class ClipboardHelper
{
    /// <summary>
    /// Attempts to retrieve an image from the clipboard, trying multiple formats for maximum compatibility
    /// </summary>
    /// <returns>A BitmapSource if successful, null otherwise</returns>
    public static BitmapSource? GetImageFromClipboard()
    {
        try
        {
            // Try standard WPF GetImage first (handles most common cases)
            if (Clipboard.ContainsImage())
            {
                BitmapSource? image = Clipboard.GetImage();
                if (image != null)
                    return image;
            }

            // If standard method fails, try alternative formats
            IDataObject? dataObject = Clipboard.GetDataObject();
            if (dataObject == null)
                return null;

            // Try DIB format (Device Independent Bitmap) - most common for screenshots
            BitmapSource? dibImage = TryGetDibImage(dataObject);
            if (dibImage != null)
                return dibImage;

            // Try PNG format
            BitmapSource? pngImage = TryGetPngImage(dataObject);
            if (pngImage != null)
                return pngImage;

            // Try JPEG format
            BitmapSource? jpegImage = TryGetJpegImage(dataObject);
            if (jpegImage != null)
                return jpegImage;

            // Try BMP format
            BitmapSource? bmpImage = TryGetBmpImage(dataObject);
            if (bmpImage != null)
                return bmpImage;

            // Try TIFF format
            BitmapSource? tiffImage = TryGetTiffImage(dataObject);
            if (tiffImage != null)
                return tiffImage;

            // Try file drop list (user may have copied an image file)
            BitmapSource? fileDropImage = TryGetFileDropImage(dataObject);
            if (fileDropImage != null)
                return fileDropImage;

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if clipboard contains any image data in supported formats
    /// </summary>
    public static bool ContainsImageData()
    {
        try
        {
            if (Clipboard.ContainsImage())
                return true;

            IDataObject? dataObject = Clipboard.GetDataObject();
            if (dataObject == null)
                return false;

            // Check for various image formats
            return dataObject.GetDataPresent("DeviceIndependentBitmap") ||
                   dataObject.GetDataPresent(DataFormats.Dib) ||
                   dataObject.GetDataPresent("PNG") ||
                   dataObject.GetDataPresent("JFIF") ||
                   dataObject.GetDataPresent("JPEG") ||
                   dataObject.GetDataPresent(DataFormats.Bitmap) ||
                   dataObject.GetDataPresent("System.Drawing.Bitmap") ||
                   dataObject.GetDataPresent(DataFormats.Tiff) ||
                   (dataObject.GetDataPresent(DataFormats.FileDrop) && HasImageFile(dataObject));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets detailed information about available clipboard formats
    /// </summary>
    public static string GetClipboardFormatsInfo()
    {
        try
        {
            IDataObject? dataObject = Clipboard.GetDataObject();
            if (dataObject == null)
                return "No data in clipboard";

            string[] formats = dataObject.GetFormats();
            return string.Join(", ", formats);
        }
        catch (Exception ex)
        {
            return $"Error reading formats: {ex.Message}";
        }
    }

    private static BitmapSource? TryGetDibImage(IDataObject dataObject)
    {
        try
        {
            // Try DeviceIndependentBitmap format (most common for screenshots)
            if (dataObject.GetDataPresent("DeviceIndependentBitmap"))
            {
                using MemoryStream? stream = dataObject.GetData("DeviceIndependentBitmap") as MemoryStream;
                if (stream != null && stream.Length > 0)
                {
                    return ConvertDibStreamToBitmap(stream);
                }
            }

            // Try standard DIB format
            if (dataObject.GetDataPresent(DataFormats.Dib))
            {
                using MemoryStream? stream = dataObject.GetData(DataFormats.Dib) as MemoryStream;
                if (stream != null && stream.Length > 0)
                {
                    return ConvertDibStreamToBitmap(stream);
                }
            }
        }
        catch
        {
            // Ignore errors and try next format
        }

        return null;
    }

    private static BitmapSource? ConvertDibStreamToBitmap(MemoryStream stream)
    {
        try
        {
            // Use ImageMagick to handle DIB format robustly
            stream.Position = 0;
            byte[] dibData = stream.ToArray();

            using MagickImage magickImage = new(dibData);
            return magickImage.ToBitmapSource();
        }
        catch
        {
            // If ImageMagick fails, try direct BMP conversion
            try
            {
                stream.Position = 0;
                BmpBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                return decoder.Frames[0];
            }
            catch
            {
                return null;
            }
        }
    }

    private static BitmapSource? TryGetPngImage(IDataObject dataObject)
    {
        try
        {
            if (dataObject.GetDataPresent("PNG"))
            {
                using MemoryStream? stream = dataObject.GetData("PNG") as MemoryStream;
                if (stream != null && stream.Length > 0)
                {
                    stream.Position = 0;
                    PngBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    return decoder.Frames[0];
                }
            }
        }
        catch
        {
            // Ignore and try next format
        }

        return null;
    }

    private static BitmapSource? TryGetJpegImage(IDataObject dataObject)
    {
        try
        {
            // Try JFIF format (JPEG File Interchange Format)
            if (dataObject.GetDataPresent("JFIF"))
            {
                using MemoryStream? stream = dataObject.GetData("JFIF") as MemoryStream;
                if (stream != null && stream.Length > 0)
                {
                    stream.Position = 0;
                    JpegBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    return decoder.Frames[0];
                }
            }

            // Try standard JPEG format
            if (dataObject.GetDataPresent("JPEG"))
            {
                using MemoryStream? stream = dataObject.GetData("JPEG") as MemoryStream;
                if (stream != null && stream.Length > 0)
                {
                    stream.Position = 0;
                    JpegBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    return decoder.Frames[0];
                }
            }
        }
        catch
        {
            // Ignore and try next format
        }

        return null;
    }

    private static BitmapSource? TryGetBmpImage(IDataObject dataObject)
    {
        try
        {
            if (dataObject.GetDataPresent(DataFormats.Bitmap) || dataObject.GetDataPresent("System.Drawing.Bitmap"))
            {
                object? data = dataObject.GetData(DataFormats.Bitmap) ?? dataObject.GetData("System.Drawing.Bitmap");
                
                if (data is BitmapSource bitmapSource)
                    return bitmapSource;

                // Try to convert System.Drawing.Bitmap if present
                if (data is System.Drawing.Bitmap drawingBitmap)
                {
                    return ConvertDrawingBitmapToBitmapSource(drawingBitmap);
                }
            }
        }
        catch
        {
            // Ignore and try next format
        }

        return null;
    }

    private static BitmapSource? TryGetTiffImage(IDataObject dataObject)
    {
        try
        {
            if (dataObject.GetDataPresent(DataFormats.Tiff))
            {
                using MemoryStream? stream = dataObject.GetData(DataFormats.Tiff) as MemoryStream;
                if (stream != null && stream.Length > 0)
                {
                    stream.Position = 0;
                    TiffBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    return decoder.Frames[0];
                }
            }
        }
        catch
        {
            // Ignore and try next format
        }

        return null;
    }

    private static BitmapSource? TryGetFileDropImage(IDataObject dataObject)
    {
        try
        {
            if (dataObject.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = dataObject.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    string firstFile = files[0];
                    if (IsImageFile(firstFile) && File.Exists(firstFile))
                    {
                        BitmapImage bitmap = new();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(firstFile);
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }
            }
        }
        catch
        {
            // Ignore and try next format
        }

        return null;
    }

    private static bool HasImageFile(IDataObject dataObject)
    {
        try
        {
            if (dataObject.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = dataObject.GetData(DataFormats.FileDrop) as string[];
                return files != null && files.Length > 0 && IsImageFile(files[0]);
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    private static bool IsImageFile(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp" or ".heic" or ".heif";
    }

    private static BitmapSource ConvertDrawingBitmapToBitmapSource(System.Drawing.Bitmap drawingBitmap)
    {
        using MemoryStream stream = new();
        drawingBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;

        BitmapImage bitmapImage = new();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.StreamSource = stream;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
    }

    /// <summary>
    /// Saves a BitmapSource to a temporary file with optimal quality
    /// </summary>
    public static string SaveImageToTempFile(BitmapSource image, string? preferredExtension = null)
    {
        string tempFileName = Path.GetTempFileName();
        
        // Determine the best format based on image characteristics
        string extension = preferredExtension ?? DetermineOptimalFormat(image);
        tempFileName = Path.ChangeExtension(tempFileName, extension);

        using FileStream stream = new(tempFileName, FileMode.Create, FileAccess.Write);

        BitmapEncoder encoder = extension switch
        {
            ".png" => new PngBitmapEncoder(),
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
            ".bmp" => new BmpBitmapEncoder(),
            ".tif" or ".tiff" => new TiffBitmapEncoder(),
            _ => new PngBitmapEncoder() // Default to PNG for lossless quality
        };

        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(stream);

        return tempFileName;
    }

    private static string DetermineOptimalFormat(BitmapSource image)
    {
        // Use PNG for images with transparency
        if (image.Format.ToString().Contains("pbgra", StringComparison.OrdinalIgnoreCase) ||
            image.Format.ToString().Contains("rgba", StringComparison.OrdinalIgnoreCase))
        {
            return ".png";
        }

        // Use PNG for smaller images to preserve quality
        if (image.PixelWidth * image.PixelHeight < 1920 * 1080)
        {
            return ".png";
        }

        // Use JPEG for larger images to save space
        return ".jpg";
    }
}
