using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models
{
    public static class WindowsSecurityOptimizations
    {
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
                        InputType = InputType.Selection,
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                                ValueName = "ConsentPromptBehaviorAdmin",
                                RecommendedValue = 5, // NotifyChangesOnly
                                EnabledValue = 5,
                                DisabledValue = 0,
                                DefaultValue = 5,
                                ValueType = RegistryValueKind.DWord,
                            },
                            new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
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
                                "Prompt for Credentials",
                                "Always notify",
                                "Notify when apps try to make changes",
                                "Notify when apps try to make changes (no dim)",
                                "Never notify",
                            },
                            [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                            {
                                [0] = new Dictionary<string, int?> // Prompt for credentials
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 1,
                                    ["PromptOnSecureDesktop"] = 1,
                                },
                                [1] = new Dictionary<string, int?> // Always notify
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 2,
                                    ["PromptOnSecureDesktop"] = 1,
                                },
                                [2] = new Dictionary<string, int?> // Notify changes only
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 5,
                                    ["PromptOnSecureDesktop"] = 1,
                                },
                                [3] = new Dictionary<string, int?> // Notify changes no dim
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 5,
                                    ["PromptOnSecureDesktop"] = 0,
                                },
                                [4] = new Dictionary<string, int?> // Never notify
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 0,
                                    ["PromptOnSecureDesktop"] = 0,
                                },
                            },
                            [CustomPropertyKeys.SupportsCustomState] = true,
                            [CustomPropertyKeys.CustomStateDisplayName] = "Custom (User Defined)",
                        },
                    },
                },
            };
        }
    }
}
