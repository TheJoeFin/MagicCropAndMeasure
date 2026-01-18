using Microsoft.Extensions.DependencyInjection;
using MagickCrop.Services;
using MagickCrop.Services.Interfaces;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.Tests.Mocks;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MagickCrop.Tests.Base
{
    /// <summary>
    /// Base class for integration tests that need full application setup with mocked I/O services.
    /// Provides common test infrastructure and helper methods for testing complex workflows.
    /// </summary>
    public abstract class IntegrationTestBase
    {
        protected IServiceProvider ServiceProvider { get; private set; } = null!;
        protected IRecentProjectsService RecentProjectsService { get; private set; } = null!;
        protected IImageProcessingService ImageProcessingService { get; private set; } = null!;
        protected IFileDialogService FileDialogService { get; private set; } = null!;
        protected IClipboardService ClipboardService { get; private set; } = null!;
        protected INavigationService NavigationService { get; private set; } = null!;
        protected ITestOutputService TestOutputService { get; private set; } = null!;

        [TestInitialize]
        public virtual void SetupTest()
        {
            var services = new ServiceCollection();
            
            // Register real services
            var testFolder = Path.Combine(Path.GetTempPath(), "MagickCropTests");
            Directory.CreateDirectory(testFolder);
            var appPaths = new MockAppPaths(testFolder);
            services.AddSingleton<IRecentProjectsService>(sp => 
                new RecentProjectsManager(appPaths, new MockThumbnailService()));
            services.AddSingleton<IImageProcessingService, ImageProcessingService>();
            
            // Register mock services
            services.AddSingleton<IFileDialogService, TestFileDialogService>();
            services.AddSingleton<IClipboardService, TestClipboardService>();
            services.AddSingleton<INavigationService, TestNavigationService>();
            services.AddSingleton<ITestOutputService, TestOutputService>();
            
            ServiceProvider = services.BuildServiceProvider();
            
            RecentProjectsService = ServiceProvider.GetRequiredService<IRecentProjectsService>();
            ImageProcessingService = ServiceProvider.GetRequiredService<IImageProcessingService>();
            FileDialogService = ServiceProvider.GetRequiredService<IFileDialogService>();
            ClipboardService = ServiceProvider.GetRequiredService<IClipboardService>();
            NavigationService = ServiceProvider.GetRequiredService<INavigationService>();
            TestOutputService = ServiceProvider.GetRequiredService<ITestOutputService>();
        }

        [TestCleanup]
        public virtual void CleanupTest()
        {
            (ServiceProvider as ServiceProvider)?.Dispose();
        }

        /// <summary>
        /// Create a test measurement package for integration testing.
        /// </summary>
        protected MagickCropMeasurementPackage CreateTestProject(string testProjectName = "TestProject")
        {
            var package = new MagickCropMeasurementPackage
            {
                Metadata = new PackageMetadata
                {
                    OriginalFilename = testProjectName,
                    OriginalImageSize = new System.Windows.Size(800, 600),
                    CurrentImageSize = new System.Windows.Size(800, 600)
                },
                Measurements = new MeasurementCollection()
            };

            return package;
        }

        /// <summary>
        /// Create a test image (solid color bitmap for testing).
        /// </summary>
        protected byte[] CreateTestImageBytes(int width = 800, int height = 600)
        {
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Bgr32);
            
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(
                    Brushes.White,
                    null,
                    new Rect(0, 0, width, height));
            }
            rtb.Render(dv);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Assert that an async operation completes within expected time.
        /// </summary>
        protected async Task AssertCompletesWithinAsync(Func<Task> action, int timeoutMs = 5000)
        {
            var task = action();
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            Assert.AreEqual(task, completed, $"Task did not complete within {timeoutMs}ms");
        }

        /// <summary>
        /// Helper to validate measurement consistency.
        /// </summary>
        protected void AssertMeasurementValid(MeasurementCollection measurements)
        {
            Assert.IsNotNull(measurements, "Measurements collection should not be null");
            // Additional validation can be added based on specific measurement rules
        }
    }
}

