using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

// Slice B2: the vestigial LocalizeSetting (+ its GetLocalized* / Localize* / GetStringOrFallback helpers) was
// retired. Display localization (Name/Description/GroupName, options, tooltips, warnings) now happens on the
// catalog path in SettingViewModelFactory via the canonical Setting_{id}_* keys. This service now only builds
// the cross-group info banner.
public class SettingLocalizationService : ISettingLocalizationService
{
    private readonly ILocalizationService _localization;
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly IWindowsVersionFilterService _windowsVersionFilter;

    public SettingLocalizationService(
        ILocalizationService localization,
        ICatalogSettingsRegistry catalogSettingsRegistry,
        IWindowsVersionFilterService windowsVersionFilter)
    {
        _localization = localization;
        _catalogSettingsRegistry = catalogSettingsRegistry;
        _windowsVersionFilter = windowsVersionFilter;
    }

    public string? BuildCrossGroupInfoMessage(SettingDefinition setting)
    {
        var crossGroupSettings = setting.CrossGroupChildSettings;
        if (crossGroupSettings == null || crossGroupSettings.Count == 0)
        {
            return null;
        }

        // Group child settings by feature and group
        var groupedSettings = new Dictionary<string, List<string>>();

        foreach (var (childSettingId, localizationKey) in crossGroupSettings)
        {
            try
            {
                // Slice 7a: one catalog GetById reproduces the old two-step resolve (feature-index hop +
                // filtered-list scan) - both old skip conditions were exactly "id not in the mode-scoped
                // membership", which GetById's null covers. The show-other-Windows-versions mode is threaded
                // explicitly (the old registry read it from its mutable SetFilterEnabled state); none of the
                // authored cross-group child ids is alias-affected, so Normalize is identity here.
                var childSetting = _catalogSettingsRegistry.GetById(
                    childSettingId, includeOtherOsVersions: !_windowsVersionFilter.IsFilterEnabled);

                if (childSetting == null) continue;

                var featureName = GetFeatureName(childSettingId);
                var groupNameKey = $"SettingGroup_{childSetting.Display.GroupName?.Replace(" ", "_")}";
                var localizedGroupName = _localization.GetString(groupNameKey);
                var groupKey = $"{featureName} ({localizedGroupName})";

                if (!groupedSettings.ContainsKey(groupKey))
                {
                    groupedSettings[groupKey] = new List<string>();
                }

                var localizedChildName = _localization.GetString(localizationKey);
                if (!string.IsNullOrEmpty(localizedChildName))
                {
                    groupedSettings[groupKey].Add(localizedChildName);
                }
            }
            catch
            {
                // Skip settings that can't be looked up
            }
        }

        if (groupedSettings.Count == 0) return null;

        var header = _localization.GetString("Setting_CrossGroupWarning_Header");
        var lines = groupedSettings.Select(kvp => $"• {kvp.Key}: {string.Join(", ", kvp.Value)}");
        return $"{header}\n{string.Join("\n", lines)}";
    }

    private string GetFeatureName(string settingId)
    {
        if (settingId.StartsWith("privacy-"))
            return _localization.GetString("Feature_Privacy_Name") ?? "Privacy & Security";
        if (settingId.StartsWith("notifications-"))
            return _localization.GetString("Feature_Notifications_Name") ?? "Notifications";
        if (settingId.StartsWith("start-"))
            return _localization.GetString("Feature_StartMenu_Name") ?? "Start Menu";
        if (settingId.StartsWith("customize-"))
            return _localization.GetString("Feature_Explorer_Name") ?? "Explorer";
        if (settingId.StartsWith("gaming-"))
            return _localization.GetString("Feature_GamingPerformance_Name") ?? "Gaming & Performance";
        if (settingId.StartsWith("power-"))
            return _localization.GetString("Feature_Power_Name") ?? "Power";

        return _localization.GetString("Nav_Settings") ?? "Settings";
    }
}
