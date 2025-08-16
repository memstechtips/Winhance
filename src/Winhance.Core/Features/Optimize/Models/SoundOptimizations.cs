using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

public static class SoundOptimizations
{
    public static OptimizationGroup GetSoundOptimizations()
    {
        return new OptimizationGroup
        {
            Name = "Sound",
            Category = OptimizationCategory.Sound,
            Settings = new List<OptimizationSetting>
            {
                new OptimizationSetting
                {
                    Id = "sound-startup",
                    Name = "Startup Sound During Boot",
                    Description = "Controls the startup sound during boot and for the user",
                    Category = OptimizationCategory.Sound,
                    GroupName = "System Sounds",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Sound",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Authentication\\LogonUI\\BootAnimation",
                            Name = "DisableStartupSound",
                            RecommendedValue = 1, // For backward compatibility
                            EnabledValue = 0, // When toggle is ON, startup sound is enabled
                            DisabledValue = 1, // When toggle is OFF, startup sound is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls the startup sound during boot",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                        new RegistrySetting
                        {
                            Category = "Sound",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\EditionOverrides",
                            Name = "UserSetting_DisableStartupSound",
                            RecommendedValue = 1, // For backward compatibility
                            EnabledValue = 0, // When toggle is ON, user startup sound is enabled
                            DisabledValue = 1, // When toggle is OFF, user startup sound is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls the startup sound for the user",
                            IsPrimary = false,
                            AbsenceMeansEnabled = false,
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
                },
                new OptimizationSetting
                {
                    Id = "sound-communication-ducking",
                    Name = "Sound Ducking Preference",
                    Description = "Controls sound behavior by reducing the volume of other sounds",
                    Category = OptimizationCategory.Sound,
                    GroupName = "System Sounds",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Sound",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Software\\Microsoft\\Multimedia\\Audio",
                            Name = "UserDuckingPreference",
                            RecommendedValue = 3, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, sound ducking is enabled (1 = reduce other sounds by 80%)
                            DisabledValue = 3, // When toggle is OFF, sound ducking is disabled (3 = do nothing)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            Description = "Controls sound communications behavior",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "sound-voice-activation",
                    Name = "Voice Activation for Apps",
                    Description = "Controls voice activation for all apps",
                    Category = OptimizationCategory.Sound,
                    GroupName = "System Sounds",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Sound",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\SpeechOneCore\\Settings",
                            Name = "AgentActivationEnabled",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, voice activation is enabled
                            DisabledValue = 0, // When toggle is OFF, voice activation is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls voice activation for all apps",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "sound-voice-activation-last-used",
                    Name = "Last Used Voice Activation Setting",
                    Description = "Controls the last used voice activation setting",
                    Category = OptimizationCategory.Sound,
                    GroupName = "System Sounds",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Sound",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\SpeechOneCore\\Settings",
                            Name = "AgentActivationLastUsed",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, last used voice activation is enabled
                            DisabledValue = 0, // When toggle is OFF, last used voice activation is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls the last used voice activation setting",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "sound-effects-enhancements",
                    Name = "Sound Effects and Enhancements",
                    Description = "Controls audio enhancements for playback devices",
                    Category = OptimizationCategory.Sound,
                    GroupName = "Audio Enhancements",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Sound",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Software\\Microsoft\\Multimedia\\Audio\\DeviceFx",
                            Name = "EnableDeviceEffects",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, audio enhancements are enabled
                            DisabledValue = 0, // When toggle is OFF, audio enhancements are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls audio enhancements for playback devices",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "sound-spatial-audio",
                    Name = "Spatial Sound Settings",
                    Description = "Controls Windows Sonic and spatial sound features",
                    Category = OptimizationCategory.Sound,
                    GroupName = "Audio Enhancements",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Sound",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Audio",
                            Name = "EnableSpatialSound",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, spatial sound is enabled
                            DisabledValue = 0, // When toggle is OFF, spatial sound is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls Windows Sonic and spatial sound features",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
            },
        };
    }
}
