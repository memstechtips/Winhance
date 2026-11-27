using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Winhance.UI.Features.Common.Helpers;
using CatalogFluentIcons = Winhance.Core.Features.Common.Catalog.FluentIcons;

namespace Winhance.UI.Features.Settings.ViewModels;

public partial class SettingsViewModel
{
    private static readonly TimeSpan RecentRemoval = TimeSpan.FromDays(30);

    private const int TaskSchedulerRunning = 0x41301;

    private const string EdgeCategory = EdgeRemovalScript.ScriptName;

    private static readonly Dictionary<string, Icon> CategoryIcons = new()
    {
        [BloatRemovalScriptGenerator.ScriptName] = CatalogFluentIcons.Apps,
        [EdgeCategory] = new(IconPack.App, "windows-app-edge", new(IconPack.Fluent, "Globe")),
        [OneDriveRemovalScript.ScriptName] = new(IconPack.App, "windows-app-onedrive", new(IconPack.Fluent, "Cloud")),
    };

    private IReadOnlyList<KeptApp> _keptApps = [];
    private KeepRemovedStatus _status = new(null, "", [], null);
    private IReadOnlyList<WinhanceTaskInfo> _otherTasks = [];
    private RemovalTaskState? _edgeRedirect;
    private IReadOnlyList<RemovalTaskState> _removalTasks = [];
    private bool _taskSafe;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeepRemovedDescription))]
    public partial SignInOptions SignIn { get; private set; } = SignInOptions.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeepRemovedDescription), nameof(ModeDescription))]
    public partial KeepRemovedMode Mode { get; private set; } = KeepRemovedMode.Winhance;

    internal Task BackgroundTasksReady { get; private set; } = Task.CompletedTask;

    // The expander's rows are of different kinds in one list, so each kind gets its own template.
    public ObservableCollection<object> KeepRemovedChildren { get; } = new();

    public ObservableCollection<ComboBoxDisplayOption> ModeOptions { get; } = new();

    // The option objects themselves, like the update pickers: the ComboBox cannot match a boxed enum.
    [ObservableProperty]
    public partial ComboBoxDisplayOption? SelectedModeOption { get; set; }

    [ObservableProperty]
    public partial bool KeepRemovedEnabled { get; set; }

    partial void OnSelectedModeOptionChanged(ComboBoxDisplayOption? oldValue, ComboBoxDisplayOption? newValue)
    {
        if (_isShowingState)
            return;

        if (newValue?.Value is KeepRemovedMode mode)
            ApplyModeAsync(mode, oldValue).FireAndForget(_logService);
        else
            ShowState(() => SelectedModeOption = oldValue);
    }

    partial void OnKeepRemovedEnabledChanged(bool value)
    {
        if (!_isShowingState)
            ApplyKeepRemovedAsync(value).FireAndForget(_logService);
    }

    public string BackgroundTasksLabel => _localizationService.GetString("BackgroundTasks_Label");
    public string KeepRemovedHeader => _localizationService.GetString("BackgroundTasks_KeepRemoved_Title");
    public string KeepRemovedDescription => Mode == KeepRemovedMode.Winhance
        ? _localizationService.GetString("BackgroundTasks_KeepRemoved_Description") + " " + _localizationService.GetString(
            SignIn.Maintain ? "BackgroundTasks_ChecksAtSignIn" : "BackgroundTasks_ChecksOnOpen")
        : _localizationService.GetString("BackgroundTasks_KeepRemoved_Description");
    public string ModeHeader => _localizationService.GetString("BackgroundTasks_Mode_Title");
    public string ModeDescription => Mode == KeepRemovedMode.Scripts
        ? _localizationService.GetString("BackgroundTasks_Mode_Scripts_Description")
        : _localizationService.GetString("BackgroundTasks_Mode_Winhance_Description");

    public void RecheckTaskSafety() => RecheckTaskSafetyAsync().FireAndForget(_logService);

    internal async Task RecheckTaskSafetyAsync()
    {
        var safe = await _backgroundTasks.IsTaskSafetyOkAsync();
        if (safe == _taskSafe)
            return;

        _taskSafe = safe;
        RebuildBackgroundRows();
    }

    private async Task RefreshBackgroundTasksAsync()
    {
        var (mode, kept, status) = await Task.Run(() => (_backgroundTasks.GetMode(), _backgroundTasks.GetKeptApps(), _backgroundTasks.GetStatus()));
        Mode = mode;
        _keptApps = kept;
        _status = status;
        var on = await _backgroundTasks.IsKeepRemovedOnAsync();
        ShowState(() => KeepRemovedEnabled = on);
        _taskSafe = await _backgroundTasks.IsTaskSafetyOkAsync();
        _removalTasks = await _backgroundTasks.GetRemovalTasksAsync();
        _otherTasks = await _backgroundTasks.GetOtherTasksAsync();
        _edgeRedirect = await _backgroundTasks.GetEdgeRedirectAsync();
        ShowSignInOptions(await _backgroundTasks.GetSignInOptionsAsync());

        EnsureModeOptions();
        ShowState(() => SelectedModeOption = ModeOptions.FirstOrDefault(o => Equals(o.Value, mode)));
        RebuildBackgroundRows();
    }

    private void EnsureModeOptions()
    {
        if (ModeOptions.Count > 0)
            return;

        foreach (var mode in Enum.GetValues<KeepRemovedMode>())
            ModeOptions.Add(new ComboBoxDisplayOption(ModeText(mode), mode));
    }

    private string ModeText(KeepRemovedMode mode)
    {
        var key = "BackgroundTasks_Mode_" + mode;
        return _localizationService.GetString(key);
    }

    private void RebuildBackgroundRows()
    {
        var hasKept = _keptApps.Count > 0;
        var rows = new List<object> { new ModeRow(this, KeepRemovedEnabled && hasKept) };

        var categories = CategoryNames().ToList();
        if (!hasKept && _removalTasks.Count == 0)
        {
            rows.Add(new EmptyListRow(
                _localizationService.GetString("BackgroundTasks_Empty_Message"),
                _localizationService.GetString("BackgroundTasks_Empty_Open"),
                OpenSoftwareAppsCommand));
        }

        if (Mode == KeepRemovedMode.Scripts || _removalTasks.Count > 0 || _edgeRedirect is not null)
        {
            var protect = _localizationService.GetString("BackgroundTasks_Safety_Protect");
            rows.Add(_taskSafe
                ? new FolderSafeRow(_localizationService.GetString("BackgroundTasks_Safety_Protected"))
                : new FolderUnsafeRow(
                    _localizationService.GetString("BackgroundTasks_Safety_NeedsRepair"),
                    protect,
                    RepairTaskSafetyCommand));
        }

        if (Mode == KeepRemovedMode.Winhance)
        {
            rows.Add(new TaskRow(
                LastCheckText(),
                LastPutBackText(),
                _localizationService.GetString("BackgroundTasks_CheckNow"),
                CheckNowCommand,
                _localizationService.GetString("BackgroundTasks_OpenLog"),
                OpenLogCommand));
        }
        else
        {
            rows.Add(new TaskRow(
                _localizationService.GetString("BackgroundTasks_ScriptFolder_Title"),
                ScriptPaths.ScriptsDirectory,
                _localizationService.GetString("BackgroundTasks_OpenFolder"),
                OpenScriptFolderCommand,
                _localizationService.GetString("BackgroundTasks_OpenLog"),
                OpenLogCommand));
        }

        var redirectAdded = false;
        foreach (var name in categories)
        {
            rows.Add(CreateCategoryRow(name, _removalTasks.FirstOrDefault(t => t.Name == name)));
            if (name == EdgeCategory && _edgeRedirect is not null)
            {
                rows.Add(CreateRedirectRow(_edgeRedirect));
                redirectAdded = true;
            }
        }

        if (_edgeRedirect is not null && !redirectAdded)
            rows.Add(CreateRedirectRow(_edgeRedirect));

        foreach (var task in _otherTasks)
        {
            rows.Add(new TaskRow(
                task.Name,
                OtherTaskDetail(task),
                _localizationService.GetString("BackgroundTasks_Remove"),
                RemoveOtherTaskCommand));
        }

        KeepRemovedChildren.ReplaceInPlace(rows);
    }

    private IEnumerable<string> CategoryNames() =>
        CategoryIcons.Keys.Where(name =>
            _keptApps.Any(app => CategoryOf(app) == name) || _removalTasks.Any(task => task.Name == name));

    private static string CategoryOf(KeptApp app) =>
        CategoryIcons.ContainsKey(app.Category) ? app.Category : BloatRemovalScriptGenerator.ScriptName;

    private CategoryRow CreateCategoryRow(string name, RemovalTaskState? state)
    {
        var kept = _keptApps.Where(app => CategoryOf(app) == name).ToList();
        var items = state is { Items.Count: > 0 } ? state.Items : [.. kept.Select(app => app.Name)];
        var lines = new List<string>();
        var removedOn = RemovedOnText(kept);
        var historyOnly = state is null && (Mode == KeepRemovedMode.Scripts || !KeepRemovedEnabled);
        if (items.Count > 0 && (!historyOnly || removedOn.Length == 0))
        {
            var names = string.Join(", ", items);
            lines.Add(historyOnly ? names : _localizationService.GetString("BackgroundTasks_Removes", names));
        }
        if (removedOn.Length > 0)
            lines.Add(removedOn);
        if (state is not null)
            AddTaskDetails(lines, state);
        else
        {
            var returned = kept.Sum(app => app.ReturnCount);
            if (returned > 0)
                lines.Add(_localizationService.GetString("BackgroundTasks_KeptApp_Returned", returned));
        }

        var title = _localizationService.GetStringOrDefault("BackgroundTasks_Card_" + name, name);
        var (icon, iconPack) = DisplayIcon(CategoryIcons[name]);
        if (name == BloatRemovalScriptGenerator.ScriptName)
            iconPack = "FluentColor";

        return CreateTaskCategoryRow(name, title, string.Join('\n', lines), icon, iconPack, state, isRedirect: false);
    }

    private CategoryRow CreateRedirectRow(RemovalTaskState state)
    {
        var lines = new List<string>();
        AddTaskDetails(lines, state);
        return CreateTaskCategoryRow(state.Name,
            _localizationService.GetString("BackgroundTasks_Redirect_Title"),
            string.Join('\n', lines), CatalogFluentIcons.Globe.Glyph, "FluentColor", state, isRedirect: true);
    }

    private void AddTaskDetails(List<string> lines, RemovalTaskState state)
    {
        lines.Add(_localizationService.GetString(
            "BackgroundTasks_TaskAndScript", $@"{ScriptPaths.TaskFolder}\{state.Name}", state.ScriptPath));
        if (state.TaskExists)
            lines.Add(LastRunText(state.LastRun, state.LastResult));
    }

    private CategoryRow CreateTaskCategoryRow(string name, string title, string detail, string icon, string iconPack,
        RemovalTaskState? state, bool isRedirect)
    {
        var inApp = state is null && Mode == KeepRemovedMode.Winhance;
        var active = inApp ? KeepRemovedEnabled : state?.Health == RemovalTaskHealth.Active;
        var taskStatus = inApp ? (active ? "BackgroundTasks_Health_Active" : "Common_Off")
            : state?.TaskEnabled switch
            {
                true => "BackgroundTasks_TaskEnabled",
                false => "BackgroundTasks_Health_TaskDisabled",
                null => "BackgroundTasks_TaskMissing",
            };
        bool? scriptPresent = inApp ? null : state?.ScriptExists == true;
        var scriptStatus = scriptPresent switch
        {
            true => _localizationService.GetString("BackgroundTasks_ScriptPresent"),
            false => _localizationService.GetString("BackgroundTasks_ScriptMissing"),
            null => string.Empty,
        };
        var row = new CategoryRow(name, title, detail, icon, iconPack,
            _localizationService.GetString(taskStatus), scriptStatus, scriptPresent, active);
        if (isRedirect)
            return row;

        return row with
        {
            ActionsText = _localizationService.GetString("BackgroundTasks_TaskActions"),
            EnableTask = state is { TaskEnabled: true } ? false
                : state is { TaskEnabled: false, ScriptExists: true } ? true : null,
            ToggleText = _localizationService.GetString(state?.TaskEnabled == true ? "BackgroundTasks_DisableTask" : "BackgroundTasks_EnableTask"),
            Toggle = ToggleBackgroundTaskCommand,
            RepairText = _localizationService.GetString("BackgroundTasks_Repair"),
            Repair = state?.Health is RemovalTaskHealth.NoTask or RemovalTaskHealth.ScriptMissing ? RepairRemovalTaskCommand : null,
            RemoveText = _localizationService.GetString("BackgroundTasks_RemoveTaskAndScript"),
            Remove = state is not null ? RemoveBackgroundTaskCommand : null,
            StopText = _localizationService.GetString("BackgroundTasks_StopKeeping"),
            Stop = inApp ? StopKeepingCommand : null,
        };
    }

    private (string Glyph, string Pack) DisplayIcon(Icon icon) =>
        CatalogIconDisplay.For(icon, _appIcons, _themeService.GetEffectiveTheme() == ElementTheme.Light);

    private string OtherTaskDetail(WinhanceTaskInfo task)
    {
        var lines = new List<string> { $"{task.Command} {task.Arguments}".Trim() };
        if (task.RunsAs.Length > 0)
            lines.Add(_localizationService.GetString("BackgroundTasks_RunsAs", task.RunsAs));

        lines.Add(LastRunText(task.LastRun, task.LastResult));
        return string.Join('\n', lines);
    }

    private string RemovedOnText(IEnumerable<KeptApp> apps)
    {
        var dated = apps.Where(app => app.RemovedOn is not null)
            .Select(app => $"{app.Name} ({app.RemovedOn!.Value.ToString("d", CultureInfo.CurrentCulture)})")
            .ToList();
        return dated.Count == 0
            ? string.Empty
            : _localizationService.GetString("BackgroundTasks_RemovedOn", string.Join(", ", dated));
    }

    // Task Scheduler reports 0 for success and 0x41301 while the task is still running; anything else is a failure code.
    private string LastRunText(DateTime? lastRun, int? result)
    {
        if (lastRun is not { } at)
            return _localizationService.GetString("BackgroundTasks_Task_NeverRan");

        var outcome = result switch
        {
            null or 0 => _localizationService.GetString("BackgroundTasks_Task_Succeeded"),
            TaskSchedulerRunning => _localizationService.GetString("BackgroundTasks_Task_Running"),
            var code => _localizationService.GetString("BackgroundTasks_Task_Failed", $"0x{code:X8}"),
        };
        return _localizationService.GetString("BackgroundTasks_Task_LastRun", at.ToString("g", CultureInfo.CurrentCulture), outcome);
    }

    [RelayCommand]
    private void OpenSoftwareApps() =>
        _eventBus.Publish(new PageRequestedEvent { PageTag = "SoftwareApps", FeatureId = FeatureIds.WindowsApps });

    private string LastCheckText() =>
        _status.LastCheck is { } at
            ? _localizationService.GetString("BackgroundTasks_LastCheck", at.ToString("g", CultureInfo.CurrentCulture), LastResultText())
            : _localizationService.GetString("BackgroundTasks_LastCheck_Never");

    private string LastPutBackText()
    {
        if (_status.LastRemovedAgain is not { } at || _status.RemovedAgain.Count == 0 || DateTime.Now - at > RecentRemoval)
            return string.Empty;

        var names = _status.RemovedAgain.Select(id => _keptApps.FirstOrDefault(a => a.Id == id)?.Name ?? id);
        return _localizationService.GetString("BackgroundTasks_LastPutBack", string.Join(", ", names), at.ToString("g", CultureInfo.CurrentCulture));
    }

    // An unknown code (from a newer or older Winhance) is shown as it is rather than hidden.
    private string LastResultText() => _status.LastResult switch
    {
        KeepRemovedResult.NoApps => _localizationService.GetString("BackgroundTasks_Result_NoApps"),
        KeepRemovedResult.AllStillRemoved => _localizationService.GetString("BackgroundTasks_Result_AllStillRemoved"),
        KeepRemovedResult.RemovalFailed => _localizationService.GetString("BackgroundTasks_Result_RemovalFailed"),
        KeepRemovedResult.RemovedAgain => _localizationService.GetString("BackgroundTasks_Result_RemovedAgain", _status.RemovedAgain.Count),
        var other => other,
    };

    private async Task<bool> ConfirmAsync(string titleKey, string messageKey, params object[] args)
    {
        var response = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Title = _localizationService.GetString(titleKey),
            Message = _localizationService.GetString(messageKey, args),
            ConfirmButtonText = _localizationService.GetString("Button_Continue"),
            CancelButtonText = _localizationService.GetString("Button_Cancel"),
        });
        return response.Confirmed;
    }

    // Nothing is deleted until the person confirms; a cancel or a failure puts the picker back to the real mode.
    private async Task ApplyModeAsync(KeepRemovedMode mode, ComboBoxDisplayOption? previous)
    {
        var confirmed = mode == KeepRemovedMode.Scripts
            ? await ConfirmAsync("BackgroundTasks_Mode_ToScripts_Title", "BackgroundTasks_Mode_ToScripts_Message", ScriptPaths.ScriptsDirectory)
            : await ConfirmAsync("BackgroundTasks_Mode_ToWinhance_Title", "BackgroundTasks_Mode_ToWinhance_Message");
        if (!confirmed)
        {
            ShowState(() => SelectedModeOption = previous);
            return;
        }

        var result = await _backgroundTasks.SetModeAsync(mode);
        if (!result.Success)
            _logService.LogWarning($"Could not switch the keep-removed mode: {result.ErrorMessage}");

        await RefreshBackgroundTasksAsync();
    }

    // The state shown is the state found: after the change the page reads it back, so a failed change turns the switch back.
    private async Task ApplyKeepRemovedAsync(bool enabled)
    {
        if (!enabled && Mode == KeepRemovedMode.Scripts
            && !await ConfirmAsync("BackgroundTasks_TurnOff_Title", "BackgroundTasks_TurnOff_Message"))
        {
            ShowState(() => KeepRemovedEnabled = true);
            return;
        }

        var result = await _backgroundTasks.SetKeepRemovedOnAsync(enabled);
        if (!result.Success)
            _logService.LogWarning($"Could not change keeping apps removed: {result.ErrorMessage}");

        await RefreshBackgroundTasksAsync();
    }

    [RelayCommand]
    private async Task CheckNowAsync()
    {
        await _backgroundTasks.MaintainAsync();
        await RefreshBackgroundTasksAsync();
    }

    [RelayCommand]
    private Task OpenLogAsync() => OpenFolderAsync(Path.Combine(AppDataPaths.MachineRoot, "Logs"));

    [RelayCommand]
    private Task OpenScriptFolderAsync() => OpenFolderAsync(ScriptPaths.ScriptsDirectory);

    private Task OpenFolderAsync(string path) =>
        _dialogService.RunOrShowErrorAsync(_logService, () => _explorerWindowManager.OpenFolderAsync(path),
            ex => _localizationService.GetString("Common_ActionFailed", ex.Message));

    [RelayCommand]
    private async Task StopKeepingAsync(CategoryRow row)
    {
        if (!await ConfirmAsync("BackgroundTasks_StopKeeping_Title", "BackgroundTasks_StopKeeping_Message", row.Title))
            return;

        foreach (var app in _keptApps.Where(app => CategoryOf(app) == row.Category))
            _backgroundTasks.RemoveKeptApp(app.Id);

        _keptApps = _backgroundTasks.GetKeptApps();
        RebuildBackgroundRows();
    }

    [RelayCommand]
    private async Task RepairRemovalTaskAsync(CategoryRow row)
    {
        var result = await _backgroundTasks.RepairRemovalTaskAsync(row.Category);
        await FinishBackgroundTaskActionAsync(result);
    }

    [RelayCommand]
    private async Task RemoveBackgroundTaskAsync(CategoryRow row)
    {
        if (!await ConfirmAsync("BackgroundTasks_RemoveTaskAndScript_Title", "BackgroundTasks_RemoveTaskAndScript_Message", row.Title))
            return;

        var result = await _backgroundTasks.RemoveRemovalTaskAsync(row.Category);
        await FinishBackgroundTaskActionAsync(result);
    }

    [RelayCommand]
    private async Task ToggleBackgroundTaskAsync(CategoryRow row)
    {
        if (row.EnableTask is not { } enabled)
            return;

        var result = await _backgroundTasks.SetBackgroundTaskEnabledAsync(row.Category, enabled);
        await FinishBackgroundTaskActionAsync(result);
    }

    private async Task FinishBackgroundTaskActionAsync(OperationResult result)
    {
        await RefreshBackgroundTasksAsync();
        if (!result.Success)
            await _dialogService.ShowErrorAsync(_localizationService.GetString("Common_ActionFailed", result.ErrorMessage ?? string.Empty));
    }

    // An unknown task's row is titled with the task's name.
    [RelayCommand]
    private async Task RemoveOtherTaskAsync(TaskRow row)
    {
        var result = await _scheduledTaskService.UnregisterScheduledTaskAsync(row.Title);
        if (!result.Success)
            _logService.LogWarning($"Could not remove the task '{row.Title}': {result.ErrorMessage}");

        _otherTasks = await _backgroundTasks.GetOtherTasksAsync();
        RebuildBackgroundRows();
    }

    [RelayCommand]
    private async Task RepairTaskSafetyAsync()
    {
        var result = await _backgroundTasks.RepairTaskSafetyAsync();
        await FinishBackgroundTaskActionAsync(result);
    }

    private void OnMaintained(object? sender, EventArgs e) =>
        _dispatcherService.RunOnUIThread(() => RefreshBackgroundTasksAsync().FireAndForget(_logService));

    private void OnBackgroundLanguageChanged()
    {
        foreach (var option in ModeOptions)
            option.DisplayText = ModeText((KeepRemovedMode)option.Value);
        RebuildBackgroundRows();
    }
}

public sealed record TaskRow(
    string Title,
    string Detail,
    string PrimaryText,
    ICommand? Primary,
    string SecondaryText = "",
    ICommand? Secondary = null,
    double Opacity = 1.0)
{
    public bool HasPrimary => Primary is not null;

    public bool HasSecondary => Secondary is not null;
}

public sealed record ModeRow(SettingsViewModel Vm, bool Enabled);

public sealed record EmptyListRow(string Message, string ActionText, ICommand Action);

public sealed record FolderSafeRow(string Message);

public sealed record FolderUnsafeRow(string Message, string ActionText, ICommand Action);

public sealed record CategoryRow(
    string Category,
    string Title,
    string Detail,
    string Icon,
    string IconPack,
    string TaskStatus,
    string ScriptStatus,
    bool? ScriptPresent,
    bool Active,
    string ActionsText = "",
    bool? EnableTask = null,
    string ToggleText = "",
    ICommand? Toggle = null,
    string RepairText = "",
    ICommand? Repair = null,
    string RemoveText = "",
    ICommand? Remove = null,
    string StopText = "",
    ICommand? Stop = null)
{
    public bool Inactive => !Active;

    public bool ShowScriptPresent => ScriptPresent == true;

    public bool ShowScriptMissing => ScriptPresent == false;

    public bool HasTaskActions => CanToggle || CanRepair || CanRemove;

    public bool CanToggle => EnableTask.HasValue;

    public bool CanRepair => Repair is not null;

    public bool CanRemove => Remove is not null;

    public bool CanStop => Stop is not null;
}
