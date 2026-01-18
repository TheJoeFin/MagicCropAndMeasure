namespace MagickCrop;

/// <summary>
/// Represents the current state of measurement placement.
/// </summary>
public enum PlacementState
{
    /// <summary>
    /// Not currently placing any measurement.
    /// </summary>
    NotPlacing,

    /// <summary>
    /// Waiting for the first point to be clicked.
    /// </summary>
    WaitingForFirstPoint,

    /// <summary>
    /// Waiting for the second point (used in distance, angle measurements).
    /// </summary>
    WaitingForSecondPoint,

    /// <summary>
    /// Waiting for additional points (used in polygon measurements).
    /// </summary>
    WaitingForMorePoints,

    /// <summary>
    /// Measurement placement is complete.
    /// </summary>
    Complete
}
