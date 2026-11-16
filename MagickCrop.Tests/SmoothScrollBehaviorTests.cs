using MagickCrop.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MagickCrop.Tests;

[TestClass]
[DoNotParallelize]
public class SmoothScrollBehaviorTests
{
    [STATestMethod]
    public void ScrollViewerMouseWheelEasesInsteadOfJumping()
    {
        SmoothScrollBehavior.RegisterGlobally();

        ScrollViewer scrollViewer = new()
        {
            Width = 240,
            Height = 120,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Children =
                {
                    new Border { Height = 1200 },
                },
            },
        };
        scrollViewer.Measure(new Size(240, 120));
        scrollViewer.Arrange(new Rect(0, 0, 240, 120));
        scrollViewer.UpdateLayout();
        Assert.IsTrue(scrollViewer.ScrollableHeight > 0);

        MouseWheelEventArgs wheel = new(Mouse.PrimaryDevice, Environment.TickCount, -120)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = scrollViewer,
        };

        scrollViewer.RaiseEvent(wheel);
        Assert.IsTrue(wheel.Handled);

        double firstOffset = scrollViewer.VerticalOffset;
        double expectedOffset = Math.Min(
            scrollViewer.ScrollableHeight,
            SystemParameters.WheelScrollLines * 16);
        SmoothScrollBehavior.AdvanceAnimations(1);
        scrollViewer.UpdateLayout();
        double finalOffset = scrollViewer.VerticalOffset;

        Assert.IsTrue(finalOffset > firstOffset);
        Assert.AreEqual(expectedOffset, finalOffset, 0.5);
    }

    [STATestMethod]
    public void ListBoxesDefaultToPixelScrolling()
    {
        SmoothScrollBehavior.RegisterGlobally();

        ListBox listBox = new();

        Assert.AreEqual(ScrollUnit.Pixel, VirtualizingPanel.GetScrollUnit(listBox));
    }
}
