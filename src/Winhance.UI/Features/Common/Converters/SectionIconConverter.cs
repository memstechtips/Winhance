using FluentIcons.Common;
using FluentIcons.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Turns a section's icon resource key into the <see cref="IconElement"/> its card and breadcrumb
/// show, honouring the suffix convention the resource dictionary already uses:
///
/// <list type="bullet">
/// <item>"…Path" — an SVG path string, rendered as a <see cref="PathIcon"/>.</item>
/// <item>"…Symbol" — a <see cref="Icon"/> enum member name, rendered as a <see cref="FluentIcon"/>.</item>
/// </list>
///
/// That rule was previously inlined in <c>UpdateContentVisibility</c> on both pages, next to a
/// per-page dictionary of keys. Having it in one converter is what lets the overview cards be
/// generated from a template instead of hand-written per section.
///
/// Returns null for an unknown or unresolvable key so the card renders without an icon rather than
/// throwing — a missing icon is a cosmetic fault, and a section page that will not load is not.
/// </summary>
public sealed partial class SectionIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string resourceKey || string.IsNullOrEmpty(resourceKey))
            return null;

        if (!Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            || resource is not string iconData)
            return null;

        if (resourceKey.EndsWith("Symbol", StringComparison.Ordinal))
        {
            return Enum.TryParse<Icon>(iconData, ignoreCase: true, out var symbol)
                ? new FluentIcon { Icon = symbol, IconVariant = IconVariant.Regular }
                : null;
        }

        return new PathIcon { Data = GeometryHelper.FromPathData(iconData) };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
