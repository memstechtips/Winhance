using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Windows Security optimization settings including UAC and related security features.
    /// Uses the standard pattern with ComboBox options for UAC level selection.
    /// </summary>
    public static class WindowsSecurityOptimizations
    {
        public const string UacRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
        public static readonly RegistryValueKind ValueKind = RegistryValueKind.DWord;

        /// <summary>
        /// Gets Windows Security optimization settings
        /// </summary>
        public static OptimizationGroup GetWindowsSecurityOptimizations()
        {
            return new OptimizationGroup
            {
                Name = "Windows Security",
                Category = OptimizationCategory.Security,
                Settings = new List<OptimizationSetting>
                {
                    new OptimizationSetting
                    {
                        Id = "windows-security-uac-level",
                        Name = "User Account Control Level",
                        Description = "Controls UAC notification level and secure desktop behavior",
                        Category = OptimizationCategory.Security,
                        GroupName = "User Account Control",
                        IsEnabled = true,
                        ControlType = ControlType.ComboBox,
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                Category = "Security",
                                Hive = RegistryHive.LocalMachine,
                                SubKey = UacRegistryPath,
                                Name = "ConsentPromptBehaviorAdmin",
                                RecommendedValue = 5, // NotifyChangesOnly
                                EnabledValue = 5,
                                DisabledValue = 0,
                                ValueType = ValueKind,
                                DefaultValue = 5,
                                Description = "Controls UAC consent prompt behavior for administrators",
                                IsPrimary = true,
                                AbsenceMeansEnabled = false,
                                CustomProperties = new Dictionary<string, object>
                                {
                                    ["ComboBoxOptions"] = new Dictionary<string, int>
                                    {
                                        ["Always notify"] = 0, // ConsentPrompt=2, SecureDesktop=1
                                        ["Notify when apps try to make changes"] = 1, // ConsentPrompt=5, SecureDesktop=1
                                        ["Notify when apps try to make changes (no dim)"] = 2, // ConsentPrompt=5, SecureDesktop=0
                                        ["Never notify"] = 3, // ConsentPrompt=0, SecureDesktop=0
                                        ["Custom UAC Setting"] = 4, // Any other combination
                                    },
                                }
                            },
                            new RegistrySetting
                            {
                                Category = "Security",
                                Hive = RegistryHive.LocalMachine,
                                SubKey = UacRegistryPath,
                                Name = "PromptOnSecureDesktop",
                                RecommendedValue = 1, // Secure desktop enabled
                                EnabledValue = 1,
                                DisabledValue = 0,
                                ValueType = ValueKind,
                                DefaultValue = 1,
                                Description = "Controls whether UAC prompts appear on secure desktop",
                                IsPrimary = false,
                                AbsenceMeansEnabled = false,
                            },
                        },
                        CustomProperties = new Dictionary<string, object>
                        {
                            ["ComboBoxOptions"] = new Dictionary<string, int>
                            {
                                ["Always notify"] = 0,
                                ["Notify when apps try to make changes"] = 1,
                                ["Notify when apps try to make changes (no dim)"] = 2,
                                ["Never notify"] = 3,
                            },
                            ["ValueMappings"] = new Dictionary<int, Dictionary<string, int>>
                            {
                                [0] = new Dictionary<string, int> // Always notify
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 2,
                                    ["PromptOnSecureDesktop"] = 1
                                },
                                [1] = new Dictionary<string, int> // Notify changes only
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 5,
                                    ["PromptOnSecureDesktop"] = 1
                                },
                                [2] = new Dictionary<string, int> // Notify changes no dim
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 5,
                                    ["PromptOnSecureDesktop"] = 0
                                },
                                [3] = new Dictionary<string, int> // Never notify
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 0,
                                    ["PromptOnSecureDesktop"] = 0
                                }
                            }
                        },
                        LinkedSettingsLogic = LinkedSettingsLogic.All
                    }
                }
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
