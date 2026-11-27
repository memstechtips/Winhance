using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Winhance.UI.Features.Common.Controls;

// Every cell ItemWidth x ItemHeight, fixed gaps, left-aligned; the page centres the containing
// StackPanel with a MaxWidth from the card column count so headers, select-all checkboxes and the grid share the
// same extents (the Store / Settings card-grid pattern). ItemsRepeater + UniformGridLayout produced a measure
// cycle that snapped the outer ScrollViewer back to the top, and the toolkit WrapLayout in the app sections
// threw "Element is already the child of another element" in MeasureOverride and still realized every row.
public sealed partial class UniformWrapPanel : Panel
{
    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(
            nameof(ItemWidth),
            typeof(double),
            typeof(UniformWrapPanel),
            new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    // Below two ItemWidth columns the cells shrink as far as this to keep two, and a lone column fills the width, so
    // a narrow window shows no empty margins beside the cards. Zero keeps every cell at ItemWidth.
    public static readonly DependencyProperty MinItemWidthProperty =
        DependencyProperty.Register(
            nameof(MinItemWidth),
            typeof(double),
            typeof(UniformWrapPanel),
            new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(
            nameof(ItemHeight),
            typeof(double),
            typeof(UniformWrapPanel),
            new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public static readonly DependencyProperty ColumnSpacingProperty =
        DependencyProperty.Register(
            nameof(ColumnSpacing),
            typeof(double),
            typeof(UniformWrapPanel),
            new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public static readonly DependencyProperty RowSpacingProperty =
        DependencyProperty.Register(
            nameof(RowSpacing),
            typeof(double),
            typeof(UniformWrapPanel),
            new PropertyMetadata(0.0, OnLayoutPropertyChanged));

    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((UniformWrapPanel)d).InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        MeasureCells(Children, Children.Count, availableSize, Cells);

    protected override Size ArrangeOverride(Size finalSize) =>
        ArrangeCells(Children, Children.Count, finalSize, Cells);

    private UniformCells Cells => new(ItemWidth, MinItemWidth, ItemHeight, ColumnSpacing, RowSpacing);

    internal static Size MeasureCells(IEnumerable<UIElement> children, int count, Size availableSize, UniformCells cells)
    {
        if (count == 0)
            return new Size(0, 0);

        double availableWidth = availableSize.Width;
        if (double.IsInfinity(availableWidth) || double.IsNaN(availableWidth) || availableWidth <= 0)
            availableWidth = cells.ItemWidth > 0 ? cells.ItemWidth * count : 0;

        var (columns, cellWidth) = Fit(availableWidth, cells.ItemWidth, cells.MinItemWidth, cells.ColumnSpacing, count);

        // Always measure children with unbounded vertical room so we learn each
        // child's true desired height; the cell height then becomes
        // max(ItemHeight, biggest child desired). ItemHeight acts as a FLOOR,
        // not a hard cap, which is what lets the card grid tolerate Windows'
        // "Make text bigger" slider — at 100% scale every card fits inside the
        // ItemHeight floor and behaviour is unchanged, at higher scale the
        // descriptions push past the floor and rows grow to fit instead of
        // clipping (issue #668).
        var childAvailable = new Size(cellWidth, double.PositiveInfinity);
        foreach (var child in children)
            child.Measure(childAvailable);

        return new Size(availableWidth, TotalHeight(count, columns, CellHeight(children, cells.ItemHeight), cells.RowSpacing));
    }

    internal static Size ArrangeCells(IEnumerable<UIElement> children, int count, Size finalSize, UniformCells cells)
    {
        if (count == 0)
            return finalSize;

        var (columns, cellWidth) = Fit(finalSize.Width, cells.ItemWidth, cells.MinItemWidth, cells.ColumnSpacing, count);
        double cellHeight = CellHeight(children, cells.ItemHeight);

        int i = 0;
        foreach (var child in children)
        {
            double x = i % columns * (cellWidth + cells.ColumnSpacing);
            double y = i / columns * (cellHeight + cells.RowSpacing);
            child.Arrange(new Rect(x, y, cellWidth, cellHeight));
            i++;
        }

        return new Size(finalSize.Width, TotalHeight(count, columns, cellHeight, cells.RowSpacing));
    }

    private static double CellHeight(IEnumerable<UIElement> children, double itemHeight)
    {
        double height = itemHeight > 0 ? itemHeight : 0;
        foreach (var child in children)
            height = Math.Max(height, child.DesiredSize.Height);
        return height;
    }

    private static double TotalHeight(int count, int columns, double cellHeight, double rowSpacing)
    {
        int rows = (int)Math.Ceiling((double)count / columns);
        return rows * cellHeight + Math.Max(0, rows - 1) * rowSpacing;
    }

    internal static (int Columns, double CellWidth) Fit(double available, double itemWidth, double minItemWidth, double spacing, int count)
    {
        if (itemWidth <= 0)
        {
            int all = Math.Max(1, count);
            return (all, available / all);
        }

        int columns = Math.Max(1, (int)Math.Floor((available + spacing) / (itemWidth + spacing)));
        if (columns >= 2 || minItemWidth <= 0)
            return (columns, itemWidth);

        return available + spacing >= 2 * (minItemWidth + spacing)
            ? (2, (available - spacing) / 2)
            : (1, available);
    }
}

internal readonly record struct UniformCells(double ItemWidth, double MinItemWidth, double ItemHeight,
    double ColumnSpacing, double RowSpacing);
