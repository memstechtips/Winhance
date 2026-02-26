using Microsoft.Win32;
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
                    Description = "Play the Windows startup sound when your computer boots up",
                    GroupName = "System Sounds",
                    Icon = "MonitorSpeaker",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation",
                            ValueName = "DisableStartupSound",
                            RecommendedValue = 1, // For backward compatibility
                            EnabledValue = null, // When toggle is ON, startup sound is enabled
                            DisabledValue = 1, // When toggle is OFF, startup sound is disabled
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\EditionOverrides",
                            ValueName = "UserSetting_DisableStartupSound",
                            RecommendedValue = 1, // For backward compatibility
                            EnabledValue = null, // When toggle is ON, user startup sound is enabled
                            DisabledValue = 1, // When toggle is OFF, user startup sound is disabled
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "sound-communication-ducking",
                    Name = "Sound Ducking Preference",
                    Description = "Automatically lower volume of media and apps when Windows detects communication activity",
                    GroupName = "System Sounds",
                    Icon = "VolumeMedium",
                    InputType = InputType.Selection,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Multimedia\Audio",
                            ValueName = "UserDuckingPreference",
                            RecommendedValue = 3, // Do Nothing
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        DisplayNames = new string[]
                        {
                            "Mute all other sounds",
                            "Reduce the volume of other sounds by 80%",
                            "Reduce the volume of other sounds by 50%",
                            "Do nothing",
                        },
                        ValueMappings = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?>
                            {
                                ["UserDuckingPreference"] = 0,
                            },
                            [1] = new Dictionary<string, object?>
                            {
                                ["UserDuckingPreference"] = 1,
                            },
                            [2] = new Dictionary<string, object?>
                            {
                                ["UserDuckingPreference"] = 2,
                            },
                            [3] = new Dictionary<string, object?>
                            {
                                ["UserDuckingPreference"] = 3,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "sound-narrator-audio-ducking",
                    Name = "Narrator Audio Ducking",
                    Description = "Allow Narrator to automatically lower the volume of other applications when it speaks",
                    GroupName = "System Sounds",
                    Icon = "VolumeOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam",
                            ValueName = "DuckAudio",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "sound-voice-activation",
                    Name = "Voice Activation for Apps",
                    Description = "Allow apps to listen and respond to voice commands like \"Hey Cortana\"",
                    GroupName = "System Sounds",
                    Icon = "AccountTieVoice",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings",
                            ValueName = "AgentActivationEnabled",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "sound-voice-activation-last-used",
                    Name = "Last Used Voice Activation Setting",
                    Description = "Remember and apply the most recently used voice activation configuration",
                    GroupName = "System Sounds",
                    Icon = "MicrophoneMessage",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings",
                            ValueName = "AgentActivationLastUsed",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = null, // When toggle is ON, last used voice activation is enabled
                            DisabledValue = 0, // When toggle is OFF, last used voice activation is disabled
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "sound-accessibility-activation",
                    Name = "Accessibility Activation Sounds",
                    Description = "Play sounds when accessibility features like StickyKeys or FilterKeys are activated",
                    GroupName = "System Sounds",
                    Icon = "Keyboard",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility",
                            ValueName = "Sound on Activation",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "sound-accessibility-warnings",
                    Name = "Accessibility Warning Sounds",
                    Description = "Play warning sounds when attempting to activate accessibility features or when accessibility-related events occur",
                    GroupName = "System Sounds",
                    IconPack = "Fluent",
                    Icon = "DesktopSpeaker",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility",
                            ValueName = "Warning Sounds",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}
