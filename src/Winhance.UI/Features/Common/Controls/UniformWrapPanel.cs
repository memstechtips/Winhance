using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Winhance.UI.Features.Common.Controls;

// Every cell ItemWidth x ItemHeight, fixed gaps, left-aligned; the page centres the containing
// StackPanel with a MaxWidth from the card column count so headers, select-all checkboxes and the grid share the
// same extents (the Store / Settings card-grid pattern). ItemsRepeater + UniformGridLayout produced a measure
// cycle that snapped the outer ScrollViewer back to the top.
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

    protected override Size MeasureOverride(Size availableSize)
    {
        int count = Children.Count;
        if (count == 0)
            return new Size(0, 0);

        double availableWidth = availableSize.Width;
        if (double.IsInfinity(availableWidth) || double.IsNaN(availableWidth) || availableWidth <= 0)
            availableWidth = ItemWidth > 0 ? ItemWidth * count : 0;

        var (columns, cellWidth) = Fit(availableWidth, ItemWidth, MinItemWidth, ColumnSpacing, count);

        // Always measure children with unbounded vertical room so we learn each
        // child's true desired height; the cell height then becomes
        // max(ItemHeight, biggest child desired). ItemHeight acts as a FLOOR,
        // not a hard cap, which is what lets the card grid tolerate Windows'
        // "Make text bigger" slider — at 100% scale every card fits inside the
        // ItemHeight floor and behaviour is unchanged, at higher scale the
        // descriptions push past the floor and rows grow to fit instead of
        // clipping (issue #668).
        var childAvailable = new Size(cellWidth, double.PositiveInfinity);
        foreach (var child in Children)
            child.Measure(childAvailable);

        double cellHeight = ComputeCellHeight();

        int rows = (int)Math.Ceiling((double)count / columns);
        double totalHeight = rows * cellHeight + Math.Max(0, rows - 1) * RowSpacing;
        return new Size(availableWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = Children.Count;
        if (count == 0)
            return finalSize;

        var (columns, cellWidth) = Fit(finalSize.Width, ItemWidth, MinItemWidth, ColumnSpacing, count);
        double cellHeight = ComputeCellHeight();

        for (int i = 0; i < count; i++)
        {
            int col = i % columns;
            int row = i / columns;
            double x = col * (cellWidth + ColumnSpacing);
            double y = row * (cellHeight + RowSpacing);
            Children[i].Arrange(new Rect(x, y, cellWidth, cellHeight));
        }

        int rows = (int)Math.Ceiling((double)count / columns);
        double totalHeight = rows * cellHeight + Math.Max(0, rows - 1) * RowSpacing;
        return new Size(finalSize.Width, totalHeight);
    }

    private double ComputeCellHeight()
    {
        double floor = ItemHeight > 0 ? ItemHeight : 0;
        double tallest = MaxChildDesiredHeight();
        return Math.Max(floor, tallest);
    }

    private double MaxChildDesiredHeight()
    {
        double max = 0;
        foreach (var child in Children)
            if (child.DesiredSize.Height > max)
                max = child.DesiredSize.Height;
        return max;
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
