using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.ViewModels.Measurements;

/// <summary>
/// ViewModel for rectangle measurement.
/// </summary>
public partial class RectangleMeasurementViewModel : MeasurementViewModelBase
{
    public RectangleMeasurementViewModel() { }

    public RectangleMeasurementViewModel(CommunityToolkit.Mvvm.Messaging.IMessenger messenger) : base(messenger) { }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Width))]
    [NotifyPropertyChangedFor(nameof(Height))]
    private Point _topLeft;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Width))]
    [NotifyPropertyChangedFor(nameof(Height))]
    private Point _bottomRight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Perimeter))]
    [NotifyPropertyChangedFor(nameof(Area))]
    private double _width;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Perimeter))]
    [NotifyPropertyChangedFor(nameof(Area))]
    private double _height;

    [ObservableProperty]
    private double _perimeter;

    [ObservableProperty]
    private double _area;

    public override string MeasurementType => "Rectangle";

    /// <summary>
    /// Gets the rectangle bounds.
    /// </summary>
    public Rect Bounds => new(TopLeft, BottomRight);

    partial void OnTopLeftChanged(Point value) => CalculateMeasurements();
    partial void OnBottomRightChanged(Point value) => CalculateMeasurements();

    private void CalculateMeasurements()
    {
        Width = Math.Abs(BottomRight.X - TopLeft.X);
        Height = Math.Abs(BottomRight.Y - TopLeft.Y);
        Perimeter = 2 * (Width + Height);
        Area = Width * Height;
        
        UpdateDisplayText();
    }

    protected override void UpdateDisplayText()
    {
        var widthText = FormatMeasurement(Width);
        var heightText = FormatMeasurement(Height);
        var areaText = FormatArea(Area);
        DisplayText = $"{widthText} × {heightText}\nArea: {areaText}";
    }
}
