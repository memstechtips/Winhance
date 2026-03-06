using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public class SettingLocalizationService : ISettingLocalizationService
{
    private readonly ILocalizationService _localization;
    private readonly IDomainServiceRouter _domainServiceRouter;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;

    public SettingLocalizationService(
        ILocalizationService localization,
        IDomainServiceRouter domainServiceRouter,
        ICompatibleSettingsRegistry compatibleSettingsRegistry)
    {
        _localization = localization;
        _domainServiceRouter = domainServiceRouter;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
    }

    public SettingDefinition LocalizeSetting(SettingDefinition setting)
    {
        var localized = setting with
        {
            Name = GetLocalizedName(setting),
            Description = GetLocalizedDescription(setting),
            GroupName = setting.GroupName != null ? GetLocalizedGroupName(setting.GroupName) : null
        };

        if (setting.ComboBox != null)
        {
            var comboBox = setting.ComboBox;

            var localizedComboBox = comboBox with
            {
                DisplayNames = LocalizeComboBoxNames(setting)
            };

            if (comboBox.CustomStateDisplayName != null)
            {
                localizedComboBox = localizedComboBox with
                {
                    CustomStateDisplayName = GetLocalizedCustomState(setting)
                };
            }

            if (comboBox.OptionWarnings != null)
            {
                localizedComboBox = localizedComboBox with
                {
                    OptionWarnings = LocalizeOptionWarnings(setting)
                };
            }

            if (comboBox.OptionTooltips != null)
            {
                localizedComboBox = localizedComboBox with
                {
                    OptionTooltips = LocalizeOptionTooltips(setting)
                };
            }

            if (comboBox.OptionConfirmations != null)
            {
                localizedComboBox = localizedComboBox with
                {
                    OptionConfirmations = LocalizeOptionConfirmations(setting)
                };
            }

            localized = localized with { ComboBox = localizedComboBox };
        }

        if (setting.NumericRange?.Units != null)
        {
            localized = localized with
            {
                NumericRange = setting.NumericRange with
                {
                    Units = LocalizeUnits(setting.NumericRange.Units)
                }
            };
        }

        // Handle compatibility messages (format: Key|Arg1|Arg2...)
        if (setting.VersionCompatibilityMessage is { } compatKey && compatKey.StartsWith("Compatibility_"))
        {
            var parts = compatKey.Split('|');
            var key = parts[0];

            if (parts.Length > 1)
            {
                var args = parts.Skip(1).ToArray();
                try
                {
                    var format = _localization.GetString(key);
                    localized = localized with { VersionCompatibilityMessage = string.Format(format, args) };
                }
                catch
                {
                    localized = localized with { VersionCompatibilityMessage = _localization.GetString(key) };
                }
            }
            else
            {
                localized = localized with { VersionCompatibilityMessage = _localization.GetString(key) };
            }
        }

        return localized;
    }

    private string GetLocalizedName(SettingDefinition setting)
    {
        var key = $"Setting_{setting.Id}_Name";
        return GetStringOrFallback(key, setting.Name);
    }

    private string GetLocalizedDescription(SettingDefinition setting)
    {
        var key = $"Setting_{setting.Id}_Description";
        return GetStringOrFallback(key, setting.Description);
    }

    private string GetLocalizedGroupName(string groupName)
    {
        // Try the compacted format first (e.g. "PrivacySecurity")
        var key = $"SettingGroup_{groupName.Replace(" ", "").Replace("&", "")}";
        var localized = _localization.GetString(key);

        if (!localized.StartsWith("[") || !localized.EndsWith("]"))
        {
            return localized;
        }

        // Try the snake case format (e.g. "Content_Delivery_Advertising")
        var snakeCaseName = groupName
            .Replace(" & ", "_")
            .Replace(" ", "_")
            .Replace("&", "_");

        // Remove double underscores
        while (snakeCaseName.Contains("__"))
        {
            snakeCaseName = snakeCaseName.Replace("__", "_");
        }

        var keySnake = $"SettingGroup_{snakeCaseName}";
        return GetStringOrFallback(keySnake, groupName);
    }

    private string GetLocalizedCustomState(SettingDefinition setting)
    {
        var key = $"Setting_{setting.Id}_CustomState";
        var original = setting.ComboBox!.CustomStateDisplayName!;
        return GetStringOrFallback(key, original);
    }

    private Dictionary<int, string> LocalizeOptionWarnings(SettingDefinition setting)
    {
        var originalWarnings = setting.ComboBox?.OptionWarnings;
        if (originalWarnings == null)
            return new Dictionary<int, string>();

        var localizedWarnings = new Dictionary<int, string>();
        foreach (var kvp in originalWarnings)
        {
            var locKey = $"Setting_{setting.Id}_OptionWarning_{kvp.Key}";
            localizedWarnings[kvp.Key] = GetStringOrFallback(locKey, kvp.Value);
        }

        return localizedWarnings;
    }

    private string[] LocalizeOptionTooltips(SettingDefinition setting)
    {
        var originalTooltips = setting.ComboBox?.OptionTooltips;
        if (originalTooltips == null)
            return Array.Empty<string>();

        var localizedTooltips = new string[originalTooltips.Length];
        for (int i = 0; i < originalTooltips.Length; i++)
        {
            var locKey = $"Setting_{setting.Id}_OptionTooltip_{i}";
            localizedTooltips[i] = GetStringOrFallback(locKey, originalTooltips[i]);
        }

        return localizedTooltips;
    }

    private Dictionary<int, (string Title, string Message)> LocalizeOptionConfirmations(SettingDefinition setting)
    {
        var originalConfirmations = setting.ComboBox?.OptionConfirmations;
        if (originalConfirmations == null)
            return new Dictionary<int, (string Title, string Message)>();

        var localizedConfirmations = new Dictionary<int, (string Title, string Message)>();
        foreach (var kvp in originalConfirmations)
        {
            var title = GetStringOrFallback(kvp.Value.Title, kvp.Value.Title);
            var message = GetStringOrFallback(kvp.Value.Message, kvp.Value.Message);
            localizedConfirmations[kvp.Key] = (title, message);
        }

        return localizedConfirmations;
    }

    private string[] LocalizeComboBoxNames(SettingDefinition setting)
    {
        var originalNames = setting.ComboBox!.DisplayNames;
        var localizedNames = new string[originalNames.Length];

        for (int i = 0; i < originalNames.Length; i++)
        {
            var key = IsLocalizationKey(originalNames[i])
                ? originalNames[i]
                : $"Setting_{setting.Id}_Option_{i}";

            localizedNames[i] = GetStringOrFallback(key, originalNames[i]);
        }

        return localizedNames;
    }

    private bool IsLocalizationKey(string value)
    {
        return value.StartsWith("Template_") ||
               value.StartsWith("Setting_") ||
               value.StartsWith("PowerPlan_") ||
               value.StartsWith("ServiceOption_");
    }

    private string LocalizeUnits(string units)
    {
        var key = units switch
        {
            "Minutes" => "Common_Unit_Minutes",
            "Milliseconds" => "Common_Unit_Milliseconds",
            "%" => "%",
            _ => null
        };

        return key != null ? GetStringOrFallback(key, units) : units;
    }

    private string GetStringOrFallback(string key, string fallback)
    {
        var localized = _localization.GetString(key);
        return localized.StartsWith("[") && localized.EndsWith("]") ? fallback : localized;
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
                var domainService = _domainServiceRouter.GetDomainService(childSettingId);
                var filteredSettings = _compatibleSettingsRegistry.GetFilteredSettings(domainService.DomainName);
                var childSetting = filteredSettings.FirstOrDefault(s => s.Id == childSettingId);

                if (childSetting == null) continue;

                var featureName = GetFeatureName(childSettingId);
                var groupNameKey = $"SettingGroup_{childSetting.GroupName?.Replace(" ", "_")}";
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
