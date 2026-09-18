using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Optimize.Catalogs;

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
                Name = LocKey.Setting.SoundStartup.Name,
                Description = LocKey.Setting.SoundStartup.Description,
                GroupName = LocKey.SettingGroup.SystemSounds,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["DisableStartupSound"] = Of(0).OrAbsent(),
                        ["UserSetting_DisableStartupSound"] = Of(0).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SoundCommunicationDucking.Name,
                Description = LocKey.Setting.SoundCommunicationDucking.Description,
                GroupName = LocKey.SettingGroup.SystemSounds,
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
                    Label = LocKey.Setting.SoundCommunicationDucking.Option0,
                    Set = new Dictionary<string, StateValue> { ["UserDuckingPreference"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.SoundCommunicationDucking.Option1,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserDuckingPreference"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.SoundCommunicationDucking.Option2,
                    Set = new Dictionary<string, StateValue> { ["UserDuckingPreference"] = Of(2) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.SoundCommunicationDucking.Option3,
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
                Name = LocKey.Setting.SoundNarratorAudioDucking.Name,
                Description = LocKey.Setting.SoundNarratorAudioDucking.Description,
                GroupName = LocKey.SettingGroup.SystemSounds,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DuckAudio"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SoundVoiceActivation.Name,
                Description = LocKey.Setting.SoundVoiceActivation.Description,
                GroupName = LocKey.SettingGroup.SystemSounds,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AgentActivationEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SoundVoiceActivationLastUsed.Name,
                Description = LocKey.Setting.SoundVoiceActivationLastUsed.Description,
                GroupName = LocKey.SettingGroup.SystemSounds,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AgentActivationLastUsed"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SoundAccessibilityActivation.Name,
                Description = LocKey.Setting.SoundAccessibilityActivation.Description,
                GroupName = LocKey.SettingGroup.SystemSounds,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Sound on Activation"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SoundAccessibilityWarnings.Name,
                Description = LocKey.Setting.SoundAccessibilityWarnings.Description,
                GroupName = LocKey.SettingGroup.SystemSounds,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Warning Sounds"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Warning Sounds"] = Of(0) },
                },
            },
        },
    };
}
