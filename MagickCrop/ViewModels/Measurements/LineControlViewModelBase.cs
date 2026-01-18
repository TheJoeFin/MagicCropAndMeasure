using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.ViewModels.Measurements;

/// <summary>
/// Base ViewModel for line guide controls.
/// </summary>
public abstract partial class LineControlViewModelBase : MeasurementViewModelBase
{
    [ObservableProperty]
    private double _position;

    [ObservableProperty]
    private double _canvasSize;

    protected override void UpdateDisplayText()
    {
        DisplayText = $"{Position:F0} px";
    }
}

/// <summary>
/// ViewModel for horizontal line guide.
/// </summary>
public partial class HorizontalLineViewModel : LineControlViewModelBase
{
    public override string MeasurementType => "HorizontalLine";

    public double Y
    {
        get => Position;
        set => Position = value;
    }
}

/// <summary>
/// ViewModel for vertical line guide.
/// </summary>
public partial class VerticalLineViewModel : LineControlViewModelBase
{
    public override string MeasurementType => "VerticalLine";

    public double X
    {
        get => Position;
        set => Position = value;
    }
}
