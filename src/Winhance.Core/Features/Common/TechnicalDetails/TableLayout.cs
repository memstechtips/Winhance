using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.TechnicalDetails;

/// <summary>
/// Where one cell sits after horizontal scrolling, and how much of its left edge must be clipped
/// away. <see cref="ClipFromLeft"/> is in the cell's own coordinate space.
/// </summary>
public readonly record struct CellPlacement(double X, double Width, double ClipFromLeft)
{
    /// <summary>The cell has scrolled entirely behind the frozen columns and should not be drawn.</summary>
    public bool IsHidden => ClipFromLeft >= Width;

    public bool NeedsClip => ClipFromLeft > 0 && !IsHidden;

    public double VisibleWidth => Math.Max(0, Width - ClipFromLeft);
}

/// <summary>
/// The horizontal arithmetic for the technical-details table: column offsets, the frozen boundary,
/// the scroll range, and the clip that keeps scrolled cells out of the frozen region.
///
/// This is deliberately pure and UI-free so it can be unit tested. The previous hand-rolled version
/// of this panel kept frozen columns readable by painting an opaque backdrop over the scrolled cells
/// at a higher z-index — a painting-order answer to a geometry question, which failed as soon as the
/// backdrop brush turned out to be semi-transparent. Clipping removes the overlapping pixels
/// outright, so z-order never enters into it.
/// </summary>
public static class TableLayout
{
    /// <summary>Combined width of the leading columns that do not scroll.</summary>
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

    /// <summary>
    /// Widens the last column to take up whatever the viewport has left over.
    ///
    /// Every column sizes to its own content, so a table with two short value columns stops well
    /// short of the card holding it and leaves a block of dead space beside it. Only the last column
    /// grows, which is what keeps the columns before it aligned with the header cells above them.
    /// Nothing ever shrinks: when the content is already wider than the viewport the widths come
    /// back untouched and the table scrolls sideways instead.
    /// </summary>
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

    /// <summary>
    /// The largest useful scroll offset. Frozen columns occupy viewport space permanently, so the
    /// scrollable extent reduces to content width minus viewport width.
    /// </summary>
    public static double MaxOffset(IReadOnlyList<double> columnWidths, double viewportWidth) =>
        Math.Max(0, TotalWidth(columnWidths) - Math.Max(0, viewportWidth));

    public static double ClampOffset(double offset, IReadOnlyList<double> columnWidths, double viewportWidth) =>
        Math.Clamp(double.IsNaN(offset) ? 0 : offset, 0, MaxOffset(columnWidths, viewportWidth));

    /// <summary>
    /// Places one cell, or a header spanning <paramref name="columnSpan"/> columns starting at
    /// <paramref name="column"/>. A span that begins inside the frozen region is treated as frozen.
    /// </summary>
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
        if (columnSpan < 1) throw new ArgumentOutOfRangeException(nameof(columnSpan));

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
