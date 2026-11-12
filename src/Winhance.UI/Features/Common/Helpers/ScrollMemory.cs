using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.Common.Helpers;

// An uncached page is rebuilt on every visit and would open at the top, so its last offset is kept for the session.
// The content often loads after the scroller does, so the offset is re-applied on each layout pass until the extent
// reaches it, and the first wheel, click or key from the user ends that.
internal static class ScrollMemory
{
    private static readonly Dictionary<string, double> Offsets = new();
    private static readonly Dictionary<string, (int Index, double Into)> ListAnchors = new();
    private static readonly HashSet<string> Restoring = new();

    // A virtualized list guesses the height of rows it has not built, so a saved pixel offset lands on other rows next
    // visit. The first visible row and how far the view sits into it are kept instead. The anchor is taken whenever a scroll
    // settles, because by Unloaded the rows are out of the tree and measure at zero. The host restores, as it
    // defers its items past Loaded.
    public static void Track(string key, ListViewBase list, ScrollViewer scroller) =>
        scroller.ViewChanged += (_, e) =>
        {
            if (!e.IsIntermediate && !Restoring.Contains(key))
                Save(key, list, scroller);
        };

    private static void Save(string key, ListViewBase list, ScrollViewer scroller)
    {
        for (var index = 0; index < list.Items.Count; index++)
        {
            if (list.ContainerFromIndex(index) is not FrameworkElement row) continue;
            var top = TopIn(scroller, row);
            if (top + row.ActualHeight <= 0) continue;
            ListAnchors[key] = (index, -top);
            return;
        }
    }

    // The scroll lands a frame later and the rows it reaches are built over the next frames, which moves the anchor
    // again (a tall expander row can grow for several frames after it first sits right), so the correction repeats a
    // frame apart until the row has held its place and height for five frames. The list's own scroll events during
    // that would overwrite the anchor with half-built rows, so they are ignored until it ends.
    public static async Task<bool> RestoreAsync(string key, ListViewBase list)
    {
        if (!ListAnchors.Remove(key, out var anchor) || list.FindDescendant<ScrollViewer>() is not { } scroller) return false;

        Restoring.Add(key);
        try
        {
            var lastHeight = -1.0;
            for (int pass = 0, settled = 0; pass < 120 && settled < 5; pass++)
            {
                if (!list.IsLoaded || anchor.Index >= list.Items.Count) return false;
                if (list.ContainerFromIndex(anchor.Index) is not FrameworkElement row)
                {
                    list.ScrollIntoView(list.Items[anchor.Index], ScrollIntoViewAlignment.Leading);
                    await NextFrameAsync();
                    continue;
                }
                var miss = TopIn(scroller, row) + anchor.Into;
                settled = Math.Abs(miss) < 1 && row.ActualHeight == lastHeight ? settled + 1 : 0;
                lastHeight = row.ActualHeight;
                if (Math.Abs(miss) >= 1)
                    scroller.ChangeView(null, scroller.VerticalOffset + miss, null, disableAnimation: true);
                await NextFrameAsync();
            }
            return true;
        }
        finally
        {
            Restoring.Remove(key);
            if (list.IsLoaded)
                Save(key, list, scroller);
        }
    }

    private static double TopIn(ScrollViewer scroller, UIElement row) =>
        row.TransformToVisual(scroller).TransformPoint(default).Y;

    private static Task NextFrameAsync()
    {
        var frame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnRendering(object? sender, object e)
        {
            CompositionTarget.Rendering -= OnRendering;
            frame.SetResult();
        }
        CompositionTarget.Rendering += OnRendering;
        return frame.Task;
    }

    // Returns a call that ends a pending restore, for a page that scrolls the view itself.
    public static Action Keep(string key, ScrollView view) => KeepCore(key, view,
        () => view.VerticalOffset,
        () => view.ScrollableHeight,
        y => view.ScrollTo(view.HorizontalOffset, y, new ScrollingScrollOptions(ScrollingAnimationMode.Disabled)));

    public static Action Keep(string key, ScrollViewer viewer) => KeepCore(key, viewer,
        () => viewer.VerticalOffset,
        () => viewer.ScrollableHeight,
        y => viewer.ChangeView(null, y, null, disableAnimation: true));

    private static Action KeepCore(string key, FrameworkElement scroller, Func<double> offset, Func<double> scrollable, Action<double> scrollTo)
    {
        double? pending = null;

        void OnLayoutUpdated(object? sender, object e)
        {
            if (pending is not double target) return;
            scrollTo(Math.Min(target, scrollable()));
            if (scrollable() >= target)
                Stop();
        }

        void Stop()
        {
            pending = null;
            scroller.LayoutUpdated -= OnLayoutUpdated;
        }

        void Restore()
        {
            if (!Offsets.TryGetValue(key, out var target) || target <= 0 || pending is not null) return;
            pending = target;
            scroller.LayoutUpdated += OnLayoutUpdated;
        }

        var userInput = new PointerEventHandler((_, _) => Stop());
        scroller.AddHandler(UIElement.PointerWheelChangedEvent, userInput, handledEventsToo: true);
        scroller.AddHandler(UIElement.PointerPressedEvent, userInput, handledEventsToo: true);
        scroller.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, _) => Stop()), handledEventsToo: true);

        scroller.Loaded += (_, _) => Restore();
        scroller.Unloaded += (_, _) =>
        {
            Offsets[key] = pending ?? offset();
            Stop();
        };

        if (scroller.IsLoaded)
            Restore();
        return Stop;
    }
}
