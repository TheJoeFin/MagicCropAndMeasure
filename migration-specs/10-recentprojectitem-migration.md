# Step 10: RecentProjectItem Control Migration

## Objective
Migrate the RecentProjectItem UserControl to MVVM, completing the welcome screen migration.

## Prerequisites
- Step 09 completed (WelcomeMessage migration)

## Current State Analysis

**RecentProjectItem characteristics:**
- Displays project thumbnail, name, and last opened time
- Has click and delete commands
- Uses DependencyProperties for Project and Commands
- Already has some MVVM patterns (uses RelayCommand)

## Changes Required

### 1. Update RecentProjectItem XAML

**File: `Controls/RecentProjectItem.xaml`**

```xml
<UserControl
    x:Class="MagickCrop.Controls.RecentProjectItem"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    xmlns:models="clr-namespace:MagickCrop.Models"
    mc:Ignorable="d"
    d:DataContext="{d:DesignInstance Type=models:RecentProjectInfo}"
    d:DesignHeight="60"
    d:DesignWidth="400"
    Cursor="Hand"
    MouseLeftButtonUp="OnMouseLeftButtonUp">

    <UserControl.Resources>
        <Style x:Key="DeleteButtonStyle" TargetType="ui:Button">
            <Setter Property="Opacity" Value="0"/>
            <Setter Property="Padding" Value="4"/>
            <Setter Property="Background" Value="Transparent"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsMouseOver, RelativeSource={RelativeSource AncestorType=UserControl}}" Value="True">
                    <Setter Property="Opacity" Value="1"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </UserControl.Resources>

    <Border x:Name="RootBorder"
            Background="{DynamicResource ControlFillColorDefaultBrush}"
            BorderBrush="{DynamicResource ControlStrokeColorDefaultBrush}"
            BorderThickness="1"
            CornerRadius="4"
            Padding="8">
        <Border.Style>
            <Style TargetType="Border">
                <Style.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter Property="Background" Value="{DynamicResource ControlFillColorSecondaryBrush}"/>
                    </Trigger>
                </Style.Triggers>
            </Style>
        </Border.Style>

        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="50"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <!-- Thumbnail -->
            <Border Grid.Column="0" 
                    Width="44" 
                    Height="44" 
                    CornerRadius="4"
                    Background="{DynamicResource ControlFillColorTertiaryBrush}">
                <Image Source="{Binding Thumbnail}" 
                       Stretch="UniformToFill"
                       RenderOptions.BitmapScalingMode="HighQuality"/>
            </Border>

            <!-- Project Info -->
            <StackPanel Grid.Column="1" 
                        VerticalAlignment="Center" 
                        Margin="12,0,0,0">
                <TextBlock Text="{Binding Name}" 
                           FontWeight="Medium"
                           TextTrimming="CharacterEllipsis"
                           MaxWidth="280"/>
                <TextBlock Text="{Binding LastOpenedFormatted}" 
                           FontSize="11"
                           Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                           Margin="0,2,0,0"/>
            </StackPanel>

            <!-- Delete Button -->
            <ui:Button Grid.Column="2"
                       Style="{StaticResource DeleteButtonStyle}"
                       VerticalAlignment="Center"
                       ToolTip="Remove from recent"
                       Click="OnDeleteClick">
                <ui:SymbolIcon Symbol="Delete24"/>
            </ui:Button>
        </Grid>
    </Border>
</UserControl>
```

### 2. Update RecentProjectItem Code-Behind

**File: `Controls/RecentProjectItem.xaml.cs`**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MagickCrop.Models;

namespace MagickCrop.Controls;

/// <summary>
/// Control for displaying a recent project item.
/// </summary>
public partial class RecentProjectItem : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ProjectProperty =
        DependencyProperty.Register(
            nameof(Project),
            typeof(RecentProjectInfo),
            typeof(RecentProjectItem),
            new PropertyMetadata(null, OnProjectChanged));

    public static readonly DependencyProperty ProjectClickedCommandProperty =
        DependencyProperty.Register(
            nameof(ProjectClickedCommand),
            typeof(ICommand),
            typeof(RecentProjectItem),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ProjectDeletedCommandProperty =
        DependencyProperty.Register(
            nameof(ProjectDeletedCommand),
            typeof(ICommand),
            typeof(RecentProjectItem),
            new PropertyMetadata(null));

    /// <summary>
    /// Gets or sets the project info to display.
    /// </summary>
    public RecentProjectInfo? Project
    {
        get => (RecentProjectInfo?)GetValue(ProjectProperty);
        set => SetValue(ProjectProperty, value);
    }

    /// <summary>
    /// Gets or sets the command to execute when the project is clicked.
    /// </summary>
    public ICommand? ProjectClickedCommand
    {
        get => (ICommand?)GetValue(ProjectClickedCommandProperty);
        set => SetValue(ProjectClickedCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command to execute when delete is clicked.
    /// </summary>
    public ICommand? ProjectDeletedCommand
    {
        get => (ICommand?)GetValue(ProjectDeletedCommandProperty);
        set => SetValue(ProjectDeletedCommandProperty, value);
    }

    #endregion

    public RecentProjectItem()
    {
        InitializeComponent();
    }

    private static void OnProjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RecentProjectItem control && e.NewValue is RecentProjectInfo project)
        {
            control.DataContext = project;
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Project != null && ProjectClickedCommand?.CanExecute(Project) == true)
        {
            ProjectClickedCommand.Execute(Project);
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent bubbling to parent click handler
        
        if (Project != null && ProjectDeletedCommand?.CanExecute(Project) == true)
        {
            ProjectDeletedCommand.Execute(Project);
        }
    }
}
```

### 3. Update RecentProjectInfo Model (if not done in Step 06)

Ensure the `RecentProjectInfo` model has the `LastOpenedFormatted` property:

**File: `Models/RecentProjectInfo.cs`**

```csharp
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MagickCrop.Models;

/// <summary>
/// Information about a recent project.
/// </summary>
public partial class RecentProjectInfo : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private DateTime _lastOpened;

    [ObservableProperty]
    private DateTime _created;

    [ObservableProperty]
    private string _thumbnailPath = string.Empty;

    private BitmapImage? _thumbnail;

    [JsonIgnore]
    public BitmapImage? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    /// <summary>
    /// Gets the formatted last opened time.
    /// </summary>
    [JsonIgnore]
    public string LastOpenedFormatted => FormatRelativeTime(LastOpened);

    /// <summary>
    /// Updates the last opened time and notifies change.
    /// </summary>
    public void MarkAsOpened()
    {
        LastOpened = DateTime.Now;
        OnPropertyChanged(nameof(LastOpenedFormatted));
    }

    private static string FormatRelativeTime(DateTime dateTime)
    {
        var span = DateTime.Now - dateTime;

        if (span.TotalMinutes < 1)
            return "Just now";
        if (span.TotalHours < 1)
            return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalDays < 1)
            return $"{(int)span.TotalHours} hours ago";
        if (span.TotalDays < 7)
            return $"{(int)span.TotalDays} days ago";
        
        return dateTime.ToString("MMM d, yyyy");
    }
}
```

---

## Implementation Steps

1. Update `Controls/RecentProjectItem.xaml`
2. Update `Controls/RecentProjectItem.xaml.cs`
3. Ensure `RecentProjectInfo` has `LastOpenedFormatted` property
4. Build and test

---

## Validation Checklist

- [ ] RecentProjectItem compiles
- [ ] Thumbnail displays correctly
- [ ] Project name shows with ellipsis for long names
- [ ] Last opened time shows relative format
- [ ] Hover effect works
- [ ] Click opens project
- [ ] Delete button appears on hover
- [ ] Delete removes project from list

---

## Files Changed

| File | Change Type |
|------|-------------|
| `Controls/RecentProjectItem.xaml` | Modified |
| `Controls/RecentProjectItem.xaml.cs` | Modified |
| `Models/RecentProjectInfo.cs` | Modified (if needed) |

---

## Notes

### DataContext vs Project Property

The control uses both:
- `Project` DependencyProperty: For parent to set the data
- `DataContext`: Bound internally to enable direct `{Binding Name}` syntax

When `Project` changes, we set `DataContext = project` so bindings work directly.

### Delete Button Visibility Animation

The delete button uses a DataTrigger on `IsMouseOver`:
```xml
<DataTrigger Binding="{Binding IsMouseOver, RelativeSource={RelativeSource AncestorType=UserControl}}" Value="True">
    <Setter Property="Opacity" Value="1"/>
</DataTrigger>
```

This provides smooth show/hide behavior on hover.

### Event Handling Pattern

Click events are handled in code-behind, then delegate to commands:
```csharp
private void OnDeleteClick(object sender, RoutedEventArgs e)
{
    e.Handled = true; // Stop bubbling
    ProjectDeletedCommand?.Execute(Project);
}
```

This keeps mouse handling in the View while business logic goes through commands.

---

## Next Steps

Proceed to **Step 11: Measurement Controls Base Class** to create a shared base for measurement controls.
