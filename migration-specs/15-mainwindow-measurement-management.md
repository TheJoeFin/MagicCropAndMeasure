# Step 15: MainWindow ViewModel - Measurement Management

## Objective
Extract measurement collection management from MainWindow.xaml.cs into the ViewModel, including adding, removing, and serializing measurements.

## Prerequisites
- Step 14 completed (Image operations)

---

## ⚠️ This step has been broken into smaller sub-steps for easier implementation

**Complete these sub-steps in order:**

| Sub-Step | Description | Estimated Effort |
|----------|-------------|-----------------|
| **15a** | Add Distance and Angle ObservableCollections to MainWindowViewModel | 30 min |
| **15b** | Add Rectangle and Circle ObservableCollections | 20 min |
| **15c** | Add Polygon, HorizontalLine, and VerticalLine collections | 20 min |
| **15d** | Create AddMeasurementCommand<T> generic approach | 45 min |
| **15e** | Add specific AddDistanceCommand and AddAngleCommand | 30 min |
| **15f** | Add AddRectangleCommand and AddCircleCommand | 20 min |
| **15g** | Add AddPolygonCommand, AddHorizontalLineCommand, AddVerticalLineCommand | 30 min |
| **15h** | Add RemoveMeasurementCommand (works for all types) | 30 min |
| **15i** | Add ClearAllMeasurementsCommand | 20 min |
| **15j** | Add CreateMeasurementCollection method (for serialization) | 45 min |
| **15k** | Add LoadMeasurementsFromCollection method (for deserialization) | 45 min |
| **15l** | Wire measurement ItemsControls in MainWindow.xaml | 45 min |
| **15m** | Test adding/removing each measurement type | 30 min |

Each sub-step should be its own commit with a working build.

---

## Current Measurement Management

MainWindow currently manages:
- 5 measurement type collections (Distance, Angle, Rectangle, Circle, Polygon)
- 2 line guide collections (Horizontal, Vertical)
- Adding/removing measurements
- Converting between controls and DTOs
- Serialization for save/load

## Changes Required

### 1. Add Measurement Collections to MainWindowViewModel

**File: `ViewModels/MainWindowViewModel.cs`** (add to existing)

```csharp
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MagickCrop.Controls;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.ViewModels.Measurements;

public partial class MainWindowViewModel : ViewModelBase
{
    #region Measurement Collections

    /// <summary>
    /// Collection of distance measurements.
    /// </summary>
    public ObservableCollection<DistanceMeasurementViewModel> DistanceMeasurements { get; } = [];

    /// <summary>
    /// Collection of angle measurements.
    /// </summary>
    public ObservableCollection<AngleMeasurementViewModel> AngleMeasurements { get; } = [];

    /// <summary>
    /// Collection of rectangle measurements.
    /// </summary>
    public ObservableCollection<RectangleMeasurementViewModel> RectangleMeasurements { get; } = [];

    /// <summary>
    /// Collection of circle measurements.
    /// </summary>
    public ObservableCollection<CircleMeasurementViewModel> CircleMeasurements { get; } = [];

    /// <summary>
    /// Collection of polygon measurements.
    /// </summary>
    public ObservableCollection<PolygonMeasurementViewModel> PolygonMeasurements { get; } = [];

    /// <summary>
    /// Collection of horizontal line guides.
    /// </summary>
    public ObservableCollection<HorizontalLineViewModel> HorizontalLines { get; } = [];

    /// <summary>
    /// Collection of vertical line guides.
    /// </summary>
    public ObservableCollection<VerticalLineViewModel> VerticalLines { get; } = [];

    /// <summary>
    /// Gets the total count of all measurements.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMeasurements))]
    private int _totalMeasurementCount;

    /// <summary>
    /// Gets whether there are any measurements.
    /// </summary>
    public bool HasMeasurements => TotalMeasurementCount > 0;

    #endregion

    // Add to constructor
    private void InitializeMeasurementCollections()
    {
        // Subscribe to collection changes
        DistanceMeasurements.CollectionChanged += OnMeasurementCollectionChanged;
        AngleMeasurements.CollectionChanged += OnMeasurementCollectionChanged;
        RectangleMeasurements.CollectionChanged += OnMeasurementCollectionChanged;
        CircleMeasurements.CollectionChanged += OnMeasurementCollectionChanged;
        PolygonMeasurements.CollectionChanged += OnMeasurementCollectionChanged;
        HorizontalLines.CollectionChanged += OnMeasurementCollectionChanged;
        VerticalLines.CollectionChanged += OnMeasurementCollectionChanged;
        
        // Register for remove requests
        Register<RemoveMeasurementRequestMessage>(OnRemoveMeasurementRequested);
    }

    private void OnMeasurementCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateMeasurementCount();
        IsDirty = true;
    }

    private void UpdateMeasurementCount()
    {
        TotalMeasurementCount = 
            DistanceMeasurements.Count +
            AngleMeasurements.Count +
            RectangleMeasurements.Count +
            CircleMeasurements.Count +
            PolygonMeasurements.Count +
            HorizontalLines.Count +
            VerticalLines.Count;
    }

    #region Add Measurement Commands

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddDistanceMeasurement(System.Windows.Point[] points)
    {
        if (points.Length < 2) return;

        var vm = new DistanceMeasurementViewModel
        {
            StartPoint = points[0],
            EndPoint = points[1],
            ScaleFactor = GlobalScaleFactor,
            Units = GlobalUnits
        };

        DistanceMeasurements.Add(vm);
        Send(new MeasurementAddedMessage("Distance", vm.Id));
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddAngleMeasurement(System.Windows.Point[] points)
    {
        if (points.Length < 3) return;

        var vm = new AngleMeasurementViewModel
        {
            Point1 = points[0],
            Vertex = points[1],
            Point2 = points[2],
            ScaleFactor = GlobalScaleFactor,
            Units = GlobalUnits
        };

        AngleMeasurements.Add(vm);
        Send(new MeasurementAddedMessage("Angle", vm.Id));
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddRectangleMeasurement(System.Windows.Point[] points)
    {
        if (points.Length < 2) return;

        var vm = new RectangleMeasurementViewModel
        {
            TopLeft = points[0],
            BottomRight = points[1],
            ScaleFactor = GlobalScaleFactor,
            Units = GlobalUnits
        };

        RectangleMeasurements.Add(vm);
        Send(new MeasurementAddedMessage("Rectangle", vm.Id));
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddCircleMeasurement(System.Windows.Point[] points)
    {
        if (points.Length < 2) return;

        var vm = new CircleMeasurementViewModel
        {
            Center = points[0],
            EdgePoint = points[1],
            ScaleFactor = GlobalScaleFactor,
            Units = GlobalUnits
        };

        CircleMeasurements.Add(vm);
        Send(new MeasurementAddedMessage("Circle", vm.Id));
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddPolygonMeasurement(System.Windows.Point[] points)
    {
        var vm = new PolygonMeasurementViewModel
        {
            ScaleFactor = GlobalScaleFactor,
            Units = GlobalUnits
        };

        foreach (var point in points)
        {
            vm.AddVertex(point);
        }

        PolygonMeasurements.Add(vm);
        Send(new MeasurementAddedMessage("Polygon", vm.Id));
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddHorizontalLine(double y)
    {
        var vm = new HorizontalLineViewModel
        {
            Position = y,
            CanvasSize = ImageHeight
        };

        HorizontalLines.Add(vm);
        Send(new MeasurementAddedMessage("HorizontalLine", vm.Id));
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void AddVerticalLine(double x)
    {
        var vm = new VerticalLineViewModel
        {
            Position = x,
            CanvasSize = ImageWidth
        };

        VerticalLines.Add(vm);
        Send(new MeasurementAddedMessage("VerticalLine", vm.Id));
    }

    #endregion

    #region Remove Measurements

    private void OnRemoveMeasurementRequested(RemoveMeasurementRequestMessage message)
    {
        RemoveMeasurementById(message.MeasurementId, message.MeasurementType);
    }

    /// <summary>
    /// Removes a measurement by its ID and type.
    /// </summary>
    public void RemoveMeasurementById(Guid id, string type)
    {
        switch (type)
        {
            case "Distance":
                var distance = DistanceMeasurements.FirstOrDefault(m => m.Id == id);
                if (distance != null) DistanceMeasurements.Remove(distance);
                break;
            case "Angle":
                var angle = AngleMeasurements.FirstOrDefault(m => m.Id == id);
                if (angle != null) AngleMeasurements.Remove(angle);
                break;
            case "Rectangle":
                var rect = RectangleMeasurements.FirstOrDefault(m => m.Id == id);
                if (rect != null) RectangleMeasurements.Remove(rect);
                break;
            case "Circle":
                var circle = CircleMeasurements.FirstOrDefault(m => m.Id == id);
                if (circle != null) CircleMeasurements.Remove(circle);
                break;
            case "Polygon":
                var polygon = PolygonMeasurements.FirstOrDefault(m => m.Id == id);
                if (polygon != null) PolygonMeasurements.Remove(polygon);
                break;
            case "HorizontalLine":
                var hLine = HorizontalLines.FirstOrDefault(m => m.Id == id);
                if (hLine != null) HorizontalLines.Remove(hLine);
                break;
            case "VerticalLine":
                var vLine = VerticalLines.FirstOrDefault(m => m.Id == id);
                if (vLine != null) VerticalLines.Remove(vLine);
                break;
        }

        Send(new MeasurementRemovedMessage(id));
    }

    [RelayCommand]
    private void ClearAllMeasurements()
    {
        if (!_navigationService.ShowConfirmation("Clear all measurements?"))
            return;

        DistanceMeasurements.Clear();
        AngleMeasurements.Clear();
        RectangleMeasurements.Clear();
        CircleMeasurements.Clear();
        PolygonMeasurements.Clear();
        HorizontalLines.Clear();
        VerticalLines.Clear();
    }

    #endregion

    #region Measurement Serialization

    /// <summary>
    /// Creates a MeasurementCollection from current ViewModels.
    /// </summary>
    public MeasurementCollection ToMeasurementCollection()
    {
        var collection = new MeasurementCollection
        {
            GlobalScaleFactor = GlobalScaleFactor,
            GlobalUnits = GlobalUnits
        };

        // Convert Distance measurements
        foreach (var vm in DistanceMeasurements)
        {
            collection.DistanceMeasurements.Add(new DistanceMeasurementControlDto
            {
                StartX = vm.StartPoint.X,
                StartY = vm.StartPoint.Y,
                EndX = vm.EndPoint.X,
                EndY = vm.EndPoint.Y,
                ScaleFactor = vm.ScaleFactor,
                Units = vm.Units
            });
        }

        // Convert Angle measurements
        foreach (var vm in AngleMeasurements)
        {
            collection.AngleMeasurements.Add(new AngleMeasurementControlDto
            {
                Point1X = vm.Point1.X,
                Point1Y = vm.Point1.Y,
                VertexX = vm.Vertex.X,
                VertexY = vm.Vertex.Y,
                Point2X = vm.Point2.X,
                Point2Y = vm.Point2.Y
            });
        }

        // Convert Rectangle measurements
        foreach (var vm in RectangleMeasurements)
        {
            collection.RectangleMeasurements.Add(new RectangleMeasurementControlDto
            {
                TopLeftX = vm.TopLeft.X,
                TopLeftY = vm.TopLeft.Y,
                BottomRightX = vm.BottomRight.X,
                BottomRightY = vm.BottomRight.Y,
                ScaleFactor = vm.ScaleFactor,
                Units = vm.Units
            });
        }

        // Convert Circle measurements
        foreach (var vm in CircleMeasurements)
        {
            collection.CircleMeasurements.Add(new CircleMeasurementControlDto
            {
                CenterX = vm.Center.X,
                CenterY = vm.Center.Y,
                EdgeX = vm.EdgePoint.X,
                EdgeY = vm.EdgePoint.Y,
                ScaleFactor = vm.ScaleFactor,
                Units = vm.Units
            });
        }

        // Convert Polygon measurements
        foreach (var vm in PolygonMeasurements)
        {
            var dto = new PolygonMeasurementControlDto
            {
                IsClosed = vm.IsClosed,
                ScaleFactor = vm.ScaleFactor,
                Units = vm.Units
            };
            foreach (var vertex in vm.Vertices)
            {
                dto.Vertices.Add(new PointDto { X = vertex.X, Y = vertex.Y });
            }
            collection.PolygonMeasurements.Add(dto);
        }

        // Convert line guides
        foreach (var vm in HorizontalLines)
        {
            collection.HorizontalLines.Add(new HorizontalLineControlDto
            {
                Y = vm.Position
            });
        }

        foreach (var vm in VerticalLines)
        {
            collection.VerticalLines.Add(new VerticalLineControlDto
            {
                X = vm.Position
            });
        }

        return collection;
    }

    /// <summary>
    /// Loads measurements from a MeasurementCollection.
    /// </summary>
    public void LoadMeasurementCollection(MeasurementCollection collection)
    {
        ClearAllMeasurementsInternal();

        GlobalScaleFactor = collection.GlobalScaleFactor;
        GlobalUnits = collection.GlobalUnits;

        // Load Distance measurements
        foreach (var dto in collection.DistanceMeasurements)
        {
            var vm = new DistanceMeasurementViewModel
            {
                StartPoint = new System.Windows.Point(dto.StartX, dto.StartY),
                EndPoint = new System.Windows.Point(dto.EndX, dto.EndY),
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            DistanceMeasurements.Add(vm);
        }

        // Load Angle measurements
        foreach (var dto in collection.AngleMeasurements)
        {
            var vm = new AngleMeasurementViewModel
            {
                Point1 = new System.Windows.Point(dto.Point1X, dto.Point1Y),
                Vertex = new System.Windows.Point(dto.VertexX, dto.VertexY),
                Point2 = new System.Windows.Point(dto.Point2X, dto.Point2Y)
            };
            AngleMeasurements.Add(vm);
        }

        // Load Rectangle measurements
        foreach (var dto in collection.RectangleMeasurements)
        {
            var vm = new RectangleMeasurementViewModel
            {
                TopLeft = new System.Windows.Point(dto.TopLeftX, dto.TopLeftY),
                BottomRight = new System.Windows.Point(dto.BottomRightX, dto.BottomRightY),
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            RectangleMeasurements.Add(vm);
        }

        // Load Circle measurements
        foreach (var dto in collection.CircleMeasurements)
        {
            var vm = new CircleMeasurementViewModel
            {
                Center = new System.Windows.Point(dto.CenterX, dto.CenterY),
                EdgePoint = new System.Windows.Point(dto.EdgeX, dto.EdgeY),
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            CircleMeasurements.Add(vm);
        }

        // Load Polygon measurements
        foreach (var dto in collection.PolygonMeasurements)
        {
            var vm = new PolygonMeasurementViewModel
            {
                ScaleFactor = dto.ScaleFactor,
                Units = dto.Units
            };
            foreach (var point in dto.Vertices)
            {
                vm.AddVertex(new System.Windows.Point(point.X, point.Y));
            }
            if (dto.IsClosed) vm.Close();
            PolygonMeasurements.Add(vm);
        }

        // Load line guides
        foreach (var dto in collection.HorizontalLines)
        {
            HorizontalLines.Add(new HorizontalLineViewModel { Position = dto.Y });
        }

        foreach (var dto in collection.VerticalLines)
        {
            VerticalLines.Add(new VerticalLineViewModel { Position = dto.X });
        }
    }

    private void ClearAllMeasurementsInternal()
    {
        DistanceMeasurements.Clear();
        AngleMeasurements.Clear();
        RectangleMeasurements.Clear();
        CircleMeasurements.Clear();
        PolygonMeasurements.Clear();
        HorizontalLines.Clear();
        VerticalLines.Clear();
    }

    #endregion
}
```

### 2. Create PointDto for Serialization

**File: `Models/MeasurementControls/PointDto.cs`**

```csharp
namespace MagickCrop.Models.MeasurementControls;

/// <summary>
/// Simple point DTO for serialization.
/// </summary>
public class PointDto
{
    public double X { get; set; }
    public double Y { get; set; }
}
```

### 3. Update MainWindow XAML for Measurement Display

**File: `MainWindow.xaml`** (add ItemsControl for measurements)

```xml
<!-- Measurement canvas overlay -->
<Canvas x:Name="MeasurementCanvas" 
        IsHitTestVisible="True">
    
    <!-- Distance Measurements -->
    <ItemsControl ItemsSource="{Binding DistanceMeasurements}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <Canvas/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <controls:DistanceMeasurementControl DataContext="{Binding}"/>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
    
    <!-- Angle Measurements -->
    <ItemsControl ItemsSource="{Binding AngleMeasurements}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <Canvas/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <controls:AngleMeasurementControl DataContext="{Binding}"/>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
    
    <!-- Rectangle Measurements -->
    <ItemsControl ItemsSource="{Binding RectangleMeasurements}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <Canvas/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <controls:RectangleMeasurementControl DataContext="{Binding}"/>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
    
    <!-- Circle Measurements -->
    <ItemsControl ItemsSource="{Binding CircleMeasurements}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <Canvas/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <controls:CircleMeasurementControl DataContext="{Binding}"/>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
    
    <!-- Polygon Measurements -->
    <ItemsControl ItemsSource="{Binding PolygonMeasurements}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <Canvas/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <controls:PolygonMeasurementControl DataContext="{Binding}"/>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
    
    <!-- Horizontal Lines -->
    <ItemsControl ItemsSource="{Binding HorizontalLines}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <Canvas/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <controls:HorizontalLineControl DataContext="{Binding}"/>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
    
    <!-- Vertical Lines -->
    <ItemsControl ItemsSource="{Binding VerticalLines}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <Canvas/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <controls:VerticalLineControl DataContext="{Binding}"/>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</Canvas>

<!-- Measurement count display -->
<TextBlock Text="{Binding TotalMeasurementCount, StringFormat='{}{0} measurements'}"
           Visibility="{Binding HasMeasurements, Converter={StaticResource BooleanToVisibilityConverter}}"/>

<!-- Clear measurements button -->
<ui:Button Content="Clear All" 
           Command="{Binding ClearAllMeasurementsCommand}"
           IsEnabled="{Binding HasMeasurements}"/>
```

### 4. Update Measurement Controls to Use ViewModels

Each measurement control needs to accept its ViewModel through DataContext binding. The controls from Step 12 should already support this.

---

## Implementation Steps

1. Add measurement collection properties to `MainWindowViewModel`
2. Create `PointDto` class
3. Implement add/remove commands
4. Implement serialization methods
5. Update `MainWindow.xaml` with ItemsControl bindings
6. Test adding, removing, and clearing measurements
7. Test save/load cycle

---

## Validation Checklist

- [ ] All measurement collections work
- [ ] Adding measurements via commands works
- [ ] Removing measurements works
- [ ] Clear all measurements works
- [ ] Total count updates correctly
- [ ] Serialization to DTOs works
- [ ] Deserialization from DTOs works
- [ ] Scale factor propagates to all measurements
- [ ] ItemsControl displays measurements correctly

---

## Files Changed/Created

| File | Change Type |
|------|-------------|
| `ViewModels/MainWindowViewModel.cs` | Modified |
| `Models/MeasurementControls/PointDto.cs` | Created |
| `MainWindow.xaml` | Modified |

---

## Notes

### ItemsControl with Canvas

Using ItemsControl with Canvas ItemsPanel allows measurements to be positioned absolutely:
```xml
<ItemsControl.ItemsPanel>
    <ItemsPanelTemplate>
        <Canvas/>
    </ItemsPanelTemplate>
</ItemsControl.ItemsPanel>
```

### Collection Change Notifications

When any measurement collection changes, we:
1. Update the total count
2. Mark document as dirty
3. (Optionally) trigger auto-save

### DTO Conversion

ViewModels → DTOs for saving:
- Clean separation of concerns
- DTOs are simple and serialization-friendly
- No UI dependencies in saved data

---

## Next Steps

Proceed to **Step 16: MainWindow ViewModel - File Operations** to complete the file save/load functionality.
