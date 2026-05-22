using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Helpers;

/// <summary>
/// Aggregates badge states across all settings in a feature for overview card display.
/// </summary>
public static class FeatureBadgeAggregator
{
    public static FeatureBadgeSummary Aggregate(ISettingsFeatureViewModel feature)
    {
        var settings = feature.Settings;
        if (settings == null || settings.Count == 0)
            return new FeatureBadgeSummary(0, 0, 0, 0, 0);

        int totalWithBadgeData = 0;
        int recommended = 0;
        int defaultCount = 0;
        int custom = 0;
        int newCount = 0;

        foreach (var s in settings)
        {
            if (s.HasBadgeData)
            {
                totalWithBadgeData++;
                // Count each kind at most once per setting. For PowerCfg AC/DC Separate
                // settings with a battery present, the BadgeRow can contain two pills of
                // the same Kind (one for AC, one for DC); we treat a setting as "at
                // Recommended" if EITHER mode is recommended, otherwise the denominator
                // stops matching the user's mental model of N settings per card.
                bool anyRecommended = false, anyDefault = false, anyCustom = false;
                foreach (var pill in s.BadgeRow)
                {
                    if (!pill.IsHighlighted) continue;
                    switch (pill.Kind)
                    {
                        case SettingBadgeKind.Recommended: anyRecommended = true; break;
                        case SettingBadgeKind.Default: anyDefault = true; break;
                        case SettingBadgeKind.Custom: anyCustom = true; break;
                    }
                }
                if (anyRecommended) recommended++;
                if (anyDefault) defaultCount++;
                if (anyCustom) custom++;
            }
            if (s.IsNew) newCount++;
        }

        return new FeatureBadgeSummary(totalWithBadgeData, recommended, defaultCount, custom, newCount);
    }
}
