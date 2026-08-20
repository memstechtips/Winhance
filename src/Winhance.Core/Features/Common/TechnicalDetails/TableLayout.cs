namespace Winhance.Core.Features.Common.TechnicalDetails;

public readonly record struct CellPlacement(double X, double Width, double ClipFromLeft)
{
    public bool IsHidden => ClipFromLeft >= Width;

    public bool NeedsClip => ClipFromLeft > 0 && !IsHidden;

    public double VisibleWidth => Math.Max(0, Width - ClipFromLeft);
}

// Pure and UI-free so it can be unit tested. Clipping removes the overlapping pixels outright, so z-order never
// enters into it; painting an opaque backdrop over the scrolled cells failed as soon as the brush was semi-transparent.
public static class TableLayout
{
    public static double FrozenWidth(IReadOnlyList<double> columnWidths, int frozenColumnCount)
    {
        ArgumentNullException.ThrowIfNull(columnWidths);
        var count = Math.Clamp(frozenColumnCount, 0, columnWidths.Count);
        double total = 0;
        for (int i = 0; i < count; i++) total += columnWidths[i];
        return total;
    }

    public static double TotalWidth(IReadOnlyList<double> columnWidths)
    {
        ArgumentNullException.ThrowIfNull(columnWidths);
        double total = 0;
        for (int i = 0; i < columnWidths.Count; i++) total += columnWidths[i];
        return total;
    }

    // Only the last column grows, which keeps the earlier columns aligned with their headers; nothing ever shrinks -
    // wider content scrolls sideways instead.
    public static double[] StretchToViewport(IReadOnlyList<double> columnWidths, double viewportWidth)
    {
        ArgumentNullException.ThrowIfNull(columnWidths);

        var stretched = new double[columnWidths.Count];
        for (int i = 0; i < columnWidths.Count; i++) stretched[i] = columnWidths[i];
        if (stretched.Length == 0) return stretched;

        // An unconstrained measure pass has nothing to fill.
        if (double.IsNaN(viewportWidth) || double.IsInfinity(viewportWidth) || viewportWidth <= 0)
            return stretched;

        var slack = viewportWidth - TotalWidth(stretched);
        if (slack > 0) stretched[^1] += slack;
        return stretched;
    }

    public static double MaxOffset(IReadOnlyList<double> columnWidths, double viewportWidth) =>
        Math.Max(0, TotalWidth(columnWidths) - Math.Max(0, viewportWidth));

    public static double ClampOffset(double offset, IReadOnlyList<double> columnWidths, double viewportWidth) =>
        Math.Clamp(double.IsNaN(offset) ? 0 : offset, 0, MaxOffset(columnWidths, viewportWidth));

    // A span that begins inside the frozen region is treated as frozen.
    public static CellPlacement Place(
        IReadOnlyList<double> columnWidths,
        int frozenColumnCount,
        double horizontalOffset,
        int column,
        int columnSpan = 1)
    {
        ArgumentNullException.ThrowIfNull(columnWidths);
        if (column < 0 || column >= columnWidths.Count)
            throw new ArgumentOutOfRangeException(nameof(column));
        ArgumentOutOfRangeException.ThrowIfLessThan(columnSpan, 1);

        double left = 0;
        for (int i = 0; i < column; i++) left += columnWidths[i];

        double width = 0;
        var last = Math.Min(column + columnSpan, columnWidths.Count);
        for (int i = column; i < last; i++) width += columnWidths[i];

        // Frozen cells never move, so they never need a clip.
        if (column < Math.Clamp(frozenColumnCount, 0, columnWidths.Count))
            return new CellPlacement(left, width, 0);

        var offset = Math.Max(0, double.IsNaN(horizontalOffset) ? 0 : horizontalOffset);
        var x = left - offset;
        var frozenEdge = FrozenWidth(columnWidths, frozenColumnCount);

        // Whatever of this cell would land left of the frozen boundary is cut away rather than
        // covered up, so it cannot paint over the frozen columns at any z-order.
        var clip = Math.Clamp(frozenEdge - x, 0, width);
        return new CellPlacement(x, width, clip);
    }
}
