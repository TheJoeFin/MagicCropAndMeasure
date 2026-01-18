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
