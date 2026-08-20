using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.Common.Helpers;

// WinUI 3's AutoSuggestBox/TextBox template renders the placeholder in a ContentPresenter named
// "PlaceholderTextContentPresenter" with no TextTrimming knob. When it doesn't fit (system text scale above
// 100% outgrows the fixed 220dp search box; the German "Tippen Sie hier, um zu suchen..." is wider than
// English) we walk to the placeholder TextBlock on Loaded and set TextTrimming + NoWrap so it single-lines
// with "...". Use: <AutoSuggestBox helpers:AutoSuggestBoxExtensions.PlaceholderEllipsis="True" />
public static class AutoSuggestBoxExtensions
{
    public static readonly DependencyProperty PlaceholderEllipsisProperty =
        DependencyProperty.RegisterAttached(
            "PlaceholderEllipsis",
            typeof(bool),
            typeof(AutoSuggestBoxExtensions),
            new PropertyMetadata(false, OnPlaceholderEllipsisChanged));

    public static bool GetPlaceholderEllipsis(DependencyObject obj) =>
        (bool)obj.GetValue(PlaceholderEllipsisProperty);

    public static void SetPlaceholderEllipsis(DependencyObject obj, bool value) =>
        obj.SetValue(PlaceholderEllipsisProperty, value);

    private static void OnPlaceholderEllipsisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not AutoSuggestBox box) return;
        if (e.NewValue is true)
        {
            box.Loaded += OnLoaded;
        }
        else
        {
            box.Loaded -= OnLoaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is AutoSuggestBox box) ApplyTrimming(box);
    }

    private static void ApplyTrimming(DependencyObject root)
    {
        var presenter = FindByName(root, "PlaceholderTextContentPresenter");
        if (presenter is null) return;

        // ContentPresenter renders string content via a TextBlock child once it
        // measures. If that child already exists, set it now; otherwise hook
        // the presenter's Loaded to retry after layout fills it in.
        var tb = FindDescendant<TextBlock>(presenter);
        if (tb is not null)
        {
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            tb.TextWrapping = TextWrapping.NoWrap;
            return;
        }
        if (presenter is FrameworkElement fe)
        {
            fe.Loaded += (_, _) =>
            {
                var late = FindDescendant<TextBlock>(presenter);
                if (late is not null)
                {
                    late.TextTrimming = TextTrimming.CharacterEllipsis;
                    late.TextWrapping = TextWrapping.NoWrap;
                }
            };
        }
    }

    private static FrameworkElement? FindByName(DependencyObject root, string name)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && fe.Name == name) return fe;
            var hit = FindByName(child, name);
            if (hit is not null) return hit;
        }
        return null;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            var hit = FindDescendant<T>(child);
            if (hit is not null) return hit;
        }
        return null;
    }
}
