using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Non-virtualising panel that lays children out in a wrapping grid: every
/// cell is exactly ItemWidth × ItemHeight, gaps are fixed at ColumnSpacing /
/// RowSpacing, and the populated grid is centred horizontally so leftover
/// row width sits as symmetric outer margin instead of as a dead-zone on
/// the right. New columns appear automatically at width breakpoints —
/// matches the Microsoft Store / Windows Settings / App Store card-grid
/// pattern. Used as the ItemsPanel for the Software &amp; Apps card view —
/// replaces ItemsRepeater + UniformGridLayout (which produced a measure
/// cycle that snapped the outer ScrollViewer back to the top).
/// </summary>
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

        int columns = ComputeColumnCount(availableWidth);
        double cellWidth = ItemWidth > 0 ? ItemWidth : availableWidth / Math.Max(1, columns);
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
        double cellWidth = ItemWidth > 0 ? ItemWidth : finalSize.Width / Math.Max(1, columns);
        double cellHeight = ItemHeight > 0 ? ItemHeight : MaxChildDesiredHeight();

        // Centre the populated grid: any leftover row width becomes symmetric
        // outer margin rather than a dead-zone on the right.
        double usedWidth = columns * cellWidth + Math.Max(0, columns - 1) * ColumnSpacing;
        double xOffset = Math.Max(0, (finalSize.Width - usedWidth) / 2);

        for (int i = 0; i < count; i++)
        {
            int col = i % columns;
            int row = i / columns;
            double x = xOffset + col * (cellWidth + ColumnSpacing);
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
        if (ItemWidth <= 0)
            return Math.Max(1, Children.Count);

        double effective = availableWidth + ColumnSpacing;
        double per = ItemWidth + ColumnSpacing;
        int columns = (int)Math.Floor(effective / per);
        return Math.Max(1, columns);
    }
}
