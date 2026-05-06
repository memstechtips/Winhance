using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Non-virtualising panel that lays children out in a wrapping grid: items
/// stretch horizontally to fill the available width (column count derives from
/// MinItemWidth), and every cell is the same fixed ItemHeight. Used as the
/// ItemsPanel for the Software &amp; Apps card view — replaces the Itempart
/// pairing of ItemsRepeater + UniformGridLayout (which produced a measure
/// cycle that snapped the outer ScrollViewer back to the top) without giving
/// up the responsive width that VariableSizedWrapGrid lacks.
/// </summary>
public sealed partial class UniformWrapPanel : Panel
{
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
            availableWidth = MinItemWidth > 0 ? MinItemWidth * count : 0;

        int columns = ComputeColumnCount(availableWidth);
        double cellWidth = ComputeCellWidth(availableWidth, columns);
        double cellHeight = ItemHeight > 0 ? ItemHeight : 0;

        var childAvailable = new Size(
            cellWidth,
            cellHeight > 0 ? cellHeight : double.PositiveInfinity);

        foreach (var child in Children)
            child.Measure(childAvailable);

        int rows = (int)Math.Ceiling((double)count / columns);
        double rowHeight = cellHeight > 0 ? cellHeight : MaxChildDesiredHeight();
        double totalHeight = rows * rowHeight + Math.Max(0, rows - 1) * RowSpacing;
        return new Size(availableWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = Children.Count;
        if (count == 0)
            return finalSize;

        int columns = ComputeColumnCount(finalSize.Width);
        double cellWidth = ComputeCellWidth(finalSize.Width, columns);
        double cellHeight = ItemHeight > 0 ? ItemHeight : MaxChildDesiredHeight();

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

    private double MaxChildDesiredHeight()
    {
        double max = 0;
        foreach (var child in Children)
            if (child.DesiredSize.Height > max)
                max = child.DesiredSize.Height;
        return max;
    }

    private int ComputeColumnCount(double availableWidth)
    {
        if (MinItemWidth <= 0)
            return Math.Max(1, Children.Count);

        double effective = availableWidth + ColumnSpacing;
        double per = MinItemWidth + ColumnSpacing;
        int columns = (int)Math.Floor(effective / per);
        return Math.Max(1, columns);
    }

    private double ComputeCellWidth(double availableWidth, int columns)
    {
        if (columns <= 0)
            return availableWidth;
        return (availableWidth - Math.Max(0, columns - 1) * ColumnSpacing) / columns;
    }
}
