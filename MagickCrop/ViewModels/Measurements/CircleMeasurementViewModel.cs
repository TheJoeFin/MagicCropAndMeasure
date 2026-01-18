using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.ViewModels.Measurements;

/// <summary>
/// ViewModel for circle measurement.
/// </summary>
public partial class CircleMeasurementViewModel : MeasurementViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Radius))]
    [NotifyPropertyChangedFor(nameof(Diameter))]
    [NotifyPropertyChangedFor(nameof(Circumference))]
    [NotifyPropertyChangedFor(nameof(Area))]
    [NotifyPropertyChangedFor(nameof(CircleCanvasLeft))]
    [NotifyPropertyChangedFor(nameof(CircleCanvasTop))]
    private Point _centerPoint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Radius))]
    [NotifyPropertyChangedFor(nameof(Diameter))]
    [NotifyPropertyChangedFor(nameof(Circumference))]
    [NotifyPropertyChangedFor(nameof(Area))]
    [NotifyPropertyChangedFor(nameof(CircleCanvasLeft))]
    [NotifyPropertyChangedFor(nameof(CircleCanvasTop))]
    private Point _edgePoint;

    [ObservableProperty]
    private double _radius;

    [ObservableProperty]
    private double _diameter;

    [ObservableProperty]
    private double _circumference;

    [ObservableProperty]
    private double _area;

    public override string MeasurementType => "Circle";

    /// <summary>
    /// Gets the left position for the circle's canvas element (center - radius).
    /// </summary>
    public double CircleCanvasLeft => CenterPoint.X - Radius;

    /// <summary>
    /// Gets the top position for the circle's canvas element (center - radius).
    /// </summary>
    public double CircleCanvasTop => CenterPoint.Y - Radius;

    partial void OnCenterPointChanged(Point value) => CalculateMeasurements();
    partial void OnEdgePointChanged(Point value) => CalculateMeasurements();

    private void CalculateMeasurements()
    {
        var dx = EdgePoint.X - CenterPoint.X;
        var dy = EdgePoint.Y - CenterPoint.Y;
        Radius = Math.Sqrt(dx * dx + dy * dy);
        Diameter = Radius * 2;
        Circumference = Math.PI * Diameter;
        Area = Math.PI * Radius * Radius;
        
        UpdateDisplayText();
    }

    protected override void UpdateDisplayText()
    {
        var radiusText = FormatMeasurement(Radius);
        var diameterText = FormatMeasurement(Diameter);
        var circumferenceText = FormatMeasurement(Circumference);
        var areaText = FormatArea(Area);
        DisplayText = $"R: {radiusText}\nD: {diameterText}\nC: {circumferenceText}\nA: {areaText}";
    }
}
