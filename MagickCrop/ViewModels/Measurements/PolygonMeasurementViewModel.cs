using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MagickCrop.Helpers;

namespace MagickCrop.ViewModels.Measurements;

/// <summary>
/// ViewModel for polygon measurement.
/// </summary>
public partial class PolygonMeasurementViewModel : MeasurementViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanClose))]
    private bool _isClosed;

    [ObservableProperty]
    private double _perimeter;

    [ObservableProperty]
    private double _area;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanClose))]
    private int _vertexCount;

    /// <summary>
    /// Gets whether the polygon can be closed (needs at least 3 vertices).
    /// </summary>
    public bool CanClose => !IsClosed && VertexCount >= 3;

    public ObservableCollection<Point> Vertices { get; } = [];

    public override string MeasurementType => "Polygon";

    public PolygonMeasurementViewModel()
    {
        Vertices.CollectionChanged += (_, _) => OnVerticesChanged();
        UpdateDisplayText();
    }

    public PolygonMeasurementViewModel(CommunityToolkit.Mvvm.Messaging.IMessenger messenger) : base(messenger)
    {
        Vertices.CollectionChanged += (_, _) => OnVerticesChanged();
        UpdateDisplayText();
    }

    /// <summary>
    /// Adds a vertex to the polygon.
    /// </summary>
    public void AddVertex(Point point)
    {
        Vertices.Add(point);
    }

    /// <summary>
    /// Updates a vertex at the specified index.
    /// </summary>
    public void UpdateVertex(int index, Point point)
    {
        if (index >= 0 && index < Vertices.Count)
        {
            Vertices[index] = point;
            OnVerticesChanged();
        }
    }

    /// <summary>
    /// Closes the polygon.
    /// </summary>
    public void Close()
    {
        if (Vertices.Count >= 3)
        {
            IsClosed = true;
            OnVerticesChanged();
        }
    }

    private void OnVerticesChanged()
    {
        VertexCount = Vertices.Count;
        CalculateMeasurements();
    }

    private void CalculateMeasurements()
    {
        if (Vertices.Count < 2)
        {
            Perimeter = 0;
            Area = 0;
            UpdateDisplayText();
            return;
        }

        Perimeter = GeometryMathHelper.PolygonPerimeter(Vertices.ToList(), IsClosed);

        if (IsClosed && Vertices.Count >= 3)
        {
            Area = GeometryMathHelper.PolygonArea(Vertices.ToList());
        }
        else
        {
            Area = 0;
        }

        UpdateDisplayText();
    }

    protected override void UpdateDisplayText()
    {
        if (Vertices.Count < 2)
        {
            DisplayText = "Add more points...";
            return;
        }

        var perimeterText = FormatMeasurement(Perimeter);

        if (IsClosed && Vertices.Count >= 3)
        {
            var areaText = FormatArea(Area);
            DisplayText = $"P: {perimeterText}\nA: {areaText}";
        }
        else
        {
            DisplayText = $"P: {perimeterText}\n({Vertices.Count} points)";
        }
    }
}
