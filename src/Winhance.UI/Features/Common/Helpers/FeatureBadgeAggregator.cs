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
                foreach (var pill in s.BadgeRow)
                {
                    if (!pill.IsHighlighted) continue;
                    switch (pill.Kind)
                    {
                        case SettingBadgeKind.Recommended:
                            recommended++;
                            break;
                        case SettingBadgeKind.Default:
                            defaultCount++;
                            break;
                        case SettingBadgeKind.Custom:
                            custom++;
                            break;
                    }
                }
            }
            if (s.IsNew) newCount++;
        }

        return new FeatureBadgeSummary(totalWithBadgeData, recommended, defaultCount, custom, newCount);
    }
}
