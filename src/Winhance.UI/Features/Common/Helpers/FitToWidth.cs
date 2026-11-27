using Microsoft.UI.Xaml;

namespace Winhance.UI.Features.Common.Helpers;

// Measured rather than a width breakpoint: the labels these rows carry run to very different lengths across the 29
// languages, so only measuring the row says whether it fits.
internal static class FitToWidth
{
    // Undoes every step, then applies them in order until the content fits.
    public static void Apply(double available, Func<double> contentWidth, Action reset, params Action[] steps)
    {
        reset();
        foreach (var step in steps)
        {
            if (contentWidth() <= available)
                return;

            step();
        }
    }

    // A label that may wrap only between words fits its width when its widest word does.
    public static double WidestWord(string text, Func<string, double> width) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(width).DefaultIfEmpty(0).Max();

    // A label inside a control that is not built right now is null and skipped.
    public static void SetVisible(bool visible, params UIElement?[] elements)
    {
        foreach (var element in elements)
            element?.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
