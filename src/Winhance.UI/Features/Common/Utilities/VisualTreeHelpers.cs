using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.Common.Utilities;

/// <summary>
/// Helper methods for traversing the WinUI 3 visual tree.
/// </summary>
public static class VisualTreeHelpers
{
    /// <summary>
    /// Finds the first visual child of the specified type.
    /// </summary>
    public static T? FindVisualChild<T>(DependencyObject? obj) where T : DependencyObject
    {
        if (obj == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(obj, i);

            if (child is T typedChild)
                return typedChild;

            T? childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }

        return null;
    }

    /// <summary>
    /// Finds a child element by name and type.
    /// </summary>
    public static T? FindChildByName<T>(DependencyObject? parent, string name) where T : FrameworkElement
    {
        if (parent == null) return null;

        if (parent is T element && element.Name == name)
            return element;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            var result = FindChildByName<T>(child, name);
            if (result != null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// Finds all visual children of the specified type.
    /// </summary>
    public static IEnumerable<T> FindVisualChildren<T>(DependencyObject? obj) where T : DependencyObject
    {
        if (obj == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(obj, i);

            if (child is T typedChild)
                yield return typedChild;

            foreach (var childOfChild in FindVisualChildren<T>(child))
                yield return childOfChild;
        }
    }

    /// <summary>
    /// Finds the first visual parent of the specified type.
    /// </summary>
    public static T? FindVisualParent<T>(DependencyObject? obj) where T : DependencyObject
    {
        if (obj == null) return null;

        DependencyObject? parent = VisualTreeHelper.GetParent(obj);
        while (parent != null)
        {
            if (parent is T typedParent)
                return typedParent;
            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }
}
