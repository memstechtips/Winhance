using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Validation;

/// <summary>
/// A single problem found while validating a setting catalog.
/// </summary>
public sealed record CatalogViolation(string SettingId, string GroupName, string Message);

/// <summary>
/// Catalog-invariant checks for authored <see cref="SettingGroup"/> definitions.
///
/// Today this enforces one rule family: every Selection setting's recommendation-
/// and-default shape matches one of the supported categories. The categories and
/// per-category invariants are:
///
///   • <b>Dynamic</b>   — options populated at runtime (PowerRecommendation.LoadDynamicOptions).
///                        Skipped — nothing to validate statically.
///   • <b>PowerCfg</b>  — PowerCfgSettings present. Recommendation/default live on
///                        PowerRecommendation + PowerCfgSetting.DefaultValueAC/DC. The
///                        ComboBox must NOT use per-option IsRecommended/IsDefault, because
///                        AC and DC can have different recommended options.
///   • <b>Subjective</b>— IsSubjectivePreference = true. Badges render as "Preference"
///                        regardless of IsRecommended/IsDefault, so authors may still
///                        mark a Winhance-preferred option as a Quick Actions hint —
///                        but never more than one of each.
///   • <b>Standard</b>  — everything else. Must have exactly one IsRecommended AND
///                        exactly one IsDefault option.
/// </summary>
public static class SettingCatalogValidator
{
    public static IReadOnlyList<CatalogViolation> Validate(SettingGroup group)
    {
        var violations = new List<CatalogViolation>();
        if (group?.Settings is null) return violations;

        foreach (var setting in group.Settings)
        {
            if (setting.InputType != InputType.Selection) continue;
            ValidateSelection(setting, group.Name, violations);
        }
        return violations;
    }

    public static IReadOnlyList<CatalogViolation> ValidateAll(IEnumerable<SettingGroup> groups)
    {
        var all = new List<CatalogViolation>();
        foreach (var group in groups)
            all.AddRange(Validate(group));
        return all;
    }

    private static void ValidateSelection(SettingDefinition setting, string groupName, List<CatalogViolation> violations)
    {
        var category = Categorize(setting);
        var options = setting.ComboBox?.Options ?? (IReadOnlyList<ComboBoxOption>)Array.Empty<ComboBoxOption>();

        int recommendedCount = options.Count(o => o.IsRecommended);
        int defaultCount = options.Count(o => o.IsDefault);

        switch (category)
        {
            case Category.Dynamic:
                return;

            case Category.PowerCfg:
                if (recommendedCount > 0)
                    violations.Add(new(setting.Id, groupName,
                        $"PowerCfg-backed Selection must not set ComboBoxOption.IsRecommended (found {recommendedCount}). " +
                        "Recommendation lives on PowerRecommendation.RecommendedOptionAC/DC."));
                if (defaultCount > 0)
                    violations.Add(new(setting.Id, groupName,
                        $"PowerCfg-backed Selection must not set ComboBoxOption.IsDefault (found {defaultCount}). " +
                        "Default lives on PowerCfgSetting.DefaultValueAC/DC."));
                break;

            case Category.Subjective:
                if (recommendedCount > 1)
                    violations.Add(new(setting.Id, groupName,
                        $"Subjective Selection has {recommendedCount} IsRecommended options; expected 0 or 1."));
                if (defaultCount > 1)
                    violations.Add(new(setting.Id, groupName,
                        $"Subjective Selection has {defaultCount} IsDefault options; expected 0 or 1."));
                break;

            case Category.Standard:
                if (recommendedCount != 1)
                    violations.Add(new(setting.Id, groupName,
                        $"Selection must have exactly one IsRecommended option (found {recommendedCount})."));
                if (defaultCount != 1)
                    violations.Add(new(setting.Id, groupName,
                        $"Selection must have exactly one IsDefault option (found {defaultCount})."));
                break;
        }
    }

    private enum Category { Standard, Subjective, PowerCfg, Dynamic }

    private static Category Categorize(SettingDefinition s)
    {
        if (s.Recommendation?.LoadDynamicOptions == true) return Category.Dynamic;
        if (s.PowerCfgSettings?.Count > 0) return Category.PowerCfg;
        if (s.IsSubjectivePreference) return Category.Subjective;
        return Category.Standard;
    }
}
