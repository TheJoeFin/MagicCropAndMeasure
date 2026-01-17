using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.Models;

/// <summary>
/// Information about a measurement stroke.
/// </summary>
public partial class StrokeInfo : ObservableObject
{
    [ObservableProperty]
    private double _pixelLength;

    [ObservableProperty]
    private double _scaledLength;

    [ObservableProperty]
    private string _units = "pixels";

    /// <summary>
    /// Gets the formatted display string.
    /// </summary>
    public string DisplayText => Units == "pixels"
        ? $"{PixelLength:F1} px"
        : $"{ScaledLength:F2} {Units}";

    partial void OnPixelLengthChanged(double value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnScaledLengthChanged(double value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    partial void OnUnitsChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayText));
    }
}
