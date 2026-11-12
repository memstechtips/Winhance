using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Winhance.UI.Features.Common.Helpers;

// A ListView consumes PageUp/PageDown for focus traversal, which inside a scroller jumps it all the way to the
// top/bottom (issue #581). This replaces that with short fixed-fraction paging; focus does not move. Not handled when
// the focused element sits inside a control that owns its own paging (open ComboBox popup, AutoSuggestBox with
// suggestions open, multi-line TextBox, an enabled nested scroller).
internal static class PageScrollHelper
{
    // Kept small so content with only modest overflow doesn't jump straight to the end.
    private const double PageStepFraction = 0.15;

    public static void Attach(UIElement keyEventSource, ScrollView scrollView) => AttachCore(keyEventSource, scrollView);

    // A ListView's own scroller: the settings list scrolls itself so it can virtualize.
    public static void Attach(UIElement keyEventSource, ScrollViewer scrollViewer) => AttachCore(keyEventSource, scrollViewer);

    // PreviewKeyDown tunnels root -> target, so e.Handled = true lands before the ListView's own handler moves focus
    // (which raises BringIntoViewRequested and jumps the scroller). A bubbling KeyDown subscription stays as a fallback.
    private static void AttachCore(UIElement keyEventSource, FrameworkElement scroller)
    {
        var handler = new KeyEventHandler((s, e) => HandleKey(scroller, e));
        keyEventSource.AddHandler(UIElement.PreviewKeyDownEvent, handler, handledEventsToo: true);
        keyEventSource.AddHandler(UIElement.KeyDownEvent, handler, handledEventsToo: true);
    }

    // Sets e.Handled = true only when it actually scrolled.
    private static void HandleKey(FrameworkElement scroller, KeyRoutedEventArgs e)
    {
        if (!IsPagingKey(e.Key)) return;
        if (ShouldSkipForFocusedElement(e.OriginalSource as DependencyObject, scroller)) return;

        switch (scroller)
        {
            case ScrollView view when TargetOffset(e.Key, view.VerticalOffset, view.ViewportHeight, view.ScrollableHeight) is double y:
                view.ScrollTo(view.HorizontalOffset, y, new ScrollingScrollOptions(ScrollingAnimationMode.Disabled));
                break;
            case ScrollViewer viewer when TargetOffset(e.Key, viewer.VerticalOffset, viewer.ViewportHeight, viewer.ScrollableHeight) is double y:
                viewer.ChangeView(null, y, null, disableAnimation: true);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    internal static bool IsPagingKey(VirtualKey key) =>
        key == VirtualKey.PageUp ||
        key == VirtualKey.PageDown ||
        key == VirtualKey.Home ||
        key == VirtualKey.End;

    // Null when there is nothing to scroll or the key is not a paging key.
    internal static double? TargetOffset(VirtualKey key, double offset, double viewport, double scrollable)
    {
        if (scrollable <= 0) return null;

        double? target = key switch
        {
            VirtualKey.PageUp => offset - viewport * PageStepFraction,
            VirtualKey.PageDown => offset + viewport * PageStepFraction,
            VirtualKey.Home => 0,
            VirtualKey.End => scrollable,
            _ => null,
        };
        return target is double t ? Math.Clamp(t, 0, scrollable) : null;
    }

    // A nested scroller only owns the key if it is actually scrollable - a ListView's internal ScrollViewer with
    // vertical scrolling disabled (the settings list inside WIMUtil) must NOT block us, or every key press is
    // swallowed by that inert scroller. The host itself is never "nested".
    internal static bool ShouldSkipForFocusedElement(DependencyObject? focused, DependencyObject host)
    {
        for (var current = focused; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, host)) continue;

            if (current is ScrollViewer svr && svr.VerticalScrollMode != ScrollMode.Disabled)
                return true;

            if (current is ScrollView sv && sv.VerticalScrollMode != ScrollingScrollMode.Disabled)
                return true;

            if (current is ComboBox combo && combo.IsDropDownOpen) return true;

            if (current is AutoSuggestBox asb && asb.IsSuggestionListOpen) return true;

            if (current is TextBox tb && (tb.AcceptsReturn || tb.TextWrapping != TextWrapping.NoWrap))
                return true;
        }

        return false;
    }
}
