using MagickCrop.Controls;
using MagickCrop.Models;
using MagickCrop.Models.MeasurementControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;

namespace MagickCrop;

public partial class MainWindow
{
    /// <summary>
    /// Overlay state captured when the resize panel opens. Each entry re-applies that state
    /// scaled by (sx, sy); (1, 1) restores it. Scaling always starts from the captured state,
    /// so repeated preview updates never accumulate rounding drift.
    /// </summary>
    private sealed class OverlayScaleBaseline
    {
        public List<Action<double, double>> Measurements { get; } = [];
        public List<Action<double, double>> Markups { get; } = [];

        public (double Sx, double Sy, bool Measurements, bool Markups)? LastApplied { get; set; }
    }

    private OverlayScaleBaseline? resizeOverlayBaseline;

    /// <summary>
    /// Captures every measurement and markup overlay so the resize preview can scale them
    /// with the image. Overlays live in ImageGrid/ShapeCanvas coordinates (same origin), so a
    /// per-axis scale keeps them over the same image content.
    /// </summary>
    private OverlayScaleBaseline CaptureOverlayBaseline()
    {
        OverlayScaleBaseline baseline = new();
        List<Action<double, double>> measurements = baseline.Measurements;
        List<Action<double, double>> markups = baseline.Markups;

        // Sizes (stroke thickness, font size) and real-world calibration use a single factor;
        // for a non-uniform stretch the geometric mean preserves area.
        static double Uniform(double sx, double sy) => Math.Sqrt(sx * sy);
        static Point Scale(Point p, double sx, double sy) => new(p.X * sx, p.Y * sy);

        double baseScale = ScaleInput.Value ?? 1.0;
        // Keep calibrated real-world lengths the same as each canvas unit covers less of the image
        double ScaledCalibration(double sx, double sy) => baseScale / Uniform(sx, sy);

        foreach (DistanceMeasurementControl control in measurementTools)
        {
            DistanceMeasurementControlDto dto = control.ToDto();
            measurements.Add((sx, sy) =>
            {
                DistanceMeasurementControlDto scaled = control.ToDto();
                scaled.StartPosition = Scale(dto.StartPosition, sx, sy);
                scaled.EndPosition = Scale(dto.EndPosition, sx, sy);
                scaled.ScaleFactor = ScaledCalibration(sx, sy);
                control.FromDto(scaled);
            });
        }

        foreach (AngleMeasurementControl control in angleMeasurementTools)
        {
            AngleMeasurementControlDto dto = control.ToDto();
            measurements.Add((sx, sy) =>
            {
                AngleMeasurementControlDto scaled = control.ToDto();
                scaled.Point1Position = Scale(dto.Point1Position, sx, sy);
                scaled.VertexPosition = Scale(dto.VertexPosition, sx, sy);
                scaled.Point3Position = Scale(dto.Point3Position, sx, sy);
                control.FromDto(scaled);
            });
        }

        foreach (RectangleMeasurementControl control in rectangleMeasurementTools)
        {
            RectangleMeasurementControlDto dto = control.ToDto();
            measurements.Add((sx, sy) =>
            {
                RectangleMeasurementControlDto scaled = control.ToDto();
                scaled.TopLeft = Scale(dto.TopLeft, sx, sy);
                scaled.BottomRight = Scale(dto.BottomRight, sx, sy);
                scaled.ScaleFactor = ScaledCalibration(sx, sy);
                control.FromDto(scaled);
            });
        }

        foreach (CircleMeasurementControl control in circleMeasurementTools)
        {
            CircleMeasurementControlDto dto = control.ToDto();
            measurements.Add((sx, sy) =>
            {
                CircleMeasurementControlDto scaled = control.ToDto();
                scaled.Center = Scale(dto.Center, sx, sy);
                scaled.EdgePoint = Scale(dto.EdgePoint, sx, sy);
                scaled.ScaleFactor = ScaledCalibration(sx, sy);
                control.FromDto(scaled);
            });
        }

        foreach (PolygonMeasurementControl control in polygonMeasurementTools)
        {
            PolygonMeasurementControlDto dto = control.ToDto();
            measurements.Add((sx, sy) =>
            {
                PolygonMeasurementControlDto scaled = control.ToDto();
                scaled.Vertices = [.. dto.Vertices.Select(p => Scale(p, sx, sy))];
                scaled.ScaleFactor = ScaledCalibration(sx, sy);
                control.FromDto(scaled);
            });
        }

        foreach (ConstructionOverlayControl control in constructionControls)
        {
            Dictionary<Guid, Point> positions = control.ToDto().Points.ToDictionary(p => p.Id, p => p.Position);
            measurements.Add((sx, sy) =>
            {
                ConstructionGeometryDto scaled = control.ToDto();
                foreach (ConstructionPointDto point in scaled.Points)
                {
                    if (positions.TryGetValue(point.Id, out Point position))
                        point.Position = Scale(position, sx, sy);
                }
                scaled.ScaleFactor = ScaledCalibration(sx, sy);
                control.FromDto(scaled);
            });
        }

        foreach (VerticalLineControl control in verticalLineControls)
        {
            double position = control.ToDto().Position;
            measurements.Add((sx, sy) =>
            {
                VerticalLineControlDto scaled = control.ToDto();
                scaled.Position = position * sx;
                control.FromDto(scaled);
            });
        }

        foreach (HorizontalLineControl control in horizontalLineControls)
        {
            double position = control.ToDto().Position;
            measurements.Add((sx, sy) =>
            {
                HorizontalLineControlDto scaled = control.ToDto();
                scaled.Position = position * sy;
                control.FromDto(scaled);
            });
        }

        // Freehand measurement strokes and their length labels
        foreach (KeyValuePair<Stroke, StrokeInfo> entry in strokeMeasurements)
        {
            Stroke stroke = entry.Key;
            StrokeInfo info = entry.Value;

            AddStroke(measurements, stroke, scaleThickness: false);
            // Runs after the stroke's points were rescaled above
            measurements.Add((sx, sy) => info.PixelLength = CalculateStrokeLength(stroke));

            StrokeLengthDisplay? display = ShapeCanvas.Children.OfType<StrokeLengthDisplay>()
                .FirstOrDefault(d => d.GetStroke() == stroke);
            if (display is not null)
                AddCanvasPosition(measurements, display);
        }

        // Global scale goes last so ScaleInput_ValueChanged pushes it to every control
        measurements.Add((sx, sy) => ScaleInput.Value = ScaledCalibration(sx, sy));

        foreach (MarkupShapeControl control in markupShapeControls)
        {
            MarkupShapeDto dto = control.ToDto();
            markups.Add((sx, sy) =>
            {
                MarkupShapeDto scaled = control.ToDto();
                scaled.Point1 = Scale(dto.Point1, sx, sy);
                scaled.Point2 = Scale(dto.Point2, sx, sy);
                scaled.StrokeThickness = dto.StrokeThickness * Uniform(sx, sy);
                control.FromDto(scaled);
            });
        }

        foreach (MarkupTextControl control in markupTextControls)
        {
            MarkupTextDto dto = control.ToDto();
            markups.Add((sx, sy) =>
            {
                MarkupTextDto scaled = control.ToDto();
                scaled.FontSize = dto.FontSize * Uniform(sx, sy);
                control.FromDto(scaled);
            });
            AddCanvasPosition(markups, control);
        }

        foreach (Stroke stroke in MarkupCanvas.Strokes)
            AddStroke(markups, stroke, scaleThickness: true);

        return baseline;

        static void AddCanvasPosition(List<Action<double, double>> actions, UIElement element)
        {
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            actions.Add((sx, sy) =>
            {
                Canvas.SetLeft(element, left * sx);
                Canvas.SetTop(element, top * sy);
            });
        }

        static void AddStroke(List<Action<double, double>> actions, Stroke stroke, bool scaleThickness)
        {
            StylusPointCollection points = stroke.StylusPoints.Clone();
            DrawingAttributes attributes = stroke.DrawingAttributes.Clone();

            actions.Add((sx, sy) =>
            {
                StylusPointCollection scaledPoints = points.Clone();
                for (int i = 0; i < scaledPoints.Count; i++)
                {
                    StylusPoint sp = scaledPoints[i];
                    sp.X *= sx;
                    sp.Y *= sy;
                    scaledPoints[i] = sp;
                }
                stroke.StylusPoints = scaledPoints;

                DrawingAttributes scaledAttributes = attributes.Clone();
                if (scaleThickness)
                {
                    scaledAttributes.Width *= Uniform(sx, sy);
                    scaledAttributes.Height *= Uniform(sx, sy);
                }
                stroke.DrawingAttributes = scaledAttributes;
            });
        }
    }

    /// <summary>
    /// Applies the baseline scaled by (sx, sy) to the checked categories and restores the
    /// unchecked ones to their captured state.
    /// </summary>
    private static void ApplyOverlayScale(OverlayScaleBaseline baseline, double sx, double sy, bool scaleMeasurements, bool scaleMarkups)
    {
        if (baseline.LastApplied == (sx, sy, scaleMeasurements, scaleMarkups))
            return;

        foreach (Action<double, double> apply in baseline.Measurements)
            apply(scaleMeasurements ? sx : 1, scaleMeasurements ? sy : 1);

        foreach (Action<double, double> apply in baseline.Markups)
            apply(scaleMarkups ? sx : 1, scaleMarkups ? sy : 1);

        baseline.LastApplied = (sx, sy, scaleMeasurements, scaleMarkups);
    }

    /// <summary>
    /// How much the image's on-screen size has changed since the resize panel opened.
    /// </summary>
    private (double Sx, double Sy) GetResizePreviewScale()
    {
        if (resizeStartImageSize.Width <= 0 || resizeStartImageSize.Height <= 0)
            return (1, 1);

        Size gripSpace = GetResizeGripSpace();
        double gridWidth = double.IsNaN(ImageGrid.Width) ? oldGridSize.Width : ImageGrid.Width;
        double gridHeight = double.IsNaN(ImageGrid.Height) ? oldGridSize.Height : ImageGrid.Height;

        double sx = (gridWidth - gripSpace.Width) / resizeStartImageSize.Width;
        double sy = (gridHeight - gripSpace.Height) / resizeStartImageSize.Height;

        if (sx <= 0 || sy <= 0 || double.IsNaN(sx) || double.IsNaN(sy))
            return (1, 1);

        return (sx, sy);
    }

    /// <summary>
    /// Keeps the measurement/markup overlays attached to the image while the resize is previewed.
    /// </summary>
    private void UpdateResizeOverlayPreview()
    {
        if (resizeOverlayBaseline is null)
            return;

        (double sx, double sy) = GetResizePreviewScale();
        ApplyOverlayScale(
            resizeOverlayBaseline,
            sx,
            sy,
            ResizeScaleMeasurementsCheckBox.IsChecked == true,
            ResizeScaleMarkupsCheckBox.IsChecked == true);
    }

    private void ResizeScaleOverlaysCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        UpdateResizeOverlayPreview();
    }

    /// <summary>
    /// Finalizes the previewed overlay scaling for an applied resize.
    /// </summary>
    /// <returns>Undo/redo actions for the overlays, or null if they were not changed.</returns>
    private (Action Undo, Action Redo)? CommitResizeOverlayPreview()
    {
        OverlayScaleBaseline? baseline = resizeOverlayBaseline;
        if (baseline is null)
            return null;

        UpdateResizeOverlayPreview();

        if (baseline.LastApplied is not (double sx, double sy, bool scaleMeasurements, bool scaleMarkups)
            || (!scaleMeasurements && !scaleMarkups)
            || (Math.Abs(sx - 1) < 1e-9 && Math.Abs(sy - 1) < 1e-9))
            return null;

        return (
            () =>
            {
                baseline.LastApplied = null;
                ApplyOverlayScale(baseline, 1, 1, scaleMeasurements, scaleMarkups);
            },
            () =>
            {
                baseline.LastApplied = null;
                ApplyOverlayScale(baseline, sx, sy, scaleMeasurements, scaleMarkups);
            });
    }

    /// <summary>
    /// Returns every overlay to its state from when the resize panel opened.
    /// </summary>
    private void CancelResizeOverlayPreview()
    {
        if (resizeOverlayBaseline is null)
            return;

        ApplyOverlayScale(resizeOverlayBaseline, 1, 1, scaleMeasurements: false, scaleMarkups: false);
    }
}
