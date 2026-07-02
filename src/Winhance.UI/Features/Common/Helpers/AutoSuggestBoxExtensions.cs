using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.Common.Helpers;

/// <summary>
/// Attached property that applies <see cref="TextTrimming.CharacterEllipsis"/>
/// to an <see cref="AutoSuggestBox"/>'s placeholder text.
///
/// WinUI 3's default AutoSuggestBox/TextBox template renders the placeholder
/// inside a ContentPresenter named "PlaceholderTextContentPresenter" with no
/// direct TextTrimming knob. To get the placeholder to ellipsize when it
/// doesn't fit (e.g. the user has bumped system text scale above 100% so the
/// rendered placeholder outgrows the fixed 220dp search-box width, or a long
/// translation like the German "Tippen Sie hier, um zu suchen..." is wider
/// than English), we walk down to the placeholder TextBlock on Loaded and
/// set TextTrimming + TextWrapping=NoWrap so it always single-lines with "...".
///
/// Use as:
///   <AutoSuggestBox helpers:AutoSuggestBoxExtensions.PlaceholderEllipsis="True" />
/// </summary>
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
