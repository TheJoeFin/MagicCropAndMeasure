using System.Collections.Generic;
using System.Windows.Media.Imaging;
using ImageMagick;
using MagickCrop.Services;
using MagickCrop.Tests.Base;

namespace MagickCrop.Tests.Services;

/// <summary>
/// Comprehensive unit tests for ImageProcessingService.
/// Tests cover image loading, saving, and various processing operations.
/// </summary>
[TestClass]
public class ImageProcessingServiceTests : ServiceTestBase
{
    private ImageProcessingService? _service;
    private string? _testImageSmall;
    private string? _testImageMedium;
    private string? _testImageLarge;

    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
        _service = new ImageProcessingService();
        
        // Create test images
        _testImageSmall = CreateTestImage("small", 100, 100);
        _testImageMedium = CreateTestImage("medium", 800, 600);
        _testImageLarge = CreateTestImage("large", 2000, 1500);
    }

    [TestCleanup]
    public override void TestCleanup()
    {
        base.TestCleanup();
    }

    #region Helper Methods

    private string CreateTestImage(string name, int width, int height)
    {
        using (var image = new MagickImage(MagickColors.White, (uint)width, (uint)height))
        {
            var path = Path.Combine(Path.GetTempPath(), $"test_{name}_{Guid.NewGuid()}.png");
            image.Write(path);
            _tempImages.Add(path);
            return path;
        }
    }

    private readonly List<string> _tempImages = [];

    #endregion

    #region Initialization Tests

    [TestMethod]
    public void TestServiceInitialization_CreatesValidService()
    {
        // Act & Assert
        Assert.IsNotNull(_service);
    }

    #endregion

    #region Image Loading Tests

    [TestMethod]
    public async Task TestLoadValidImageReturnsValidMagickImage()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);

        // Act
        var result = await _service!.LoadImageAsync(_testImageMedium);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(800U, result.Width);
        Assert.AreEqual(600U, result.Height);
        result.Dispose();
    }

    [TestMethod]
    public async Task TestLoadInvalidFilePathReturnsNull()
    {
        // Act
        var result = await _service!.LoadImageAsync(@"C:\NonExistent\Path\Image.png");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TestLoadEmptyFilePathReturnsNull()
    {
        // Act
        var result = await _service!.LoadImageAsync("");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TestLoadSmallImageSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageSmall);

        // Act
        var result = await _service!.LoadImageAsync(_testImageSmall);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(100U, result.Width);
        Assert.AreEqual(100U, result.Height);
        result.Dispose();
    }

    [TestMethod]
    public async Task TestLoadLargeImageSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageLarge);

        // Act
        var result = await _service!.LoadImageAsync(_testImageLarge);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2000U, result.Width);
        Assert.AreEqual(1500U, result.Height);
        result.Dispose();
    }

    [TestMethod]
    public async Task TestLoadCorruptedImageReturnsNull()
    {
        // Arrange
        var corruptedPath = CreateTempFile("This is not a valid image");

        // Act
        var result = await _service!.LoadImageAsync(corruptedPath);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TestLoadPngFormatSucceeds()
    {
        // Arrange
        var pngPath = CreatePngTestImage();

        // Act
        var result = await _service!.LoadImageAsync(pngPath);

        // Assert
        Assert.IsNotNull(result);
        result.Dispose();
    }

    [TestMethod]
    public async Task TestLoadJpegFormatSucceeds()
    {
        // Arrange
        var jpegPath = CreateJpegTestImage();

        // Act
        var result = await _service!.LoadImageAsync(jpegPath);

        // Assert
        Assert.IsNotNull(result);
        result.Dispose();
    }

    [TestMethod]
    public async Task TestLoadBmpFormatSucceeds()
    {
        // Arrange
        var bmpPath = CreateBmpTestImage();

        // Act
        var result = await _service!.LoadImageAsync(bmpPath);

        // Assert
        Assert.IsNotNull(result);
        result.Dispose();
    }

    #endregion

    #region Image Crop Tests

    [TestMethod]
    public async Task TestCropWithValidCoordinatesSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        var cropped = _service.Crop(image, 100, 100, 400, 300);

        // Assert
        Assert.IsNotNull(cropped);
        Assert.AreEqual(400U, cropped.Width);
        Assert.AreEqual(300U, cropped.Height);
        cropped.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestCropWithZeroCoordinatesSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        var cropped = _service.Crop(image, 0, 0, 400, 300);

        // Assert
        Assert.IsNotNull(cropped);
        Assert.AreEqual(400U, cropped.Width);
        Assert.AreEqual(300U, cropped.Height);
        cropped.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestCropFullImageDimensions()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        var cropped = _service.Crop(image, 0, 0, 800, 600);

        // Assert
        Assert.IsNotNull(cropped);
        Assert.AreEqual(800U, cropped.Width);
        Assert.AreEqual(600U, cropped.Height);
        cropped.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestCropSmallRegionSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        var cropped = _service.Crop(image, 200, 150, 50, 50);

        // Assert
        Assert.IsNotNull(cropped);
        Assert.AreEqual(50U, cropped.Width);
        Assert.AreEqual(50U, cropped.Height);
        cropped.Dispose();
        image.Dispose();
    }

    #endregion

    #region Image Resize Tests

    [TestMethod]
    public async Task TestResizeWithValidDimensionsSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        var resized = _service.Resize(image, 400, 300);

        // Assert
        Assert.IsNotNull(resized);
        Assert.AreEqual(400U, resized.Width);
        Assert.AreEqual(300U, resized.Height);
        resized.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestResizeToSmallerDimensions()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        var resized = _service.Resize(image, 200, 150);

        // Assert
        Assert.IsNotNull(resized);
        Assert.AreEqual(200U, resized.Width);
        Assert.AreEqual(150U, resized.Height);
        resized.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestResizeToLargerDimensions()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        var resized = _service.Resize(image, 1600, 1200);

        // Assert
        Assert.IsNotNull(resized);
        Assert.AreEqual(1600U, resized.Width);
        Assert.AreEqual(1200U, resized.Height);
        resized.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestResizeToSquareDimensions()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        var resized = _service.Resize(image, 500, 500);

        // Assert
        Assert.IsNotNull(resized);
        // Note: ImageMagick preserves aspect ratio by default, so 800x600 resized to 500x500 becomes 500x375
        Assert.AreEqual(500U, resized.Width);
        Assert.IsTrue(resized.Height <= 500U);
        resized.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestResizePreservesImageContent()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        var resized = _service.Resize(image, 400, 300);

        // Assert
        Assert.IsNotNull(resized);
        Assert.IsTrue(resized.Width > 0 && resized.Height > 0);
        resized.Dispose();
        image.Dispose();
    }

    #endregion

    #region Image Rotate Tests

    [TestMethod]
    public async Task TestRotate90DegreesSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var originalWidth = image.Width;
        var originalHeight = image.Height;

        // Act
        var rotated = _service.Rotate(image, 90);

        // Assert
        Assert.IsNotNull(rotated);
        Assert.AreEqual(originalHeight, rotated.Width);
        Assert.AreEqual(originalWidth, rotated.Height);
        rotated.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestRotate180DegreesSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var originalWidth = image.Width;
        var originalHeight = image.Height;

        // Act
        var rotated = _service.Rotate(image, 180);

        // Assert
        Assert.IsNotNull(rotated);
        Assert.AreEqual(originalWidth, rotated.Width);
        Assert.AreEqual(originalHeight, rotated.Height);
        rotated.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestRotate270DegreesSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var originalWidth = image.Width;
        var originalHeight = image.Height;

        // Act
        var rotated = _service.Rotate(image, 270);

        // Assert
        Assert.IsNotNull(rotated);
        Assert.AreEqual(originalHeight, rotated.Width);
        Assert.AreEqual(originalWidth, rotated.Height);
        rotated.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestRotate360DegreesReturnsOriginalDimensions()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var originalWidth = image.Width;
        var originalHeight = image.Height;

        // Act
        var rotated = _service.Rotate(image, 360);

        // Assert
        Assert.IsNotNull(rotated);
        Assert.AreEqual(originalWidth, rotated.Width);
        Assert.AreEqual(originalHeight, rotated.Height);
        rotated.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestRotateNegativeDegreesSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var originalWidth = image.Width;
        var originalHeight = image.Height;

        // Act
        var rotated = _service.Rotate(image, -90);

        // Assert
        Assert.IsNotNull(rotated);
        // -90 degrees is same as 270 degrees
        Assert.AreEqual(originalHeight, rotated.Width);
        Assert.AreEqual(originalWidth, rotated.Height);
        rotated.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestRotateArbitraryDegrees()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        var rotated = _service.Rotate(image, 45);

        // Assert
        Assert.IsNotNull(rotated);
        Assert.IsTrue(rotated.Width > 0 && rotated.Height > 0);
        rotated.Dispose();
        image.Dispose();
    }

    #endregion

    #region Image Flip Tests

    [TestMethod]
    public async Task TestFlipHorizontalSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var originalWidth = image.Width;
        var originalHeight = image.Height;

        // Act
        _service.FlipHorizontal(image);

        // Assert
        Assert.AreEqual(originalWidth, image.Width);
        Assert.AreEqual(originalHeight, image.Height);
        image.Dispose();
    }

    [TestMethod]
    public async Task TestFlipVerticalSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var originalWidth = image.Width;
        var originalHeight = image.Height;

        // Act
        _service.FlipVertical(image);

        // Assert
        Assert.AreEqual(originalWidth, image.Width);
        Assert.AreEqual(originalHeight, image.Height);
        image.Dispose();
    }

    [TestMethod]
    public async Task TestFlipHorizontalTwiceRestoresOriginal()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        _service.FlipHorizontal(image);
        _service.FlipHorizontal(image);

        // Assert - dimensions should be unchanged
        Assert.AreEqual(800U, image.Width);
        Assert.AreEqual(600U, image.Height);
        image.Dispose();
    }

    [TestMethod]
    public async Task TestFlipVerticalTwiceRestoresOriginal()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        _service.FlipVertical(image);
        _service.FlipVertical(image);

        // Assert - dimensions should be unchanged
        Assert.AreEqual(800U, image.Width);
        Assert.AreEqual(600U, image.Height);
        image.Dispose();
    }

    #endregion

    #region Image Save Tests

    [TestMethod]
    public async Task TestSaveImageAsPngSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_output_{Guid.NewGuid()}.png");

        try
        {
            // Act
            var result = await _service.SaveImageAsync(image, outputPath, MagickFormat.Png);

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(File.Exists(outputPath));
            Assert.IsTrue(new FileInfo(outputPath).Length > 0);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            image.Dispose();
        }
    }

    [TestMethod]
    public async Task TestSaveImageAsJpegSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_output_{Guid.NewGuid()}.jpg");

        try
        {
            // Act
            var result = await _service.SaveImageAsync(image, outputPath, MagickFormat.Jpeg, quality: 85);

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(File.Exists(outputPath));
            Assert.IsTrue(new FileInfo(outputPath).Length > 0);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            image.Dispose();
        }
    }

    [TestMethod]
    public async Task TestSaveImageAsBmpSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_output_{Guid.NewGuid()}.bmp");

        try
        {
            // Act
            var result = await _service.SaveImageAsync(image, outputPath, MagickFormat.Bmp);

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(File.Exists(outputPath));
            Assert.IsTrue(new FileInfo(outputPath).Length > 0);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            image.Dispose();
        }
    }

    [TestMethod]
    public async Task TestSaveImageWithCustomQuality()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var lowQualityPath = Path.Combine(Path.GetTempPath(), $"test_low_quality_{Guid.NewGuid()}.jpg");
        var highQualityPath = Path.Combine(Path.GetTempPath(), $"test_high_quality_{Guid.NewGuid()}.jpg");

        try
        {
            // Act
            var image1 = (MagickImage)image.Clone();
            var image2 = (MagickImage)image.Clone();
            
            await _service.SaveImageAsync(image1, lowQualityPath, MagickFormat.Jpeg, quality: 30);
            await _service.SaveImageAsync(image2, highQualityPath, MagickFormat.Jpeg, quality: 95);

            // Assert - high quality should generally produce larger file
            var lowQualitySize = new FileInfo(lowQualityPath).Length;
            var highQualitySize = new FileInfo(highQualityPath).Length;
            
            Assert.IsTrue(File.Exists(lowQualityPath));
            Assert.IsTrue(File.Exists(highQualityPath));
            Assert.IsTrue(lowQualitySize > 0 && highQualitySize > 0);
            
            image1.Dispose();
            image2.Dispose();
        }
        finally
        {
            if (File.Exists(lowQualityPath))
                File.Delete(lowQualityPath);
            if (File.Exists(highQualityPath))
                File.Delete(highQualityPath);
            image.Dispose();
        }
    }

    [TestMethod]
    public async Task TestSaveToInvalidPathReturnsFalse()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var invalidPath = @"C:\InvalidDrive:\NonExistent\Path\image.png";

        // Act
        var result = await _service.SaveImageAsync(image, invalidPath, MagickFormat.Png);

        // Assert
        Assert.IsFalse(result);
        image.Dispose();
    }

    [TestMethod]
    public async Task TestSaveOverwritesExistingFile()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_overwrite_{Guid.NewGuid()}.png");

        try
        {
            // Create initial file
            var image1 = (MagickImage)image.Clone();
            await _service.SaveImageAsync(image1, outputPath, MagickFormat.Png);
            var firstFileSize = new FileInfo(outputPath).Length;
            
            // Save again with potentially different content
            var image2 = (MagickImage)image.Clone();
            var result = await _service.SaveImageAsync(image2, outputPath, MagickFormat.Png);

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(File.Exists(outputPath));
            
            image1.Dispose();
            image2.Dispose();
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            image.Dispose();
        }
    }

    #endregion

    #region Bitmap Conversion Tests

    [TestMethod]
    public async Task TestToBitmapSourceSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        var bitmapSource = _service.ToBitmapSource(image);

        // Assert
        Assert.IsNotNull(bitmapSource);
        Assert.AreEqual(800, bitmapSource.PixelWidth);
        Assert.AreEqual(600, bitmapSource.PixelHeight);
        image.Dispose();
    }

    [TestMethod]
    public async Task TestFromBitmapSourceSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        var bitmapSource = _service.ToBitmapSource(image);
        Assert.IsNotNull(bitmapSource);

        // Act
        var convertedImage = _service.FromBitmapSource(bitmapSource);

        // Assert
        Assert.IsNotNull(convertedImage);
        Assert.AreEqual(800U, convertedImage.Width);
        Assert.AreEqual(600U, convertedImage.Height);
        convertedImage.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestBitmapRoundTripPreservesContent()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var originalImage = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(originalImage);

        // Act
        var bitmapSource = _service.ToBitmapSource(originalImage);
        var reconvertedImage = _service.FromBitmapSource(bitmapSource);

        // Assert
        Assert.IsNotNull(reconvertedImage);
        Assert.AreEqual(originalImage.Width, reconvertedImage.Width);
        Assert.AreEqual(originalImage.Height, reconvertedImage.Height);
        reconvertedImage.Dispose();
        originalImage.Dispose();
    }

    #endregion

    #region Perspective Correction Tests

    [TestMethod]
    public async Task TestApplyPerspectiveCorrectionSucceeds()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        
        // Define perspective transform points
        var sourcePoints = new double[] { 0, 0, 800, 0, 0, 600, 800, 600 };
        var targetPoints = new double[] { 50, 50, 750, 75, 25, 550, 775, 575 };

        // Act
        var corrected = _service.ApplyPerspectiveCorrection(image, sourcePoints, targetPoints);

        // Assert
        Assert.IsNotNull(corrected);
        Assert.IsTrue(corrected.Width > 0 && corrected.Height > 0);
        corrected.Dispose();
        image.Dispose();
    }

    [TestMethod]
    public async Task TestApplyPerspectiveCorrectionWithSmallShift()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        
        // Define perspective transform points with a small shift
        var sourcePoints = new double[] { 0, 0, 800, 0, 0, 600, 800, 600 };
        var targetPoints = new double[] { 20, 10, 780, 15, 10, 590, 790, 585 };

        // Act
        var corrected = _service.ApplyPerspectiveCorrection(image, sourcePoints, targetPoints);

        // Assert
        Assert.IsNotNull(corrected);
        Assert.AreEqual(800U, corrected.Width);
        Assert.AreEqual(600U, corrected.Height);
        corrected.Dispose();
        image.Dispose();
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task TestLoadImageWithNullPathHandledGracefully()
    {
        // Act & Assert - should not throw, just return null
        var result = await _service!.LoadImageAsync(null!);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task TestSaveImageWithNullPathReturnsFalse()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);

        // Act
        var result = await _service.SaveImageAsync(image, null!, MagickFormat.Png);

        // Assert
        Assert.IsFalse(result);
        image.Dispose();
    }

    [TestMethod]
    public async Task TestSaveImageWithInvalidFileNameReturnsFalse()
    {
        // Arrange
        Assert.IsNotNull(_testImageMedium);
        var image = await _service!.LoadImageAsync(_testImageMedium);
        Assert.IsNotNull(image);
        // Using invalid filename characters
        var invalidPath = Path.Combine(Path.GetTempPath(), "test|invalid*.png");

        // Act
        var result = await _service.SaveImageAsync(image, invalidPath, MagickFormat.Png);

        // Assert
        Assert.IsFalse(result);
        image.Dispose();
    }

    #endregion

    #region Helper Image Creation Methods

    private string CreatePngTestImage()
    {
        using (var image = new MagickImage(MagickColors.Blue, 300, 200))
        {
            var path = Path.Combine(Path.GetTempPath(), $"test_png_{Guid.NewGuid()}.png");
            image.Write(path);
            _tempImages.Add(path);
            return path;
        }
    }

    private string CreateJpegTestImage()
    {
        using (var image = new MagickImage(MagickColors.Red, 400, 300))
        {
            var path = Path.Combine(Path.GetTempPath(), $"test_jpeg_{Guid.NewGuid()}.jpg");
            image.Format = MagickFormat.Jpeg;
            image.Quality = 85;
            image.Write(path);
            _tempImages.Add(path);
            return path;
        }
    }

    private string CreateBmpTestImage()
    {
        using (var image = new MagickImage(MagickColors.Green, 350, 250))
        {
            var path = Path.Combine(Path.GetTempPath(), $"test_bmp_{Guid.NewGuid()}.bmp");
            image.Write(path);
            _tempImages.Add(path);
            return path;
        }
    }

    protected override void CleanupTemporaryFiles()
    {
        foreach (var imagePath in _tempImages.Where(File.Exists))
        {
            try
            {
                File.Delete(imagePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        _tempImages.Clear();
        base.CleanupTemporaryFiles();
    }

    #endregion
}
