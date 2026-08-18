using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Helpers;

public static class FeatureBadgeAggregator
{
    public static FeatureBadgeSummary Aggregate(ISettingsFeatureViewModel feature)
    {
        var settings = feature.Settings;
        if (settings == null || settings.Count == 0)
            return new FeatureBadgeSummary(0, 0, 0, 0, 0, 0, 0);

        int totalWithBadgeData = 0;
        int recommended = 0;
        int defaultCount = 0;
        int newCount = 0;
        int unrecognized = 0, malformed = 0, undetermined = 0;

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
                bool anyRecommended = false, anyDefault = false;
                foreach (var pill in s.BadgeRow)
                {
                    if (!pill.IsHighlighted) continue;
                    switch (pill.Kind)
                    {
                        case SettingBadgeKind.Recommended: anyRecommended = true; break;
                        case SettingBadgeKind.Default: anyDefault = true; break;
                    }
                }
                if (anyRecommended) recommended++;
                if (anyDefault) defaultCount++;
            }

            // Detection outcomes are read from the OUTCOME itself, not inferred from the badge row, which
            // could only ever report one kind of problem and would depend on the badge row being built first.
            // Counted for EVERY setting, not only those with badge data: a setting we could not read still
            // needs surfacing on the overview.
            switch (s.Outcome)
            {
                case SettingDetectionOutcome.Custom: unrecognized++; break;
                case SettingDetectionOutcome.Malformed: malformed++; break;
                case SettingDetectionOutcome.Undetermined: undetermined++; break;
            }

            if (s.IsNew) newCount++;
        }

        return new FeatureBadgeSummary(
            totalWithBadgeData, recommended, defaultCount, newCount,
            unrecognized, malformed, undetermined);
    }
}
