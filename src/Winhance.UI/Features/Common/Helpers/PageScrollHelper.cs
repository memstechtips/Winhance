using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Winhance.UI.Features.Common.Helpers;

// The feature-detail pages host a ListView with inner scrolling disabled inside an outer ScrollView; the
// ListView consumes PageUp/PageDown for focus traversal, which scrolls the outer ScrollView all the way to the
// top/bottom (issue #581). This replaces that with viewport-sized paging; focus does not move. Not handled when
// the focused element sits inside a control that owns its own paging (open ComboBox popup, AutoSuggestBox with
// suggestions open, multi-line TextBox, an enabled nested scroller).
internal static class PageScrollHelper
{
    // Kept small so content with only modest overflow doesn't jump straight to the end.
    private const double PageStepFraction = 0.15;

    // PreviewKeyDown tunnels root -> target, so e.Handled = true lands before the ListView's own handler moves focus
    // (which raises BringIntoViewRequested and jumps the ScrollView). A bubbling KeyDown subscription stays as a fallback.
    public static void Attach(UIElement keyEventSource, ScrollView scrollView)
    {
        if (keyEventSource == null || scrollView == null) return;

        keyEventSource.AddHandler(
            UIElement.PreviewKeyDownEvent,
            new KeyEventHandler((s, e) => HandleKey(scrollView, e)),
            handledEventsToo: true);

        keyEventSource.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler((s, e) => HandleKey(scrollView, e)),
            handledEventsToo: true);
    }

    // Sets e.Handled = true only when it actually scrolled.
    public static void HandleKey(ScrollView scrollView, KeyRoutedEventArgs e)
    {
        if (scrollView == null || e == null) return;
        if (!IsPagingKey(e.Key)) return;

        if (ShouldSkipForFocusedElement(e.OriginalSource as DependencyObject, scrollView))
            return;

        if (scrollView.ScrollableHeight <= 0) return;

        var options = new ScrollingScrollOptions(ScrollingAnimationMode.Disabled);

        switch (e.Key)
        {
            case VirtualKey.PageUp:
                scrollView.ScrollBy(0, -scrollView.ViewportHeight * PageStepFraction, options);
                e.Handled = true;
                break;

            case VirtualKey.PageDown:
                scrollView.ScrollBy(0, scrollView.ViewportHeight * PageStepFraction, options);
                e.Handled = true;
                break;

            case VirtualKey.Home:
                scrollView.ScrollTo(scrollView.HorizontalOffset, 0, options);
                e.Handled = true;
                break;

            case VirtualKey.End:
                scrollView.ScrollTo(scrollView.HorizontalOffset, scrollView.ScrollableHeight, options);
                e.Handled = true;
                break;
        }
    }

    internal static bool IsPagingKey(VirtualKey key) =>
        key == VirtualKey.PageUp ||
        key == VirtualKey.PageDown ||
        key == VirtualKey.Home ||
        key == VirtualKey.End;

    // A nested scroller only owns the key if it is actually scrollable - a ListView's internal ScrollViewer with
    // vertical scrolling disabled (SettingsListView) must NOT block us, or every key press is swallowed by that inert scroller.
    internal static bool ShouldSkipForFocusedElement(DependencyObject? focused, ScrollView scrollViewHost)
    {
        for (var current = focused; current != null; current = VisualTreeHelper.GetParent(current))
        {
            // Classic ScrollViewer — skip past it if vertical scrolling is disabled.
            if (current is ScrollViewer svr && svr.VerticalScrollMode != ScrollMode.Disabled)
                return true;

            // WinUI 3 ScrollView — same deal, and don't claim the host as "nested".
            if (current is ScrollView sv
                && !ReferenceEquals(sv, scrollViewHost)
                && sv.VerticalScrollMode != ScrollingScrollMode.Disabled)
                return true;

            if (current is ComboBox combo && combo.IsDropDownOpen) return true;

            if (current is AutoSuggestBox asb && asb.IsSuggestionListOpen) return true;

            if (current is TextBox tb && (tb.AcceptsReturn || tb.TextWrapping != TextWrapping.NoWrap))
                return true;
        }

        return false;
    }
}
