using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using MagickCrop.Helpers;
using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.Windows;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MagickCrop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private IMainWindowView? _view;

    public void SetView(IMainWindowView view)
    {
        _view = view;
        UndoRedo.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(UndoRedo.CanUndo) or nameof(UndoRedo.CanRedo))
            {
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();
            }
        };
    }

    // ──────────────────────────────────────────────
    //  Observable State Properties
    // ──────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    [NotifyPropertyChangedFor(nameof(OpenedFileDisplayName))]
    [NotifyCanExecuteChangedFor(nameof(CopyToClipboardCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShareCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyAutoContrastCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyBlackPointCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyWhitePointCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyGrayscaleCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyInvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyWhiteBalanceCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyAutoLevelsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyAutoGammaCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyBlurCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyFindEdgesCommand))]
    [NotifyCanExecuteChangedFor(nameof(Rotate90CwCommand))]
    [NotifyCanExecuteChangedFor(nameof(Rotate90CcwCommand))]
    [NotifyCanExecuteChangedFor(nameof(FlipVerticalCommand))]
    [NotifyCanExecuteChangedFor(nameof(FlipHorizontalCommand))]
    private string? imagePath;

    [ObservableProperty]
    private string? originalFilePath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFolderCommand))]
    private string? savedPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenedFileDisplayName))]
    [NotifyPropertyChangedFor(nameof(HasOpenedFileName))]
    private string openedFileName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenedFileDisplayName))]
    private MagickCropMeasurementPackage? openedPackage;

    [ObservableProperty]
    private string? currentProjectId;

    [ObservableProperty]
    private Size originalImageSize;

    [ObservableProperty]
    private Size actualImageSize;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [ObservableProperty]
    private string windowTitle = "Magick Crop & Measure by TheJoeFin";

    [ObservableProperty]
    private double scaleFactor = 1.0;

    [ObservableProperty]
    private string measurementUnits = "Pixels";

    // ──────────────────────────────────────────────
    //  Computed Properties
    // ──────────────────────────────────────────────

    public bool HasImage => !string.IsNullOrEmpty(ImagePath);
    public bool IsNotBusy => !IsBusy;
    public bool HasOpenedFileName => !string.IsNullOrEmpty(OpenedFileName);

    public string OpenedFileDisplayName
    {
        get
        {
            if (string.IsNullOrEmpty(OpenedFileName))
                return "Image/Project Name";

            if (OpenedPackage is not null && !string.IsNullOrEmpty(OpenedPackage.Metadata.OriginalFilename))
                return $" {OpenedPackage.Metadata.OriginalFilename}";

            return OpenedFileName;
        }
    }

    // ──────────────────────────────────────────────
    //  UndoRedo (moved from MainWindow)
    // ──────────────────────────────────────────────

    public UndoRedo UndoRedo { get; } = new();

    // ──────────────────────────────────────────────
    //  Static Data
    // ──────────────────────────────────────────────

    public static readonly List<FormatItem> Formats =
    [
        new FormatItem { Name = "JPEG Image", Format = MagickFormat.Jpg, Extension = ".jpg", SupportsQuality = true },
        new FormatItem { Name = "PNG Image", Format = MagickFormat.Png, Extension = ".png", SupportsQuality = false },
        new FormatItem { Name = "BMP Image", Format = MagickFormat.Bmp, Extension = ".bmp", SupportsQuality = false },
        new FormatItem { Name = "TIFF Image", Format = MagickFormat.Tiff, Extension = ".tiff", SupportsQuality = false },
        new FormatItem { Name = "WebP Image", Format = MagickFormat.WebP, Extension = ".webp", SupportsQuality = true },
    ];

    // ──────────────────────────────────────────────
    //  Commands: Undo / Redo / Info
    // ──────────────────────────────────────────────

    private bool CanUndo() => UndoRedo.CanUndo;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        string path = UndoRedo.Undo();
        if (!string.IsNullOrWhiteSpace(path))
            ImagePath = path;
    }

    private bool CanRedo() => UndoRedo.CanRedo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        string path = UndoRedo.Redo();
        if (!string.IsNullOrWhiteSpace(path))
            ImagePath = path;
    }

    [RelayCommand]
    private void ShowAbout()
    {
        if (_view is null) return;
        AboutWindow aboutWindow = new()
        {
            Owner = _view.OwnerWindow
        };
        aboutWindow.ShowDialog();
    }

    // ──────────────────────────────────────────────
    //  Commands: Clipboard / Folder / Share
    // ──────────────────────────────────────────────

    private bool CanCopyToClipboard() => HasImage;

    [RelayCommand(CanExecute = nameof(CanCopyToClipboard))]
    private async Task CopyToClipboard()
    {
        if (_view?.ImageSource is not BitmapSource bitmapSource)
            return;

        try
        {
            ClipboardHelper.CopyImageToClipboard(bitmapSource);
        }
        catch (Exception ex)
        {
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Copy Error",
                Content = ex.Message,
                PrimaryButtonText = "OK",
            };
            await uiMessageBox.ShowDialogAsync();
        }
    }

    private bool CanOpenFolder() => !string.IsNullOrEmpty(SavedPath);

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenFolder()
    {
        string? folderPath = Path.GetDirectoryName(SavedPath);
        if (folderPath is null)
            return;

        Process.Start("explorer.exe", folderPath);
    }

    private bool CanShare() => HasImage;

    [RelayCommand(CanExecute = nameof(CanShare))]
    private async Task Share()
    {
        if (_view is null || string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
            return;

        try
        {
            string title = string.IsNullOrEmpty(OpenedFileName)
                ? "Shared Image"
                : OpenedFileName;

            ShareHelper.ShareImageFile(_view.OwnerWindow, ImagePath, title, OpenedFileName);
        }
        catch (Exception ex)
        {
            Wpf.Ui.Controls.MessageBox uiMessageBox = new()
            {
                Title = "Share Error",
                Content = ex.Message,
                PrimaryButtonText = "OK",
            };
            await uiMessageBox.ShowDialogAsync();
        }
    }

    // ──────────────────────────────────────────────
    //  Commands: Image Adjustments
    // ──────────────────────────────────────────────

    private bool CanApplyAdjustment() => HasImage;

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyAutoContrast() => ApplyAdjustmentAsync(img => img.SigmoidalContrast(10));

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyBlackPoint() => ApplyAdjustmentAsync(img => img.BlackThreshold(new Percentage(10)));

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyWhitePoint() => ApplyAdjustmentAsync(img => img.WhiteThreshold(new Percentage(90)));

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyGrayscale() => ApplyAdjustmentAsync(img => img.Grayscale());

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyInvert() => ApplyAdjustmentAsync(img => img.Negate());

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyWhiteBalance() => ApplyAdjustmentAsync(img => img.WhiteBalance());

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyAutoLevels() => ApplyAdjustmentAsync(img => img.AutoLevel());

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyAutoGamma() => ApplyAdjustmentAsync(img => img.AutoGamma());

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyBlur() => ApplyAdjustmentAsync(img => img.Blur(20, 10));

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyFindEdges() => ApplyAdjustmentAsync(img => img.CannyEdge());

    private async Task ApplyAdjustmentAsync(Action<MagickImage> adjustment)
    {
        if (_view is null || string.IsNullOrWhiteSpace(ImagePath))
            return;

        _view.SetBusy(true);

        try
        {
            using MagickImage magickImage = new(ImagePath);

            if (_view.IsLocalAdjustment)
            {
                MagickGeometry region = _view.GetLocalAdjustmentRegion();

                double displayWidth = _view.ImageActualWidth;
                double displayHeight = _view.ImageActualHeight;
                if (displayWidth == 0 || displayHeight == 0)
                    return;

                double factor = magickImage.Height / displayHeight;
                region.ScaleAll(factor);

                if (region.X < 0) region.X = 0;
                if (region.Y < 0) region.Y = 0;
                if (region.X + region.Width > magickImage.Width)
                    region.Width = (uint)(magickImage.Width - region.X);
                if (region.Y + region.Height > magickImage.Height)
                    region.Height = (uint)(magickImage.Height - region.Y);

                int regionX = region.X;
                int regionY = region.Y;

                await Task.Run(() =>
                {
                    using MagickImage cropped = (MagickImage)magickImage.Clone();
                    cropped.Crop(region);
                    cropped.Page = new MagickGeometry(0, 0, cropped.Width, cropped.Height);
                    adjustment(cropped);
                    magickImage.Composite(cropped, regionX, regionY, CompositeOperator.Over);
                });
            }
            else
            {
                await Task.Run(() => adjustment(magickImage));
            }

            string tempFileName = Path.GetTempFileName();
            await magickImage.WriteAsync(tempFileName);

            MagickImageUndoRedoItem undoRedoItem = new(_view.MainImageControl, ImagePath, tempFileName);
            UndoRedo.AddUndo(undoRedoItem);

            ImagePath = tempFileName;
            _view.ImageSource = magickImage.ToBitmapSource();
            ActualImageSize = new Size(magickImage.Width, magickImage.Height);
        }
        finally
        {
            _view.SetBusy(false);
        }
    }

    // ──────────────────────────────────────────────
    //  Commands: Rotate & Flip
    // ──────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task Rotate90Cw() => ApplyAdjustmentAsync(img => img.Rotate(90));

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task Rotate90Ccw() => ApplyAdjustmentAsync(img => img.Rotate(-90));

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task FlipVertical() => ApplyAdjustmentAsync(img => img.Flip());

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task FlipHorizontal() => ApplyAdjustmentAsync(img => img.Flop());
}
