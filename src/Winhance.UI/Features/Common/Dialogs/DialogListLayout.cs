using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winhance.UI.Features.Common.Dialogs;

internal static class DialogListLayout
{
    internal const double ConfirmationListLimit = 250;
    internal const double SummaryListLimit = 320;
    private const double DialogChrome = 180;
    private const double MinimumBodyHeight = 96;
    private const double MinimumListHeight = 64;

    internal static double BodyHeight(double windowHeight) =>
        Math.Max(MinimumBodyHeight, windowHeight - DialogChrome);

    internal static double ListHeight(double bodyHeight, double otherContentHeight, double listLimit) =>
        Math.Clamp(bodyHeight - otherContentHeight, MinimumListHeight, listLimit);

    internal static StackPanel Wrap(XamlRoot root, StackPanel content, ScrollViewer? list, double listLimit)
    {
        if (list is null)
            return content;

        list.MaxHeight = listLimit;
        list.VerticalScrollMode = ScrollMode.Enabled;
        list.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
        list.IsVerticalScrollChainingEnabled = false;
        list.IsTabStop = true;

        void Fit()
        {
            if (content.ActualHeight > 0)
            {
                // ContentDialog already scrolls its body. Only the list needs its own viewport, sized
                // from the wrapped labels, warnings, border and spacing measured around it.
                var otherHeight = Math.Max(0, content.ActualHeight - list.ActualHeight);
                list.MaxHeight = ListHeight(BodyHeight(root.Size.Height), otherHeight, listLimit);
            }
        }

        void RootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => Fit();

        void ContentSizeChanged(object sender, SizeChangedEventArgs args) => Fit();

        void BodyLoaded(object sender, RoutedEventArgs args)
        {
            content.Loaded -= BodyLoaded;
            root.Changed += RootChanged;
            content.SizeChanged += ContentSizeChanged;
            Fit();
        }

        void BodyUnloaded(object sender, RoutedEventArgs args)
        {
            content.Unloaded -= BodyUnloaded;
            root.Changed -= RootChanged;
            content.SizeChanged -= ContentSizeChanged;
        }

        content.Loaded += BodyLoaded;
        content.Unloaded += BodyUnloaded;
        return content;
    }
}
