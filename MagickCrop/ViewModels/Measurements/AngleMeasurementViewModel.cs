using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.ViewModels.Measurements;

/// <summary>
/// ViewModel for angle measurement.
/// </summary>
public partial class AngleMeasurementViewModel : MeasurementViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AngleDegrees))]
    private Point _point1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AngleDegrees))]
    private Point _vertex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AngleDegrees))]
    private Point _point2;

    [ObservableProperty]
    private double _angleDegrees;

    public override string MeasurementType => "Angle";

    partial void OnPoint1Changed(Point value) => CalculateAngle();
    partial void OnVertexChanged(Point value) => CalculateAngle();
    partial void OnPoint2Changed(Point value) => CalculateAngle();

    private void CalculateAngle()
    {
        // Vector from vertex to point1
        var v1 = new Vector(Point1.X - Vertex.X, Point1.Y - Vertex.Y);
        // Vector from vertex to point2
        var v2 = new Vector(Point2.X - Vertex.X, Point2.Y - Vertex.Y);

        if (v1.Length == 0 || v2.Length == 0)
        {
            AngleDegrees = 0;
            return;
        }

        // Calculate angle using dot product
        var dot = v1.X * v2.X + v1.Y * v2.Y;
        var cross = v1.X * v2.Y - v1.Y * v2.X;
        AngleDegrees = Math.Atan2(Math.Abs(cross), dot) * 180 / Math.PI;
        
        UpdateDisplayText();
    }

    protected override void UpdateDisplayText()
    {
        DisplayText = $"{AngleDegrees:F1}°";
    }
}
