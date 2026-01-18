using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.DependencyInjection;
using MagickCrop.Models.MeasurementControls;
using MagickCrop.ViewModels.Measurements;
using MagickCrop.Helpers;

namespace MagickCrop.Controls;

/// <summary>
/// Control for measuring polygons using MVVM pattern.
/// </summary>
public partial class PolygonMeasurementControl : MeasurementControlBase
{
    private int _pointDraggingIndex = -1;

    // Backward compatibility events for MainWindow
    public new event MouseButtonEventHandler? MeasurementPointMouseDown;
    public delegate void RemoveControlRequestedEventHandler(object sender, EventArgs e);
    public event RemoveControlRequestedEventHandler? RemoveControlRequested;

    /// <summary>
    /// Gets the ViewModel for this control.
    /// </summary>
    public PolygonMeasurementViewModel? ViewModel => DataContext as PolygonMeasurementViewModel;

    /// <summary>
    /// Gets whether the polygon is closed (backward compatibility).
    /// </summary>
    public bool IsClosed => ViewModel?.IsClosed ?? false;

    /// <summary>
    /// Gets the number of vertices (backward compatibility).
    /// </summary>
    public int VertexCount => ViewModel?.VertexCount ?? 0;

    /// <summary>
    /// Initializes a new instance of the PolygonMeasurementControl class.
    /// </summary>
    public PolygonMeasurementControl()
    {
        InitializeComponent();

        // Create or use injected ViewModel
        if (DataContext is not PolygonMeasurementViewModel)
        {
            try
            {
                DataContext = Ioc.Default.GetService<PolygonMeasurementViewModel>() ?? new PolygonMeasurementViewModel();
            }
            catch
            {
                DataContext = new PolygonMeasurementViewModel();
            }
        }
    }

    /// <summary>
    /// Initializes the measurement with a starting position.
    /// </summary>
    public void InitializePositions(double canvasWidth, double canvasHeight)
    {
        if (ViewModel is null)
            return;

        // Start with an empty polygon - vertices will be added by user interactions
        ViewModel.Vertices.Clear();
    }

    /// <summary>
    /// Adds a vertex to the polygon.
    /// </summary>
    public void AddVertex(Point vertex)
    {
        if (ViewModel is null)
            return;

        if (ViewModel.IsClosed)
            return;

        System.Diagnostics.Debug.WriteLine($"AddVertex called: Point({vertex.X:F1}, {vertex.Y:F1}), Current vertex count: {ViewModel.VertexCount}");

        // Check if clicking near first vertex to close polygon
        if (ViewModel.VertexCount >= 3 && IsNearFirstVertex(vertex))
        {
            System.Diagnostics.Debug.WriteLine("Closing polygon - clicked near first vertex");
            ClosePolygon();
            return;
        }

        ViewModel.AddVertex(vertex);
        UpdateFirstVertexAppearance();

        System.Diagnostics.Debug.WriteLine($"Added vertex {ViewModel.VertexCount}: Point({vertex.X:F1}, {vertex.Y:F1})");
    }

    /// <summary>
    /// Updates the preview line while the user moves the mouse.
    /// </summary>
    public void UpdatePreviewLine(Point mousePosition)
    {
        if (ViewModel is null || ViewModel.IsClosed || ViewModel.VertexCount == 0)
        {
            PreviewLine.Visibility = Visibility.Collapsed;
            return;
        }

        Point lastVertex = ViewModel.Vertices[ViewModel.VertexCount - 1];
        PreviewLine.X1 = lastVertex.X;
        PreviewLine.Y1 = lastVertex.Y;
        PreviewLine.X2 = mousePosition.X;
        PreviewLine.Y2 = mousePosition.Y;
        PreviewLine.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Closes the polygon.
    /// </summary>
    public void ClosePolygon()
    {
        if (ViewModel is null)
            return;

        if (ViewModel.VertexCount < 3 || ViewModel.IsClosed)
        {
            System.Diagnostics.Debug.WriteLine($"ClosePolygon early return: vertices.Count={ViewModel.VertexCount}, isClosed={ViewModel.IsClosed}");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"ClosePolygon: Closing polygon with {ViewModel.VertexCount} vertices");
        ViewModel.Close();
        PreviewLine.Visibility = Visibility.Collapsed;

        // Re-enable hit testing on the polygon path now that it's closed
        PolygonPath.IsHitTestVisible = true;

        // Reset first vertex appearance back to normal
        ResetFirstVertexAppearance();

        PositionMeasurementText();

        System.Diagnostics.Debug.WriteLine("ClosePolygon: Polygon successfully closed");
    }

    private bool IsNearFirstVertex(Point point)
    {
        if (ViewModel is null || ViewModel.VertexCount == 0)
            return false;

        Point firstVertex = ViewModel.Vertices[0];
        double dx = point.X - firstVertex.X;
        double dy = point.Y - firstVertex.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        System.Diagnostics.Debug.WriteLine($"Distance from click to first vertex: {distance:F1}, Tolerance: {Defaults.VertexCloseTolerance}");

        return distance <= Defaults.VertexCloseTolerance;
    }

    private void UpdateFirstVertexAppearance()
    {
        if (ViewModel is null || ViewModel.VertexCount < 3 || ViewModel.IsClosed)
            return;

        // The ItemsControl will handle rendering vertices, but we track state in ViewModel
        // For now, this method is kept for future enhancement if visual feedback is needed
    }

    private void ResetFirstVertexAppearance()
    {
        if (ViewModel is null || ViewModel.IsClosed)
            return;

        // Reset appearance is handled by ViewModel state
    }

    private void UpdatePolygonPath()
    {
        PositionMeasurementText();
    }

    private void PositionMeasurementText()
    {
        if (ViewModel is null || ViewModel.VertexCount == 0)
            return;

        // Position text at the centroid of the polygon
        double centerX = ViewModel.Vertices.Average(v => v.X);
        double centerY = ViewModel.Vertices.Average(v => v.Y);

        Canvas.SetLeft(MeasurementText, centerX - (MeasurementText.ActualWidth / 2));
        Canvas.SetTop(MeasurementText, centerY - MeasurementText.ActualHeight - 10);
    }

    /// <summary>
    /// Moves a measurement point to a new position.
    /// </summary>
    public void MovePoint(int pointIndex, Point newPosition)
    {
        if (ViewModel is null || pointIndex < 0 || pointIndex >= ViewModel.VertexCount)
            return;

        ViewModel.UpdateVertex(pointIndex, newPosition);
        PositionMeasurementText();
    }

    /// <summary>
    /// Gets the index of the currently dragging point (-1 if none).
    /// </summary>
    public int GetActivePointIndex() => _pointDraggingIndex;

    /// <summary>
    /// Resets the active point index.
    /// </summary>
    public void ResetActivePoint()
    {
        _pointDraggingIndex = -1;
    }

    /// <summary>
    /// Converts this control to a DTO for serialization.
    /// </summary>
    public PolygonMeasurementControlDto ToDto()
    {
        if (ViewModel is null)
            throw new InvalidOperationException("ViewModel is not initialized");

        return new PolygonMeasurementControlDto
        {
            Vertices = new List<Point>(ViewModel.Vertices),
            ScaleFactor = ViewModel.ScaleFactor,
            Units = ViewModel.Units,
            IsClosed = ViewModel.IsClosed
        };
    }

    /// <summary>
    /// Loads data from a DTO for deserialization.
    /// </summary>
    public void FromDto(PolygonMeasurementControlDto dto)
    {
        if (ViewModel is null)
            throw new InvalidOperationException("ViewModel is not initialized");

        System.Diagnostics.Debug.WriteLine($"FromDto: Restoring polygon with {dto.Vertices.Count} vertices, IsClosed: {dto.IsClosed}");

        // Clear existing vertices
        ViewModel.Vertices.Clear();

        // Restore from DTO
        foreach (var vertex in dto.Vertices)
        {
            ViewModel.AddVertex(vertex);
        }

        ViewModel.ScaleFactor = dto.ScaleFactor;
        ViewModel.Units = dto.Units;

        if (dto.IsClosed && dto.Vertices.Count >= 3)
        {
            ViewModel.Close();
            PolygonPath.IsHitTestVisible = true;
        }

        PositionMeasurementText();

        System.Diagnostics.Debug.WriteLine($"FromDto: Polygon restoration complete");
    }

    private void MeasurementPoint_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse ellipse || ViewModel is null || !ViewModel.IsClosed)
            return;

        // Find the vertex index by comparing positions
        var position = e.GetPosition(MeasurementCanvas);
        int vertexIndex = -1;

        for (int i = 0; i < ViewModel.VertexCount; i++)
        {
            var dx = ViewModel.Vertices[i].X - position.X;
            var dy = ViewModel.Vertices[i].Y - position.Y;
            if (Math.Sqrt(dx * dx + dy * dy) <= 8)
            {
                vertexIndex = i;
                break;
            }
        }

        if (vertexIndex >= 0)
        {
            _pointDraggingIndex = vertexIndex;
            MeasurementPointMouseDown?.Invoke(sender, e);
            RaiseMeasurementPointMouseDown(e);
        }

        e.Handled = true;
    }

    private void CopyMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.DisplayText is not null)
        {
            Clipboard.SetText(ViewModel.DisplayText);
        }
    }

    private void MeasurementButton_Click(object sender, RoutedEventArgs e)
    {
        ContextMenu? contextMenu = MeasurementText.ContextMenu;
        if (contextMenu is not null)
        {
            contextMenu.PlacementTarget = MeasurementText;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void RemoveMeasurementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RemoveControlRequested?.Invoke(this, EventArgs.Empty);
        RaiseRemoveRequested();
    }
}