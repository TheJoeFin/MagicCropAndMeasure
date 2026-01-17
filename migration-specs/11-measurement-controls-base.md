# Step 11: Measurement Controls Base Class

## Objective
Create a shared base ViewModel and base UserControl class for all measurement controls to reduce code duplication and establish consistent patterns.

## Prerequisites
- Step 10 completed (RecentProjectItem migration)

---

## ⚠️ This step has been broken into smaller sub-steps for easier implementation

Complete these sub-steps in order:

| Sub-Step | Description | Estimated Effort |
|----------|-------------|-----------------|
| **11a** | Create ViewModels/Measurements folder | 5 min |
| **11b** | Create MeasurementViewModelBase with common properties (Id, ScaleFactor, Units, DisplayText) | 45 min |
| **11c** | Add IRecipient<ScaleFactorChangedMessage> to MeasurementViewModelBase | 20 min |
| **11d** | Create Controls/MeasurementControlBase.cs with DependencyProperties | 30 min |
| **11e** | Create DistanceMeasurementViewModel | 30 min |
| **11f** | Create AngleMeasurementViewModel | 30 min |
| **11g** | Create RectangleMeasurementViewModel | 30 min |
| **11h** | Create CircleMeasurementViewModel | 30 min |
| **11i** | Create LineControlViewModelBase, HorizontalLineViewModel, VerticalLineViewModel | 30 min |
| **11j** | Build and verify all ViewModels compile | 15 min |

Each sub-step should be its own commit with a working build.

**Note:** PolygonMeasurementViewModel will be created in Step 12 during the actual control migration since it requires more complex vertex handling.

---

## Current State Analysis

**Existing measurement controls:**
- DistanceMeasurementControl
- AngleMeasurementControl
- CircleMeasurementControl
- RectangleMeasurementControl
- PolygonMeasurementControl
- HorizontalLineControl
- VerticalLineControl

**Common patterns across controls:**
- Scale factor and units properties
- Remove control event
- Mouse event handling for dragging
- Point/position management
- Formatting display text

## Changes Required

### 1. Create MeasurementViewModelBase

**File: `ViewModels/Measurements/MeasurementViewModelBase.cs`**

```csharp
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MagickCrop.Messages;
using MagickCrop.ViewModels.Base;

namespace MagickCrop.ViewModels.Measurements;

/// <summary>
/// Base ViewModel for all measurement controls.
/// Implements IRecipient for automatic message registration.
/// </summary>
public abstract partial class MeasurementViewModelBase : ViewModelBase, 
    IRecipient<ScaleFactorChangedMessage>
{
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private double _scaleFactor = 1.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string _units = "px";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private System.Windows.Media.Color _color = System.Windows.Media.Colors.Cyan;

    [ObservableProperty]
    private double _strokeThickness = 2.0;

    /// <summary>
    /// Gets the measurement type name.
    /// </summary>
    public abstract string MeasurementType { get; }

    protected MeasurementViewModelBase()
    {
        // Use IRecipient pattern - RegisterAll handles all IRecipient<T> implementations
        Messenger.RegisterAll(this);
    }

    protected MeasurementViewModelBase(IMessenger messenger) : base(messenger)
    {
        Messenger.RegisterAll(this);
    }

    /// <summary>
    /// Handles ScaleFactorChangedMessage via IRecipient interface.
    /// </summary>
    public void Receive(ScaleFactorChangedMessage message)
    {
        ScaleFactor = message.NewScaleFactor;
        Units = message.Units;
        UpdateDisplayText();
    }

    /// <summary>
    /// Updates the display text based on current measurements.
    /// Override in derived classes.
    /// </summary>
    protected abstract void UpdateDisplayText();

    /// <summary>
    /// Calculates scaled measurement from pixel value.
    /// </summary>
    protected double ToScaledValue(double pixelValue)
    {
        return pixelValue * ScaleFactor;
    }

    /// <summary>
    /// Formats a measurement value for display.
    /// </summary>
    protected string FormatMeasurement(double pixelValue)
    {
        var scaled = ToScaledValue(pixelValue);
        return Units == "px" 
            ? $"{pixelValue:F1} px" 
            : $"{scaled:F2} {Units}";
    }

    /// <summary>
    /// Formats an area measurement for display.
    /// </summary>
    protected string FormatArea(double pixelArea)
    {
        var scaled = pixelArea * ScaleFactor * ScaleFactor;
        return Units == "px" 
            ? $"{pixelArea:F1} px²" 
            : $"{scaled:F2} {Units}²";
    }

    /// <summary>
    /// Requests removal of this measurement.
    /// </summary>
    [RelayCommand]
    protected virtual void Remove()
    {
        Send(new RemoveMeasurementRequestMessage(Id, MeasurementType));
    }

    /// <summary>
    /// Called when scale factor changes. Triggers display text update.
    /// </summary>
    partial void OnScaleFactorChanged(double value)
    {
        UpdateDisplayText();
    }

    /// <summary>
    /// Called when units change. Triggers display text update.
    /// </summary>
    partial void OnUnitsChanged(string value)
    {
        UpdateDisplayText();
    }
}
```

### 2. Create MeasurementControlBase UserControl

**File: `Controls/MeasurementControlBase.cs`**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MagickCrop.ViewModels.Measurements;

namespace MagickCrop.Controls;

/// <summary>
/// Base class for measurement UserControls.
/// </summary>
public abstract class MeasurementControlBase : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ScaleFactorProperty =
        DependencyProperty.Register(
            nameof(ScaleFactor),
            typeof(double),
            typeof(MeasurementControlBase),
            new PropertyMetadata(1.0, OnScaleFactorChanged));

    public static readonly DependencyProperty UnitsProperty =
        DependencyProperty.Register(
            nameof(Units),
            typeof(string),
            typeof(MeasurementControlBase),
            new PropertyMetadata("px", OnUnitsChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(MeasurementControlBase),
            new PropertyMetadata(false));

    /// <summary>
    /// Gets or sets the scale factor for converting pixels to real-world units.
    /// </summary>
    public double ScaleFactor
    {
        get => (double)GetValue(ScaleFactorProperty);
        set => SetValue(ScaleFactorProperty, value);
    }

    /// <summary>
    /// Gets or sets the units string (e.g., "px", "cm", "in").
    /// </summary>
    public string Units
    {
        get => (string)GetValue(UnitsProperty);
        set => SetValue(UnitsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this measurement is selected.
    /// </summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    private static void OnScaleFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MeasurementControlBase control && control.DataContext is MeasurementViewModelBase vm)
        {
            vm.ScaleFactor = (double)e.NewValue;
        }
    }

    private static void OnUnitsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MeasurementControlBase control && control.DataContext is MeasurementViewModelBase vm)
        {
            vm.Units = (string)e.NewValue;
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a measurement point receives mouse down.
    /// </summary>
    public event MouseButtonEventHandler? MeasurementPointMouseDown;

    /// <summary>
    /// Event raised when the control requests to be removed.
    /// </summary>
    public event EventHandler? RemoveRequested;

    /// <summary>
    /// Event raised when the user wants to set a real-world length.
    /// </summary>
    public event EventHandler<SetRealWorldLengthEventArgs>? SetRealWorldLengthRequested;

    #endregion

    protected MeasurementControlBase()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected virtual void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MeasurementViewModelBase vm)
        {
            vm.ScaleFactor = ScaleFactor;
            vm.Units = Units;
        }
    }

    protected virtual void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MeasurementViewModelBase vm)
        {
            vm.Cleanup();
        }
    }

    /// <summary>
    /// Raises the MeasurementPointMouseDown event.
    /// </summary>
    protected void RaiseMeasurementPointMouseDown(MouseButtonEventArgs e)
    {
        MeasurementPointMouseDown?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the RemoveRequested event.
    /// </summary>
    protected void RaiseRemoveRequested()
    {
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises the SetRealWorldLengthRequested event.
    /// </summary>
    protected void RaiseSetRealWorldLengthRequested(double pixelLength)
    {
        SetRealWorldLengthRequested?.Invoke(this, new SetRealWorldLengthEventArgs(pixelLength));
    }
}

/// <summary>
/// Event args for SetRealWorldLengthRequested event.
/// </summary>
public class SetRealWorldLengthEventArgs : EventArgs
{
    public double PixelLength { get; }

    public SetRealWorldLengthEventArgs(double pixelLength)
    {
        PixelLength = pixelLength;
    }
}
```

### 3. Create Specific Measurement ViewModels

**File: `ViewModels/Measurements/DistanceMeasurementViewModel.cs`**

```csharp
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
```

**File: `ViewModels/Measurements/AngleMeasurementViewModel.cs`**

```csharp
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
```

**File: `ViewModels/Measurements/RectangleMeasurementViewModel.cs`**

```csharp
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.ViewModels.Measurements;

/// <summary>
/// ViewModel for rectangle measurement.
/// </summary>
public partial class RectangleMeasurementViewModel : MeasurementViewModelBase
{
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
```

### 4. Create Line Control ViewModels

**File: `ViewModels/Measurements/LineControlViewModelBase.cs`**

```csharp
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
```

---

## Implementation Steps

1. Create `ViewModels/Measurements` folder
2. Create `MeasurementViewModelBase.cs`
3. Create `Controls/MeasurementControlBase.cs`
4. Create specific measurement ViewModels
5. Build and verify base classes compile
6. (Individual control migrations in Step 12)

---

## Validation Checklist

- [ ] MeasurementViewModelBase compiles
- [ ] MeasurementControlBase compiles
- [ ] All specific ViewModels compile
- [ ] Base class properly handles scale/units changes
- [ ] Remove command sends correct message

---

## Files Created

| File | Description |
|------|-------------|
| `ViewModels/Measurements/MeasurementViewModelBase.cs` | Base ViewModel |
| `Controls/MeasurementControlBase.cs` | Base UserControl |
| `ViewModels/Measurements/DistanceMeasurementViewModel.cs` | Distance ViewModel |
| `ViewModels/Measurements/AngleMeasurementViewModel.cs` | Angle ViewModel |
| `ViewModels/Measurements/RectangleMeasurementViewModel.cs` | Rectangle ViewModel |
| `ViewModels/Measurements/LineControlViewModelBase.cs` | Line guides ViewModels |

---

## Notes

### Why Base Classes?

1. **Code Reuse**: Common logic lives in one place
2. **Consistency**: All measurements work the same way
3. **Testability**: Base behavior can be tested once
4. **Extensibility**: Easy to add new measurement types

### Scale Factor Propagation

Scale factor changes propagate through messaging:
```
MainWindow changes scale
    → Sends ScaleFactorChangedMessage
        → All MeasurementViewModels receive
            → Each recalculates DisplayText
```

### Partial Methods Pattern

CommunityToolkit.Mvvm generates property change handlers:
```csharp
partial void OnStartPointChanged(Point value)
{
    // Called automatically when StartPoint changes
    CalculateLength();
}
```

---

## Next Steps

Proceed to **Step 12: Individual Measurement Control Migrations** to update each control to use the new base classes.
