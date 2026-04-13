using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts a <see cref="SettingBadgeState"/> value to the matching pill foreground brush.
/// Brushes are looked up from Application.Current.Resources by key
/// ("BadgeRecommendedForeground", "BadgeDefaultForeground", "BadgeCustomForeground").
/// </summary>
public partial class BadgeStateToForegroundConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not SettingBadgeState state)
        {
            return null;
        }

        var key = state switch
        {
            SettingBadgeState.Recommended => "BadgeRecommendedForeground",
            SettingBadgeState.Default => "BadgeDefaultForeground",
            SettingBadgeState.Custom => "BadgeCustomForeground",
            SettingBadgeState.Preference => "BadgePreferenceForeground",
            _ => null,
        };

        if (key is null)
        {
            return null;
        }

        return Application.Current.Resources.TryGetValue(key, out var brush) ? brush as Brush : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
