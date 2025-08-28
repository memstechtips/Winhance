using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;

using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Models.WindowsRegistry;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Windows Security optimization settings including UAC and related security features.
    /// Uses the standard pattern with ComboBox options for UAC level selection.
    /// </summary>
    public static class WindowsSecurityOptimizations
    {
        public const string UacRegistryPath =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
        public static readonly RegistryValueKind ValueKind = RegistryValueKind.DWord;

        public static SettingGroup GetWindowsSecurityOptimizations()
        {
            return new SettingGroup
            {
                Name = "Windows Security",
                FeatureId = FeatureIds.Security,
                Settings = new List<SettingDefinition>
                {
                    new SettingDefinition
                    {
                        Id = "security-uac-level",
                        Name = "User Account Control Level",
                        Description = "Controls UAC notification level and secure desktop behavior",
                        GroupName = "Windows Security Settings",
                        InputType = SettingInputType.Selection,
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                                ValueName = "ConsentPromptBehaviorAdmin",
                                RecommendedValue = 5, // NotifyChangesOnly
                                EnabledValue = 5,
                                DisabledValue = 0,
                                DefaultValue = 5,
                                ValueType = RegistryValueKind.DWord,
                            },
                            new RegistrySetting
                            {
                                KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                                ValueName = "PromptOnSecureDesktop",
                                RecommendedValue = 1, // Secure desktop enabled
                                EnabledValue = 1,
                                DisabledValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord,
                            },
                        },
                        CustomProperties = new Dictionary<string, object>
                        {
                            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                            {
                                "Always notify",
                                "Notify when apps try to make changes",
                                "Notify when apps try to make changes (no dim)",
                                "Never notify",
                            },
                            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int>>
                            {
                                [0] = new Dictionary<string, int> // Always notify
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 2,
                                    ["PromptOnSecureDesktop"] = 1,
                                },
                                [1] = new Dictionary<string, int> // Notify changes only
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 5,
                                    ["PromptOnSecureDesktop"] = 1,
                                },
                                [2] = new Dictionary<string, int> // Notify changes no dim
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 5,
                                    ["PromptOnSecureDesktop"] = 0,
                                },
                                [3] = new Dictionary<string, int> // Never notify
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 0,
                                    ["PromptOnSecureDesktop"] = 0,
                                },
                            },
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Gets the UAC level from registry values
        /// </summary>
        public static int GetUacLevelFromRegistry(int consentPromptValue, int secureDesktopValue)
        {
            // Check for exact matches of standard levels
            var mappings = new Dictionary<(int ConsentPrompt, int SecureDesktop), int>
            {
                [(2, 1)] = 0, // Always notify
                [(5, 1)] = 1, // Notify changes only
                [(5, 0)] = 2, // Notify changes no dim
                [(0, 0)] = 3, // Never notify
            };

            var key = (consentPromptValue, secureDesktopValue);
            return mappings.TryGetValue(key, out var level) ? level : 4; // Custom if no match
        }

        /// <summary>
        /// Gets registry values for a given UAC level
        /// </summary>
        public static (int ConsentPrompt, int SecureDesktop) GetRegistryValuesForUacLevel(int level)
        {
            return level switch
            {
                0 => (2, 1), // Always notify
                1 => (5, 1), // Notify changes only
                2 => (5, 0), // Notify changes no dim
                3 => (0, 0), // Never notify
                _ => (5, 1), // Default to notify changes only
            };
        }
    }
}
