using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Winhance.UI.Features.Common.Controls;

// UniformWrapPanel's cells for an ItemsRepeater. A repeater given new items hands its elements back to the template's
// recycle pool and builds the new ones from it, where an ItemsControl drops them all and builds afresh; the compact
// app lists re-split every line on each zoom step, and that rebuild held tens of MB.
public sealed partial class UniformWrapLayout : NonVirtualizingLayout
{
    public double ItemWidth { get; set; }

    public double ColumnSpacing { get; set; }

    public double RowSpacing { get; set; }

    private UniformCells Cells => new(ItemWidth, 0, 0, ColumnSpacing, RowSpacing);

    protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize) =>
        UniformWrapPanel.MeasureCells(context.Children, context.Children.Count, availableSize, Cells);

    protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize) =>
        UniformWrapPanel.ArrangeCells(context.Children, context.Children.Count, finalSize, Cells);
}
