using System.Text.Json;
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
    IConfigurationService configuration) : IProfileService
{
    internal const string FocusId = "builtin:focus";
    internal const string EverydayId = "builtin:everyday";
    internal const string BeforeWinhanceId = "builtin:before-winhance";

    private const string FocusFile = "focus";
    private const string EverydayFile = "everyday";
    private const string EverydayDefaultsFile = "everyday-defaults";
    private const string BeforeWinhanceFile = "before-winhance";

    public const int MaxNameLength = 60;

    private readonly SemaphoreSlim _gate = new(1, 1);
    // LoadAsync builds the built-ins outside _gate, so two writes of the same file take turns here instead.
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private IReadOnlyList<string> _pendingReboot = [];

    public event EventHandler? Changed;

    public bool IsBusy { get; private set; }

    public string? ActiveProfileId => ActiveProfileIdIn(preferences);

    public bool LastSwitchFailed => LastSwitchFailedIn(preferences);

    public IReadOnlyList<string> PendingReboot => _pendingReboot;

    private string Folder => FolderFor(interactiveUser, fileSystem);

    // A subfolder, so the user's own profiles listed from Folder never include these.
    private string BuiltinFolder => BuiltinFolderIn(fileSystem, Folder);

    public IReadOnlyList<SettingsProfile> GetProfiles() => List(localization, fileSystem, Folder);

    // The light tray host reads the active profile without the apply engine this service is built on.
    internal static string? ActiveProfileIdIn(IUserPreferencesService preferences) =>
        preferences.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty) is { Length: > 0 } id ? id : null;

    internal static bool LastSwitchFailedIn(IUserPreferencesService preferences) => preferences.GetPreference(UserPreferenceKeys.ActiveProfileFailed, false);

    // The light tray host lists profiles without the apply engine this service is built on.
    internal static string FolderFor(IInteractiveUserService interactiveUser, IFileSystemService fileSystem) => fileSystem.CombinePath(
        AppDataPaths.UserRoot(interactiveUser.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData)),
        "Profiles");

    private static string BuiltinFolderIn(IFileSystemService fileSystem, string folder) => fileSystem.CombinePath(folder, "Builtin");

    // Everyday names its file even before the first switch into Focus writes it, so it is a defaults profile until then.
    internal static IReadOnlyList<SettingsProfile> List(ILocalizationService localization, IFileSystemService fileSystem, string folder)
    {
        var builtin = BuiltinFolderIn(fileSystem, folder);
        var profiles = new List<SettingsProfile>
        {
            new(FocusId, localization.GetString("Profiles_Builtin_Focus"), ProfileKind.Focus, null,
                localization.GetString("Profiles_Builtin_Focus_Description")),
            new(EverydayId, localization.GetString("Profiles_Builtin_Everyday"), ProfileKind.Everyday,
                fileSystem.CombinePath(builtin, EverydayFile + ConfigFileConstants.FileExtension),
                localization.GetString("Profiles_Builtin_Everyday_Description")),
        };
        var beforeWinhance = fileSystem.CombinePath(builtin, BeforeWinhanceFile + ConfigFileConstants.FileExtension);
        if (fileSystem.FileExists(beforeWinhance))
        {
            profiles.Add(new(BeforeWinhanceId, localization.GetString("Profiles_Builtin_BeforeWinhance"), ProfileKind.BeforeWinhance,
                beforeWinhance, localization.GetString("Profiles_Builtin_BeforeWinhance_Description")));
        }
        if (fileSystem.DirectoryExists(folder))
        {
            profiles.AddRange(fileSystem.GetFiles(folder, ConfigFileConstants.FilePattern)
                .Select(path => fileSystem.GetFileNameWithoutExtension(path))
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .Select(name => new SettingsProfile(name, name, ProfileKind.User, fileSystem.CombinePath(folder, name + ConfigFileConstants.FileExtension))));
        }
        return profiles;
    }

    public SettingsProfile? Find(string nameOrId) =>
        GetProfiles().FirstOrDefault(p =>
            string.Equals(p.Id, nameOrId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Name, nameOrId, StringComparison.CurrentCultureIgnoreCase)
            || string.Equals(p.Id, "builtin:" + nameOrId, StringComparison.OrdinalIgnoreCase));

    // A stored id names one profile; Find would also match a user profile named like a built-in's localized name.
    public SettingsProfile? FindById(string id) =>
        GetProfiles().FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

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
            await WriteChoicesAsync(settings, PathFor(name.Trim()));
            return Written(name);
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
            if (HasFile(profile))
                await fileSystem.WriteAllBytesAsync(destination, await fileSystem.ReadAllBytesAsync(profile.FilePath!));
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
        if (profile.Kind != ProfileKind.User || profile.FilePath is not { } path)
            return;
        fileSystem.DeleteFile(path);
        if (profile.Id == ActiveProfileId)
            await preferences.SetPreferenceAsync(UserPreferenceKeys.ActiveProfile, string.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Before Winhance is the machine as the oldest backup found it, which on a first launch is the one just taken.
    // Built once, and made active only when no profile was ever chosen: an id this version no longer has stays as it
    // is, so a sign-in reapply cannot roll the machine back to the backup unasked.
    public async Task EnsureBeforeWinhanceAsync()
    {
        try
        {
            var path = BuiltinPath(BeforeWinhanceFile);
            if (fileSystem.FileExists(path))
                return;
            var backups = ConfigurationService.BackupFolderFor(interactiveUser, fileSystem);
            if (!fileSystem.DirectoryExists(backups)
                || fileSystem.GetFiles(backups, "UserBackup_*" + ConfigFileConstants.FileExtension).Order(StringComparer.Ordinal).FirstOrDefault() is not { } oldest
                || configLoad.ParseConfiguration(await fileSystem.ReadAllBytesAsync(oldest), oldest) is not { } backup)
            {
                logService.Log(LogLevel.Info, "No backup to build the Before Winhance profile from yet");
                return;
            }

            fileSystem.CreateDirectory(BuiltinFolder);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(SettingsOnly(backup), ConfigFileConstants.JsonOptions);
            await fileSystem.WriteAllBytesAsync(path, bytes);
            if (ActiveProfileId is null)
            {
                await preferences.SetPreferenceAsync(UserPreferenceKeys.ActiveProfile, BeforeWinhanceId);
                await preferences.SetPreferenceAsync(UserPreferenceKeys.ActiveProfileFailed, false);
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Could not build the Before Winhance profile: {ex.Message}");
        }
    }

    public Task<ProfileSwitchResult?> SwitchAsync(SettingsProfile profile) => ApplyAsync(profile, reapply: false);

    public Task<ProfileSwitchResult?> ReapplyAsync(SettingsProfile profile) => ApplyAsync(profile, reapply: true);

    // Read-only and shown beside the profile list, so a profile file that cannot be read shows no drift rather than
    // failing the list.
    public async Task<IReadOnlyList<string>> DriftAsync()
    {
        try
        {
            if (ActiveProfileId is not { } id || FindById(id) is not { } active || await LoadAsync(active) is not { } loaded)
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
        try
        {
            (result, usable) = await ApplyCoreAsync(profile, reapply);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Profile '{profile.Name}' failed: {ex.Message}");
            result = new ProfileSwitchResult(false, [ex.Message], []);
            usable = false;
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }

        if (usable)
            await preferences.SetPreferenceAsync(UserPreferenceKeys.ActiveProfile, profile.Id);

        await preferences.SetPreferenceAsync(UserPreferenceKeys.ActiveProfileFailed, !result.Success);
        Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    // Only a setting this PC positively reads as already set is skipped; anything Winhance cannot be sure of is
    // applied. The second read afterwards is what makes a skip or a write that did not take show up as a failure.
    // Usable is false when the profile could not be loaded, so it must not become the active one.
    private async Task<(ProfileSwitchResult Result, bool Usable)> ApplyCoreAsync(SettingsProfile profile, bool reapply)
    {
        var loaded = !HasFile(profile) && profile.Kind == ProfileKind.Focus
            ? await FocusAsync(snapshotEveryday: !reapply && ActiveProfileId != FocusId && (!LastSwitchFailed || !fileSystem.FileExists(BuiltinPath(EverydayFile))))
            : await LoadAsync(profile);

        if (loaded is null)
            return (new ProfileSwitchResult(false, [localization.GetString("Profiles_Error_Load")], []), false);

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
                isWindowsDefaults: false,
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
        return (new ProfileSwitchResult(notApplied.Count == 0, notApplied, needsRestart), true);

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
                var setting = registry.GetById(item.Id, CatalogScope.CurrentMachine);
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

    public async Task<WinhanceConfigFile?> LoadAsync(SettingsProfile profile) =>
        HasFile(profile) ? Parse(await ReadFileAsync(profile), profile.FilePath!)
        : profile.Kind switch
        {
            ProfileKind.Focus => await FocusAsync(snapshotEveryday: false),
            ProfileKind.Everyday => await EverydayDefaultsAsync(),
            _ => null,
        };

    // A built-in without its file yet is made from data at each use.
    private bool HasFile(SettingsProfile profile) =>
        profile.Kind == ProfileKind.User || (profile.FilePath is { } path && fileSystem.FileExists(path));

    // Made from the machine at each use, so the power settings keep the DC this PC reads now. The values Focus is about
    // to change are kept as Everyday when the switch comes from another profile, so Everyday puts them back. After a
    // failed switch the machine may still hold Focus values, so the snapshot already on disk is kept.
    private async Task<WinhanceConfigFile?> FocusAsync(bool snapshotEveryday)
    {
        await registry.InitializeAsync();
        var current = (await snapshot.CaptureAsync(CatalogScope.CurrentMachine))
            .Where(c => FocusProfile.Ids.Contains(c.SettingId))
            .ToList();
        fileSystem.CreateDirectory(BuiltinFolder);
        if (snapshotEveryday)
            await WriteChoicesAsync(current, BuiltinPath(EverydayFile));
        var path = BuiltinPath(FocusFile);
        return Parse(await WriteChoicesAsync(FocusProfile.Overlay(current), path), path);
    }

    // Everyday before the first switch into Focus: Windows' defaults for what Focus changes. Written to its own file so
    // Everyday stays a defaults profile until there is a snapshot to go back to.
    private async Task<WinhanceConfigFile?> EverydayDefaultsAsync()
    {
        if (await configLoad.ReadEmbeddedConfigurationAsync(windowsDefaults: true) is not { } defaults)
            return null;
        await registry.InitializeAsync();
        var current = await snapshot.CaptureAsync(CatalogScope.CurrentMachine);
        var choices = new List<SettingChoice>();
        foreach (var (_, _, _, items) in defaults.SettingSections())
        {
            foreach (var item in items)
            {
                if (item.Id is { } id && FocusProfile.Ids.Contains(id)
                    && registry.GetById(id, CatalogScope.CurrentMachine) is { } setting
                    && ConfigFileMapper.DecodeValue(setting, item) is { } value
                    && FocusProfile.KeepMachineDc(id, value, current.FirstOrDefault(c => c.SettingId == id)?.Value) is { } kept)
                    choices.Add(new SettingChoice(id, kept));
            }
        }
        fileSystem.CreateDirectory(BuiltinFolder);
        var path = BuiltinPath(EverydayDefaultsFile);
        return Parse(await WriteChoicesAsync(choices, path), path);
    }

    // Builder hands over its answer-file choices too; a profile is applied to this PC, so it keeps settings only.
    private async Task<byte[]> WriteChoicesAsync(IReadOnlyList<SettingChoice> settings, string path)
    {
        var kept = settings.Where(c => registry.GetFeatureIdForSetting(c.SettingId) is { } feature
            && FeatureDefinitions.IsSettingsFeature(feature)).ToList();
        var set = await fileStore.LoadAsync(new SelectionSet(kept, [], []));
        await _writeGate.WaitAsync();
        try
        {
            return await configFiles.WriteAsync(set, scopes.Current, path);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private WinhanceConfigFile? Parse(byte[]? bytes, string source) =>
        bytes is not null && configLoad.ParseConfiguration(bytes, source) is { } config ? SettingsOnly(config) : null;

    private async Task<byte[]?> ReadFileAsync(SettingsProfile profile)
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

    private string BuiltinPath(string name) => fileSystem.CombinePath(BuiltinFolder, name + ConfigFileConstants.FileExtension);

    private string NameOf(Setting setting) => localization.SettingName(setting);

    private async Task<SettingsProfile?> WriteAsync(string name, WinhanceConfigFile file)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(file, ConfigFileConstants.JsonOptions);
        await fileSystem.WriteAllBytesAsync(PathFor(name.Trim()), bytes);
        return Written(name);
    }

    private SettingsProfile? Written(string name)
    {
        var profile = FindById(name.Trim());
        Changed?.Invoke(this, EventArgs.Empty);
        return profile;
    }
}
