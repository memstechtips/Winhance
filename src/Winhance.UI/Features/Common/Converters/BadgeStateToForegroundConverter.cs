using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts a <see cref="SettingBadgeKind"/> value to the matching pill foreground brush.
/// Brushes are looked up from Application.Current.Resources by key
/// ("BadgeRecommendedForeground", "BadgeDefaultForeground", "BadgeCustomForeground").
/// </summary>
public sealed partial class BadgeStateToForegroundConverter : IValueConverter
{
    public static string? GetResourceKey(SettingBadgeKind state) => state switch
    {
        SettingBadgeKind.Recommended => "BadgeRecommendedForeground",
        SettingBadgeKind.Default => "BadgeDefaultForeground",
        SettingBadgeKind.Custom => "BadgeCustomForeground",
        SettingBadgeKind.Preference => "BadgePreferenceForeground",
        _ => null,
    };

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not SettingBadgeKind state)
        {
            return null;
        }

        var key = GetResourceKey(state);
        if (key is null)
        {
            return null;
        }

        return Application.Current.Resources.TryGetValue(key, out var brush) ? brush as Brush : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
