using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;

namespace Winhance.UI.Features.Common.Services;

public sealed class ProfileService(
    ILogService logService,
    ILocalizationService localization,
    IUserPreferencesService preferences,
    IFileSystemService fileSystem,
    IInteractiveUserService interactiveUser,
    ICatalogSettingsRegistry registry,
    ISettingSnapshotSource snapshot,
    IFileStore fileStore,
    ICatalogScopeProvider scopes,
    IConfigFileWriter configFiles,
    IConfigLoadService configLoad,
    IConfigApplicationExecutionService execution,
    IConfigurationService configuration,
    IWindowsRegistryService windowsRegistry) : IProfileService
{
    private const string RecommendedId = "builtin:recommended";
    private const string WindowsDefaultsId = "builtin:windows-defaults";

    // Profile files sit in the user's own folder, which any program running as that user can write. The sign-in task
    // re-applies one elevated and unattended, so Winhance records each profile's SHA-256 here, where only an
    // administrator can write, whenever the user switches to it or saves it, and the task applies nothing that differs.
    // Profiles are per user, so each user's hashes sit under their SID.
    private const string TrustRoot = @"HKEY_LOCAL_MACHINE\SOFTWARE\Winhance\ProfileHashes";

    public const int MaxNameLength = 60;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<string> _pendingReboot = [];

    public event EventHandler? Changed;

    public bool IsBusy { get; private set; }

    public string? ActiveProfileId => preferences.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty) is { Length: > 0 } id ? id : null;

    public bool LastSwitchFailed => preferences.GetPreference(UserPreferenceKeys.ActiveProfileFailed, false);

    public IReadOnlyList<string> PendingReboot => _pendingReboot;

    private string Folder => fileSystem.CombinePath(
        AppDataPaths.UserRoot(interactiveUser.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData)),
        "Profiles");

    public IReadOnlyList<SettingsProfile> GetProfiles()
    {
        var profiles = new List<SettingsProfile>
        {
            new(RecommendedId, localization.GetString("Profiles_Builtin_Recommended"), ProfileKind.Recommended, null),
            new(WindowsDefaultsId, localization.GetString("Profiles_Builtin_WindowsDefaults"), ProfileKind.WindowsDefaults, null),
        };
        if (fileSystem.DirectoryExists(Folder))
        {
            profiles.AddRange(fileSystem.GetFiles(Folder, ConfigFileConstants.FilePattern)
                .Select(path => fileSystem.GetFileNameWithoutExtension(path))
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .Select(name => new SettingsProfile(name, name, ProfileKind.User, PathFor(name))));
        }
        return profiles;
    }

    public SettingsProfile? Find(string nameOrId) =>
        GetProfiles().FirstOrDefault(p =>
            string.Equals(p.Id, nameOrId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Name, nameOrId, StringComparison.CurrentCultureIgnoreCase)
            || string.Equals(p.Id, "builtin:" + nameOrId, StringComparison.OrdinalIgnoreCase));

    public bool IsNameAvailable(string name) =>
        name.Trim() is { Length: > 0 and <= MaxNameLength } trimmed
        && trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && !trimmed.EndsWith('.')
        && !WindowsFileNames.IsReservedDeviceName(trimmed)
        && Find(trimmed) is null;

    public async Task<SettingsProfile?> SaveCurrentAsync(string name)
    {
        IReadOnlyList<SettingChoice> settings;
        try
        {
            await registry.InitializeAsync();
            settings = await snapshot.CaptureAsync(CatalogScope.CurrentMachine);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Could not save profile '{name}': {ex.Message}");
            return null;
        }
        return await SaveAsync(name, settings);
    }

    public async Task<SettingsProfile?> SaveAsync(string name, IReadOnlyList<SettingChoice> settings)
    {
        try
        {
            fileSystem.CreateDirectory(Folder);
            // Builder hands over its answer-file choices too; a profile is applied to this PC, so it keeps settings only.
            var kept = settings.Where(c => registry.GetFeatureIdForSetting(c.SettingId) is { } feature
                && FeatureDefinitions.IsSettingsFeature(feature)).ToList();
            var set = await fileStore.LoadAsync(new SelectionSet(kept, [], []));
            return Written(name, await configFiles.WriteAsync(set, scopes.Current, PathFor(name.Trim())));
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Could not save profile '{name}': {ex.Message}");
            return null;
        }
    }

    public async Task<SettingsProfile?> AddFromFileAsync(string filePath, string name)
    {
        if (await configLoad.ReadConfigurationAsync(filePath) is not { } config)
            return null;
        try
        {
            fileSystem.CreateDirectory(Folder);
            return await WriteAsync(name, SettingsOnly(config));
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Could not add profile '{name}' from {filePath}: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> ExportAsync(SettingsProfile profile, string destination)
    {
        try
        {
            // Written, not copied: a copy keeps the profile's own modified date, so the export looks old in Explorer.
            if (profile.FilePath is { } path)
                await fileSystem.WriteAllBytesAsync(destination, await fileSystem.ReadAllBytesAsync(path));
            else if (await LoadAsync(profile) is { } config)
                await fileSystem.WriteAllTextAsync(destination, JsonSerializer.Serialize(config, ConfigFileConstants.JsonOptions));
            else
                return false;
            return true;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Could not export profile '{profile.Name}': {ex.Message}");
            return false;
        }
    }

    public async Task DeleteAsync(SettingsProfile profile)
    {
        if (profile.FilePath is not { } path)
            return;
        fileSystem.DeleteFile(path);
        if (TrustKey() is { } key)
            windowsRegistry.DeleteValue(key, profile.Id);
        if (profile.Id == ActiveProfileId)
            await preferences.SetPreferenceAsync(UserPreferenceKeys.ActiveProfile, string.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task<ProfileSwitchResult?> SwitchAsync(SettingsProfile profile) => ApplyAsync(profile, reapply: false);

    public Task<ProfileSwitchResult?> ReapplyAsync(SettingsProfile profile) => ApplyAsync(profile, reapply: true);

    // Read-only and shown beside the profile list, so a profile file that cannot be read shows no drift rather than
    // failing the list.
    public async Task<IReadOnlyList<string>> DriftAsync()
    {
        try
        {
            if (ActiveProfileId is not { } id || Find(id) is not { } active || await LoadAsync(active) is not { } loaded)
                return [];
            var plan = await PlanAsync(configLoad.FilterConfigForCurrentSystem(loaded));
            var comparison = await snapshot.CompareAsync(plan.Where(p => p.Choice is not null).Select(p => p.Choice!).ToList(), Carried(plan), CatalogScope.CurrentMachine);
            return plan
                .Where(p => p.Choice is not null && comparison.GetValueOrDefault(p.Choice.SettingId) == SettingMatch.Differs)
                .Select(p => NameOf(p.Setting!))
                .ToList();
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Could not check the active profile for drift: {ex.Message}");
            return [];
        }
    }

    private async Task<ProfileSwitchResult?> ApplyAsync(SettingsProfile profile, bool reapply)
    {
        if (!await _gate.WaitAsync(0))
            return null;
        IsBusy = true;
        Changed?.Invoke(this, EventArgs.Empty);
        ProfileSwitchResult result;
        var usable = true;
        string? appliedHash = null;
        try
        {
            (result, usable, appliedHash) = await ApplyCoreAsync(profile, reapply);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Profile '{profile.Name}' failed: {ex.Message}");
            result = new ProfileSwitchResult(false, [ex.Message], []);
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }

        if (usable)
        {
            await preferences.SetPreferenceAsync(UserPreferenceKeys.ActiveProfile, profile.Id);
            if (!reapply && appliedHash is not null)
                RecordTrust(profile, appliedHash);
        }

        await preferences.SetPreferenceAsync(UserPreferenceKeys.ActiveProfileFailed, !result.Success);
        Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    // Only a setting this PC positively reads as already set is skipped; anything Winhance cannot be sure of is
    // applied. The second read afterwards is what makes a skip or a write that did not take show up as a failure.
    // Usable is false when the profile could not be loaded or trusted, so it must not become the active one.
    // A saved file is read once: the bytes that are hashed are the bytes that are parsed and applied, so nothing
    // swapped in between can pass the check or get its own hash recorded.
    private async Task<(ProfileSwitchResult Result, bool Usable, string? Hash)> ApplyCoreAsync(SettingsProfile profile, bool reapply)
    {
        string? hash = null;
        WinhanceConfigFile? loaded;
        if (profile.Kind == ProfileKind.User)
        {
            var bytes = await ReadUserAsync(profile);
            hash = bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes));
            if (reapply && !IsTrusted(profile, hash))
            {
                logService.Log(LogLevel.Warning, $"Profile '{profile.Name}' was not re-applied: its file is not the one Winhance last applied or saved. Switch to it again to trust it.");
                return (new ProfileSwitchResult(false, [localization.GetString("Profiles_Error_Untrusted")], []), false, null);
            }
            loaded = Parse(profile, bytes);
        }
        else
            loaded = await LoadAsync(profile);

        if (loaded is null)
            return (new ProfileSwitchResult(false, [localization.GetString("Profiles_Error_Load")], []), false, null);

        var config = configLoad.FilterConfigForCurrentSystem(loaded);
        var plan = await PlanAsync(config);
        var choices = plan.Where(p => p.Choice is not null).Select(p => p.Choice!).ToList();
        var carried = Carried(plan);
        var before = await snapshot.CompareAsync(choices, carried, CatalogScope.CurrentMachine);
        var changing = plan
            .Where(p => p.Choice is null || before.GetValueOrDefault(p.Choice.SettingId, SettingMatch.Unknown) != SettingMatch.Matches)
            .ToList();
        logService.Log(LogLevel.Info, $"Profile '{profile.Name}': {changing.Count} of {plan.Count} settings to apply");

        if (!reapply && profile.Id != ActiveProfileId)
            await configuration.CreateUserBackupConfigAsync();

        var notApplied = new List<string>();
        IReadOnlyDictionary<string, SettingMatch> after = new Dictionary<string, SettingMatch>();
        if (changing.Count > 0)
        {
            notApplied.AddRange(await execution.ApplySettingsQuietlyAsync(
                Subset(config, changing),
                profile.Name,
                profile.Kind == ProfileKind.WindowsDefaults,
                restartExplorer: changing.Any(p => RestartTarget.IsExplorer(p.Setting?.Apply.Restart)),
                broadcastRegional: changing.Any(p => RestartTarget.IsRegional(p.Setting?.Apply.Restart))));

            var applied = changing.Where(p => p.Choice is not null).ToList();
            after = await snapshot.CompareAsync(applied.Select(p => p.Choice!).ToList(), carried, CatalogScope.CurrentMachine);
            var didNotTake = applied.Where(p => after.GetValueOrDefault(p.Choice!.SettingId) == SettingMatch.Differs).Select(p => NameOf(p.Setting!)).ToList();
            if (didNotTake.Count > 0)
                logService.Log(LogLevel.Warning, $"Profile '{profile.Name}': still different after applying: {string.Join(", ", didNotTake)}");
            notApplied.AddRange(didNotTake);
        }

        // A re-apply asks for a restart only for a setting it saw change: one that read differently, or one it could not
        // read until the write. A setting it cannot read at all would otherwise ask at every sign-in.
        var needsRestart = changing
            .Where(p => p.Setting?.Apply.RequiresReboot == true && (!reapply || Changed(p.Choice?.SettingId ?? string.Empty)))
            .Select(p => NameOf(p.Setting!))
            .ToList();
        _pendingReboot = _pendingReboot.Union(needsRestart).ToList();
        return (new ProfileSwitchResult(notApplied.Count == 0, notApplied, needsRestart), true, hash);

        bool Changed(string id) => before.GetValueOrDefault(id, SettingMatch.Unknown) switch
        {
            SettingMatch.Differs => true,
            SettingMatch.Unknown => after.GetValueOrDefault(id, SettingMatch.Unknown) == SettingMatch.Matches,
            _ => false,
        };
    }

    private sealed record PlannedItem(string Group, string Feature, ConfigSection Section, ConfigurationItem Item, Setting? Setting, SettingChoice? Choice);

    // An Action is a one-off command with no state to compare, so a profile never runs one. An item whose setting
    // or value cannot be resolved here is still passed on, for the import path to apply or report.
    private async Task<List<PlannedItem>> PlanAsync(WinhanceConfigFile config)
    {
        await registry.InitializeAsync();
        var plan = new List<PlannedItem>();
        foreach (var (group, feature, section, items) in config.SettingSections())
        {
            foreach (var item in items)
            {
                if (item.Id is not { Length: > 0 })
                    continue;
                var setting = registry.GetById(SettingIdAliases.Normalize(item.Id), CatalogScope.CurrentMachine);
                if (setting?.Control == ControlKind.Action)
                    continue;
                var choice = setting is not null && ConfigFileMapper.DecodeValue(setting, item) is { } value
                    ? new SettingChoice(setting.Id, value)
                    : null;
                plan.Add(new PlannedItem(group, feature, section, item, setting, choice));
            }
        }
        return plan;
    }

    private static List<CarriedFile> Carried(IEnumerable<PlannedItem> plan) =>
        plan.Where(p => p.Setting is not null && p.Item.File is { Base64.Length: > 0 })
            .Select(p => new CarriedFile(p.Setting!.Id, p.Item.File!.Destination, p.Item.File.Base64))
            .ToList();

    private static WinhanceConfigFile Subset(WinhanceConfigFile config, IReadOnlyList<PlannedItem> items)
    {
        FeatureGroupSection Group(string group) => new()
        {
            IsIncluded = true,
            Features = items.Where(p => p.Group == group)
                .GroupBy(p => p.Feature)
                .ToDictionary(g => g.Key, g => new ConfigSection { IsIncluded = g.First().Section.IsIncluded, Items = g.Select(p => p.Item).ToList() }),
        };
        return new WinhanceConfigFile { Version = config.Version, Optimize = Group("Optimize"), Customize = Group("Customize") };
    }

    private static WinhanceConfigFile SettingsOnly(WinhanceConfigFile config) => new()
    {
        Version = config.Version,
        CreatedAt = config.CreatedAt,
        ComputerName = config.ComputerName,
        Optimize = config.Optimize,
        Customize = config.Customize,
    };

    public async Task<WinhanceConfigFile?> LoadAsync(SettingsProfile profile) => profile.Kind == ProfileKind.User
        ? Parse(profile, await ReadUserAsync(profile))
        : await configLoad.ReadEmbeddedConfigurationAsync(windowsDefaults: profile.Kind == ProfileKind.WindowsDefaults) is { } config
            ? SettingsOnly(config)
            : null;

    private WinhanceConfigFile? Parse(SettingsProfile profile, byte[]? bytes) =>
        bytes is not null && configLoad.ParseConfiguration(bytes, profile.FilePath!) is { } config ? SettingsOnly(config) : null;

    private async Task<byte[]?> ReadUserAsync(SettingsProfile profile)
    {
        try
        {
            return await fileSystem.ReadAllBytesAsync(profile.FilePath!);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Could not read profile '{profile.Name}': {ex.Message}");
            return null;
        }
    }

    private string PathFor(string name) => fileSystem.CombinePath(Folder, name + ConfigFileConstants.FileExtension);

    private string NameOf(Setting setting) => localization.SettingName(setting);

    private async Task<SettingsProfile?> WriteAsync(string name, WinhanceConfigFile file)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(file, ConfigFileConstants.JsonOptions);
        await fileSystem.WriteAllBytesAsync(PathFor(name.Trim()), bytes);
        return Written(name, bytes);
    }

    private SettingsProfile? Written(string name, byte[] bytes)
    {
        var profile = Find(name.Trim());
        if (profile is not null && profile.Id == ActiveProfileId)
            RecordTrust(profile, Convert.ToHexString(SHA256.HashData(bytes)));
        Changed?.Invoke(this, EventArgs.Empty);
        return profile;
    }

    private void RecordTrust(SettingsProfile profile, string hash)
    {
        if (TrustKey() is not { } key || !windowsRegistry.SetValue(key, profile.Id, hash, RegistryValueKind.String))
            logService.Log(LogLevel.Warning, $"Could not record the integrity hash for profile '{profile.Name}'; the sign-in re-apply will skip it");
    }

    private bool IsTrusted(SettingsProfile profile, string? hash) =>
        hash is not null
        && TrustKey() is { } key
        && windowsRegistry.GetValue(key, profile.Id) is string recorded
        && string.Equals(recorded, hash, StringComparison.OrdinalIgnoreCase);

    // Null when no user can be named, which leaves every saved profile untrusted.
    private string? TrustKey()
    {
        var sid = interactiveUser.InteractiveUserSid;
        if (sid is null)
        {
            using var identity = WindowsIdentity.GetCurrent();
            sid = identity.User?.Value;
        }
        return sid is null ? null : $@"{TrustRoot}\{sid}";
    }
}
