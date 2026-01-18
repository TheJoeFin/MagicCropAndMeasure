using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.ViewModels.Measurements;

/// <summary>
/// ViewModel for distance measurement.
/// </summary>
public partial class DistanceMeasurementViewModel : MeasurementViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MidPoint))]
    [NotifyPropertyChangedFor(nameof(Angle))]
    [NotifyPropertyChangedFor(nameof(PixelLength))]
    private Point _startPoint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MidPoint))]
    [NotifyPropertyChangedFor(nameof(Angle))]
    [NotifyPropertyChangedFor(nameof(PixelLength))]
    private Point _endPoint;

    [ObservableProperty]
    private double _pixelLength;

    public override string MeasurementType => "Distance";

    /// <summary>
    /// Gets the midpoint of the line for label positioning.
    /// </summary>
    public Point MidPoint => new(
        (StartPoint.X + EndPoint.X) / 2,
        (StartPoint.Y + EndPoint.Y) / 2);

    /// <summary>
    /// Gets the angle of the line in degrees.
    /// </summary>
    public double Angle
    {
        get
        {
            var dx = EndPoint.X - StartPoint.X;
            var dy = EndPoint.Y - StartPoint.Y;
            return Math.Atan2(dy, dx) * 180 / Math.PI;
        }
    }

    partial void OnStartPointChanged(Point value)
    {
        CalculateLength();
    }

    partial void OnEndPointChanged(Point value)
    {
        CalculateLength();
    }

    private void CalculateLength()
    {
        var dx = EndPoint.X - StartPoint.X;
        var dy = EndPoint.Y - StartPoint.Y;
        PixelLength = Math.Sqrt(dx * dx + dy * dy);
        UpdateDisplayText();
    }

    protected override void UpdateDisplayText()
    {
        DisplayText = FormatMeasurement(PixelLength);
    }
}
