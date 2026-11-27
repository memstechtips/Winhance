using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.Common.Controls;

// Squeezed into the leftover column a description wraps a word per line, so on a narrow window it moves under the
// bar's controls, and when even the controls overflow one line, the middle group moves down with it.
internal static class ModeBarLayout
{
    private const double DescriptionMinWidth = 320;

    public static void Fit(double available, Grid bar, FrameworkElement description, FrameworkElement middle, int middleColumn, Thickness middleMargin)
    {
        if (available <= 0)
            return;

        int columns = bar.ColumnDefinitions.Count;
        var below = new Thickness(0, 8, 0, 0);
        FitToWidth.Apply(
            available,
            () => FirstLineWidth(bar, description),
            reset: () =>
            {
                Place(description, 0, 2, 1, new Thickness(0, 0, 16, 0));
                Place(middle, 0, middleColumn, 1, middleMargin);
            },
            () => Place(description, 1, 0, columns, below),
            () =>
            {
                Place(middle, 1, 0, columns, below);
                Place(description, 2, 0, columns, below);
            });
    }

    private static void Place(FrameworkElement element, int row, int column, int columnSpan, Thickness margin)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, columnSpan);
        element.Margin = margin;
    }

    private static double FirstLineWidth(Grid bar, FrameworkElement description)
    {
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        double width = bar.Padding.Left + bar.Padding.Right;
        foreach (var child in bar.Children.OfType<FrameworkElement>().Where(c => Grid.GetRow(c) == 0))
        {
            if (child == description)
            {
                width += DescriptionMinWidth;
                continue;
            }

            child.Measure(unbounded);
            width += child.DesiredSize.Width;
        }

        return width;
    }
}
