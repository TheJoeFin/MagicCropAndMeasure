using MagickCrop.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            typeof(RecentProjectItem));

    public static readonly DependencyProperty ProjectDeletedCommandProperty =
        DependencyProperty.Register(
            nameof(ProjectDeletedCommand),
            typeof(ICommand),
            typeof(RecentProjectItem));

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
