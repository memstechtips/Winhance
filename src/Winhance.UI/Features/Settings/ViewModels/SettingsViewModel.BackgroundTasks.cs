using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.UI.Features.Settings.ViewModels;

public partial class SettingsViewModel
{
    private static readonly TimeSpan RecentRemoval = TimeSpan.FromDays(30);

    private const int TaskSchedulerRunning = 0x41301;

    private const string RedirectIconKey = "TimeRegionLanguageIconSymbol";
    private const string EdgeCategory = EdgeRemovalScript.ScriptName;

    private static readonly Dictionary<string, string> CategoryIcons = new()
    {
        [BloatRemovalScriptGenerator.ScriptName] = "DeleteSweepIconPath",
        [EdgeCategory] = "MicrosoftEdgeIconPath",
        [OneDriveRemovalScript.ScriptName] = "MicrosoftOneDriveIconPath",
    };

    private IReadOnlyList<KeptApp> _keptApps = [];
    private KeepRemovedStatus _status = new(null, "", [], null);
    private IReadOnlyList<WinhanceTaskInfo> _otherTasks = [];
    private RemovalTaskState? _edgeRedirect;
    private IReadOnlyList<RemovalTaskState> _removalTasks = [];
    private SignInOptions _signIn = SignInOptions.None;
    private KeepRemovedMode _mode = KeepRemovedMode.Winhance;
    private bool _keepRemovedOn;
    private bool _taskSafe;

    internal Task BackgroundTasksReady { get; private set; } = Task.CompletedTask;

    // The expander's rows are of different kinds in one list, so each kind gets its own template.
    public ObservableCollection<object> KeepRemovedChildren { get; } = new();

    public ObservableCollection<ComboBoxDisplayOption> ModeOptions { get; } = new();

    // The option objects themselves, like the update pickers: the ComboBox cannot match a boxed enum.
    private ComboBoxDisplayOption? _selectedModeOption;
    public ComboBoxDisplayOption? SelectedModeOption
    {
        get => _selectedModeOption;
        set
        {
            if (value?.Value is not KeepRemovedMode mode || ReferenceEquals(value, _selectedModeOption))
                return;

            var previous = _selectedModeOption;
            _selectedModeOption = value;
            OnPropertyChanged();
            ApplyModeAsync(mode, previous).FireAndForget(_logService);
        }
    }

    public bool KeepRemovedEnabled
    {
        get => _keepRemovedOn;
        set
        {
            if (SetProperty(ref _keepRemovedOn, value))
                ApplyKeepRemovedAsync(value).FireAndForget(_logService);
        }
    }

    public string BackgroundTasksLabel => _localizationService.GetString("BackgroundTasks_Label");
    public string KeepRemovedHeader => _localizationService.GetString("BackgroundTasks_KeepRemoved_Title");
    public string KeepRemovedDescription => _mode == KeepRemovedMode.Winhance
        ? _localizationService.GetString("BackgroundTasks_KeepRemoved_Description") + " " + _localizationService.GetString(
            _signIn.Maintain ? "BackgroundTasks_ChecksAtSignIn" : "BackgroundTasks_ChecksOnOpen")
        : _localizationService.GetString("BackgroundTasks_KeepRemoved_Description");
    public string ModeHeader => _localizationService.GetString("BackgroundTasks_Mode_Title");
    public string ModeDescription => _mode == KeepRemovedMode.Scripts
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
        _mode = mode;
        _keptApps = kept;
        _status = status;
        _keepRemovedOn = await _backgroundTasks.IsKeepRemovedOnAsync();
        _taskSafe = await _backgroundTasks.IsTaskSafetyOkAsync();
        _removalTasks = mode == KeepRemovedMode.Scripts ? await _backgroundTasks.GetRemovalTasksAsync() : [];
        _otherTasks = await _backgroundTasks.GetOtherTasksAsync();
        _edgeRedirect = await _backgroundTasks.GetEdgeRedirectAsync();
        ShowSignInOptions(await _backgroundTasks.GetSignInOptionsAsync());

        EnsureModeOptions();
        _selectedModeOption = ModeOptions.FirstOrDefault(o => Equals(o.Value, _mode));

        OnPropertyChanged(nameof(SelectedModeOption));
        OnPropertyChanged(nameof(KeepRemovedEnabled));
        OnPropertyChanged(nameof(ModeDescription));
        OnPropertyChanged(nameof(KeepRemovedDescription));
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
        KeepRemovedChildren.Clear();
        KeepRemovedChildren.Add(new ModeRow(this, _keepRemovedOn && hasKept));

        var categories = CategoryNames().ToList();
        if (!hasKept && _removalTasks.Count == 0)
        {
            KeepRemovedChildren.Add(new EmptyListRow(
                _localizationService.GetString("BackgroundTasks_Empty_Message"),
                _localizationService.GetString("BackgroundTasks_Empty_Open"),
                new RelayCommand(OpenSoftwareApps)));
        }

        if (_mode == KeepRemovedMode.Scripts || _edgeRedirect is not null)
        {
            var protect = _localizationService.GetString("BackgroundTasks_Safety_Protect");
            KeepRemovedChildren.Add(_taskSafe
                ? new FolderSafeRow(_localizationService.GetString("BackgroundTasks_Safety_Protected"))
                : new FolderUnsafeRow(
                    _localizationService.GetString("BackgroundTasks_Safety_NeedsRepair"),
                    protect,
                    new AsyncRelayCommand(RepairTaskSafetyAsync)));
        }

        if (_mode == KeepRemovedMode.Winhance)
        {
            KeepRemovedChildren.Add(new TaskRow(
                LastCheckText(),
                LastPutBackText(),
                _localizationService.GetString("BackgroundTasks_CheckNow"),
                new AsyncRelayCommand(CheckNowAsync),
                _localizationService.GetString("BackgroundTasks_OpenLog"),
                new AsyncRelayCommand(OpenLogAsync)));
        }
        else
        {
            KeepRemovedChildren.Add(new TaskRow(
                _localizationService.GetString("BackgroundTasks_ScriptFolder_Title"),
                ScriptPaths.ScriptsDirectory,
                _localizationService.GetString("BackgroundTasks_OpenFolder"),
                new AsyncRelayCommand(OpenScriptFolderAsync),
                _localizationService.GetString("BackgroundTasks_OpenLog"),
                new AsyncRelayCommand(OpenLogAsync)));
        }

        var redirectAdded = false;
        foreach (var name in categories)
        {
            KeepRemovedChildren.Add(CreateCategoryRow(name, _removalTasks.FirstOrDefault(t => t.Name == name)));
            if (name == EdgeCategory && _edgeRedirect is not null)
            {
                KeepRemovedChildren.Add(CreateRedirectRow(_edgeRedirect));
                redirectAdded = true;
            }
        }

        if (_edgeRedirect is not null && !redirectAdded)
            KeepRemovedChildren.Add(CreateRedirectRow(_edgeRedirect));

        foreach (var task in _otherTasks)
        {
            KeepRemovedChildren.Add(new TaskRow(
                task.Name,
                OtherTaskDetail(task),
                _localizationService.GetString("BackgroundTasks_Remove"),
                new AsyncRelayCommand(() => RemoveOtherTaskAsync(task))));
        }
    }

    // Bloat, Edge, OneDrive: the categories that have kept apps or, in Scripts mode, a task or script on disk.
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
        if (items.Count > 0)
            lines.Add(_localizationService.GetString("BackgroundTasks_Removes", string.Join(", ", items)));

        if (RemovedOnText(kept) is { Length: > 0 } removedOn)
            lines.Add(removedOn);

        var returned = kept.Sum(app => app.ReturnCount);
        if (state is not null)
        {
            lines.Add(_localizationService.GetString(
                "BackgroundTasks_TaskAndScript", $@"{ScriptPaths.TaskFolder}\{name}", state.ScriptPath));
            if (state.Health != RemovalTaskHealth.NoTask)
                lines.Add(LastRunText(state.LastRun, state.LastResult));
        }
        else if (returned > 0)
        {
            lines.Add(_localizationService.GetString("BackgroundTasks_KeptApp_Returned", returned));
        }

        var title = _localizationService.GetStringOrDefault("BackgroundTasks_Card_" + name, name);
        var opacity = _keepRemovedOn ? 1.0 : 0.5;
        var icon = CategoryIcons[name];
        var detail = string.Join('\n', lines);
        if (state is null)
        {
            return new CategoryRow(title, detail, icon, opacity,
                StopText: _localizationService.GetString("BackgroundTasks_StopKeeping"),
                Stop: new AsyncRelayCommand(() => StopKeepingAsync(name, title)),
                ButtonsEnabled: _keepRemovedOn);
        }

        var healthKey = "BackgroundTasks_Health_" + state.Health;
        return new CategoryRow(title, detail, icon, opacity,
            _localizationService.GetString(healthKey),
            state.Health == RemovalTaskHealth.Active,
            _localizationService.GetString("BackgroundTasks_Repair"),
            state.Health != RemovalTaskHealth.Active ? new AsyncRelayCommand(() => RepairRemovalTaskAsync(name)) : null,
            _localizationService.GetString("BackgroundTasks_Remove"),
            new AsyncRelayCommand(() => RemoveRemovalTaskAsync(name)),
            ButtonsEnabled: _keepRemovedOn);
    }

    private CategoryRow CreateRedirectRow(RemovalTaskState state)
    {
        var healthKey = state.Health == RemovalTaskHealth.ScriptMissing
            ? "BackgroundTasks_Redirect_FilesMissing"
            : "BackgroundTasks_Health_" + state.Health;
        var details = _localizationService.GetString(
            "BackgroundTasks_Redirect_Details", $@"{ScriptPaths.TaskFolder}\{ScriptPaths.OpenWebSearchTaskName}", state.ScriptPath);
        if (state.Health != RemovalTaskHealth.NoTask)
            details += "\n" + LastRunText(state.LastRun, state.LastResult);

        return new CategoryRow(
            _localizationService.GetString("BackgroundTasks_Redirect_Title"),
            details,
            RedirectIconKey,
            1.0,
            _localizationService.GetString(healthKey),
            state.Health == RemovalTaskHealth.Active);
    }

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
            _selectedModeOption = previous;
            OnPropertyChanged(nameof(SelectedModeOption));
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
        if (!enabled && _removalTasks.Count > 0
            && !await ConfirmAsync("BackgroundTasks_TurnOff_Title", "BackgroundTasks_TurnOff_Message"))
        {
            _keepRemovedOn = true;
            OnPropertyChanged(nameof(KeepRemovedEnabled));
            return;
        }

        var result = await _backgroundTasks.SetKeepRemovedOnAsync(enabled);
        if (!result.Success)
            _logService.LogWarning($"Could not change keeping apps removed: {result.ErrorMessage}");

        await RefreshBackgroundTasksAsync();
    }

    private async Task CheckNowAsync()
    {
        await _backgroundTasks.MaintainAsync();
        await RefreshBackgroundTasksAsync();
    }

    private async Task OpenLogAsync()
    {
        try
        {
            await _explorerWindowManager.OpenFolderAsync(Path.Combine(AppDataPaths.MachineRoot, "Logs"));
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error opening logs folder: {ex.Message}", ex);
        }
    }

    private async Task OpenScriptFolderAsync()
    {
        try
        {
            await _explorerWindowManager.OpenFolderAsync(ScriptPaths.ScriptsDirectory);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error opening the script folder: {ex.Message}", ex);
        }
    }

    private async Task StopKeepingAsync(string category, string title)
    {
        if (!await ConfirmAsync("BackgroundTasks_StopKeeping_Title", "BackgroundTasks_StopKeeping_Message", title))
            return;

        foreach (var app in _keptApps.Where(app => CategoryOf(app) == category))
            _backgroundTasks.RemoveKeptApp(app.Id);

        _keptApps = _backgroundTasks.GetKeptApps();
        RebuildBackgroundRows();
    }

    private async Task RepairRemovalTaskAsync(string name)
    {
        var result = await _backgroundTasks.RepairRemovalTaskAsync(name);
        if (!result.Success)
            _logService.LogWarning($"Could not repair the removal task '{name}': {result.ErrorMessage}");

        await RefreshBackgroundTasksAsync();
    }

    private async Task RemoveRemovalTaskAsync(string name)
    {
        var result = await _backgroundTasks.RemoveRemovalTaskAsync(name);
        if (!result.Success)
            _logService.LogWarning($"Could not remove the removal task '{name}': {result.ErrorMessage}");

        await RefreshBackgroundTasksAsync();
    }

    private async Task RemoveOtherTaskAsync(WinhanceTaskInfo task)
    {
        var result = await _scheduledTaskService.UnregisterScheduledTaskAsync(task.Name);
        if (!result.Success)
            _logService.LogWarning($"Could not remove the task '{task.Name}': {result.ErrorMessage}");

        _otherTasks = await _backgroundTasks.GetOtherTasksAsync();
        RebuildBackgroundRows();
    }

    private async Task RepairTaskSafetyAsync()
    {
        await _backgroundTasks.RepairTaskSafetyAsync();
        await RefreshBackgroundTasksAsync();
    }

    private void OnMaintained(object? sender, EventArgs e) =>
        _dispatcherService.RunOnUIThread(() => RefreshBackgroundTasksAsync().FireAndForget(_logService));

    private void OnBackgroundLanguageChanged()
    {
        foreach (var option in ModeOptions)
            option.DisplayText = ModeText((KeepRemovedMode)option.Value);
        OnPropertyChanged(nameof(BackgroundTasksLabel));
        OnPropertyChanged(nameof(KeepRemovedHeader));
        OnPropertyChanged(nameof(KeepRemovedDescription));
        OnPropertyChanged(nameof(ModeHeader));
        OnPropertyChanged(nameof(ModeDescription));
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
    string Title,
    string Detail,
    string IconKey,
    double Opacity,
    string Pill = "",
    bool Healthy = false,
    string RepairText = "",
    ICommand? Repair = null,
    string RemoveText = "",
    ICommand? Remove = null,
    string StopText = "",
    ICommand? Stop = null,
    bool ButtonsEnabled = true)
{
    public bool ShowHealthy => Pill.Length > 0 && Healthy;

    public bool ShowUnhealthy => Pill.Length > 0 && !Healthy;

    public bool CanRepair => Repair is not null;

    public bool CanRemove => Remove is not null;

    public bool CanStop => Stop is not null;
}
