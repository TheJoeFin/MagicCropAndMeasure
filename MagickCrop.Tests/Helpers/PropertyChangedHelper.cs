using System.Collections.Generic;
using System.ComponentModel;

namespace MagickCrop.Tests.Helpers;

/// <summary>
/// Represents a single property change event with old and new values.
/// </summary>
public class PropertyChange
{
    /// <summary>
    /// Gets the name of the property that changed.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the previous value of the property.
    /// </summary>
    public object? OldValue { get; }

    /// <summary>
    /// Gets the current value of the property.
    /// </summary>
    public object? NewValue { get; }

    /// <summary>
    /// Gets the timestamp when the change was recorded.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Initializes a new instance of the PropertyChange class.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="oldValue">The previous value.</param>
    /// <param name="newValue">The current value.</param>
    public PropertyChange(string propertyName, object? oldValue, object? newValue)
    {
        PropertyName = propertyName;
        OldValue = oldValue;
        NewValue = newValue;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Tracks property change notifications from objects implementing INotifyPropertyChanged.
/// Allows tests to verify that properties changed and with what values.
/// </summary>
public class PropertyChangeTracker
{
    private readonly Dictionary<string, List<PropertyChange>> _changes = [];

    /// <summary>
    /// Gets all property changes recorded for a specific property.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>List of property changes, or null if no changes recorded.</returns>
    public IReadOnlyList<PropertyChange>? GetChanges(string propertyName)
    {
        return _changes.TryGetValue(propertyName, out var changes) ? changes.AsReadOnly() : null;
    }

    /// <summary>
    /// Gets the number of times a specific property changed.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The number of changes, or 0 if no changes recorded.</returns>
    public int GetChangeCount(string propertyName)
    {
        return _changes.TryGetValue(propertyName, out var changes) ? changes.Count : 0;
    }

    /// <summary>
    /// Gets all property changes across all properties.
    /// </summary>
    /// <returns>A dictionary of all recorded changes keyed by property name.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<PropertyChange>> GetAllChanges()
    {
        var result = new Dictionary<string, IReadOnlyList<PropertyChange>>();
        foreach (var kvp in _changes)
        {
            result[kvp.Key] = kvp.Value.AsReadOnly();
        }
        return result;
    }

    /// <summary>
    /// Resets all recorded changes.
    /// </summary>
    public void Reset()
    {
        _changes.Clear();
    }

    /// <summary>
    /// Records a property change event.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    /// <param name="oldValue">The previous value.</param>
    /// <param name="newValue">The current value.</param>
    internal void RecordChange(string propertyName, object? oldValue, object? newValue)
    {
        if (!_changes.ContainsKey(propertyName))
        {
            _changes[propertyName] = [];
        }

        _changes[propertyName].Add(new PropertyChange(propertyName, oldValue, newValue));
    }
}

/// <summary>
/// Helper class for monitoring and verifying INotifyPropertyChanged events in unit tests.
/// </summary>
public class PropertyChangedHelper
{
    /// <summary>
    /// Creates and returns a tracker that monitors property changes on the specified object.
    /// </summary>
    /// <param name="notifyPropertyChanged">The object implementing INotifyPropertyChanged to monitor.</param>
    /// <returns>A PropertyChangeTracker that records all property change notifications.</returns>
    /// <exception cref="ArgumentNullException">Thrown if notifyPropertyChanged is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the object does not implement INotifyPropertyChanged.</exception>
    public PropertyChangeTracker CreateTracker(INotifyPropertyChanged notifyPropertyChanged)
    {
        if (notifyPropertyChanged == null)
        {
            throw new ArgumentNullException(nameof(notifyPropertyChanged));
        }

        var tracker = new PropertyChangeTracker();

        // Subscribe to PropertyChanged events, storing old values as we detect changes
        var lastValues = new Dictionary<string, object?>();

        notifyPropertyChanged.PropertyChanged += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName))
            {
                return; // Ignore changes with no property name
            }

            // Get the current value (we can't get actual old value from the event, so null)
            var oldValue = lastValues.ContainsKey(e.PropertyName) 
                ? lastValues[e.PropertyName] 
                : null;

            tracker.RecordChange(e.PropertyName, oldValue, GetPropertyValue(sender, e.PropertyName));

            // Update our cached value for next time
            var newValue = GetPropertyValue(sender, e.PropertyName);
            lastValues[e.PropertyName] = newValue;
        };

        return tracker;
    }

    /// <summary>
    /// Gets the current value of a property from an object using reflection.
    /// </summary>
    /// <param name="obj">The object to get the property value from.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The current property value, or null if not found or null.</returns>
    private static object? GetPropertyValue(object? obj, string propertyName)
    {
        if (obj == null)
        {
            return null;
        }

        var property = obj.GetType().GetProperty(propertyName);
        return property?.GetValue(obj);
    }
}
