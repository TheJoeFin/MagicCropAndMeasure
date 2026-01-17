using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagick;

namespace MagickCrop.Models;

/// <summary>
/// Options for saving/exporting an image.
/// Inherits from ObservableValidator to support data validation.
/// </summary>
public partial class SaveOptions : ObservableValidator
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupportsQuality))]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Please select an output format")]
    private MagickFormat? _selectedFormat;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 100, ErrorMessage = "Quality must be between 1 and 100")]
    private int _quality = 90;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResizeValid))]
    private bool _shouldResize;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResizeValid))]
    [NotifyDataErrorInfo]
    [Range(1, 32000, ErrorMessage = "Width must be between 1 and 32000")]
    private int _resizeWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResizeValid))]
    [NotifyDataErrorInfo]
    [Range(1, 32000, ErrorMessage = "Height must be between 1 and 32000")]
    private int _resizeHeight;

    [ObservableProperty]
    private bool _maintainAspectRatio = true;

    [ObservableProperty]
    private int _originalWidth;

    [ObservableProperty]
    private int _originalHeight;

    [ObservableProperty]
    private string _extension = string.Empty;

    partial void OnResizeWidthChanged(int value)
    {
        if (MaintainAspectRatio && OriginalWidth > 0)
        {
            var ratio = (double)OriginalHeight / OriginalWidth;
            var newHeight = (int)(value * ratio);
            if (newHeight != ResizeHeight)
            {
                _resizeHeight = newHeight;
                OnPropertyChanged(nameof(ResizeHeight));
            }
        }
    }

    partial void OnResizeHeightChanged(int value)
    {
        if (MaintainAspectRatio && OriginalHeight > 0)
        {
            var ratio = (double)OriginalWidth / OriginalHeight;
            var newWidth = (int)(value * ratio);
            if (newWidth != ResizeWidth)
            {
                _resizeWidth = newWidth;
                OnPropertyChanged(nameof(ResizeWidth));
            }
        }
    }

    /// <summary>
    /// Gets whether quality adjustment is supported for the selected format.
    /// </summary>
    public bool SupportsQuality => SelectedFormat.HasValue;

    /// <summary>
    /// Gets whether the resize dimensions are valid.
    /// </summary>
    public bool IsResizeValid => !ShouldResize || (ResizeWidth > 0 && ResizeHeight > 0);

    /// <summary>
    /// Validates all properties and returns whether the options are valid.
    /// </summary>
    public bool IsValid()
    {
        ValidateAllProperties();
        return !HasErrors;
    }
}
