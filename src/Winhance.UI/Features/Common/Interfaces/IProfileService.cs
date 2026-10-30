using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Models;

namespace Winhance.UI.Features.Common.Interfaces;

// A profile holds Optimize and Customize settings only; apps are never part of one.
public interface IProfileService
{
    event EventHandler? Changed;

    bool IsBusy { get; }
    string? ActiveProfileId { get; }
    bool LastSwitchFailed { get; }

    // The reboot-only settings profile switches have changed since Winhance started. Not kept across a Winhance restart.
    IReadOnlyList<string> PendingReboot { get; }

    IReadOnlyList<SettingsProfile> GetProfiles();
    SettingsProfile? Find(string nameOrId);
    bool IsNameAvailable(string name);

    Task<SettingsProfile?> SaveCurrentAsync(string name);

    // Overwrites a profile of the same name.
    Task<SettingsProfile?> SaveAsync(string name, IReadOnlyList<SettingChoice> settings);

    Task<SettingsProfile?> AddFromFileAsync(string filePath, string name);
    Task<bool> ExportAsync(SettingsProfile profile, string destination);
    Task DeleteAsync(SettingsProfile profile);

    // Settings only; null when the file cannot be read.
    Task<WinhanceConfigFile?> LoadAsync(SettingsProfile profile);

    // Null when another switch is already running.
    Task<ProfileSwitchResult?> SwitchAsync(SettingsProfile profile);

    // The sign-in task: applies nothing when a saved profile's file differs from the one last switched to or saved.
    Task<ProfileSwitchResult?> ReapplyAsync(SettingsProfile profile);

    // The active profile's settings this PC now reads differently, by name.
    Task<IReadOnlyList<string>> DriftAsync();
}
