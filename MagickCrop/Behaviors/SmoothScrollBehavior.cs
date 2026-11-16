using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MagickCrop.Behaviors;

internal static class SmoothScrollBehavior
{
    private const double EaseTimeConstantSeconds = 0.055;
    private const double ExternalScrollTolerance = 2;

    private sealed class ScrollState
    {
        public double Current;
        public double Target;
        public bool IsAnimating;
        public double LastApplied;
        public double PreviousApplied;
        public bool HasApplied;
    }

    private static readonly ConditionalWeakTable<ScrollViewer, ScrollState> States = [];
    private static readonly List<ScrollViewer> Active = [];
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly Lock RegistrationLock = new();
    private static double lastFrameSeconds;
    private static bool isRegistered;
    private static bool isRenderHooked;

    public static void RegisterGlobally()
    {
        lock (RegistrationLock)
        {
            if (isRegistered)
                return;

            EventManager.RegisterClassHandler(
                typeof(ScrollViewer),
                UIElement.MouseWheelEvent,
                new MouseWheelEventHandler(ScrollViewer_MouseWheel));

            VirtualizingPanel.ScrollUnitProperty.OverrideMetadata(
                typeof(ListBox),
                new FrameworkPropertyMetadata(ScrollUnit.Pixel));
            VirtualizingPanel.ScrollUnitProperty.OverrideMetadata(
                typeof(DataGrid),
                new FrameworkPropertyMetadata(ScrollUnit.Pixel));
            VirtualizingPanel.ScrollUnitProperty.OverrideMetadata(
                typeof(TreeView),
                new FrameworkPropertyMetadata(ScrollUnit.Pixel));

            isRegistered = true;
        }
    }

    private static void ScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled
            || Keyboard.Modifiers != ModifierKeys.None
            || sender is not ScrollViewer scrollViewer
            || scrollViewer.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled
            || IsLogicalScrolling(scrollViewer))
        {
            return;
        }

        if (ScrollBy(scrollViewer, e.Delta))
            e.Handled = true;
    }

    private static bool IsLogicalScrolling(ScrollViewer scrollViewer)
    {
        if (!scrollViewer.CanContentScroll)
            return false;

        if (scrollViewer.TemplatedParent is ItemsControl itemsControl)
            return VirtualizingPanel.GetScrollUnit(itemsControl) == ScrollUnit.Item;

        return scrollViewer.Content is ItemsPresenter or IScrollInfo;
    }

    private static bool ScrollBy(ScrollViewer scrollViewer, int wheelDelta)
    {
        if (scrollViewer.ScrollableHeight <= 0)
            return false;

        ScrollState state = States.GetOrCreateValue(scrollViewer);
        double basis = state.IsAnimating ? state.Target : scrollViewer.VerticalOffset;
        double pixels = -wheelDelta / 120.0 * SystemParameters.WheelScrollLines * 16;
        double target = Math.Clamp(basis + pixels, 0, scrollViewer.ScrollableHeight);

        if (!state.IsAnimating
            && Math.Abs(target - scrollViewer.VerticalOffset) < 0.5)
        {
            return false;
        }

        if (!state.IsAnimating)
        {
            state.Current = scrollViewer.VerticalOffset;
            state.HasApplied = false;
        }

        state.Target = target;
        StartAnimating(scrollViewer, state);
        return true;
    }

    private static void StartAnimating(ScrollViewer scrollViewer, ScrollState state)
    {
        if (!state.IsAnimating)
        {
            state.IsAnimating = true;
            Active.Add(scrollViewer);
        }

        if (!isRenderHooked)
        {
            isRenderHooked = true;
            lastFrameSeconds = Clock.Elapsed.TotalSeconds;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }
    }

    private static void EnsureSettledAtTarget(ScrollViewer scrollViewer, double target)
    {
        scrollViewer.Dispatcher.BeginInvoke(() =>
        {
            if (States.TryGetValue(scrollViewer, out ScrollState? state) && state.IsAnimating)
                return;

            double expected = Math.Clamp(target, 0, scrollViewer.ScrollableHeight);
            if (Math.Abs(scrollViewer.VerticalOffset - expected) > 0.5)
                scrollViewer.ScrollToVerticalOffset(expected);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        double now = Clock.Elapsed.TotalSeconds;
        double deltaSeconds = Math.Clamp(now - lastFrameSeconds, 0.001, 0.1);
        lastFrameSeconds = now;
        AdvanceAnimations(deltaSeconds);
    }

    internal static void AdvanceAnimations(double deltaSeconds)
    {
        double blend = 1 - Math.Exp(-deltaSeconds / EaseTimeConstantSeconds);

        for (int index = Active.Count - 1; index >= 0; index--)
        {
            ScrollViewer scrollViewer = Active[index];
            if (!States.TryGetValue(scrollViewer, out ScrollState? state))
            {
                Active.RemoveAt(index);
                continue;
            }

            if (state.HasApplied
                && Math.Abs(scrollViewer.VerticalOffset - state.LastApplied) > ExternalScrollTolerance
                && Math.Abs(scrollViewer.VerticalOffset - state.PreviousApplied) > ExternalScrollTolerance)
            {
                state.IsAnimating = false;
                Active.RemoveAt(index);
                state.HasApplied = false;
                continue;
            }

            state.Target = Math.Clamp(state.Target, 0, scrollViewer.ScrollableHeight);
            state.Current += (state.Target - state.Current) * blend;

            bool settled = Math.Abs(state.Target - state.Current) < 0.5;
            if (settled)
            {
                state.Current = state.Target;
                state.IsAnimating = false;
                Active.RemoveAt(index);
            }

            state.PreviousApplied = state.HasApplied ? state.LastApplied : state.Current;
            state.LastApplied = state.Current;
            state.HasApplied = !settled;
            scrollViewer.ScrollToVerticalOffset(state.Current);

            if (settled)
                EnsureSettledAtTarget(scrollViewer, state.Target);
        }

        if (Active.Count == 0)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            isRenderHooked = false;
        }
    }
}
