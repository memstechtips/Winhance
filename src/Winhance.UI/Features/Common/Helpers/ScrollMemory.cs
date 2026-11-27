using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.UI.Controls;
using CommunityToolkit.WinUI.UI.Controls.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.Common.Helpers;

// An uncached page is rebuilt on every visit and would open at the top, so its last offset is kept for the session.
// The content often loads after the scroller does, so the offset is re-applied each frame while the extent grows,
// and any wheel, click, key or scroll it did not make ends that early.
internal static class ScrollMemory
{
    private static readonly Dictionary<string, double> Offsets = new();
    private static readonly Dictionary<string, (int Index, double Into)> ListAnchors = new();
    private static readonly Dictionary<string, (int Index, double Into)> GridRows = new();
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
                    await RenderFrames.NextAsync();
                    continue;
                }
                var miss = TopIn(scroller, row) + anchor.Into;
                settled = Math.Abs(miss) < 1 && row.ActualHeight == lastHeight ? settled + 1 : 0;
                lastHeight = row.ActualHeight;
                if (Math.Abs(miss) >= 1)
                    scroller.ChangeView(null, scroller.VerticalOffset + miss, null, disableAnimation: true);
                await RenderFrames.NextAsync();
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

    // The toolkit grid has no scroller, so it keeps the row at the top of the view and how far the view sits into it,
    // taken each time its own scroll bar moves. Called on each Loaded, after the grid has its items; the page clears
    // the grid's source on Unloaded, which moves the bar too, so an unloaded grid saves nothing.
    public static Task<bool> KeepAsync(string key, DataGrid grid)
    {
        if (grid.FindDescendant<ScrollBar>(bar => bar.Orientation == Orientation.Vertical) is not { } scrollBar
            || grid.FindDescendant<DataGridRowsPresenter>() is not { } rows)
            return Task.FromResult(false);

        // The bar moves before the grid places its rows (a thumb drag scrolls on the next measure), so the rows are
        // read two frames after the last move.
        var framesLeft = 0;

        void OnRendering(object? sender, object e)
        {
            if (--framesLeft > 0) return;
            CompositionTarget.Rendering -= OnRendering;
            if (!Restoring.Contains(key))
                Save(key, grid, rows);
        }

        void OnValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (Restoring.Contains(key)) return;
            if (framesLeft <= 0)
                CompositionTarget.Rendering += OnRendering;
            framesLeft = 2;
        }

        void OnUnloaded(object sender, RoutedEventArgs e)
        {
            scrollBar.ValueChanged -= OnValueChanged;
            CompositionTarget.Rendering -= OnRendering;
            framesLeft = 0;
            grid.Unloaded -= OnUnloaded;
        }

        scrollBar.ValueChanged += OnValueChanged;
        grid.Unloaded += OnUnloaded;
        return RestoreAsync(key, grid, rows, scrollBar);
    }

    private static void Save(string key, DataGrid grid, DataGridRowsPresenter rows)
    {
        if (!grid.IsLoaded) return;
        var placed = rows.Children.OfType<DataGridRow>()
            .Select(row => (Row: row, Top: TopIn(rows, row)))
            .Where(r => r.Top is not null)
            .Select(r => (r.Row.GetIndex(), r.Top!.Value, r.Row.ActualHeight));
        if (GridAnchor(placed) is { } anchor)
            GridRows[key] = anchor;
    }

    internal static (int Index, double Into)? GridAnchor(IEnumerable<(int Index, double Top, double Height)> rows)
    {
        (int Index, double Top)? best = null;
        foreach (var (index, top, height) in rows)
        {
            if (top + height > 0 && (best is null || top < best.Value.Top))
                best = (index, top);
        }
        return best is { } row ? (row.Index, Math.Max(0, -row.Top)) : null;
    }

    // Scrolling to the last row first leaves the kept row above the view, so bringing it back puts it flush at the top;
    // the rest of the way into the row is a pixel scroll. The grid's only public pixel scroll is its automation peer's
    // SetScrollPercent, whose percent is of the bar's Maximum. Near the end of the list the grid cannot scroll that
    // far, so after three tries it settles where it can.
    private static async Task<bool> RestoreAsync(string key, DataGrid grid, DataGridRowsPresenter rows, ScrollBar scrollBar)
    {
        if (!GridRows.Remove(key, out var anchor) || anchor is (0, < 1)) return false;
        var (index, into) = anchor;

        Restoring.Add(key);
        try
        {
            var (lastTop, lastHeight, tries) = (double.NaN, -1.0, 0);
            for (int pass = 0, settled = 0; pass < 120 && settled < 5; pass++)
            {
                var items = grid.ItemsSource?.Cast<object>().ToList();
                if (!grid.IsLoaded || items is null || index >= items.Count) return false;
                var row = rows.Children.OfType<DataGridRow>().FirstOrDefault(r => r.GetIndex() == index && TopIn(rows, r) is not null);
                var top = row is null ? null : TopIn(rows, row);
                if (row is null || top is null)
                {
                    settled = 0;
                    grid.ScrollIntoView(items[^1], null);
                    grid.ScrollIntoView(items[index], null);
                }
                else if (Math.Abs(top.Value + into) >= 1 && tries++ < 3)
                {
                    // The peer moves the bar at once and the rows on a later measure, so the next look waits a frame
                    // more; a look at rows not yet moved would scroll the same distance twice.
                    settled = 0;
                    ScrollBy(grid, scrollBar, top.Value + into);
                    await RenderFrames.NextAsync();
                }
                else
                {
                    settled = top == lastTop && row.ActualHeight == lastHeight ? settled + 1 : 0;
                    (lastTop, lastHeight) = (top.Value, row.ActualHeight);
                }
                await RenderFrames.NextAsync();
            }
            return true;
        }
        finally
        {
            Restoring.Remove(key);
            Save(key, grid, rows);
        }
    }

    private static void ScrollBy(DataGrid grid, ScrollBar scrollBar, double pixels)
    {
        if (grid.IsEnabled && scrollBar.Maximum > 0
            && FrameworkElementAutomationPeer.CreatePeerForElement(grid).GetPattern(PatternInterface.Scroll) is IScrollProvider scroll)
            scroll.SetScrollPercent(ScrollPatternIdentifiers.NoScroll,
                Math.Clamp((scrollBar.Value + pixels) / scrollBar.Maximum * 100, 0, 100));
    }

    // The grid keeps rows for reuse collapsed or outside its rows area, so those do not count as in view.
    private static double? TopIn(DataGridRowsPresenter rows, DataGridRow row)
    {
        if (row.Visibility != Visibility.Visible || row.GetIndex() < 0) return null;
        var top = row.TransformToVisual(rows).TransformPoint(default).Y;
        return top < rows.ActualHeight ? top : null;
    }

    // Returns a call that ends a pending restore, for a page that scrolls the view itself.
    public static Action Keep(string key, ScrollViewer viewer)
    {
        ScrollRestore? restore = null;

        void OnRendering(object? sender, object e)
        {
            if (restore is null) return;
            var y = restore.NextOffset(viewer.ScrollableHeight);
            if (Math.Abs(viewer.VerticalOffset - y) >= 1)
                viewer.ChangeView(null, y, null, disableAnimation: true);
            if (restore.IsDone)
                Stop();
        }

        void Stop()
        {
            if (restore is null) return;
            restore = null;
            CompositionTarget.Rendering -= OnRendering;
        }

        void Restore()
        {
            if (!Offsets.TryGetValue(key, out var target) || target <= 0 || restore is not null) return;
            restore = new ScrollRestore(target, viewer.VerticalOffset);
            CompositionTarget.Rendering += OnRendering;
        }

        var userInput = new PointerEventHandler((_, _) => Stop());
        viewer.AddHandler(UIElement.PointerWheelChangedEvent, userInput, handledEventsToo: true);
        viewer.AddHandler(UIElement.PointerPressedEvent, userInput, handledEventsToo: true);
        viewer.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, _) => Stop()), handledEventsToo: true);

        // Anything else that moves the view ends it too, including the page's own ChangeView.
        viewer.ViewChanged += (_, _) =>
        {
            if (restore is not null && !restore.CausedOffset(viewer.VerticalOffset))
                Stop();
        };

        viewer.Loaded += (_, _) => Restore();
        viewer.Unloaded += (_, _) =>
        {
            Offsets[key] = restore?.Target ?? viewer.VerticalOffset;
            Stop();
        };

        if (viewer.IsLoaded)
            Restore();
        return Stop;
    }
}
