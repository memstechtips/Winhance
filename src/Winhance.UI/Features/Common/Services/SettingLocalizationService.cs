using System.Text.Json;
using Winhance.Core.Features.Common.Constants;
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

        if (setting.CustomProperties.Count > 0)
        {
            var customProps = new Dictionary<string, object>(setting.CustomProperties);

            if (customProps.ContainsKey(CustomPropertyKeys.ComboBoxDisplayNames))
            {
                customProps[CustomPropertyKeys.ComboBoxDisplayNames] = LocalizeComboBoxNames(setting);
            }

            if (customProps.TryGetValue(CustomPropertyKeys.CustomStateDisplayName, out var customState) && customState is string csKey)
            {
                customProps[CustomPropertyKeys.CustomStateDisplayName] = GetLocalizedCustomState(setting);
            }

            if (customProps.TryGetValue("Units", out var units) && units is string unitsKey)
            {
                customProps["Units"] = LocalizeUnits(unitsKey);
            }

            if (customProps.ContainsKey(CustomPropertyKeys.OptionWarnings))
            {
                customProps[CustomPropertyKeys.OptionWarnings] = LocalizeOptionWarnings(setting);
            }

            if (customProps.ContainsKey(CustomPropertyKeys.OptionTooltips))
            {
                customProps[CustomPropertyKeys.OptionTooltips] = LocalizeOptionTooltips(setting);
            }

            if (customProps.ContainsKey(CustomPropertyKeys.OptionConfirmations))
            {
                customProps[CustomPropertyKeys.OptionConfirmations] = LocalizeOptionConfirmations(setting);
            }

            // Handle compatibility messages (format: Key|Arg1|Arg2...)
            if (customProps.TryGetValue(CustomPropertyKeys.VersionCompatibilityMessage, out var compatMsg) && compatMsg is string compatKey)
            {
                if (compatKey.StartsWith("Compatibility_"))
                {
                    var parts = compatKey.Split('|');
                    var key = parts[0];

                    if (parts.Length > 1)
                    {
                        // Handle args
                        var args = parts.Skip(1).ToArray();
                        try
                        {
                            var format = _localization.GetString(key);
                            customProps[CustomPropertyKeys.VersionCompatibilityMessage] = string.Format(format, args);
                        }
                        catch
                        {
                            customProps[CustomPropertyKeys.VersionCompatibilityMessage] = _localization.GetString(key);
                        }
                    }
                    else
                    {
                        customProps[CustomPropertyKeys.VersionCompatibilityMessage] = _localization.GetString(key);
                    }
                }
            }

            localized = localized with { CustomProperties = customProps };
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
        var original = setting.CustomProperties[CustomPropertyKeys.CustomStateDisplayName].ToString();
        return GetStringOrFallback(key, original!);
    }

    private Dictionary<int, string> LocalizeOptionWarnings(SettingDefinition setting)
    {
        if (!setting.CustomProperties.TryGetValue(CustomPropertyKeys.OptionWarnings, out var value))
            return new Dictionary<int, string>();

        var localizedWarnings = new Dictionary<int, string>();

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (int.TryParse(property.Name, out var key))
                {
                    var val = property.Value.GetString();
                    if (val != null)
                    {
                        var locKey = $"Setting_{setting.Id}_OptionWarning_{key}";
                        localizedWarnings[key] = GetStringOrFallback(locKey, val);
                    }
                }
            }
        }
        else if (value is Dictionary<int, string> originalWarnings)
        {
            foreach (var kvp in originalWarnings)
            {
                var locKey = $"Setting_{setting.Id}_OptionWarning_{kvp.Key}";
                localizedWarnings[kvp.Key] = GetStringOrFallback(locKey, kvp.Value);
            }
        }

        return localizedWarnings;
    }

    private Dictionary<int, string> LocalizeOptionTooltips(SettingDefinition setting)
    {
        if (!setting.CustomProperties.TryGetValue(CustomPropertyKeys.OptionTooltips, out var value))
            return new Dictionary<int, string>();

        var localizedTooltips = new Dictionary<int, string>();

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (int.TryParse(property.Name, out var key))
                {
                    var val = property.Value.GetString();
                    if (val != null)
                    {
                        var locKey = $"Setting_{setting.Id}_OptionTooltip_{key}";
                        localizedTooltips[key] = GetStringOrFallback(locKey, val);
                    }
                }
            }
        }
        else if (value is Dictionary<int, string> originalTooltips)
        {
            foreach (var kvp in originalTooltips)
            {
                var locKey = $"Setting_{setting.Id}_OptionTooltip_{kvp.Key}";
                localizedTooltips[kvp.Key] = GetStringOrFallback(locKey, kvp.Value);
            }
        }

        return localizedTooltips;
    }

    private Dictionary<int, (string Title, string Message)> LocalizeOptionConfirmations(SettingDefinition setting)
    {
        if (!setting.CustomProperties.TryGetValue(CustomPropertyKeys.OptionConfirmations, out var value))
            return new Dictionary<int, (string Title, string Message)>();

        var localizedConfirmations = new Dictionary<int, (string Title, string Message)>();

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (int.TryParse(property.Name, out var key) && property.Value.ValueKind == JsonValueKind.Object)
                {
                    string? title = null;
                    string? message = null;

                    if (property.Value.TryGetProperty("Title", out var titleProp))
                        title = titleProp.GetString();

                    if (property.Value.TryGetProperty("Message", out var messageProp))
                        message = messageProp.GetString();

                    if (title != null && message != null)
                    {
                         var locTitle = GetStringOrFallback(title, title);
                         var locMessage = GetStringOrFallback(message, message);
                         localizedConfirmations[key] = (locTitle, locMessage);
                    }
                }
            }
        }
        else if (value is Dictionary<int, (string Title, string Message)> originalConfirmations)
        {
            foreach (var kvp in originalConfirmations)
            {
                var title = GetStringOrFallback(kvp.Value.Title, kvp.Value.Title);
                var message = GetStringOrFallback(kvp.Value.Message, kvp.Value.Message);
                localizedConfirmations[kvp.Key] = (title, message);
            }
        }

        return localizedConfirmations;
    }

    private string[] LocalizeComboBoxNames(SettingDefinition setting)
    {
        if (!setting.CustomProperties.TryGetValue(CustomPropertyKeys.ComboBoxDisplayNames, out var value))
            return Array.Empty<string>();

        var originalNames = (string[])value;
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
        if (setting.CustomProperties?.TryGetValue(CustomPropertyKeys.CrossGroupChildSettings, out var crossGroupObj) != true ||
            crossGroupObj is not Dictionary<string, string> crossGroupSettings ||
            crossGroupSettings.Count == 0)
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
