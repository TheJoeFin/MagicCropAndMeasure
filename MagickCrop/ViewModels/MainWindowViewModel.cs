using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using MagickCrop.Helpers;
using MagickCrop.Models;
using MagickCrop.Services;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.Windows;
using System.Collections.ObjectModel;
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
    [NotifyCanExecuteChangedFor(nameof(ApplyDespeckleCommand))]
    [NotifyCanExecuteChangedFor(nameof(Rotate90CwCommand))]
    [NotifyCanExecuteChangedFor(nameof(Rotate90CcwCommand))]
    [NotifyCanExecuteChangedFor(nameof(FlipVerticalCommand))]
    [NotifyCanExecuteChangedFor(nameof(FlipHorizontalCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyThresholdCommand))]
    // Lens-related commands do not need NotifyCanExecuteChanged entries here
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

    /// <summary>What the app is currently busy doing, shown next to the canvas progress ring.</summary>
    [ObservableProperty]
    private string busyMessage = DefaultBusyMessage;

    public const string DefaultBusyMessage = "Working";

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

    [RelayCommand]
    private void ShowLensCorrection()
    {
        if (_view is null) return;
        var window = new MagickCrop.Windows.LensCorrectionWindow(this)
        {
            Owner = _view.OwnerWindow
        };
        window.ShowDialog();
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

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyDespeckle() => ApplyAdjustmentAsync(img => img.Despeckle());

    [ObservableProperty]
    private double thresholdValue = 128.0;

    // Lens correction coefficients (barrel distortion)
    [ObservableProperty]
    private double lensCorrectionA = 0.0;

    [ObservableProperty]
    private double lensCorrectionB = 0.0;

    [ObservableProperty]
    private double lensCorrectionC = 0.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetectedLensDescription))]
    private string detectedLensDescription = string.Empty;

    public bool HasDetectedLensDescription => !string.IsNullOrEmpty(DetectedLensDescription);

    public ObservableCollection<LensProfileEntry> LensProfiles { get; } = [];

    [ObservableProperty]
    private LensProfileEntry? selectedLensProfile;

    // Guards against re-entrancy when a profile selection writes the coefficients.
    private bool applyingLensProfile;

    partial void OnSelectedLensProfileChanged(LensProfileEntry? value)
    {
        if (value is null || applyingLensProfile) return;

        applyingLensProfile = true;
        try
        {
            LensCorrectionA = value.A;
            LensCorrectionB = value.B;
            LensCorrectionC = value.C;
        }
        finally
        {
            applyingLensProfile = false;
        }
    }

    public void LoadLensProfiles()
    {
        LensProfiles.Clear();
        foreach (LensProfileEntry entry in LensProfileService.GetProfiles())
            LensProfiles.Add(entry);
    }

    private void SelectProfileByKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        LensProfileEntry? match = LensProfiles
            .FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

        if (match is null) return;

        applyingLensProfile = true;
        try
        {
            SelectedLensProfile = match;
        }
        finally
        {
            applyingLensProfile = false;
        }
    }

    [RelayCommand]
    private void SaveLensProfile()
    {
        string name = LensProfileName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            DetectedLensDescription = "Enter a name to save this profile";
            return;
        }

        LensProfileEntry? saved = LensProfileService.Save(name, LensCorrectionA, LensCorrectionB, LensCorrectionC);
        if (saved is null)
        {
            DetectedLensDescription = "Could not save the lens profile";
            return;
        }

        LoadLensProfiles();
        SelectProfileByKey(name);
        LensProfileName = string.Empty;
        DetectedLensDescription = $"Saved profile \u201c{name}\u201d";
    }

    [ObservableProperty]
    private string lensProfileName = string.Empty;

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyThreshold() => ApplyAdjustmentAsync(img => img.Threshold(new Percentage(ThresholdValue / 255.0 * 100.0)));

    private async Task ApplyAdjustmentAsync(Action<MagickImage> adjustment, bool forceFullImage = false)
    {
        if (_view is null || string.IsNullOrWhiteSpace(ImagePath))
            return;

        _view.SetBusy(true);

        try
        {
            using MagickImage magickImage = new(ImagePath);

            if (!forceFullImage && _view.IsLocalAdjustment)
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

            // Path.GetTempFileName() hands back a ".tmp" name, and Magick picks its
            // encoder from the extension - ".tmp" resolves to Unknown and fails to
            // encode. Pick an explicit format, promoting to PNG when the operation
            // introduced transparency that the source format cannot represent.
            MagickFormat targetFormat = magickImage.Format;

            if (targetFormat is MagickFormat.Unknown)
                targetFormat = MagickFormat.Png;

            if (magickImage.HasAlpha && targetFormat is MagickFormat.Jpeg or MagickFormat.Jpg or MagickFormat.Bmp)
                targetFormat = MagickFormat.Png;

            magickImage.Format = targetFormat;

            string tempFileName = Path.ChangeExtension(
                Path.GetTempFileName(),
                targetFormat.ToString().ToLowerInvariant());

            await magickImage.WriteAsync(tempFileName, targetFormat);

            MagickImageUndoRedoItem undoRedoItem = new(_view.MainImageControl, ImagePath, tempFileName);
            UndoRedo.AddUndo(undoRedoItem);

            ImagePath = tempFileName;
            _view.ImageSource = magickImage.ToBitmapSource();
            ActualImageSize = new Size(magickImage.Width, magickImage.Height);
        }
        catch (Exception ex)
        {
            // Never let an image operation take down the app; surface it instead.
            System.Windows.MessageBox.Show(
                $"The image operation could not be completed.\n\n{ex.Message}",
                "Image Operation Failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            _view.SetBusy(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private async Task ApplyLensCorrection()
    {
        if (_view is null)
            return;

        // Lens correction warps the image, so any calibrated scale and existing
        // measurements no longer line up with the pixels underneath them.
        if (_view.HasMeasurements)
        {
            Wpf.Ui.Controls.MessageBox confirm = new()
            {
                Title = "Lens Correction",
                Content = "Lens correction changes the image geometry, so existing measurements and scale calibration will no longer be accurate.\n\nApply anyway?",
                PrimaryButtonText = "Apply",
                CloseButtonText = "Cancel",
            };

            if (await confirm.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
                return;
        }

        double a = LensCorrectionA;
        double b = LensCorrectionB;
        double c = LensCorrectionC;
        double d = 1.0 - a - b - c;

        await ApplyAdjustmentAsync(img =>
        {
            img.VirtualPixelMethod = VirtualPixelMethod.Transparent;
            // Barrel distortion takes four coefficients: A, B, C, D
            img.Distort(DistortMethod.Barrel, a, b, c, d);
        }, forceFullImage: true);
    }

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private void ResetLensCorrection()
    {
        applyingLensProfile = true;
        try
        {
            SelectedLensProfile = null;
        }
        finally
        {
            applyingLensProfile = false;
        }

        LensCorrectionA = 0.0;
        LensCorrectionB = 0.0;
        LensCorrectionC = 0.0;
        DetectedLensDescription = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private void AutoDetectLensCorrection()
    {
        if (string.IsNullOrWhiteSpace(ImagePath)) return;

        LensMetadata? meta = LensMetadataHelper.Read(ImagePath);
        if (meta is null)
        {
            DetectedLensDescription = "No EXIF metadata found";
            return;
        }

        string describe = string.Join(" ", new[] { meta.CameraMake, meta.CameraModel, meta.LensModel }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        LensCorrectionSettings? profile = LensProfileService.Lookup(meta);
        if (profile is not null)
        {
            LensCorrectionA = profile.A;
            LensCorrectionB = profile.B;
            LensCorrectionC = profile.C;

            // Reflect the EXIF match in the profile list so both paths agree.
            string combined = string.Join(" ", new[] { meta.CameraMake, meta.CameraModel, meta.LensMake, meta.LensModel });
            LensProfileEntry? matched = LensProfiles.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.Key) && combined.Contains(p.Key, StringComparison.OrdinalIgnoreCase));
            SelectProfileByKey(matched?.Key);

            DetectedLensDescription = $"Detected: {describe}";
            return;
        }

        DetectedLensDescription = string.IsNullOrWhiteSpace(describe)
            ? "No matching lens profile found"
            : $"{describe} — no profile, adjust manually";
    }

    [RelayCommand(CanExecute = nameof(CanApplyAdjustment))]
    private Task ApplyExifOrientation()
    {
        return ApplyAdjustmentAsync(img => img.AutoOrient(), forceFullImage: true);
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
