namespace MagickCrop.Tests.Base
{
    using System.Windows.Media.Imaging;

    /// <summary>
    /// Interface for capturing test output messages during integration tests.
    /// </summary>
    public interface ITestOutputService
    {
        void CaptureMessage(string message);
        void CaptureWarning(string warning);
        void CaptureError(string error);
        IReadOnlyList<string> GetCapturedMessages();
        void Clear();
    }

    /// <summary>
    /// Interface for mocking file dialog operations in tests.
    /// </summary>
    public interface ITestFileDialogService : IFileDialogService
    {
        void SetNextOpenResult(string? filePath);
        void SetNextSaveResult(string? filePath);
        IReadOnlyList<string> GetRequestedOpenPaths();
        IReadOnlyList<string> GetRequestedSavePaths();
    }

    /// <summary>
    /// Interface for mocking clipboard operations in tests.
    /// </summary>
    public interface ITestClipboardService : IClipboardService
    {
        IReadOnlyList<string> GetSetTextCalls();
        IReadOnlyList<BitmapSource> GetSetImageCalls();
    }

    /// <summary>
    /// Interface for mocking navigation in tests.
    /// </summary>
    public interface ITestNavigationService : INavigationService
    {
        IReadOnlyList<string> GetNavigationHistory();
        void ClearNavigationHistory();
    }
}

