using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Services
{
    public class SettingLocalizationService
    {
        private readonly ILocalizationService _localization;

        public ILocalizationService LocalizationService => _localization;

        public SettingLocalizationService(ILocalizationService localization)
        {
            _localization = localization;
        }

        public SettingGroup LocalizeSettingGroup(SettingGroup group)
        {
            var localizedSettings = group.Settings
                .Select(LocalizeSetting)
                .ToList();

            return new SettingGroup
            {
                Name = GetLocalizedGroupName(group.Name),
                FeatureId = group.FeatureId,
                Settings = localizedSettings
            };
        }

        public SettingDefinition LocalizeSetting(SettingDefinition setting)
        {
            var localized = setting with
            {
                Name = GetLocalizedName(setting),
                Description = GetLocalizedDescription(setting),
                GroupName = setting.GroupName != null ? GetLocalizedGroupName(setting.GroupName) : null,
                ConfirmationTitle = setting.ConfirmationTitle != null ? GetLocalizedConfirmationTitle(setting) : null,
                ConfirmationMessage = setting.ConfirmationMessage != null ? GetLocalizedConfirmationMessage(setting) : null,
                ConfirmationCheckboxText = setting.ConfirmationCheckboxText != null ? GetLocalizedConfirmationCheckbox(setting) : null
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
            var key = $"SettingGroup_{groupName.Replace(" ", "").Replace("&", "")}";
            return GetStringOrFallback(key, groupName);
        }

        private string GetLocalizedConfirmationTitle(SettingDefinition setting)
        {
            var key = $"Setting_{setting.Id}_ConfirmTitle";
            return GetStringOrFallback(key, setting.ConfirmationTitle!);
        }

        private string GetLocalizedConfirmationMessage(SettingDefinition setting)
        {
            var key = $"Setting_{setting.Id}_ConfirmMessage";
            return GetStringOrFallback(key, setting.ConfirmationMessage!);
        }

        private string GetLocalizedConfirmationCheckbox(SettingDefinition setting)
        {
            var key = $"Setting_{setting.Id}_ConfirmCheckbox";
            return GetStringOrFallback(key, setting.ConfirmationCheckboxText!);
        }

        private string GetLocalizedCustomState(SettingDefinition setting)
        {
            var key = $"Setting_{setting.Id}_CustomState";
            var original = setting.CustomProperties[CustomPropertyKeys.CustomStateDisplayName].ToString();
            return GetStringOrFallback(key, original!);
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
    }
}
