using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MagickCrop.Behaviors;

public static class PinchZoomBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PinchZoomBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static readonly DependencyProperty ZoomElementProperty =
        DependencyProperty.RegisterAttached(
            "ZoomElement",
            typeof(FrameworkElement),
            typeof(PinchZoomBehavior),
            new PropertyMetadata(null));

    public static void SetZoomElement(DependencyObject element, FrameworkElement? value) => element.SetValue(ZoomElementProperty, value);
    public static FrameworkElement? GetZoomElement(DependencyObject element) => (FrameworkElement?)element.GetValue(ZoomElementProperty);

    public static readonly DependencyProperty MinScaleProperty =
        DependencyProperty.RegisterAttached(
            "MinScale",
            typeof(double),
            typeof(PinchZoomBehavior),
            new PropertyMetadata(0.1));

    public static void SetMinScale(DependencyObject element, double value) => element.SetValue(MinScaleProperty, value);
    public static double GetMinScale(DependencyObject element) => (double)element.GetValue(MinScaleProperty);

    public static readonly DependencyProperty MaxScaleProperty =
        DependencyProperty.RegisterAttached(
            "MaxScale",
            typeof(double),
            typeof(PinchZoomBehavior),
            new PropertyMetadata(10.0));

    public static void SetMaxScale(DependencyObject element, double value) => element.SetValue(MaxScaleProperty, value);
    public static double GetMaxScale(DependencyObject element) => (double)element.GetValue(MaxScaleProperty);

    public static readonly DependencyProperty EnableInertiaProperty =
        DependencyProperty.RegisterAttached(
            "EnableInertia",
            typeof(bool),
            typeof(PinchZoomBehavior),
            new PropertyMetadata(true));

    public static void SetEnableInertia(DependencyObject element, bool value) => element.SetValue(EnableInertiaProperty, value);
    public static bool GetEnableInertia(DependencyObject element) => (bool)element.GetValue(EnableInertiaProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement host)
            return;

        if ((bool)e.NewValue)
        {
            host.IsManipulationEnabled = true; // ensure enabled
            host.ManipulationStarting += Host_ManipulationStarting;
            host.ManipulationDelta += Host_ManipulationDelta;
            host.ManipulationInertiaStarting += Host_ManipulationInertiaStarting;
            host.ManipulationCompleted += Host_ManipulationCompleted;
        }
        else
        {
            host.ManipulationStarting -= Host_ManipulationStarting;
            host.ManipulationDelta -= Host_ManipulationDelta;
            host.ManipulationInertiaStarting -= Host_ManipulationInertiaStarting;
            host.ManipulationCompleted -= Host_ManipulationCompleted;
        }
    }

    private static void Host_ManipulationStarting(object? sender, ManipulationStartingEventArgs e)
    {
        if (sender is not UIElement host) return;

        // Use the host as the manipulation container so translation units match TranslateTransform
        e.ManipulationContainer = host;
        e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
        e.Handled = true;
    }

    private static void Host_ManipulationInertiaStarting(object? sender, ManipulationInertiaStartingEventArgs e)
    {
        if (sender is not DependencyObject host) return;

        if (!GetEnableInertia(host))
            return;

        // Gentle inertia for a nice glide; values are device-independent units/ms^2
        e.TranslationBehavior.DesiredDeceleration = 0.002;
        e.ExpansionBehavior.DesiredDeceleration = 0.004;
        e.Handled = true;
    }

    private static void Host_ManipulationCompleted(object? sender, ManipulationCompletedEventArgs e)
    {
        e.Handled = true;
    }

    private static void Host_ManipulationDelta(object? sender, ManipulationDeltaEventArgs e)
    {
        if (sender is not FrameworkElement host) return;

        FrameworkElement? zoomElement = GetZoomElement(host) ?? host;
        if (zoomElement is null) return;

        EnsureTransforms(zoomElement, out ScaleTransform? scale, out TranslateTransform? translate, out _);

        double minScale = Math.Max(0.01, GetMinScale(host));
        double maxScale = Math.Max(minScale, GetMaxScale(host));

        // 1) Apply pan in host units (matches TranslateTransform units)
        Vector trans = e.DeltaManipulation.Translation;
        translate.X += trans.X;
        translate.Y += trans.Y;

        // 2) Compute the content point currently under the gesture origin (host -> content)
        Point originInHost = e.ManipulationOrigin;
        Point contentPointUnderOrigin;
        try
        {
            GeneralTransform hostToContent = host.TransformToVisual(zoomElement);
            contentPointUnderOrigin = hostToContent.Transform(originInHost);
        }
        catch
        {
            // Fallback if transform chain not available
            contentPointUnderOrigin = originInHost;
        }

        // 3) Apply zoom with clamping (uniform)
        double deltaScale = e.DeltaManipulation.Scale.X;
        if (double.IsNaN(deltaScale) || deltaScale <= 0) deltaScale = 1.0;

        double currentScale = scale.ScaleX;
        double targetScale = Math.Clamp(currentScale * deltaScale, minScale, maxScale);
        double usedScaleFactor = targetScale / currentScale;

        if (!double.IsNaN(usedScaleFactor) && !double.IsInfinity(usedScaleFactor) && usedScaleFactor > 0)
        {
            scale.ScaleX = targetScale;
            scale.ScaleY = targetScale;

            // 4) After scaling, re-evaluate where that content point ended up in host coords,
            // then correct translation so it stays under the fingers (zoom around origin)
            try
            {
                GeneralTransform contentToHost = zoomElement.TransformToVisual(host);
                Point originAfterScale = contentToHost.Transform(contentPointUnderOrigin);

                Vector correction = originInHost - originAfterScale;
                translate.X += correction.X;
                translate.Y += correction.Y;
            }
            catch
            {
                // ignore transform exceptions
            }
        }

        e.Handled = true;
    }

    private static void EnsureTransforms(FrameworkElement element, out ScaleTransform scale, out TranslateTransform translate, out TransformGroup group)
    {
        group = element.RenderTransform as TransformGroup ?? new TransformGroup();

        scale = null!;
        translate = null!;

        // Try to reuse existing transforms if they exist in any order
        foreach (Transform? child in group.Children)
        {
            if (child is ScaleTransform st && scale is null) scale = st;
            else if (child is TranslateTransform tt && translate is null) translate = tt;
        }

        bool changed = false;

        if (scale is null)
        {
            scale = new ScaleTransform(1.0, 1.0);
            group.Children.Insert(0, scale);
            changed = true;
        }

        if (translate is null)
        {
            translate = new TranslateTransform(0, 0);
            group.Children.Add(translate);
            changed = true;
        }

        if (!ReferenceEquals(element.RenderTransform, group) || changed)
            element.RenderTransform = group;
    }
}
