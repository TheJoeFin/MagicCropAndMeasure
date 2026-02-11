using ImageMagick;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace MagickCrop.ViewModels;

/// <summary>
/// Interface for view-specific operations that the ViewModel needs from MainWindow.
/// Keeps the ViewModel decoupled from concrete WPF types while allowing
/// necessary UI interactions (image display, dialogs, canvas operations).
/// </summary>
public interface IMainWindowView
{
    // Image display
    BitmapSource? ImageSource { get; set; }
    Image MainImageControl { get; }
    double ImageActualWidth { get; }
    double ImageActualHeight { get; }

    // Local adjustment
    bool IsLocalAdjustment { get; }
    MagickGeometry GetLocalAdjustmentRegion();

    // Busy state (delegates to existing SetUiForLongTask/SetUiForCompletedTask)
    void SetBusy(bool busy);

    // Window access
    Window OwnerWindow { get; }
}
