using MagickCrop.Services;
using MagickCrop.Services.Interfaces;
using MagickCrop.Tests.Base;
using MagickCrop.Tests.Mocks;
using System.IO;
using System.Windows.Media.Imaging;

namespace MagickCrop.Tests.Services;

/// <summary>
/// Unit tests for ThumbnailService
/// </summary>
[TestClass]
public class ThumbnailServiceTests : ServiceTestBase
{
    private ThumbnailService? _service;
    private MockAppPaths? _mockAppPaths;

    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
        
        // Create an isolated test folder for app data
        var testAppDataFolder = CreateTempDirectory();
        
        // Create the service instance with the isolated folder
        _mockAppPaths = new MockAppPaths(testAppDataFolder);
        _mockAppPaths.EnsureDirectoriesExist();
        _service = new ThumbnailService(_mockAppPaths);
    }

    [TestCleanup]
    public override void TestCleanup()
    {
        base.TestCleanup();
        _service = null;
        _mockAppPaths = null;
    }

    #region Initialization Tests

    [TestMethod]
    public void TestInitialization_CreatesValidService()
    {
        // Act & Assert
        Assert.IsNotNull(_service);
        Assert.IsNotNull(_mockAppPaths);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void TestInitialization_ThrowsOnNullAppPaths()
    {
        // Act & Assert
        _ = new ThumbnailService(null!);
    }

    #endregion

    #region CreateThumbnail Tests

    [TestMethod]
    public void TestCreateThumbnail_NullImageReturnsEmpty()
    {
        // Arrange
        BitmapSource nullImage = null!;
        string projectId = "test-null-image";

        // Act
        string result = _service!.CreateThumbnail(nullImage, projectId);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void TestCreateThumbnail_NullProjectIdReturnsEmpty()
    {
        // Arrange
        var testImage = CreateTestBitmapSource(800, 600);
        string projectId = null!;

        // Act
        string result = _service!.CreateThumbnail(testImage, projectId);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void TestCreateThumbnail_EmptyProjectIdReturnsEmpty()
    {
        // Arrange
        var testImage = CreateTestBitmapSource(800, 600);
        string projectId = string.Empty;

        // Act
        string result = _service!.CreateThumbnail(testImage, projectId);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void TestCreateThumbnail_GeneratesThumbnailFileForValidInputs()
    {
        // Arrange
        var testImage = CreateTestBitmapSource(800, 600);
        string projectId = "test-valid-thumbnail";

        // Act
        string thumbnailPath = _service!.CreateThumbnail(testImage, projectId);

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(thumbnailPath), "Thumbnail path should not be empty");
        Assert.IsTrue(thumbnailPath.EndsWith(".jpg"), "Thumbnail should be a JPG file");
        Assert.IsTrue(File.Exists(thumbnailPath), $"Thumbnail file should exist at {thumbnailPath}");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test BitmapSource with specified dimensions
    /// </summary>
    private BitmapSource CreateTestBitmapSource(int width, int height)
    {
        // Create a simple solid-color bitmap for testing
        var bitmap = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
        bitmap.Lock();
        
        // Fill with a simple color pattern
        int pixelCount = width * height;
        byte[] pixels = new byte[pixelCount * 4];
        
        for (int i = 0; i < pixelCount; i++)
        {
            pixels[i * 4] = 0;        // Blue
            pixels[i * 4 + 1] = 128;  // Green
            pixels[i * 4 + 2] = 255;  // Red
            pixels[i * 4 + 3] = 255;  // Alpha
        }
        
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        bitmap.Unlock();
        
        return bitmap;
    }

    #endregion
}
