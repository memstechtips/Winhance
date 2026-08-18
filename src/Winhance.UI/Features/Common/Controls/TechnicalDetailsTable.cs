using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Winhance.Core.Features.Common.TechnicalDetails;

namespace Winhance.UI.Features.Common.Controls;

// Set by OptionMatrixView when it creates the children; the panel only does arithmetic, never content.
internal sealed class TableCellInfo
{
    public required int Column { get; init; }
    public int ColumnSpan { get; init; } = 1;
    public required int Row { get; init; }
    public int RowSpan { get; init; } = 1;

    // Group headers size to their span rather than contributing to any one column's width.
    public bool IsSpanning => ColumnSpan > 1;

    // A cell covering two header rows must not inflate either one alone.
    public bool IsRowSpanning => RowSpan > 1;
}

// A Grid cannot do this - it has no way to arrange a subset of its children at a scroll offset, and WinUI has no
// SharedSizeGroup to make per-row Grids agree on column widths. The arithmetic lives in TableLayout so it can be
// unit tested; this class is measure/arrange plumbing only.
internal sealed partial class TechnicalDetailsTable : Panel
{
    private double[] _columnWidths = [];
    private double[] _rowHeights = [];

    public static readonly DependencyProperty ColumnCountProperty =
        DependencyProperty.Register(nameof(ColumnCount), typeof(int), typeof(TechnicalDetailsTable),
            new PropertyMetadata(0, OnLayoutPropertyChanged));

    public int ColumnCount
    {
        get => (int)GetValue(ColumnCountProperty);
        set => SetValue(ColumnCountProperty, value);
    }

    public static readonly DependencyProperty FrozenColumnCountProperty =
        DependencyProperty.Register(nameof(FrozenColumnCount), typeof(int), typeof(TechnicalDetailsTable),
            new PropertyMetadata(0, OnLayoutPropertyChanged));

    public int FrozenColumnCount
    {
        get => (int)GetValue(FrozenColumnCountProperty);
        set => SetValue(FrozenColumnCountProperty, value);
    }

    // Changing it re-arranges without re-measuring - one number every cell reads in the same layout pass, which
    // keeps header and body in lockstep without a second ScrollViewer.
    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(nameof(HorizontalOffset), typeof(double), typeof(TechnicalDetailsTable),
            new PropertyMetadata(0d, OnOffsetChanged));

    public double HorizontalOffset
    {
        get => (double)GetValue(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    public double TotalColumnWidth => TableLayout.TotalWidth(_columnWidths);

    // ActualWidth is a full layout cycle behind during measure, so anything deciding whether the content overflows
    // has to read this instead.
    public double ViewportWidth { get; private set; }

    public double FrozenWidth => TableLayout.FrozenWidth(_columnWidths, FrozenColumnCount);

    public event EventHandler? LayoutMeasured;

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((TechnicalDetailsTable)d).InvalidateMeasure();

    private static void OnOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((TechnicalDetailsTable)d).InvalidateArrange();

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Math.Max(0, ColumnCount);
        if (columns == 0 || Children.Count == 0)
        {
            _columnWidths = [];
            _rowHeights = [];
            return new Size(0, 0);
        }

        // Recomputed from scratch every pass. A production grid that virtualizes has to ratchet this
        // upward because off-screen rows never measure; every row here is realized, so starting from
        // zero is both correct and lets a column shrink when its content does.
        _columnWidths = new double[columns];
        var rowCount = 0;
        foreach (var child in Children)
        {
            if (child is not FrameworkElement { Tag: TableCellInfo info }) continue;
            rowCount = Math.Max(rowCount, info.Row + 1);
        }
        foreach (var child in Children)
        {
            if (child is not FrameworkElement { Tag: TableCellInfo info }) continue;
            rowCount = Math.Max(rowCount, info.Row + info.RowSpan);
        }
        _rowHeights = new double[Math.Max(0, rowCount)];

        foreach (var child in Children)
        {
            if (child is not FrameworkElement { Tag: TableCellInfo info } element) continue;
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (!info.IsSpanning && info.Column < columns)
                _columnWidths[info.Column] = Math.Max(_columnWidths[info.Column], element.DesiredSize.Width);
        }

        // A spanning header widens its LAST column if the group needs more room than its members give
        // it, so the group and its columns always agree on where the group ends.
        foreach (var child in Children)
        {
            if (child is not FrameworkElement { Tag: TableCellInfo info } element) continue;
            if (!info.IsSpanning || info.Column >= columns) continue;

            var last = Math.Min(info.Column + info.ColumnSpan, columns) - 1;
            double spanned = 0;
            for (int i = info.Column; i <= last; i++) spanned += _columnWidths[i];
            var shortfall = element.DesiredSize.Width - spanned;
            if (shortfall > 0) _columnWidths[last] += shortfall;
        }

        foreach (var child in Children)
        {
            if (child is not FrameworkElement { Tag: TableCellInfo info } element) continue;
            if (info.IsRowSpanning || info.Row >= _rowHeights.Length) continue;
            _rowHeights[info.Row] = Math.Max(_rowHeights[info.Row], element.DesiredSize.Height);
        }

        // A row-spanning cell grows its LAST row if the rows it covers don't add up to what it needs.
        // Same rule as the column pass above, so the two stay easy to reason about together.
        foreach (var child in Children)
        {
            if (child is not FrameworkElement { Tag: TableCellInfo info } element) continue;
            if (!info.IsRowSpanning || info.Row >= _rowHeights.Length) continue;

            var last = Math.Min(info.Row + info.RowSpan, _rowHeights.Length) - 1;
            double spanned = 0;
            for (int i = info.Row; i <= last; i++) spanned += _rowHeights[i];
            var shortfall = element.DesiredSize.Height - spanned;
            if (shortfall > 0) _rowHeights[last] += shortfall;
        }

        // The viewport is what the host offered. Columns size to their content, so without the
        // stretch below a short table stops mid-card and leaves dead space beside it.
        ViewportWidth = double.IsInfinity(availableSize.Width) ? TotalColumnWidth : availableSize.Width;
        _columnWidths = TableLayout.StretchToViewport(_columnWidths, ViewportWidth);

        double totalHeight = 0;
        foreach (var height in _rowHeights) totalHeight += height;

        // Raised after the stretch, so a host sizing a scrollbar sees the widths that will actually
        // be arranged. Reading ActualWidth here instead would see the PREVIOUS pass's value -- zero
        // on the first one, which is what made the scrollbar appear on tables that fit.
        LayoutMeasured?.Invoke(this, EventArgs.Empty);

        // Report only what the viewport can show horizontally: the table scrolls sideways rather than
        // forcing its host wider, which is what keeps it inside the settings card.
        return new Size(ViewportWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_columnWidths.Length == 0) return finalSize;

        var offset = TableLayout.ClampOffset(HorizontalOffset, _columnWidths, finalSize.Width);
        var rowTops = new double[_rowHeights.Length + 1];
        for (int i = 0; i < _rowHeights.Length; i++) rowTops[i + 1] = rowTops[i] + _rowHeights[i];

        foreach (var child in Children)
        {
            if (child is not FrameworkElement { Tag: TableCellInfo info } element)
            {
                child.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }
            if (info.Column >= _columnWidths.Length || info.Row >= _rowHeights.Length)
            {
                element.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            var placement = TableLayout.Place(_columnWidths, FrozenColumnCount, offset, info.Column, info.ColumnSpan);

            if (placement.IsHidden)
            {
                // Scrolled entirely behind the frozen columns. Arranging at zero size keeps it out of
                // the visual tree's paint pass without disturbing measure.
                element.Arrange(new Rect(0, 0, 0, 0));
                element.Clip = null;
                continue;
            }

            var top = rowTops[info.Row];
            var lastRow = Math.Min(info.Row + info.RowSpan, _rowHeights.Length);
            var height = rowTops[lastRow] - top;
            element.Arrange(new Rect(placement.X, top, placement.Width, height));

            // Clip rather than layer: the overlapping pixels are removed, so no z-order or backdrop
            // opacity can let a scrolled cell bleed over the pinned ones.
            element.Clip = placement.NeedsClip
                ? new RectangleGeometry
                {
                    Rect = new Rect(placement.ClipFromLeft, 0, placement.Width - placement.ClipFromLeft, height),
                }
                : null;
        }

        return finalSize;
    }
}
