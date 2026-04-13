using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts a <see cref="SettingBadgeKind"/> value to the matching pill Style resource.
/// Styles are looked up from Application.Current.Resources by key
/// ("BadgeRecommendedStyle", "BadgeDefaultStyle", "BadgeCustomStyle").
/// </summary>
public partial class BadgeStateToStyleConverter : IValueConverter
{
    public static string? GetResourceKey(SettingBadgeKind state) => state switch
    {
        SettingBadgeKind.Recommended => "BadgeRecommendedStyle",
        SettingBadgeKind.Default => "BadgeDefaultStyle",
        SettingBadgeKind.Custom => "BadgeCustomStyle",
        SettingBadgeKind.Preference => "BadgePreferenceStyle",
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

        return Application.Current.Resources.TryGetValue(key, out var style) ? style as Style : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
