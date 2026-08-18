using FluentIcons.Common;
using FluentIcons.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.Common.Converters;

// Suffix convention of the resource dictionary: "...Path" = an SVG path string (PathIcon), "...Symbol" = an
// Icon enum member name (FluentIcon). One converter is what lets the overview cards be generated from a
// template. Null for an unknown key so the card renders without an icon rather than throwing - a missing icon
// is cosmetic, a page that will not load is not.
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
