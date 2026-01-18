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

    /// <summary>
    /// Parameterless constructor - uses default messenger (calls base class).
    /// The base ViewModelBase class handles RegisterAll() for IRecipient implementations.
    /// </summary>
    protected MeasurementViewModelBase()
    {
    }

    /// <summary>
    /// Constructor with messenger parameter for dependency injection (e.g., for testing).
    /// The base ViewModelBase class handles RegisterAll() for IRecipient implementations.
    /// </summary>
    protected MeasurementViewModelBase(CommunityToolkit.Mvvm.Messaging.IMessenger messenger) : base(messenger)
    {
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
