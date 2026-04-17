using System;
using System.Collections.Generic;
using System.Linq;
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
                Options = LocalizeComboBoxOptions(setting)
            };

            if (comboBox.CustomStateDisplayName != null)
            {
                localizedComboBox = localizedComboBox with
                {
                    CustomStateDisplayName = GetLocalizedCustomState(setting)
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
        var key = $"Setting_{setting.Id}_Option_Custom";
        var original = setting.ComboBox!.CustomStateDisplayName!;
        return GetStringOrFallback(key, original);
    }

    private IReadOnlyList<Winhance.Core.Features.Common.Models.ComboBoxOption> LocalizeComboBoxOptions(SettingDefinition setting)
    {
        var originalOptions = setting.ComboBox?.Options;
        if (originalOptions == null || originalOptions.Count == 0)
            return Array.Empty<Winhance.Core.Features.Common.Models.ComboBoxOption>();

        var localized = new List<Winhance.Core.Features.Common.Models.ComboBoxOption>(originalOptions.Count);
        for (int i = 0; i < originalOptions.Count; i++)
        {
            var original = originalOptions[i];

            var displayKey = IsLocalizationKey(original.DisplayName)
                ? original.DisplayName
                : $"Setting_{setting.Id}_Option_{i}";
            var localizedDisplay = GetStringOrFallback(displayKey, original.DisplayName);

            string? localizedTooltip = original.Tooltip;
            if (!string.IsNullOrEmpty(original.Tooltip))
            {
                var tooltipKey = $"Setting_{setting.Id}_OptionTooltip_{i}";
                localizedTooltip = GetStringOrFallback(tooltipKey, original.Tooltip);
            }

            string? localizedWarning = original.Warning;
            if (!string.IsNullOrEmpty(original.Warning))
            {
                var warningKey = $"Setting_{setting.Id}_OptionWarning_{i}";
                localizedWarning = GetStringOrFallback(warningKey, original.Warning);
            }

            (string Title, string Message)? localizedConfirmation = original.Confirmation;
            if (original.Confirmation is { } confirmation)
            {
                var title = GetStringOrFallback(confirmation.Title, confirmation.Title);
                var message = GetStringOrFallback(confirmation.Message, confirmation.Message);
                localizedConfirmation = (title, message);
            }

            localized.Add(original with
            {
                DisplayName = localizedDisplay,
                Tooltip = localizedTooltip,
                Warning = localizedWarning,
                Confirmation = localizedConfirmation,
            });
        }

        return localized;
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
