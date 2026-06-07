using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.Common.Validation;

public sealed record CatalogViolation(string SettingId, string GroupName, string Message);

public static class SettingCatalogValidator
{
    private static readonly Regex RegistryPathRegex = new(@"^(HKEY_LOCAL_MACHINE|HKLM|HKEY_CURRENT_USER|HKCU|HKEY_CLASSES_ROOT|HKCR|HKEY_USERS|HKU|HKEY_CURRENT_CONFIG|HKCC)\\", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<CatalogViolation> Validate(SettingGroup group)
    {
        var violations = new List<CatalogViolation>();
        if (group?.Settings is null) return violations;

        foreach (var setting in group.Settings)
        {
            ValidateCommon(setting, group.Name, violations);
            if (setting.InputType == InputType.Selection)
            {
                ValidateSelection(setting, group.Name, violations);
            }
        }
        return violations;
    }

    private static void ValidateCommon(SettingDefinition setting, string groupName, List<CatalogViolation> violations)
    {
        // Registry Path Validation
        if (setting.RegistrySettings != null)
        {
            foreach (var reg in setting.RegistrySettings)
            {
                if (!RegistryPathRegex.IsMatch(reg.KeyPath))
                {
                    violations.Add(new(setting.Id, groupName, $"Invalid registry hive in path: {reg.KeyPath}"));
                }
            }
        }

        // Feature Mapping Validation
        var featureId = FeatureDefinitions.All.FirstOrDefault(f => f.Id == setting.Id); // This logic might need refinement based on how features are actually mapped
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
