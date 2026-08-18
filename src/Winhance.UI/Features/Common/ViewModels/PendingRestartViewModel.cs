using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.ViewModels;

// Winhance does not restart Explorer as a side effect of applying a setting (once per toggle could leave the
// user with no shell); this bar is how the user performs the single restart when ready.
public partial class PendingRestartViewModel : ObservableObject, IDisposable
{
    private readonly IPendingRestartService _pendingRestartService;
    private readonly IExplorerRestartService _explorerRestartService;
    private readonly IEventBus _eventBus;
    private readonly ILocalizationService _localizationService;
    private readonly IConfigImportState _configImportState;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogService _logService;
    private readonly ISubscriptionToken? _pendingChangedSubscription;
    private bool _disposed;

    // There is no dismiss - the pending state is real, and hiding it would leave the user believing their changes
    // already took effect.
    [ObservableProperty]
    public partial bool IsBarVisible { get; set; }

    [ObservableProperty]
    public partial bool IsRestarting { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; }

    [ObservableProperty]
    public partial string ButtonText { get; set; }

    [ObservableProperty]
    public partial string RestartingText { get; set; }

    // The count lives here as a list of names, so no localized string has to agree with a number.
    [ObservableProperty]
    public partial string TooltipText { get; set; }

    // False during a config import or a running task, so a restart cannot land mid-apply; the bar stays visible,
    // only the button greys out.
    [ObservableProperty]
    public partial bool CanRestart { get; set; }

    public PendingRestartViewModel(
        IPendingRestartService pendingRestartService,
        IExplorerRestartService explorerRestartService,
        IEventBus eventBus,
        ILocalizationService localizationService,
        IConfigImportState configImportState,
        ITaskProgressService taskProgressService,
        IDispatcherService dispatcherService,
        ILogService logService)
    {
        _pendingRestartService = pendingRestartService;
        _explorerRestartService = explorerRestartService;
        _eventBus = eventBus;
        _localizationService = localizationService;
        _configImportState = configImportState;
        _taskProgressService = taskProgressService;
        _dispatcherService = dispatcherService;
        _logService = logService;

        // Settings apply on background threads, so this event does NOT arrive on the dispatcher.
        // Marshalling here rather than in the host keeps the guarantee inside the class: a second
        // host cannot forget to do it, and Refresh writes bound properties.
        _pendingChangedSubscription = _eventBus.Subscribe<PendingRestartChangedEvent>(
            _ => _dispatcherService.RunOnUIThread(Refresh));

        Refresh();
    }

    public void Refresh()
    {
        IsBarVisible = _pendingRestartService.IsPending;
        Message = Localize("PendingRestart_Message");
        ButtonText = Localize("PendingRestart_Button");
        RestartingText = Localize("PendingRestart_Restarting");
        TooltipText = BuildTooltip();
        CanRestart = IsBarVisible && !IsRestarting && !_configImportState.IsActive && !_taskProgressService.IsTaskRunning;
    }

    [RelayCommand]
    private async Task RestartAsync()
    {
        if (IsRestarting)
            return;

        IsRestarting = true;
        CanRestart = false;
        try
        {
            // The service clears the pending state on success only, so a failure naturally leaves the
            // bar up and the user a way to retry.
            await _explorerRestartService.RestartAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // This is a [RelayCommand], so an unhandled throw is rethrown on the UI thread and takes
            // the process down - while the user is staring at no desktop and trying to recover.
            _logService.LogError("Failed to restart Explorer from the pending-restart bar", ex);
        }
        finally
        {
            IsRestarting = false;
            Refresh();
        }
    }

    // Falls back to the catalog's English name, then the raw ID, so an unknown ID degrades rather than throwing.
    private string BuildTooltip()
    {
        var names = _pendingRestartService.PendingSettingIds
            .Select(ResolveDisplayName)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return names.Count == 0 ? string.Empty : string.Join(Environment.NewLine, names);
    }

    private string ResolveDisplayName(string settingId)
    {
        var setting = SettingCatalog.Find(settingId);
        if (setting is null)
            return settingId;

        var localized = Localize(SettingLocalizationKeys.Name(setting));
        return string.IsNullOrEmpty(localized) ? setting.Display.Name : localized;
    }

    private string Localize(string key) =>
        _localizationService.TryGetString(key, out var value) ? value : string.Empty;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_pendingChangedSubscription != null)
            _eventBus.Unsubscribe(_pendingChangedSubscription);

        GC.SuppressFinalize(this);
    }
}
