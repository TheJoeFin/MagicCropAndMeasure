using MagickCrop.Controls;
using MagickCrop.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace MagickCrop;

public partial class MainWindow
{
    /// <summary>Distance, in screen pixels, from a handle centre to its navigation buttons.</summary>
    private const double CornerNavOffset = 55;

    /// <summary>
    /// Neighbouring handles closer together than this (in screen pixels) don't get navigation
    /// buttons, because the jump would be pointless and the buttons would cover the handles.
    /// </summary>
    private const double CornerNavMinSeparation = 140;

    private readonly List<CornerNavButton> cornerNavButtons = [];
    private bool isCanvasTranslateAnimating;
    private int canvasTranslateAnimationToken;

    /// <summary>
    /// Returns the handles of the active transform, ordered as a closed ring, so each handle
    /// knows its previous and next neighbour.
    /// </summary>
    private List<Ellipse> GetActiveHandleRing()
    {
        if (isUnWarpMode)
        {
            return [TopLeft, UnWarpMidTop, TopRight, UnWarpMidRight,
                BottomRight, UnWarpMidBottom, BottomLeft, UnWarpMidLeft];
        }

        if (isTriFoldMode)
        {
            return [TopLeft, TopRight, UpperFoldRight, LowerFoldRight,
                BottomRight, BottomLeft, LowerFoldLeft, UpperFoldLeft];
        }

        Ellipse[] corners = [TopLeft, TopRight, BottomRight, BottomLeft];
        if (corners.All(corner => corner.Visibility == Visibility.Visible))
            return [.. corners];

        return [];
    }

    /// <summary>
    /// Rebuilds the per-handle "jump to neighbour" buttons for the active transform mode.
    /// Call whenever the set of visible handles changes.
    /// </summary>
    private void RefreshCornerNavButtons()
    {
        foreach (CornerNavButton button in cornerNavButtons)
        {
            button.Click -= CornerNavButton_Click;
            ShapeCanvas.Children.Remove(button);
        }

        cornerNavButtons.Clear();

        List<Ellipse> ring = GetActiveHandleRing();
        if (ring.Count < 3)
            return;

        for (int i = 0; i < ring.Count; i++)
        {
            Ellipse anchor = ring[i];
            Ellipse next = ring[(i + 1) % ring.Count];
            Ellipse previous = ring[((i - 1) + ring.Count) % ring.Count];

            AddCornerNavButton(anchor, next, "Center the view on the next point");
            AddCornerNavButton(anchor, previous, "Center the view on the previous point");
        }

        UpdateCornerNavButtons();
    }

    private void AddCornerNavButton(Ellipse anchor, Ellipse target, string toolTip)
    {
        CornerNavButton button = new()
        {
            Anchor = anchor,
            Target = target,
            ToolTip = toolTip,
            Visibility = Visibility.Collapsed,
        };

        button.Click += CornerNavButton_Click;
        cornerNavButtons.Add(button);
        ShapeCanvas.Children.Add(button);
    }

    /// <summary>
    /// Positions each navigation button a fixed screen distance from its handle, along the
    /// direction of its target, and counter-scales it so it stays the same size at any zoom.
    /// </summary>
    private void UpdateCornerNavButtons()
    {
        if (cornerNavButtons.Count == 0)
            return;

        double scale = Math.Max(MinZoom, canvasScale.ScaleX);
        double inverseScale = 1.0 / scale;

        foreach (CornerNavButton button in cornerNavButtons)
        {
            if (button.Anchor is not Ellipse anchor || button.Target is not Ellipse target
                || anchor.Visibility != Visibility.Visible || target.Visibility != Visibility.Visible)
            {
                button.Visibility = Visibility.Collapsed;
                continue;
            }

            Point anchorCenter = GeometryMathHelper.GetEllipseCenter(anchor);
            Point targetCenter = GeometryMathHelper.GetEllipseCenter(target);

            Vector direction = targetCenter - anchorCenter;
            double screenDistance = direction.Length * scale;

            // Hide the shortcut when the neighbour is already close enough to reach by eye.
            if (screenDistance < CornerNavMinSeparation)
            {
                button.Visibility = Visibility.Collapsed;
                continue;
            }

            direction.Normalize();
            Point position = anchorCenter + (direction * (CornerNavOffset * inverseScale));

            Canvas.SetLeft(button, position.X - (button.Width / 2));
            Canvas.SetTop(button, position.Y - (button.Height / 2));

            button.RenderTransformOrigin = new Point(0.5, 0.5);
            button.RenderTransform = new ScaleTransform(inverseScale, inverseScale);
            button.ArrowAngle = Math.Atan2(direction.Y, direction.X) * 180.0 / Math.PI;
            button.Visibility = Visibility.Visible;
        }
    }

    private void CornerNavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CornerNavButton { Target: Ellipse target })
            return;

        CenterViewportOnCanvasPoint(GeometryMathHelper.GetEllipseCenter(target), animate: true);
        e.Handled = true;
    }

    /// <summary>
    /// Pans the canvas so the given canvas-space point sits in the middle of the viewport.
    /// The zoom level is left untouched.
    /// </summary>
    private void CenterViewportOnCanvasPoint(Point canvasPoint, bool animate)
    {
        double scale = canvasScale.ScaleX;
        if (scale <= 0 || MainGrid.ActualWidth <= 0 || MainGrid.ActualHeight <= 0)
            return;

        double targetX = (MainGrid.ActualWidth / 2) - CanvasOriginOffset - (canvasPoint.X * scale);
        double targetY = (MainGrid.ActualHeight / 2) - CanvasOriginOffset - (canvasPoint.Y * scale);

        StopCanvasTranslateAnimation();

        if (!animate)
        {
            canvasTranslate.X = targetX;
            canvasTranslate.Y = targetY;
            return;
        }

        DoubleAnimation xAnimation = CreateTranslateAnimation(targetX);
        DoubleAnimation yAnimation = CreateTranslateAnimation(targetY);

        // Removing an animation does not cancel its clock, so a superseded animation still
        // raises Completed. The token makes those late callbacks no-ops.
        int token = ++canvasTranslateAnimationToken;

        xAnimation.Completed += (_, _) =>
        {
            if (token != canvasTranslateAnimationToken)
                return;

            isCanvasTranslateAnimating = false;
            canvasTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            canvasTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            canvasTranslate.X = targetX;
            canvasTranslate.Y = targetY;
        };

        isCanvasTranslateAnimating = true;
        canvasTranslate.BeginAnimation(TranslateTransform.XProperty, xAnimation);
        canvasTranslate.BeginAnimation(TranslateTransform.YProperty, yAnimation);
    }

    private static DoubleAnimation CreateTranslateAnimation(double to) => new(to, TimeSpan.FromMilliseconds(220))
    {
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        FillBehavior = FillBehavior.Stop,
    };

    /// <summary>
    /// Cancels an in-flight jump animation, keeping whatever position it had reached, so manual
    /// panning and zooming stay responsive.
    /// </summary>
    private void StopCanvasTranslateAnimation()
    {
        if (!isCanvasTranslateAnimating)
            return;

        isCanvasTranslateAnimating = false;
        canvasTranslateAnimationToken++;

        double currentX = canvasTranslate.X;
        double currentY = canvasTranslate.Y;

        canvasTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        canvasTranslate.BeginAnimation(TranslateTransform.YProperty, null);

        canvasTranslate.X = currentX;
        canvasTranslate.Y = currentY;
    }
}
