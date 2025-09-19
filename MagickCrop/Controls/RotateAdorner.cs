using System;
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

    private bool _isDragging;
    private Point _center;
    private double _radius;
    private double _dragStartVectorAngle; // absolute angle of mouse vector at start
    private double _dragStartAdornerAngle; // Angle value when drag started

    private double _angle; // backing field

    private readonly Pen _circlePen;
    private readonly Pen _tickPen;
    private readonly Brush _handleFill;
    private readonly Typeface _typeface = new("Segoe UI");

    public event EventHandler<double>? AngleChanging;
    public event EventHandler<double>? AngleChangedFinal; // fired on mouse up

    public RotateAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = true;
        SnapsToDevicePixels = true;
        _circlePen = new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 102, 255)), 1.5);
        _circlePen.Freeze();
        _tickPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 1);
        _tickPen.Freeze();
        _handleFill = new SolidColorBrush(Color.FromArgb(255, 0, 102, 255));
        _handleFill.Freeze();
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

    protected override void OnRender(DrawingContext dc)
    {
        if (AdornedElement == null) return;
        Size sz = AdornedElement.RenderSize;
        _center = new Point(sz.Width / 2, sz.Height / 2);
        _radius = Math.Max(sz.Width, sz.Height) / 2 + CircleMargin;

        // Circle
        dc.DrawEllipse(null, _circlePen, _center, _radius, _radius);

        // Ticks
        for (int deg = -180; deg < 180; deg += MinorTickEvery)
        {
            bool major = deg % MajorTickEvery == 0;
            double len = major ? TickLengthMajor : TickLengthMinor;
            DrawTick(dc, deg, len, major);
        }

        // Handle position (0° is to the right, increasing clockwise)
        Point handlePoint = PointOnCircle(_center, _radius, Angle);
        dc.DrawEllipse(_handleFill, new Pen(Brushes.White, 1), handlePoint, HandleRadius, HandleRadius);

        // Optional current angle text near handle
        FormattedText ft = CreateFormattedText($"{Angle:0.0}°");
        Point textPos = handlePoint + new Vector(12, -12);
        dc.DrawText(ft, textPos);
    }

    private void DrawTick(DrawingContext dc, double deg, double len, bool major)
    {
        Point outer = PointOnCircle(_center, _radius, deg);
        Point inner = PointOnCircle(_center, _radius - len, deg);
        dc.DrawLine(_tickPen, inner, outer);

        if (major)
        {
            // Draw label
            FormattedText ft = CreateFormattedText(NormalizeAngle(deg).ToString("0"));
            Vector dir = (outer - _center);
            dir.Normalize();
            Point labelPos = PointOnCircle(_center, _radius + 14, deg) - new Vector(ft.Width / 2, ft.Height / 2);
            dc.DrawText(ft, labelPos);
        }
    }

    private FormattedText CreateFormattedText(string text) => new(
        text,
        System.Globalization.CultureInfo.CurrentUICulture,
        FlowDirection.LeftToRight,
        _typeface,
        11,
        Brushes.White,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        // Provide generous hit target around handle
        Point p = hitTestParameters.HitPoint;
        Point handle = PointOnCircle(_center, _radius, Angle);
        if ((p - handle).Length <= HandleHitRadius)
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
            Cursor = (p - handle).Length <= HandleHitRadius ? Cursors.Hand : Cursors.Arrow;
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
