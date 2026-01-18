using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MagickCrop.Messages;

// ============================================
// Image-related messages
// ============================================

/// <summary>
/// Sent when an image is loaded into the application.
/// </summary>
public class ImageLoadedMessage
{
    public string FilePath { get; }
    public int Width { get; }
    public int Height { get; }

    public ImageLoadedMessage(string filePath, int width, int height)
    {
        FilePath = filePath;
        Width = width;
        Height = height;
    }
}

/// <summary>
/// Sent when the image is modified (cropped, rotated, etc.)
/// </summary>
public class ImageModifiedMessage
{
    public string Operation { get; }
    
    public ImageModifiedMessage(string operation)
    {
        Operation = operation;
    }
}

/// <summary>
/// Sent when the image is saved.
/// </summary>
public class ImageSavedMessage
{
    public string FilePath { get; }
    
    public ImageSavedMessage(string filePath)
    {
        FilePath = filePath;
    }
}

// ============================================
// Measurement-related messages
// ============================================

/// <summary>
/// Sent when a measurement is added.
/// </summary>
public class MeasurementAddedMessage
{
    public string MeasurementType { get; }
    public Guid MeasurementId { get; }

    public MeasurementAddedMessage(string type, Guid id)
    {
        MeasurementType = type;
        MeasurementId = id;
    }
}

/// <summary>
/// Sent when a measurement is removed.
/// </summary>
public class MeasurementRemovedMessage
{
    public Guid MeasurementId { get; }

    public MeasurementRemovedMessage(Guid id)
    {
        MeasurementId = id;
    }
}

/// <summary>
/// Request to remove a specific measurement control.
/// </summary>
public class RemoveMeasurementRequestMessage
{
    public Guid MeasurementId { get; }
    public string MeasurementType { get; }

    public RemoveMeasurementRequestMessage(Guid id, string type)
    {
        MeasurementId = id;
        MeasurementType = type;
    }
}

/// <summary>
/// Sent when scale factor changes globally.
/// </summary>
public class ScaleFactorChangedMessage
{
    public double NewScaleFactor { get; }
    public string Units { get; }

    public ScaleFactorChangedMessage(double scaleFactor, string units)
    {
        NewScaleFactor = scaleFactor;
        Units = units;
    }
}

// ============================================
// Navigation/UI messages
// ============================================

/// <summary>
/// Request to navigate to a different view.
/// </summary>
public class NavigateToMessage
{
    public string ViewName { get; }
    public object? Parameter { get; }

    public NavigateToMessage(string viewName, object? parameter = null)
    {
        ViewName = viewName;
        Parameter = parameter;
    }
}

/// <summary>
/// Request to show a dialog window.
/// </summary>
public class ShowDialogRequestMessage : RequestMessage<bool>
{
    public string DialogType { get; }
    public object? Parameter { get; }

    public ShowDialogRequestMessage(string dialogType, object? parameter = null)
    {
        DialogType = dialogType;
        Parameter = parameter;
    }
}

/// <summary>
/// Sent when application busy state changes.
/// </summary>
public class BusyStateChangedMessage
{
    public bool IsBusy { get; }
    public string? Message { get; }

    public BusyStateChangedMessage(bool isBusy, string? message = null)
    {
        IsBusy = isBusy;
        Message = message;
    }
}

// ============================================
// Project/File messages
// ============================================

/// <summary>
/// Sent when a project is opened.
/// </summary>
public class ProjectOpenedMessage
{
    public string FilePath { get; }
    public Guid ProjectId { get; }

    public ProjectOpenedMessage(string filePath, Guid projectId)
    {
        FilePath = filePath;
        ProjectId = projectId;
    }
}

/// <summary>
/// Sent when a project is saved.
/// </summary>
public class ProjectSavedMessage
{
    public string FilePath { get; }
    
    public ProjectSavedMessage(string filePath)
    {
        FilePath = filePath;
    }
}

/// <summary>
/// Sent when the recent projects list changes.
/// </summary>
public class RecentProjectsChangedMessage { }

// ============================================
// Tool state messages
// ============================================

/// <summary>
/// Sent when the active tool changes.
/// </summary>
public class ActiveToolChangedMessage
{
    public string ToolName { get; }
    
    public ActiveToolChangedMessage(string toolName)
    {
        ToolName = toolName;
    }
}

/// <summary>
/// Sent when undo/redo state changes.
/// </summary>
public class UndoRedoStateChangedMessage
{
    public bool CanUndo { get; }
    public bool CanRedo { get; }

    public UndoRedoStateChangedMessage(bool canUndo, bool canRedo)
    {
        CanUndo = canUndo;
        CanRedo = canRedo;
    }
}

// ============================================
// View/Viewport messages
// ============================================

/// <summary>
/// Request to reset the view (zoom and pan) to default.
/// </summary>
public class ResetViewMessage { }

/// <summary>
/// Request to center and zoom the image to fit the viewport.
/// </summary>
public class CenterAndZoomToFitMessage { }

/// <summary>
/// Request to clear all drawings from the canvas.
/// </summary>
public class ClearDrawingsMessage { }

/// <summary>
/// Request to close the measurement panel and clear all measurements.
/// </summary>
public class CloseMeasurementPanelMessage { }

/// <summary>
/// Request to cancel the current crop operation.
/// </summary>
public class CancelCropMessage { }

/// <summary>
/// Request to cancel the current transform operation.
/// </summary>
public class CancelTransformMessage { }
