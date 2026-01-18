# Step 04: Messaging Service Setup

## Objective
Implement a messaging/event aggregator pattern to enable loose communication between ViewModels and components without direct references.

## Prerequisites
- Step 03 completed (Service interfaces)

## Why Messaging?

In the current architecture, components communicate through:
1. Direct method calls (tight coupling)
2. Events with direct subscriptions (memory leak risk)
3. Parent-child references (inflexible)

A messaging service enables:
- Decoupled communication
- Easier testing
- No memory leaks from event subscriptions
- Request/response patterns

## Changes Required

### 1. Use CommunityToolkit.Mvvm's Built-in Messenger

CommunityToolkit.Mvvm includes `WeakReferenceMessenger` which provides:
- Weak references (no memory leaks)
- Type-safe messages
- Request/response pattern
- Thread-safe operations

### 2. Create Message Classes

**Create folder: `Messages/`**

#### Base Message (optional, for grouping)

**File: `Messages/AppMessages.cs`**

```csharp
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
```

### 3. Register Messenger in DI

**File: `App.xaml.cs`**

Update `ConfigureServices`:

```csharp
using CommunityToolkit.Mvvm.Messaging;

private static void ConfigureServices(IServiceCollection services)
{
    // Register Messenger as singleton
    services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
    
    // ... rest of registrations
}
```

### 4. Update ViewModelBase to Support Messaging

**File: `ViewModels/Base/ViewModelBase.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace MagickCrop.ViewModels.Base;

/// <summary>
/// Base class for all ViewModels with messaging support.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    protected IMessenger Messenger { get; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    protected ViewModelBase() : this(WeakReferenceMessenger.Default)
    {
    }

    protected ViewModelBase(IMessenger messenger)
    {
        Messenger = messenger;
    }

    /// <summary>
    /// Called when the ViewModel is first loaded.
    /// Override to perform initialization and register message handlers.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the associated View is being closed.
    /// Override to perform cleanup. Base implementation unregisters all messages.
    /// </summary>
    public virtual void Cleanup()
    {
        Messenger.UnregisterAll(this);
    }

    /// <summary>
    /// Sends a message to all registered recipients.
    /// </summary>
    protected void Send<TMessage>(TMessage message) where TMessage : class
    {
        Messenger.Send(message);
    }

    /// <summary>
    /// Registers this ViewModel to receive messages of type TMessage.
    /// </summary>
    protected void Register<TMessage>(Action<TMessage> handler) where TMessage : class
    {
        Messenger.Register<TMessage>(this, (r, m) => handler(m));
    }
}
```

### 5. Example Usage in ViewModel

```csharp
public partial class ExampleViewModel : ViewModelBase
{
    public override Task InitializeAsync()
    {
        // Register to receive messages
        Register<ImageLoadedMessage>(OnImageLoaded);
        Register<ScaleFactorChangedMessage>(OnScaleFactorChanged);
        
        return base.InitializeAsync();
    }

    private void OnImageLoaded(ImageLoadedMessage message)
    {
        // Handle image loaded
        Debug.WriteLine($"Image loaded: {message.FilePath}");
    }

    private void OnScaleFactorChanged(ScaleFactorChangedMessage message)
    {
        // Update measurements with new scale
    }

    [RelayCommand]
    private void DoSomething()
    {
        // Send a message
        Send(new ImageModifiedMessage("Cropped"));
    }
}
```

### 6. Migrating Existing Events to Messages

Current event pattern (in measurement controls):
```csharp
// Current: Direct event
public event RemoveControlRequestedEventHandler? RemoveControlRequested;
RemoveControlRequested?.Invoke(this);
```

New pattern (with messaging):
```csharp
// New: Message-based
WeakReferenceMessenger.Default.Send(new RemoveMeasurementRequestMessage(Id, "Distance"));
```

---

## Implementation Steps

1. Create `Messages` folder
2. Create `AppMessages.cs` with all message classes
3. Update DI registration to include `IMessenger`
4. Update `ViewModelBase` with messaging support
5. Build and verify

---

## Validation Checklist

- [ ] Messages folder created
- [ ] All message classes compile
- [ ] IMessenger registered in DI
- [ ] ViewModelBase updated with messaging
- [ ] Application builds and runs

---

## Files Changed/Created

| File | Change Type |
|------|-------------|
| `Messages/AppMessages.cs` | Created |
| `ViewModels/Base/ViewModelBase.cs` | Modified |
| `App.xaml.cs` | Modified |

---

## Notes

### Message Design Guidelines

1. **Immutable**: Messages should be immutable (readonly properties)
2. **Specific**: Each message has a single purpose
3. **Self-contained**: Include all needed data in the message
4. **Namespaced**: Group related messages in the same file

### When to Use Messaging vs Direct Calls

| Use Messaging When | Use Direct Calls When |
|-------------------|----------------------|
| Components don't know each other | Parent-child relationship |
| Multiple receivers possible | Single known receiver |
| Fire-and-forget notifications | Need return value |
| Cross-cutting concerns | Within same class |

### Memory Management

`WeakReferenceMessenger` uses weak references, so:
- No need to manually unsubscribe (but we do in Cleanup for clarity)
- Garbage collector can clean up unused ViewModels
- No risk of memory leaks from forgotten subscriptions

### Request/Response Pattern

For messages that need a response:
```csharp
// Define request message
public class GetUserConfirmationMessage : RequestMessage<bool>
{
    public string Question { get; }
    public GetUserConfirmationMessage(string question) => Question = question;
}

// Send and await response
var result = Messenger.Send(new GetUserConfirmationMessage("Are you sure?"));
if (result.Response)
{
    // User confirmed
}

// Handle in recipient
Register<GetUserConfirmationMessage>(msg => 
{
    msg.Reply(ShowConfirmationDialog(msg.Question));
});
```

### IRecipient<T> Interface Pattern (Recommended)

Instead of manually registering messages in `InitializeAsync()`, you can implement `IRecipient<T>` for a cleaner, more declarative approach:

**Benefits:**
- Compile-time safety for message handlers
- Automatic registration with `RegisterAll()`
- Clearer intent - interfaces declare what messages the ViewModel handles
- Better for ViewModels that handle many message types

**Example Implementation:**

```csharp
using CommunityToolkit.Mvvm.Messaging;

public partial class MeasurementViewModel : ViewModelBase, 
    IRecipient<ScaleFactorChangedMessage>,
    IRecipient<ImageModifiedMessage>,
    IRecipient<ProjectOpenedMessage>
{
    public MeasurementViewModel()
    {
        // Register all IRecipient<T> implementations at once
        Messenger.RegisterAll(this);
    }

    // Implement each message handler as a Receive method
    public void Receive(ScaleFactorChangedMessage message)
    {
        ScaleFactor = message.NewScaleFactor;
        Units = message.Units;
        UpdateDisplayText();
    }

    public void Receive(ImageModifiedMessage message)
    {
        // Handle image modification
        IsDirty = true;
    }

    public void Receive(ProjectOpenedMessage message)
    {
        // Handle project opened
        CurrentProjectPath = message.FilePath;
    }
}
```

**Updated ViewModelBase with IRecipient Support:**

```csharp
public abstract partial class ViewModelBase : ObservableObject
{
    protected IMessenger Messenger { get; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    protected ViewModelBase() : this(WeakReferenceMessenger.Default)
    {
    }

    protected ViewModelBase(IMessenger messenger)
    {
        Messenger = messenger;
        
        // Auto-register if this ViewModel implements any IRecipient<T> interfaces
        // This allows derived classes to just implement the interface
        if (GetType().GetInterfaces().Any(i => 
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRecipient<>)))
        {
            Messenger.RegisterAll(this);
        }
    }

    // ... rest of implementation
}
```

**Comparison: Manual Registration vs IRecipient<T>**

| Aspect | Manual Registration | IRecipient<T> |
|--------|-------------------|---------------|
| Registration | In `InitializeAsync()` | Automatic via interface |
| Compile-time safety | No | Yes |
| Handler signature | Any method name | Must be `Receive(T)` |
| Multiple message types | Multiple `Register<T>()` calls | Multiple interfaces |
| Flexibility | More flexible | More structured |

**When to Use Which:**
- Use `IRecipient<T>` when a ViewModel handles multiple message types (recommended)
- Use manual `Register<T>()` for one-off registrations or dynamic scenarios

---

## Next Steps

Proceed to **Step 05: Navigation Service** to enable window navigation through DI.
