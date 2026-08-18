using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

// Display localization (Name/Description/GroupName, options, tooltips, warnings) happens in
// SettingViewModelFactory via the canonical Setting_{id}_* keys. This service only builds the
// cross-group info banner.
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

    public string? BuildCrossGroupInfoMessage(Setting setting)
    {
        var crossGroupSettings = setting.Display.CrossGroupChildSettings;
        if (crossGroupSettings == null || crossGroupSettings.Count == 0)
        {
            return null;
        }

        var groupedSettings = new Dictionary<string, List<string>>();

        foreach (var (childSettingId, localizationKey) in crossGroupSettings)
        {
            try
            {
                // A null means the id is not in the mode-scoped membership (the skip condition). None of the
                // authored cross-group child ids is alias-affected, so Normalize is identity here.
                var childSetting = _catalogSettingsRegistry.GetById(
                    childSettingId, includeOtherOsVersions: !_windowsVersionFilter.IsFilterEnabled);

                if (childSetting == null) continue;

                var featureName = GetFeatureName(childSettingId);
                var groupNameKey = $"SettingGroup_{childSetting.Display.GroupName?.Replace(" ", "_")}";
                var localizedGroupName = _localization.GetString(groupNameKey);
                var groupKey = $"{featureName} ({localizedGroupName})";

                if (!groupedSettings.TryGetValue(groupKey, out var groupItems))
                {
                    groupItems = new List<string>();
                    groupedSettings[groupKey] = groupItems;
                }

                var localizedChildName = _localization.GetString(localizationKey);
                if (!string.IsNullOrEmpty(localizedChildName))
                {
                    groupItems.Add(localizedChildName);
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

    // The English strings here are real fallbacks, not decoration: GetString returns the "[key]"
    // marker for a missing key and never null, so a `??` on it could never fire.
    private string GetFeatureName(string settingId)
    {
        if (settingId.StartsWith("privacy-"))
            return Localized("Feature_Privacy_Name", "Privacy & Security");
        if (settingId.StartsWith("notifications-"))
            return Localized("Feature_Notifications_Name", "Notifications");
        if (settingId.StartsWith("start-"))
            return Localized("Feature_StartMenu_Name", "Start Menu");
        if (settingId.StartsWith("customize-"))
            return Localized("Feature_Explorer_Name", "Explorer");
        if (settingId.StartsWith("gaming-"))
            return Localized("Feature_GamingPerformance_Name", "Gaming & Performance");
        if (settingId.StartsWith("power-"))
            return Localized("Feature_Power_Name", "Power");

        return Localized("Nav_Settings", "Settings");
    }

    private string Localized(string key, string fallback) =>
        _localization.TryGetString(key, out var value) && !string.IsNullOrEmpty(value) ? value : fallback;
}
