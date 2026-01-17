# Step 01: MVVM Infrastructure Setup

## Objective
Add the foundational NuGet packages and create the base infrastructure classes that all ViewModels will inherit from.

## Prerequisites
- Step 00 completed (architecture review)

## Changes Required

### 1. Add NuGet Packages

**File: `MagickCrop.csproj`**

Add the following package references:

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
```

### 2. Create ViewModels Folder Structure

Create the following folder structure:
```
MagickCrop/
└── ViewModels/
    └── Base/
```

### 3. Create ViewModelBase Class

**File: `ViewModels/Base/ViewModelBase.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MagickCrop.ViewModels.Base;

/// <summary>
/// Base class for all ViewModels in the application.
/// Inherits from ObservableObject which provides INotifyPropertyChanged implementation.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Indicates whether the ViewModel is currently performing a loading operation.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Indicates whether the ViewModel is busy with any operation.
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Gets or sets the title for the ViewModel/View.
    /// </summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>
    /// Called when the ViewModel is first loaded.
    /// Override to perform initialization.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the associated View is being closed.
    /// Override to perform cleanup.
    /// </summary>
    public virtual void Cleanup()
    {
    }
}
```

### Important CommunityToolkit.Mvvm Attributes

The toolkit provides several attributes to reduce boilerplate. Here's a quick reference:

| Attribute | Purpose |
|-----------|---------|
| `[ObservableProperty]` | Generates property with `INotifyPropertyChanged` |
| `[RelayCommand]` | Generates `ICommand` implementation |
| `[NotifyPropertyChangedFor]` | Raises `PropertyChanged` for dependent properties |
| `[NotifyCanExecuteChangedFor]` | Raises `CanExecuteChanged` for dependent commands |
| `[NotifyDataErrorInfo]` | Enables validation error notifications |

**Example of advanced attribute usage:**
```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(FullName))]
[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
private string _firstName = string.Empty;

public string FullName => $"{FirstName} {LastName}";

[RelayCommand(CanExecute = nameof(CanSave))]
private void Save() { }

private bool CanSave => !string.IsNullOrEmpty(FirstName);
```

### 4. Create RelayCommand Enhancement (Optional - Already Exists)

The existing `RelayCommand<T>` in `Models/RelayCommand.cs` can remain as-is, but CommunityToolkit.Mvvm provides `[RelayCommand]` attribute that generates commands automatically.

**Note:** We'll transition to using `[RelayCommand]` attributes over time, but the existing RelayCommand can coexist.

### 5. Create AsyncRelayCommand Wrapper (Optional)

If needed, CommunityToolkit.Mvvm includes `AsyncRelayCommand` out of the box. No custom implementation needed.

---

## Implementation Steps

### Step 1: Add Package References

Edit `MagickCrop.csproj` and add the new package references in the existing `<ItemGroup>` with other packages.

### Step 2: Create Folder Structure

```powershell
# Create ViewModels folder structure
New-Item -ItemType Directory -Path "MagickCrop\ViewModels\Base" -Force
```

### Step 3: Create ViewModelBase.cs

Create the file `MagickCrop\ViewModels\Base\ViewModelBase.cs` with the content above.

### Step 4: Verify Build

```powershell
dotnet build MagickCrop.sln
```

---

## Validation Checklist

- [ ] Solution builds without errors
- [ ] NuGet packages restore successfully
- [ ] ViewModelBase class compiles
- [ ] Application launches and works normally (no regressions)

---

## Files Changed

| File | Change Type |
|------|-------------|
| `MagickCrop.csproj` | Modified - Added package references |
| `ViewModels/Base/ViewModelBase.cs` | Created |

---

## Notes

### Why CommunityToolkit.Mvvm?

1. **Source Generators**: `[ObservableProperty]` and `[RelayCommand]` reduce boilerplate
2. **IMessenger**: Built-in messenger for loose coupling
3. **Well-Maintained**: Microsoft-backed, widely adopted
4. **No Runtime Overhead**: Source generators work at compile time

### About ObservableObject

`ObservableObject` from CommunityToolkit.Mvvm provides:
- `INotifyPropertyChanged` implementation
- `INotifyPropertyChanging` implementation
- `SetProperty<T>()` helper method
- `OnPropertyChanged()` / `OnPropertyChanging()` methods

### Backward Compatibility

The existing `RelayCommand<T>` in `Models/RelayCommand.cs` will continue to work. We can gradually transition to `[RelayCommand]` attributes in future steps.

---

## Next Steps

Proceed to **Step 02: Dependency Injection Setup** to configure the DI container in App.xaml.cs.
