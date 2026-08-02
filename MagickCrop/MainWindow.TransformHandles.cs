using MagickCrop.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MagickCrop;

/// <summary>
/// Interaction polish for the perspective/tri-fold/un-warp transform handles: keeping the pixel
/// under the dragged handle visible, and nudging a handle with the arrow keys.
/// </summary>
public partial class MainWindow
{
    /// <summary>Half-extent of the crosshair, in screen pixels. Geometry spans 0..2x this.</summary>
    private const double CrosshairRadius = 16;

    /// <summary>Gap left around the centre so the targeted pixel itself stays uncovered.</summary>
    private const double CrosshairCenterGap = 5;

    private static readonly Brush TransformHandleAccent =
        new SolidColorBrush(Color.FromRgb(0x00, 0x66, 0xFF));

    private Ellipse? draggedHandle;
    private Brush? draggedHandleOriginalFill;
    private Brush? draggedHandleOriginalStroke;
    private double draggedHandleOriginalStrokeThickness;

    private Path? activeHandleCrosshair;

    /// <summary>The handle most recently grabbed, so the arrow keys have something to nudge.</summary>
    private Ellipse? lastActiveTransformHandle;
    private int lastActiveTransformIndex = -1;

    /// <summary>
    /// Places a transform handle so its centre lands on <paramref name="desiredCenter"/>, applying
    /// the image-bounds constraint and refreshing everything that follows the handle.
    /// Shared by the drag path and the arrow-key nudge. Returns the centre actually used.
    /// </summary>
    private Point MoveTransformHandleTo(FrameworkElement handle, int handleIndex, Point desiredCenter)
    {
        Point center = ConstrainHandlePosition(desiredCenter);

        Canvas.SetLeft(handle, center.X - (handle.Width / 2));
        Canvas.SetTop(handle, center.Y - (handle.Height / 2));

        MovePolyline(handleIndex, center);
        UpdateActiveHandleCrosshair(center);
        UpdateCornerNavButtons();

        return center;
    }

    /// <summary>
    /// Switches the grabbed handle to a hollow ring and drops a crosshair over it, so the pixel
    /// being targeted stays visible instead of sitting under an opaque dot.
    /// </summary>
    private void BeginTransformHandleDrag(Ellipse handle, Point center)
    {
        // A previous drag that never saw a mouse-up would otherwise leave a handle hollow.
        EndTransformHandleDrag();

        draggedHandle = handle;
        draggedHandleOriginalFill = handle.Fill;
        draggedHandleOriginalStroke = handle.Stroke;
        draggedHandleOriginalStrokeThickness = handle.StrokeThickness;

        handle.Fill = null;
        handle.Stroke = TransformHandleAccent;
        handle.StrokeThickness = 2;

        EnsureActiveHandleCrosshair().Visibility = Visibility.Visible;
        UpdateActiveHandleCrosshair(center);
    }

    /// <summary>Restores the dragged handle's normal appearance and hides the crosshair.</summary>
    private void EndTransformHandleDrag()
    {
        if (draggedHandle is not null)
        {
            draggedHandle.Fill = draggedHandleOriginalFill;
            draggedHandle.Stroke = draggedHandleOriginalStroke;
            draggedHandle.StrokeThickness = draggedHandleOriginalStrokeThickness;
            draggedHandle = null;
            draggedHandleOriginalFill = null;
            draggedHandleOriginalStroke = null;
        }

        if (activeHandleCrosshair is not null)
            activeHandleCrosshair.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Builds the crosshair once, in a 32x32 box centred on (16, 16), with a hole in the middle.
    /// </summary>
    private Path EnsureActiveHandleCrosshair()
    {
        if (activeHandleCrosshair is not null)
            return activeHandleCrosshair;

        const double c = CrosshairRadius;
        GeometryGroup arms = new();
        arms.Children.Add(new LineGeometry(new Point(0, c), new Point(c - CrosshairCenterGap, c)));
        arms.Children.Add(new LineGeometry(new Point(c + CrosshairCenterGap, c), new Point(2 * c, c)));
        arms.Children.Add(new LineGeometry(new Point(c, 0), new Point(c, c - CrosshairCenterGap)));
        arms.Children.Add(new LineGeometry(new Point(c, c + CrosshairCenterGap), new Point(c, 2 * c)));
        arms.Freeze();

        activeHandleCrosshair = new Path
        {
            Data = arms,
            Stroke = TransformHandleAccent,
            StrokeThickness = 1,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };

        Panel.SetZIndex(activeHandleCrosshair, 950);
        ShapeCanvas.Children.Add(activeHandleCrosshair);
        return activeHandleCrosshair;
    }

    /// <summary>
    /// Moves the crosshair onto the handle centre. The geometry is authored in screen pixels, so
    /// it is counter-scaled against the canvas zoom the same way the handles themselves are.
    /// </summary>
    private void UpdateActiveHandleCrosshair(Point center)
    {
        if (activeHandleCrosshair is null || activeHandleCrosshair.Visibility != Visibility.Visible)
            return;

        double inverseScale = 1.0 / Math.Max(MinZoom, canvasScale.ScaleX);

        // Layout puts the geometry's own (0,0) at Canvas.Left/Top, and the scale is taken about
        // the geometry centre, so the crosshair centre stays exactly on Left + CrosshairRadius.
        Canvas.SetLeft(activeHandleCrosshair, center.X - CrosshairRadius);
        Canvas.SetTop(activeHandleCrosshair, center.Y - CrosshairRadius);
        activeHandleCrosshair.RenderTransform =
            new ScaleTransform(inverseScale, inverseScale, CrosshairRadius, CrosshairRadius);
    }

    /// <summary>Keeps the crosshair at a constant screen size when the zoom changes mid-drag.</summary>
    private void UpdateActiveHandleCrosshairScale()
    {
        if (draggedHandle is null)
            return;

        UpdateActiveHandleCrosshair(GeometryMathHelper.GetEllipseCenter(draggedHandle));
    }

    /// <summary>
    /// Nudges the most recently grabbed transform handle by one step. Returns false when there is
    /// nothing to nudge so the key press can fall through to its normal handling.
    /// </summary>
    private bool TryNudgeTransformHandle(Key key)
    {
        if (lastActiveTransformHandle is not Ellipse handle
            || handle.Visibility != Visibility.Visible
            || lastActiveTransformIndex < 0)
        {
            return false;
        }

        Vector direction = key switch
        {
            Key.Left => new Vector(-1, 0),
            Key.Right => new Vector(1, 0),
            Key.Up => new Vector(0, -1),
            Key.Down => new Vector(0, 1),
            _ => default,
        };

        if (direction.LengthSquared == 0)
            return false;

        double step = 1;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            step = 10;
        else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            step = 0.25;

        Point center = GeometryMathHelper.GetEllipseCenter(handle) + (direction * step);
        Point placed = MoveTransformHandleTo(handle, lastActiveTransformIndex, center);

        // Show where it landed — the nudge is otherwise easy to miss when zoomed out.
        ShowPixelZoom(placed);
        return true;
    }
}
