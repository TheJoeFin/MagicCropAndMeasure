using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace MagickCrop.Controls;

/// <summary>
/// Adorner that provides a visual rotation gizmo (circle + draggable handle)
/// for an adorned element. Raises <see cref="AngleChanging"/> as the user drags.
/// Angle range is normalized to [-180,180]. Positive angles are clockwise (WPF convention).
/// </summary>
internal sealed class RotateAdorner : Adorner
{
    private const double CircleMargin = 28; // distance outside element bounds
    private const double HandleRadius = 9;  // visual radius of handle circle
    private const double HandleHitRadius = 18; // hit test radius for easier grabbing
    private const double TickLengthMajor = 10;
    private const double TickLengthMinor = 5;
    private const int MinorTickEvery = 15; // degrees
    private const int MajorTickEvery = 45; // degrees

    private const double BasePenThickness = 1.5;
    private const double BaseTickPenThickness = 1.0;
    private const double BaseFontSize = 11.0;
    private const double LabelRadialOffset = 14.0; // distance beyond circle to place labels

    private const double EarlyStartFactor = 0.92; // start scaling a bit earlier to avoid clipping
    private const double BaseInnerPaddingScreen = 8.0; // extra screen padding inside viewport

    private bool _isDragging;
    private Point _center;
    private double _radius;
    private double _dragStartVectorAngle; // absolute angle of mouse vector at start
    private double _dragStartAdornerAngle; // Angle value when drag started

    private double _angle; // backing field

    private readonly Typeface _typeface = new("Segoe UI");

#if DEBUG
    private long _lastLogTick;
    private const int LogIntervalMs = 333;
#endif

    public event EventHandler<double>? AngleChanging;
    public event EventHandler<double>? AngleChangedFinal; // fired on mouse up

    public RotateAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = true;
        SnapsToDevicePixels = true;
    }

    /// <summary>
    /// Current angle in degrees (normalized -180..180)
    /// </summary>
    public double Angle
    {
        get => _angle;
        set
        {
            double norm = NormalizeAngle(value);
            if (Math.Abs(norm - _angle) > double.Epsilon)
            {
                _angle = norm;
                InvalidateVisual();
            }
        }
    }

    public void SetAngle(double angle)
    {
        Angle = angle;
    }

    // Try to locate the AdornerLayer that hosts this adorner by walking up from this visual.
    private static AdornerLayer? FindOwningAdornerLayer(DependencyObject start)
    {
        DependencyObject? v = start;
        while (v != null)
        {
            if (v is AdornerLayer al)
                return al;
            v = VisualTreeHelper.GetParent(v);
        }
        return null;
    }

    // Returns scaling (sx, sy) from this adorner to its layer, the center mapped to that layer, and the layer size.
    private (double sx, double sy, Point centerOnLayer, Size layerSize) GetLayerMetrics(Point localCenter)
    {
        // Use the adorner's own ancestor layer; TransformToAncestor requires the ancestor to be in THIS visual tree.
        AdornerLayer? layer = FindOwningAdornerLayer(this);
        if (layer is null)
            return (1.0, 1.0, new Point(), new Size(double.PositiveInfinity, double.PositiveInfinity));

        Size layerSize = layer.RenderSize;

        try
        {
            GeneralTransform t = this.TransformToAncestor(layer);
            // Map unit vectors to estimate scale (assumes no shear; acceptable for our purpose)
            Point p0 = t.Transform(new Point(0, 0));
            Point px = t.Transform(new Point(1, 0));
            Point py = t.Transform(new Point(0, 1));
            double sx = Math.Max(0.0001, (px - p0).Length);
            double sy = Math.Max(0.0001, (py - p0).Length);
            Point centerOnLayer = t.Transform(localCenter);
            return (sx, sy, centerOnLayer, layerSize);
        }
        catch (InvalidOperationException)
        {
            // Occurs when not connected yet; return safe defaults
            return (1.0, 1.0, new Point(), layerSize);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (AdornedElement == null) return;
        Size sz = AdornedElement.RenderSize;
        _center = new Point(sz.Width / 2, sz.Height / 2);

        // Base radius surrounding the element with a fixed margin
        double baseRadius = Math.Max(sz.Width, sz.Height) / 2 + CircleMargin;

        // Viewport metrics
        (double sx, double sy, Point centerOnLayer, Size layerSize) = GetLayerMetrics(_center);
        double zoom = (Math.Abs(sx) + Math.Abs(sy)) * 0.5;

        // Estimate outward glyph extent in screen space assuming no scaling yet
        double estFontSize = BaseFontSize;
        double estLabelOffsetLocal = LabelRadialOffset;
        double estTicksLocal = TickLengthMajor;
        double estHandleLocal = HandleRadius;
        double estPenLocal = BasePenThickness;
        double estOutwardLocal = Math.Max(estHandleLocal, estTicksLocal + estLabelOffsetLocal + estFontSize * 0.5) + estPenLocal;
        double extraPaddingScreen = BaseInnerPaddingScreen + (estOutwardLocal * zoom);

        // Available radius in screen space from center to viewport edge, minus padding
        double maxScreenR = Math.Max(0.0,
            Math.Min(
                Math.Min(centerOnLayer.X, layerSize.Width - centerOnLayer.X),
                Math.Min(centerOnLayer.Y, layerSize.Height - centerOnLayer.Y)
            ) - extraPaddingScreen);

        // Convert screen-space cap back to local units using effective zoom
        double capLocalRadius = zoom > 0 ? maxScreenR / zoom : baseRadius;

        // Start shrinking a bit early to avoid last-moment clipping
        double radiusScale = Math.Min(1.0, (capLocalRadius * EarlyStartFactor) / baseRadius);
        if (radiusScale < 0) radiusScale = 0;

        // Final radius to draw
        _radius = baseRadius * radiusScale;

        // Visual scale drives pen thickness, tick length, font size, handle size, and offsets
        double visualScale = Math.Max(0.1, radiusScale); // keep a sane minimum for visibility

        double circlePenThickness = BasePenThickness * visualScale;
        double tickPenThickness = BaseTickPenThickness * visualScale;
        double handleRadius = HandleRadius * visualScale;
        double majorTickLen = TickLengthMajor * visualScale;
        double minorTickLen = TickLengthMinor * visualScale;
        double fontSize = BaseFontSize * visualScale;
        double labelOffset = LabelRadialOffset * visualScale;

        Pen circlePen = new(new SolidColorBrush(Color.FromArgb(140, 0, 102, 255)), circlePenThickness);
        circlePen.Freeze();
        Pen tickPen = new(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), tickPenThickness);
        tickPen.Freeze();
        Pen handleOutline = new(Brushes.White, Math.Max(0.75, 1.0 * visualScale));
        handleOutline.Freeze();

#if DEBUG
        LogViewportMetrics(sz, baseRadius, sx, sy, zoom, centerOnLayer, layerSize, maxScreenR, capLocalRadius, _radius,
            visualScale, circlePenThickness, tickPenThickness, handleRadius, fontSize, labelOffset, extraPaddingScreen);
#endif

        // Circle
        dc.DrawEllipse(null, circlePen, _center, _radius, _radius);

        // Ticks
        for (int deg = -180; deg < 180; deg += MinorTickEvery)
        {
            bool major = deg % MajorTickEvery == 0;
            double len = major ? majorTickLen : minorTickLen;
            DrawTick(dc, tickPen, fontSize, labelOffset, deg, len, major);
        }

        // Handle position (0° is to the right, increasing clockwise)
        Point handlePoint = PointOnCircle(_center, _radius, Angle);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(255, 0, 102, 255)), handleOutline, handlePoint, handleRadius, handleRadius);

        // Optional current angle text near handle
        FormattedText ft = CreateFormattedText($"{Angle:0.0}°", fontSize);
        Point textPos = handlePoint + new Vector(12 * visualScale, -12 * visualScale);
        dc.DrawText(ft, textPos);
    }

#if DEBUG
    [Conditional("DEBUG")]
    private void LogViewportMetrics(Size elementSize, double baseRadius, double sx, double sy, double zoom,
        Point centerOnLayer, Size layerSize, double maxScreenR, double capLocalRadius, double finalRadius,
        double visualScale, double circlePenThickness, double tickPenThickness, double handleRadius,
        double fontSize, double labelOffset, double extraPaddingScreen)
    {
        long now = Environment.TickCount64;
        if (now - _lastLogTick < LogIntervalMs)
            return;
        _lastLogTick = now;

        Debug.WriteLine($"[RotateAdorner] ElementSize={elementSize.Width:F2}x{elementSize.Height:F2} CenterLocal=({_center.X:F2},{_center.Y:F2}) BaseR={baseRadius:F2}");
        Debug.WriteLine($"[RotateAdorner] LayerSize={layerSize.Width:F2}x{layerSize.Height:F2} CenterOnLayer=({centerOnLayer.X:F2},{centerOnLayer.Y:F2}) sx={sx:F3} sy={sy:F3} zoom={zoom:F3}");
        Debug.WriteLine($"[RotateAdorner] maxScreenR={maxScreenR:F2} capLocalR={capLocalRadius:F2} FinalR={finalRadius:F2} VisualScale={visualScale:F3}");
        Debug.WriteLine($"[RotateAdorner] Pens(Circle={circlePenThickness:F2}, Tick={tickPenThickness:F2}) HandleR={handleRadius:F2} Font={fontSize:F2} LabelOffset={labelOffset:F2} PaddingScreen={extraPaddingScreen:F2}");
    }
#endif

    private void DrawTick(DrawingContext dc, Pen tickPen, double fontSize, double labelOffset, double deg, double len, bool major)
    {
        Point outer = PointOnCircle(_center, _radius, deg);
        Point inner = PointOnCircle(_center, _radius - len, deg);
        dc.DrawLine(tickPen, inner, outer);

        if (major)
        {
            // Draw label
            FormattedText ft = CreateFormattedText(NormalizeAngle(deg).ToString("0"), fontSize);
            Point labelPos = PointOnCircle(_center, _radius + labelOffset, deg) - new Vector(ft.Width / 2, ft.Height / 2);
            dc.DrawText(ft, labelPos);
        }
    }

    private FormattedText CreateFormattedText(string text, double fontSize) => new(
        text,
        System.Globalization.CultureInfo.CurrentUICulture,
        FlowDirection.LeftToRight,
        _typeface,
        fontSize,
        Brushes.White,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        // Provide generous hit target around handle, adjusted for zoom so it feels consistent
        (double sx, double sy, Point _, Size _) = GetLayerMetrics(_center);
        double zoom = (Math.Abs(sx) + Math.Abs(sy)) * 0.5;

        // Estimate current visual scale based on radius if available
        double baseRadius = Math.Max(AdornedElement?.RenderSize.Width ?? 0, AdornedElement?.RenderSize.Height ?? 0) / 2 + CircleMargin;
        double visualScale = baseRadius > 0 ? Math.Clamp(_radius / baseRadius, 0.1, 1.0) : 1.0;

        double localHitRadius = (HandleHitRadius * visualScale) / Math.Max(0.0001, zoom);

        Point p = hitTestParameters.HitPoint;
        Point handle = PointOnCircle(_center, _radius, Angle);
        if ((p - handle).Length <= localHitRadius)
            return new PointHitTestResult(this, p);

        return null;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            CaptureMouse();
            _isDragging = true;
            Point p = e.GetPosition(this);
            _dragStartVectorAngle = VectorAngleDegrees(p - _center);
            _dragStartAdornerAngle = Angle;
            e.Handled = true;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isDragging)
        {
            Point p = e.GetPosition(this);
            double currentVectorAngle = VectorAngleDegrees(p - _center);
            double delta = currentVectorAngle - _dragStartVectorAngle; // clockwise positive already

            double newAngle = _dragStartAdornerAngle + delta;
            newAngle = NormalizeAngle(newAngle);

            // Modifiers
            ModifierKeys mods = Keyboard.Modifiers;
            if (mods.HasFlag(ModifierKeys.Control))
            {
                // Fine control - reduce change effect
                double fineStart = _dragStartAdornerAngle;
                double fineDelta = delta * 0.25; // quarter speed
                newAngle = NormalizeAngle(fineStart + fineDelta);
            }
            if (mods.HasFlag(ModifierKeys.Shift))
            {
                // Snap to 5° increments
                newAngle = Math.Round(newAngle / 5.0) * 5.0;
            }

            Angle = newAngle;
            AngleChanging?.Invoke(this, Angle);
            e.Handled = true;
        }
        else
        {
            // Update cursor when hovering handle
            Point p = e.GetPosition(this);
            Point handle = PointOnCircle(_center, _radius, Angle);

            (double sx, double sy, Point _, Size _) = GetLayerMetrics(_center);
            double zoom = (Math.Abs(sx) + Math.Abs(sy)) * 0.5;
            double baseRadius = Math.Max(AdornedElement?.RenderSize.Width ?? 0, AdornedElement?.RenderSize.Height ?? 0) / 2 + CircleMargin;
            double visualScale = baseRadius > 0 ? Math.Clamp(_radius / baseRadius, 0.1, 1.0) : 1.0;

            double localHitRadius = (HandleHitRadius * visualScale) / Math.Max(0.0001, zoom);
            Cursor = (p - handle).Length <= localHitRadius ? Cursors.Hand : Cursors.Arrow;
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_isDragging && e.ChangedButton == MouseButton.Left)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            AngleChangedFinal?.Invoke(this, Angle);
            e.Handled = true;
        }
        base.OnMouseUp(e);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        double rad = angleDegrees * Math.PI / 180.0;
        double x = center.X + radius * Math.Cos(rad);
        double y = center.Y + radius * Math.Sin(rad);
        return new Point(x, y);
    }

    private static double VectorAngleDegrees(Vector v)
    {
        if (v.LengthSquared < double.Epsilon)
            return 0;
        return Math.Atan2(v.Y, v.X) * 180.0 / Math.PI;
    }

    private static double NormalizeAngle(double deg)
    {
        while (deg > 180) deg -= 360;
        while (deg < -180) deg += 360;
        return deg;
    }
}
