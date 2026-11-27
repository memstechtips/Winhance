using Microsoft.UI.Xaml;

namespace Winhance.UI.Features.Common.Helpers;

// x:Load takes a bool, so these turn a Visibility-typed or inverted source into one.
internal static class LoadWhen
{
    public static bool Shown(Visibility visibility) => visibility == Visibility.Visible;

    public static bool Not(bool value) => !value;
}
