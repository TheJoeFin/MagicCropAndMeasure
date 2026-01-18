namespace MagickCrop.Tests.Mocks
{
    using System.Windows;
    using System.Windows.Media.Imaging;

    public class TestOutputService : ITestOutputService
    {
        private readonly List<string> _messages = [];

        public void CaptureMessage(string message)
        {
            _messages.Add($"[INFO] {message}");
        }

        public void CaptureWarning(string warning)
        {
            _messages.Add($"[WARN] {warning}");
        }

        public void CaptureError(string error)
        {
            _messages.Add($"[ERROR] {error}");
        }

        public IReadOnlyList<string> GetCapturedMessages() => _messages.AsReadOnly();

        public void Clear() => _messages.Clear();
    }

    public class TestFileDialogService : ITestFileDialogService
    {
        private string? _nextOpenResult;
        private string? _nextSaveResult;
        private readonly List<string> _openRequests = [];
        private readonly List<string> _saveRequests = [];

        public void SetNextOpenResult(string? filePath) => _nextOpenResult = filePath;
        public void SetNextSaveResult(string? filePath) => _nextSaveResult = filePath;
        public IReadOnlyList<string> GetRequestedOpenPaths() => _openRequests.AsReadOnly();
        public IReadOnlyList<string> GetRequestedSavePaths() => _saveRequests.AsReadOnly();

        public string? ShowOpenFileDialog(string filter, string? title = null)
        {
            var result = _nextOpenResult;
            if (!string.IsNullOrEmpty(result))
                _openRequests.Add(result);
            _nextOpenResult = null;
            return result;
        }

        public string? ShowSaveFileDialog(string filter, string? defaultFileName = null, string? title = null)
        {
            var result = _nextSaveResult;
            if (!string.IsNullOrEmpty(result))
                _saveRequests.Add(result);
            _nextSaveResult = null;
            return result;
        }

        public string? ShowFolderBrowserDialog(string? description = null)
        {
            return null;
        }
    }

    public class TestClipboardService : ITestClipboardService
    {
        private string? _clipboardText;
        private BitmapSource? _clipboardImage;
        private readonly List<string> _textCalls = [];
        private readonly List<BitmapSource> _imageCalls = [];

        public bool ContainsImage() => _clipboardImage != null;
        public bool ContainsFileDropList() => false;
        
        public BitmapSource? GetImage() => _clipboardImage;
        public IReadOnlyList<string> GetFileDropList() => [];

        public void SetImage(BitmapSource image)
        {
            _clipboardImage = image;
            _imageCalls.Add(image);
        }

        public void SetText(string text)
        {
            _clipboardText = text;
            _textCalls.Add(text);
        }

        public IReadOnlyList<string> GetSetTextCalls() => _textCalls.AsReadOnly();
        public IReadOnlyList<BitmapSource> GetSetImageCalls() => _imageCalls.AsReadOnly();
    }

    public class TestNavigationService : ITestNavigationService
    {
        private readonly List<string> _navigationHistory = [];

        public IReadOnlyList<string> GetNavigationHistory() => _navigationHistory.AsReadOnly();
        public void ClearNavigationHistory() => _navigationHistory.Clear();

        public bool? ShowDialog<TWindow>() where TWindow : Window
        {
            _navigationHistory.Add($"ShowDialog {typeof(TWindow).Name}");
            return true;
        }

        public bool? ShowDialog<TWindow>(object parameter) where TWindow : Window
        {
            _navigationHistory.Add($"ShowDialog {typeof(TWindow).Name} with parameter");
            return true;
        }

        public void ShowWindow<TWindow>() where TWindow : Window
        {
            _navigationHistory.Add($"ShowWindow {typeof(TWindow).Name}");
        }

        public void ShowWindow<TWindow>(object parameter) where TWindow : Window
        {
            _navigationHistory.Add($"ShowWindow {typeof(TWindow).Name} with parameter");
        }

        public MessageBoxResult ShowMessage(string message, string title = "", 
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.Information)
        {
            _navigationHistory.Add($"ShowMessage: {message}");
            return MessageBoxResult.OK;
        }

        public void ShowError(string message, string title = "Error")
        {
            _navigationHistory.Add($"ShowError: {message}");
        }

        public bool ShowConfirmation(string message, string title = "Confirm")
        {
            _navigationHistory.Add($"ShowConfirmation: {message}");
            return true;
        }

        public Window? GetActiveWindow() => null;
    }
}

