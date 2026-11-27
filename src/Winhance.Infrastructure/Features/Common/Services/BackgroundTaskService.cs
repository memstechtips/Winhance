using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.Common.Services;

internal partial class BackgroundTaskService(
    ILogService logService,
    IScheduledTaskService scheduledTaskService,
    IScheduledTaskStateService scheduledTaskState,
    IWindowsRegistryService registry,
    IInstalledAppsService installedAppsService,
    IBloatRemovalService bloatRemovalService,
    IFileSystemService fileSystem,
    IInteractiveUserService interactiveUser,
    string? lockRoot = null,
    string? exePath = null,
    bool? isPortable = null) : IBackgroundTaskService
{
    private readonly string _lockRoot = lockRoot ?? FolderLock.DefaultRoot;
    private readonly string? _exePath = exePath ?? Environment.ProcessPath;
    private readonly bool _isPortable = isPortable ?? AppDataPaths.IsPortable;

    internal const string KeptKey = KeepRemovedModeStore.Key;
    internal const string StatusKey = KeptKey + @"\Status";

    private const string ReturnCountSuffix = ".ReturnCount";

    private const string LegacyOneNoteTask = "OneNoteRemoval";
    private const string LegacyPerAppTaskPrefix = "Remove_";

    [GeneratedRegex(@"ReadAllText\('(?<path>(?:[^']|'')*)'\)")]
    private static partial Regex ScriptPathInArguments();

    private static readonly Lazy<Dictionary<string, ItemDefinition>> Catalog = new(() =>
        ScriptMigrationService.CatalogItems.Value
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase));

    // A script lists package, capability and feature names; the card shows the catalog name for each.
    private static readonly Lazy<Dictionary<string, string>> DisplayNames = new(() =>
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Catalog.Value.Values)
        {
            foreach (var name in ScriptMigrationService.ItemNames(item))
                names.TryAdd(name, item.Name);
        }

        return names;
    });

    public event EventHandler? Maintained;

    public KeepRemovedMode GetMode() => KeepRemovedModeStore.Read(registry, fileSystem);

    private bool IsFolderSecure()
    {
        try
        {
            return FolderLock.Run(_lockRoot, apply: false, logService) == 0;
        }
        catch (Exception ex)
        {
            logService.LogDebug($"Could not read the Winhance folder permissions: {ex.Message}");
            return false;
        }
    }

    private void SecureFolder()
    {
        if (!FolderLock.IsElevated())
            return;

        try
        {
            Directory.CreateDirectory(_lockRoot);
            FolderLock.Run(_lockRoot, apply: true, logService);
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Could not lock the Winhance folder: {ex.Message}");
        }
    }

    public IReadOnlyList<KeptApp> GetKeptApps() =>
        KeptIds()
            .Select(id => new KeptApp(
                id,
                Catalog.Value.TryGetValue(id, out var item) ? item.Name : id,
                ParseDate(registry.GetValue(KeptKey, id) as string),
                registry.GetValue(StatusKey, id + ReturnCountSuffix) is int count ? count : 0,
                Catalog.Value.TryGetValue(id, out var kept) ? CategoryOf(kept) : BloatRemovalScriptGenerator.ScriptName))
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public KeepRemovedStatus GetStatus() => new(
        ParseDate(registry.GetValue(StatusKey, "LastCheck") as string),
        registry.GetValue(StatusKey, "LastResult") as string ?? "",
        (registry.GetValue(StatusKey, "RemovedAgain") as string ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        ParseDate(registry.GetValue(StatusKey, "LastRemovedAgain") as string));

    public async Task<OperationResult> AddKeptAppsAsync(IEnumerable<string> appIds)
    {
        var recorded = RecordKeptApps(appIds);
        if (!recorded.Success)
            return recorded;

        SetEnabled(true);
        await SyncSignInTaskAsync().ConfigureAwait(false);
        return OperationResult.Succeeded();
    }

    public void RemoveKeptApp(string appId)
    {
        registry.DeleteValue(KeptKey, appId);
        registry.DeleteValue(StatusKey, appId + ReturnCountSuffix);
    }

    public async Task<bool> IsKeepRemovedOnAsync() =>
        GetMode() == KeepRemovedMode.Winhance
            ? IsWinhanceEnabled()
            : await HasScriptsAsync().ConfigureAwait(false) || (!KeptIds().Any() && IsWinhanceEnabled());

    public async Task<OperationResult> SetKeepRemovedOnAsync(bool on)
    {
        if (GetMode() == KeepRemovedMode.Winhance)
        {
            SetEnabled(on);
            await SyncSignInTaskAsync().ConfigureAwait(false);
            return OperationResult.Succeeded();
        }

        var result = on
            ? await WriteScriptsFromKeptListAsync().ConfigureAwait(false)
            : await ImportAndDeleteScriptsAsync().ConfigureAwait(false);
        if (result.Success)
            SetEnabled(on);

        return result;
    }

    public async Task<OperationResult> SetModeAsync(KeepRemovedMode mode)
    {
        if (GetMode() == mode)
            return OperationResult.Succeeded();

        var hadScripts = false;
        OperationResult converted;
        if (mode == KeepRemovedMode.Scripts)
        {
            converted = IsWinhanceEnabled()
                ? await WriteScriptsFromKeptListAsync().ConfigureAwait(false)
                : OperationResult.Succeeded();
        }
        else
        {
            hadScripts = await HasScriptsAsync().ConfigureAwait(false);
            converted = await ImportAndDeleteScriptsAsync().ConfigureAwait(false);
        }

        if (!converted.Success)
            return converted;

        if (hadScripts)
            SetEnabled(true);

        if (!registry.SetValue(KeptKey, KeepRemovedModeStore.ModeValue, mode.ToString(), RegistryValueKind.String))
            return OperationResult.Failed("Windows refused the write. Run Winhance as administrator.");

        logService.LogInformation($"Keep removed apps now works in {mode} mode");
        await SyncSignInTaskAsync().ConfigureAwait(false);
        return OperationResult.Succeeded();
    }

    public async Task<IReadOnlyList<RemovalTaskState>> GetRemovalTasksAsync()
    {
        var tasks = await scheduledTaskService.GetWinhanceTasksAsync().ConfigureAwait(false);
        var states = new List<RemovalTaskState>();
        foreach (var name in KeepRemovedModeStore.RemovalTaskNames)
        {
            var task = tasks.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var scriptPath = RegisteredScriptPath(task) ?? KeepRemovedModeStore.ScriptPath(fileSystem, name);
            var scriptExists = fileSystem.FileExists(scriptPath);
            if (task is null && !scriptExists)
                continue;

            var content = scriptExists ? await fileSystem.ReadAllTextAsync(scriptPath).ConfigureAwait(false) : string.Empty;
            states.Add(new RemovalTaskState(name, task?.Enabled, scriptExists, scriptPath, task?.LastRun, task?.LastResult, ItemsOf(name, content)));
        }

        return states;
    }

    public async Task<OperationResult> RepairRemovalTaskAsync(string name)
    {
        if (!IsFolderSecure())
            return OperationResult.Failed("The Winhance script folder is not protected from non-administrators.");

        var state = (await GetRemovalTasksAsync().ConfigureAwait(false))
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (state is null)
            return OperationResult.Failed($"There is no removal task called '{name}'.");

        var items = state.Health == RemovalTaskHealth.ScriptMissing
            ? ResolveKeptItems().Where(item => BelongsTo(item, name)).ToList()
            : Catalog.Value.Values.Where(item => item.RemovalScriptName == name).Take(1).ToList();
        if (state.Health == RemovalTaskHealth.ScriptMissing)
        {
            if (items.Count == 0)
                return OperationResult.Failed("The kept list has nothing to rebuild this script from.");

            await bloatRemovalService.WriteRemovalScriptsAsync(items).ConfigureAwait(false);
        }

        await bloatRemovalService.PersistRemovalScriptsAsync(items).ConfigureAwait(false);
        var repaired = (await GetRemovalTasksAsync().ConfigureAwait(false))
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return repaired?.Health == RemovalTaskHealth.Active
            ? OperationResult.Succeeded()
            : OperationResult.Failed("The removal task could not be repaired.");
    }

    public async Task<OperationResult> RemoveRemovalTaskAsync(string name)
    {
        if (!KeepRemovedModeStore.RemovalTaskNames.Contains(name, StringComparer.Ordinal))
            return OperationResult.Failed($"'{name}' is not a Winhance removal task.");

        var scriptPath = KeepRemovedModeStore.ScriptPath(fileSystem, name);
        var content = fileSystem.FileExists(scriptPath)
            ? await fileSystem.ReadAllTextAsync(scriptPath).ConfigureAwait(false)
            : string.Empty;
        var result = await DeleteTaskAndScriptAsync(name).ConfigureAwait(false);
        if (!result.Success)
            return result;

        var ids = ScriptMigrationService.ResolveKeptIds(name, content)
            .Concat(ResolveKeptItems().Where(item => BelongsTo(item, name)).Select(item => item.Id));
        foreach (var id in ids.Distinct(StringComparer.OrdinalIgnoreCase))
            RemoveKeptApp(id);

        return result;
    }

    public async Task<bool> IsTaskSafetyOkAsync()
    {
        if (!IsFolderSecure())
            return false;

        if (GetMode() != KeepRemovedMode.Scripts)
            return true;

        return (await scheduledTaskService.GetWinhanceTasksAsync().ConfigureAwait(false))
            .Where(task => KeepRemovedModeStore.RemovalTaskNames.Contains(task.Name, StringComparer.OrdinalIgnoreCase))
            .All(task => string.Equals(RegisteredScriptPath(task),
                KeepRemovedModeStore.ScriptPath(fileSystem, task.Name), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<OperationResult> RepairTaskSafetyAsync()
    {
        SecureFolder();
        if (!IsFolderSecure())
            return OperationResult.Failed("The Winhance script folder could not be protected from non-administrators.");

        // Locking existing files does not prove their contents are safe to execute. Do not
        // re-register scripts from an installation that just failed a safety check.
        return await IsTaskSafetyOkAsync().ConfigureAwait(false)
            ? OperationResult.Succeeded()
            : OperationResult.Failed("The Winhance background tasks could not be made safe.");
    }

    public async Task<KeepRemovedStatus> MaintainAsync(CancellationToken ct = default)
    {
        if (GetMode() != KeepRemovedMode.Winhance || !IsWinhanceEnabled())
            return GetStatus();

        FileStream? gate = TryTakeGate();
        if (gate is null)
        {
            logService.LogInformation("Keep removed apps: another check is already running");
            return GetStatus();
        }

        using (gate)
        {
            SecureFolder();

            var kept = ResolveKeptItems();
            var back = new List<ItemDefinition>();
            if (kept.Count > 0)
            {
                installedAppsService.Invalidate();
                var scan = await installedAppsService.GetInstalledAppsAsync(kept, StatusRules.Windows, ct).ConfigureAwait(false);
                back = kept.Where(item => scan.Status.GetValueOrDefault(item.Id)).ToList();
            }

            var failed = back.Count > 0 && await RemoveAsync(back, ct).ConfigureAwait(false);
            var result = kept.Count == 0 ? KeepRemovedResult.NoApps
                : back.Count == 0 ? KeepRemovedResult.AllStillRemoved
                : failed ? KeepRemovedResult.RemovalFailed
                : KeepRemovedResult.RemovedAgain;

            WriteStatus(result, result == KeepRemovedResult.RemovedAgain ? back : []);
            logService.LogInformation($"Keep removed apps: {result} ({back.Count} came back)");
        }

        Maintained?.Invoke(this, EventArgs.Empty);
        return GetStatus();
    }

    public async Task<SignInOptions> GetSignInOptionsAsync()
    {
        var tasks = await scheduledTaskService.GetWinhanceTasksAsync().ConfigureAwait(false);
        var options = SignInOptions.None;
        foreach (var task in tasks.Where(t => IsSignInTaskName(t.Name)))
        {
            if (!await scheduledTaskService.IsSignInTaskRegisteredForAsync(task.Name, interactiveUser.InteractiveUserSid).ConfigureAwait(false))
                continue;

            var flags = task.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            options = options with
            {
                StartInTray = options.StartInTray || task.Name == SignInTask.LegacyTrayName || flags.Contains(SignInTask.TrayFlag),
                Reapply = options.Reapply || task.Name == SignInTask.LegacyReapplyName || flags.Contains(SignInTask.ReapplyFlag),
                Maintain = options.Maintain || flags.Contains(SignInTask.MaintainFlag),
            };
        }

        return options;
    }

    public async Task<OperationResult> SetSignInOptionAsync(string flag, bool on)
    {
        var current = await GetSignInOptionsAsync().ConfigureAwait(false);
        return await ApplySignInTaskAsync(
            flag == SignInTask.TrayFlag ? on : current.StartInTray,
            flag == SignInTask.ReapplyFlag ? on : current.Reapply).ConfigureAwait(false);
    }

    public async Task InitializeAsync()
    {
        try
        {
            SecureFolder();

            if (await scheduledTaskService.IsTaskRegisteredAsync(SignInTask.LegacyMaintainName).ConfigureAwait(false))
            {
                await scheduledTaskService.UnregisterScheduledTaskAsync(SignInTask.LegacyMaintainName).ConfigureAwait(false);
                logService.LogInformation("Deleted the old Keep removed apps SYSTEM task");
            }

            // An older Winhance run on the same PC can add scripts after Winhance mode was chosen. The mode follows what is
            // there, so the page shows them and turning keeping off or switching to Winhance mode deletes them.
            var scripts = await HasScriptsAsync().ConfigureAwait(false);
            if (registry.GetValue(KeptKey, KeepRemovedModeStore.ModeValue) is not string
                || (scripts && GetMode() == KeepRemovedMode.Winhance))
            {
                if (scripts)
                    RecordKeptApps(await ScriptKeptIdsAsync().ConfigureAwait(false));

                var mode = scripts ? KeepRemovedMode.Scripts : KeepRemovedMode.Winhance;
                registry.SetValue(KeptKey, KeepRemovedModeStore.ModeValue, mode.ToString(), RegistryValueKind.String);
            }

            await FoldOldSignInTasksAsync().ConfigureAwait(false);
            await SyncSignInTaskAsync().ConfigureAwait(false);
            await EnsureEdgeRedirectRepairAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Background tasks start-up check failed: {ex.Message}");
        }
    }

    public async Task<RemovalTaskState?> GetEdgeRedirectAsync()
    {
        var tasks = await scheduledTaskService.GetWinhanceTasksAsync().ConfigureAwait(false);
        var task = tasks.FirstOrDefault(t => t.Name.Equals(ScriptPaths.OpenWebSearchTaskName, StringComparison.OrdinalIgnoreCase));
        var script = EdgeRedirectRepairScriptPath();
        var scriptExists = fileSystem.FileExists(script);
        return task is null && !scriptExists && !HasManagedEdgeRedirect()
            ? null
            : new RemovalTaskState(ScriptPaths.OpenWebSearchTaskName, task?.Enabled, scriptExists, script, task?.LastRun, task?.LastResult, []);
    }

    private bool HasManagedEdgeRedirect()
    {
        var folder = ScriptPaths.OpenWebSearchDirectoryLiteral;
        var stub = fileSystem.CombinePath(folder, EdgeRemovalScript.RedirectStubName);
        return registry.GetValue(EdgeRemovalScript.RedirectFilterKey, "FilterFullPath") is string target
            && stub.Equals(target, StringComparison.OrdinalIgnoreCase)
            && fileSystem.FileExists(stub)
            && fileSystem.FileExists(fileSystem.CombinePath(folder, EdgeRemovalScript.RedirectCommandName));
    }

    internal async Task EnsureEdgeRedirectRepairAsync()
    {
        if (!HasManagedEdgeRedirect() || !IsFolderSecure())
            return;

        var name = ScriptPaths.OpenWebSearchTaskName;
        var script = EdgeRedirectRepairScriptPath();
        var tasks = await scheduledTaskService.GetWinhanceTasksAsync().ConfigureAwait(false);
        var task = tasks.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (task is { Enabled: true } && script.Equals(RegisteredScriptPath(task), StringComparison.OrdinalIgnoreCase)
            && fileSystem.FileExists(script))
            return;

        var content = EdgeRemovalScript.GetRepairScript();
        await fileSystem.WriteAllTextAsync(script, content).ConfigureAwait(false);
        var result = await scheduledTaskService.RegisterScheduledTaskAsync(new RemovalScript
        {
            Name = name,
            TargetScheduledTaskName = name,
            ActualScriptPath = script,
            Content = content,
        }).ConfigureAwait(false);

        if (result.Success && task is { Enabled: false })
            result = await SetTaskEnabledAsync(name, true).ConfigureAwait(false);
        if (!result.Success)
            logService.LogWarning($"Could not restore the Edge link repair task: {result.ErrorMessage}");
    }

    public async Task<OperationResult> SetBackgroundTaskEnabledAsync(string name, bool enabled)
    {
        if (!KeepRemovedModeStore.RemovalTaskNames.Contains(name, StringComparer.Ordinal))
            return OperationResult.Failed($"'{name}' is not a managed Winhance background task.");

        if (enabled)
        {
            var tasks = await scheduledTaskService.GetWinhanceTasksAsync().ConfigureAwait(false);
            var task = tasks.FirstOrDefault(task => task.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var script = KeepRemovedModeStore.ScriptPath(fileSystem, name);
            if (task is null || !fileSystem.FileExists(script))
                return OperationResult.Failed("The task and its script must both exist before it can be enabled.");

            if (!script.Equals(RegisteredScriptPath(task), StringComparison.OrdinalIgnoreCase) || !IsFolderSecure())
                return OperationResult.Failed("The task must use its Winhance script in a folder protected from non-administrators.");
        }

        return await SetTaskEnabledAsync(name, enabled).ConfigureAwait(false);
    }

    private Task<OperationResult> SetTaskEnabledAsync(string name, bool enabled) =>
        Task.Run(() => scheduledTaskState.SetTaskEnabled($@"{ScriptPaths.TaskFolder}\{name}", enabled));

    private static string? RegisteredScriptPath(WinhanceTaskInfo? task) =>
        task is not null && ScriptPathInArguments().Match(task.Arguments) is { Success: true } match
            ? match.Groups["path"].Value.Replace("''", "'")
            : null;

    private string EdgeRedirectRepairScriptPath() =>
        fileSystem.CombinePath(ScriptPaths.OpenWebSearchDirectoryLiteral, ScriptPaths.OpenWebSearchTaskName + ".ps1");

    public async Task<IReadOnlyList<WinhanceTaskInfo>> GetOtherTasksAsync() =>
        (await scheduledTaskService.GetWinhanceTasksAsync().ConfigureAwait(false))
            .Where(t => !IsSignInTaskName(t.Name)
                && !t.Name.Equals(SignInTask.LegacyMaintainName, StringComparison.OrdinalIgnoreCase)
                && !t.Name.Equals(ScriptPaths.OpenWebSearchTaskName, StringComparison.OrdinalIgnoreCase)
                && !KeepRemovedModeStore.RemovalTaskNames.Contains(t.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

    private static bool IsSignInTaskName(string name) =>
        name is SignInTask.Name or SignInTask.LegacyTrayName or SignInTask.LegacyReapplyName;

    private static bool BelongsTo(ItemDefinition item, string taskName) =>
        taskName == BloatRemovalScriptGenerator.ScriptName ? item.RemovalScript == null : item.RemovalScriptName == taskName;

    private static string CategoryOf(ItemDefinition item) =>
        item.RemovalScript == null ? BloatRemovalScriptGenerator.ScriptName : item.RemovalScriptName ?? BloatRemovalScriptGenerator.ScriptName;

    private IEnumerable<string> KeptIds() =>
        registry.GetValueNames(KeptKey).Where(id => id.Length > 0 && !KeepRemovedModeStore.IsSetting(id));

    private bool IsWinhanceEnabled() =>
        registry.GetValue(KeptKey, KeepRemovedModeStore.EnabledValue) is int enabled ? enabled != 0 : KeptIds().Any();

    private void SetEnabled(bool on) =>
        registry.SetValue(KeptKey, KeepRemovedModeStore.EnabledValue, on ? 1 : 0, RegistryValueKind.DWord);

    private OperationResult RecordKeptApps(IEnumerable<string> appIds)
    {
        foreach (var id in appIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Catalog.Value.ContainsKey(id))
            {
                logService.LogWarning($"Not keeping '{id}' removed: it is not in the app catalog");
                continue;
            }

            if (registry.ValueExists(KeptKey, id))
                continue;

            if (!registry.SetValue(KeptKey, id, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), RegistryValueKind.String))
                return OperationResult.Failed("Windows refused the write to the kept-apps list. Run Winhance as administrator.");
        }

        return OperationResult.Succeeded();
    }

    private async Task<OperationResult> WriteScriptsFromKeptListAsync()
    {
        var kept = ResolveKeptItems();
        if (kept.Count == 0)
            return OperationResult.Succeeded();

        await bloatRemovalService.WriteRemovalScriptsAsync(kept).ConfigureAwait(false);
        await bloatRemovalService.PersistRemovalScriptsAsync(kept).ConfigureAwait(false);
        return OperationResult.Succeeded();
    }

    // The scripts' contents go to the kept list first, so turning keeping back on restores them.
    private async Task<OperationResult> ImportAndDeleteScriptsAsync()
    {
        var ids = await ScriptKeptIdsAsync().ConfigureAwait(false);
        var recorded = RecordKeptApps(ids);
        if (!recorded.Success)
            return recorded;

        foreach (var name in KeepRemovedModeStore.RemovalTaskNames)
        {
            var deleted = await DeleteTaskAndScriptAsync(name).ConfigureAwait(false);
            if (!deleted.Success)
                return deleted;
        }

        foreach (var task in await LegacyRemovalTasksAsync().ConfigureAwait(false))
        {
            var deleted = await scheduledTaskService.UnregisterScheduledTaskAsync(task.Name).ConfigureAwait(false);
            if (!deleted.Success)
                return deleted;
        }

        logService.LogInformation($"Moved {ids.Count} kept app(s) from the removal scripts to the kept list");
        return OperationResult.Succeeded();
    }

    private async Task<HashSet<string>> ScriptKeptIdsAsync()
    {
        // Only tasks or scripts that exist: an Edge or OneDrive name alone resolves to its app, which would then be removed.
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in await GetRemovalTasksAsync().ConfigureAwait(false))
        {
            var scriptPath = KeepRemovedModeStore.ScriptPath(fileSystem, state.Name);
            var content = fileSystem.FileExists(scriptPath)
                ? await fileSystem.ReadAllTextAsync(scriptPath).ConfigureAwait(false)
                : string.Empty;
            ids.UnionWith(ScriptMigrationService.ResolveKeptIds(state.Name, content));
        }

        foreach (var task in await LegacyRemovalTasksAsync().ConfigureAwait(false))
            ids.UnionWith(LegacyTaskIds(task.Name)!);

        return ids;
    }

    private async Task<bool> HasScriptsAsync() =>
        (await GetRemovalTasksAsync().ConfigureAwait(false)).Count > 0
        || (await LegacyRemovalTasksAsync().ConfigureAwait(false)).Count > 0;

    private async Task<List<WinhanceTaskInfo>> LegacyRemovalTasksAsync() =>
        [.. (await scheduledTaskService.GetWinhanceTasksAsync().ConfigureAwait(false)).Where(t => LegacyTaskIds(t.Name) is not null)];

    // The WPF releases (25.05 and earlier) kept OneNote in a task of its own and every other app in Remove_<package name,
    // dots as underscores>, running scripts from Program Files. Those tasks still run on PCs that never removed them.
    // Null for any other name.
    internal static IReadOnlyList<string>? LegacyTaskIds(string taskName)
    {
        if (taskName.Equals(LegacyOneNoteTask, StringComparison.OrdinalIgnoreCase))
            return ["windows-app-onenote"];

        if (!taskName.StartsWith(LegacyPerAppTaskPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var package = taskName[LegacyPerAppTaskPrefix.Length..];
        return [.. Catalog.Value.Values
            .Where(item => (item.AppxPackageName ?? []).Any(name => name.Replace('.', '_').Equals(package, StringComparison.OrdinalIgnoreCase)))
            .Select(item => item.Id)];
    }

    private async Task<OperationResult> DeleteTaskAndScriptAsync(string name, string? scriptPath = null)
    {
        var result = await scheduledTaskService.UnregisterScheduledTaskAsync(name).ConfigureAwait(false);
        if (!result.Success)
            return result;

        scriptPath ??= KeepRemovedModeStore.ScriptPath(fileSystem, name);
        try
        {
            if (fileSystem.FileExists(scriptPath))
                fileSystem.DeleteFile(scriptPath);

            return OperationResult.Succeeded();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return OperationResult.Failed(ex.Message, ex);
        }
    }

    private IReadOnlyList<string> ItemsOf(string taskName, string scriptContent)
    {
        if (scriptContent.Length == 0)
            return [];

        var names = taskName == BloatRemovalScriptGenerator.ScriptName
            ? ScriptMigrationService.BloatArrays
                .SelectMany(array => BloatRemovalScriptGenerator.ExtractArrayFromScript(scriptContent, array))
                .Select(name => DisplayNames.Value.GetValueOrDefault(name, name))
            : ScriptMigrationService.ResolveKeptIds(taskName, scriptContent)
                .Select(id => Catalog.Value.TryGetValue(id, out var item) ? item.Name : id);
        return [.. names.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase)];
    }

    // The sign-in task is the only scheduled task Winhance mode ever has, and only for a sign-in option.
    private async Task<OperationResult> ApplySignInTaskAsync(bool startInTray, bool reapply)
    {
        var sid = interactiveUser.InteractiveUserSid;
        var flags = ComposeSignInFlags(startInTray, reapply);
        if (flags.Length > 0)
        {
            if (SignInTaskRefusal() is { } refusal)
                return refusal;

            var registered = await scheduledTaskService.RegisterSignInTaskAsync(SignInTask.Name, _exePath!, flags).ConfigureAwait(false);
            if (!registered.Success)
                return registered;
        }
        else if (await scheduledTaskService.IsSignInTaskRegisteredForAsync(SignInTask.Name, sid).ConfigureAwait(false))
        {
            var removed = await scheduledTaskService.UnregisterScheduledTaskAsync(SignInTask.Name).ConfigureAwait(false);
            if (!removed.Success)
                return removed;
        }

        foreach (var legacy in new[] { SignInTask.LegacyTrayName, SignInTask.LegacyReapplyName })
        {
            if (await scheduledTaskService.IsSignInTaskRegisteredForAsync(legacy, sid).ConfigureAwait(false))
                await scheduledTaskService.UnregisterScheduledTaskAsync(legacy).ConfigureAwait(false);
        }

        return OperationResult.Succeeded();
    }

    // The task starts the exe elevated, so it must not be one a portable copy moves around or other accounts can replace.
    private OperationResult? SignInTaskRefusal()
    {
        if (_isPortable)
            return OperationResult.Failed("A portable copy cannot start at sign-in");

        if (_exePath is null)
            return OperationResult.Failed("Winhance's own path is unknown");

        return CanBeChangedByOthers(_exePath) ? OperationResult.Failed(SignInTask.UnsafeFolderError) : null;
    }

    // A relative or unreadable path counts as changeable.
    private static bool CanBeChangedByOthers(string exe) =>
        !Path.IsPathFullyQualified(exe) || FolderLock.CanBeChangedByOthers(exe) || Path.GetDirectoryName(exe) is not { } folder || FolderLock.CanBeChangedByOthers(folder);

    // --maintain rides along only when Winhance mode is keeping apps removed; it never makes a task on its own.
    private string ComposeSignInFlags(bool startInTray, bool reapply)
    {
        var flags = new List<string>();
        if (startInTray)
            flags.Add(SignInTask.TrayFlag);
        if (reapply)
            flags.Add(SignInTask.ReapplyFlag);
        if (flags.Count > 0 && GetMode() == KeepRemovedMode.Winhance && IsWinhanceEnabled())
            flags.Add(SignInTask.MaintainFlag);

        return string.Join(' ', flags);
    }

    // Keeping on or off, or the mode, changes the arguments the task needs.
    private async Task SyncSignInTaskAsync()
    {
        var options = await GetSignInOptionsAsync().ConfigureAwait(false);
        if (!options.StartInTray && !options.Reapply)
            return;

        var existing = (await scheduledTaskService.GetWinhanceTasksAsync().ConfigureAwait(false))
            .FirstOrDefault(t => t.Name == SignInTask.Name);
        if (existing is not null && CanBeChangedByOthers(existing.Command.Trim('"')))
        {
            logService.LogWarning($"Removing the sign-in task: other accounts can change {existing.Command}");
            var removed = await ApplySignInTaskAsync(startInTray: false, reapply: false).ConfigureAwait(false);
            if (!removed.Success)
                logService.LogWarning($"Could not remove the sign-in task: {removed.ErrorMessage}");
            return;
        }

        // The installed copy owns the task, so a portable copy leaves it as it is.
        if (_isPortable)
            return;

        if (SignInTaskRefusal() is { } refusal)
        {
            logService.LogWarning($"Not updating the sign-in task: {refusal.ErrorMessage}");
            return;
        }

        var wanted = ComposeSignInFlags(options.StartInTray, options.Reapply);
        if (existing is not null && existing.Arguments.Trim() == wanted)
            return;

        var result = await ApplySignInTaskAsync(options.StartInTray, options.Reapply).ConfigureAwait(false);
        if (!result.Success)
            logService.LogWarning($"Could not update the sign-in task: {result.ErrorMessage}");
    }

    private async Task FoldOldSignInTasksAsync()
    {
        var tasks = await scheduledTaskService.GetWinhanceTasksAsync().ConfigureAwait(false);
        if (!tasks.Any(t => t.Name is SignInTask.LegacyTrayName or SignInTask.LegacyReapplyName))
            return;

        var options = await GetSignInOptionsAsync().ConfigureAwait(false);
        var result = await ApplySignInTaskAsync(options.StartInTray, options.Reapply).ConfigureAwait(false);
        if (!result.Success)
            logService.LogWarning($"Could not fold the old sign-in tasks into one: {result.ErrorMessage}");
    }

    // Held across awaits, which a Mutex cannot be; a file opened with no sharing works for the sign-in run and for "Check now".
    private FileStream? TryTakeGate()
    {
        try
        {
            Directory.CreateDirectory(_lockRoot);
            return new FileStream(
                Path.Combine(_lockRoot, "keep-removed.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            logService.LogWarning($"Keep removed apps cannot take its lock: {ex.Message}");
            return null;
        }
    }

    private List<ItemDefinition> ResolveKeptItems()
    {
        var items = new List<ItemDefinition>();
        foreach (var id in KeptIds())
        {
            if (Catalog.Value.TryGetValue(id, out var item))
                items.Add(item);
            else
                logService.LogWarning($"Ignoring '{id}' on the kept list: it is not in the app catalog");
        }

        return items;
    }

    // True when any removal failed.
    private async Task<bool> RemoveAsync(List<ItemDefinition> back, CancellationToken ct)
    {
        var anyFailed = false;
        foreach (var scriptApp in DedicatedRemovalScripts.OnePerScript(back))
        {
            var outcome = await bloatRemovalService.ExecuteDedicatedScriptAsync(scriptApp, null, ct).ConfigureAwait(false);
            anyFailed |= outcome == RemovalOutcome.Failed;
        }

        var regular = back.Where(item => item.RemovalScript == null).ToList();
        if (regular.Count > 0)
        {
            var outcome = await bloatRemovalService.ExecuteBloatRemovalAsync(regular, null, ct).ConfigureAwait(false);
            anyFailed |= outcome == RemovalOutcome.Failed;
        }

        return anyFailed;
    }

    internal void WriteStatus(string result, List<ItemDefinition> back)
    {
        registry.SetValue(StatusKey, "LastCheck", DateTime.Now.ToString("o", CultureInfo.InvariantCulture), RegistryValueKind.String);
        registry.SetValue(StatusKey, "LastResult", result, RegistryValueKind.String);

        // Kept across runs: a sign-in run that put an app back must still show after the next run finds nothing.
        if (back.Count > 0)
        {
            registry.SetValue(StatusKey, "RemovedAgain", string.Join(';', back.Select(item => item.Id)), RegistryValueKind.String);
            registry.SetValue(StatusKey, "LastRemovedAgain", DateTime.Now.ToString("o", CultureInfo.InvariantCulture), RegistryValueKind.String);
        }

        foreach (var item in back)
        {
            var name = item.Id + ReturnCountSuffix;
            var count = registry.GetValue(StatusKey, name) is int previous ? previous : 0;
            registry.SetValue(StatusKey, name, count + 1, RegistryValueKind.DWord);
        }
    }

    private static DateTime? ParseDate(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) ? date : null;
}
