using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using static Winhance.Core.Features.Common.Catalog.StateValue;

namespace Winhance.Core.Features.Optimize.Models;

public static class SoundOptimizationsCatalog
{
    public const string FeatureId = FeatureIds.Sound;
    public const string FeatureName = "Sound";

    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "sound-startup",
            Display = new()
            {
                Name = "Startup Sound During Boot",
                Description = "Play the Windows startup sound when your computer boots up",
                GroupName = "System Sounds",
                Icon = MaterialIcons.MonitorSpeaker,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("DisableStartupSound", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation" }, "DisableStartupSound", RegistryValueKind.DWord),
                new RegTarget("UserSetting_DisableStartupSound", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\EditionOverrides" }, "UserSetting_DisableStartupSound", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue>
                    {
                        ["DisableStartupSound"] = Of(0).OrAbsent(),
                        ["UserSetting_DisableStartupSound"] = Of(0).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["DisableStartupSound"] = Of(1),
                        ["UserSetting_DisableStartupSound"] = Of(1).OrAbsent(),
                    },
                },
            },
        },
        new()
        {
            Id = "sound-communication-ducking",
            Display = new()
            {
                Name = "Sound Ducking Preference",
                Description = "Automatically lower volume of media and apps when Windows detects communication activity",
                GroupName = "System Sounds",
                Icon = MaterialIcons.VolumeMedium,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("UserDuckingPreference", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Multimedia\Audio" }, "UserDuckingPreference", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Mute all other sounds",
                    Set = new Dictionary<string, StateValue> { ["UserDuckingPreference"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Reduce the volume of other sounds by 80%",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserDuckingPreference"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Reduce the volume of other sounds by 50%",
                    Set = new Dictionary<string, StateValue> { ["UserDuckingPreference"] = Of(2) },
                },
                new SettingState
                {
                    Label = "Do nothing",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["UserDuckingPreference"] = Of(3) },
                },
            },
        },
        new()
        {
            Id = "sound-narrator-audio-ducking",
            Display = new()
            {
                Name = "Narrator Audio Ducking",
                Description = "Allow Narrator to automatically lower the volume of other applications when it speaks",
                GroupName = "System Sounds",
                Icon = MaterialIcons.VolumeOff,
            },
            Targets = new Target[]
            {
                new RegTarget("DuckAudio", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam" }, "DuckAudio", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DuckAudio"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DuckAudio"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "sound-voice-activation",
            Display = new()
            {
                Name = "Voice Activation for Apps",
                Description = "Allow apps to listen and respond to voice commands like \"Hey Cortana\"",
                GroupName = "System Sounds",
                Icon = MaterialIcons.AccountTieVoice,
            },
            Targets = new Target[]
            {
                new RegTarget("AgentActivationEnabled", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings" }, "AgentActivationEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AgentActivationEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AgentActivationEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "sound-voice-activation-last-used",
            Display = new()
            {
                Name = "Last Used Voice Activation Setting",
                Description = "Remember and apply the most recently used voice activation configuration",
                GroupName = "System Sounds",
                Icon = MaterialIcons.MicrophoneMessage,
            },
            Targets = new Target[]
            {
                new RegTarget("AgentActivationLastUsed", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings" }, "AgentActivationLastUsed", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AgentActivationLastUsed"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AgentActivationLastUsed"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "sound-accessibility-activation",
            Display = new()
            {
                Name = "Accessibility Activation Sounds",
                Description = "Play sounds when accessibility features like StickyKeys or FilterKeys are activated",
                GroupName = "System Sounds",
                Icon = MaterialIcons.Keyboard,
            },
            Targets = new Target[]
            {
                new RegTarget("Sound on Activation", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility" }, "Sound on Activation", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Sound on Activation"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Sound on Activation"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "sound-accessibility-warnings",
            Display = new()
            {
                Name = "Accessibility Warning Sounds",
                Description = "Play warning sounds when attempting to activate accessibility features or when accessibility-related events occur",
                GroupName = "System Sounds",
                Icon = FluentIcons.DesktopSpeaker,
            },
            Targets = new Target[]
            {
                new RegTarget("Warning Sounds", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility" }, "Warning Sounds", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Warning Sounds"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Warning Sounds"] = Of(0) },
                },
            },
        },
    };
}
