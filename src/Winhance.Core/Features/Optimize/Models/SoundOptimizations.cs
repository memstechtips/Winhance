using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

public static class SoundOptimizations
{
    public static SettingGroup GetSoundOptimizations()
    {
        return new SettingGroup
        {
            Name = "Sound",
            FeatureId = FeatureIds.Sound,
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = "sound-startup",
                    Name = "Startup Sound During Boot",
                    Description = "Controls the startup sound during boot and for the user",
                    GroupName = "System Sounds",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation",
                            ValueName = "DisableStartupSound",
                            RecommendedValue = 1, // For backward compatibility
                            EnabledValue = 0, // When toggle is ON, startup sound is enabled
                            DisabledValue = 1, // When toggle is OFF, startup sound is disabled
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\EditionOverrides",
                            ValueName = "UserSetting_DisableStartupSound",
                            RecommendedValue = 1, // For backward compatibility
                            EnabledValue = 0, // When toggle is ON, user startup sound is enabled
                            DisabledValue = 1, // When toggle is OFF, user startup sound is disabled
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "sound-communication-ducking",
                    Name = "Sound Ducking Preference",
                    Description = "Controls sound behavior by reducing the volume of other sounds",
                    GroupName = "System Sounds",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Multimedia\Audio",
                            ValueName = "UserDuckingPreference",
                            RecommendedValue = 3, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, sound ducking is enabled (1 = reduce other sounds by 80%)
                            DisabledValue = 3, // When toggle is OFF, sound ducking is disabled (3 = do nothing)
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "sound-voice-activation",
                    Name = "Voice Activation for Apps",
                    Description = "Controls voice activation for all apps",
                    GroupName = "System Sounds",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings",
                            ValueName = "AgentActivationEnabled",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, voice activation is enabled
                            DisabledValue = 0, // When toggle is OFF, voice activation is disabled
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "sound-voice-activation-last-used",
                    Name = "Last Used Voice Activation Setting",
                    Description = "Controls the last used voice activation setting",
                    GroupName = "System Sounds",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings",
                            ValueName = "AgentActivationLastUsed",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, last used voice activation is enabled
                            DisabledValue = 0, // When toggle is OFF, last used voice activation is disabled
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "sound-effects-enhancements",
                    Name = "Sound Effects and Enhancements",
                    Description = "Controls audio enhancements for playback devices",
                    GroupName = "Audio Enhancements",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Multimedia\Audio\DeviceFx",
                            ValueName = "EnableDeviceEffects",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, audio enhancements are enabled
                            DisabledValue = 0, // When toggle is OFF, audio enhancements are disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "sound-spatial-audio",
                    Name = "Spatial Sound Settings",
                    Description = "Controls Windows Sonic and spatial sound features",
                    GroupName = "Audio Enhancements",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Audio",
                            ValueName = "EnableSpatialSound",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, spatial sound is enabled
                            DisabledValue = 0, // When toggle is OFF, spatial sound is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}
