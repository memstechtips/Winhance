using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Utils;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.SoftwareApps.Models;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

public partial class WindowsAppsViewModel : BaseViewModel, IWindowsAppsItemsProvider, ISelectAllSource
{
    private readonly IWindowsAppsService _windowsAppsService;
    private readonly IInstalledAppsService _installedAppsService;
    private readonly IAppInstallationService _appInstallationService;
    private readonly IWindowsAppUninstallService _windowsAppUninstallService;
    private readonly ITaskProgressService _progressService;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IThemeService _themeService;
    private readonly IAppIconResolver? _iconResolver;

    public WindowsAppsViewModel(
        IWindowsAppsService windowsAppsService,
        IInstalledAppsService installedAppsService,
        IAppInstallationService appInstallationService,
        IWindowsAppUninstallService windowsAppUninstallService,
        ITaskProgressService progressService,
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IThemeService themeService,
        IAppIconResolver? iconResolver = null)
    {
        _windowsAppsService = windowsAppsService;
        _installedAppsService = installedAppsService;
        _appInstallationService = appInstallationService;
        _windowsAppUninstallService = windowsAppUninstallService;
        _progressService = progressService;
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _dispatcherService = dispatcherService;
        _themeService = themeService;
        _iconResolver = iconResolver;

        _localizationService.LanguageChanged += OnLanguageChanged;

        Items = new ObservableCollection<AppItemViewModel>();
        ItemsView = new AdvancedCollectionView(Items, true);
        ItemsView.Filter = FilterItem;
        AppSortHelper.ApplySortDescriptions(ItemsView, SortMode);

        // Must come after ItemsView: OnSearchTextChanged uses it.
        StatusText = string.Empty;
        SearchText = string.Empty;
    }

    public ObservableCollection<AppItemViewModel> Items { get; }
    public AdvancedCollectionView ItemsView { get; }

    // Empty until the load finishes: listed earlier, every app still reads not installed, so the cards sort by name
    // behind the loader and jump when the status check re-sorts them installed first.
    public IEnumerable<AppItemViewModel> WindowsAppsFiltered => !IsInitialized ? [] : AppSortHelper.Order(
        Items.Where(a =>
            string.IsNullOrEmpty(a.Definition.CapabilityName) &&
            string.IsNullOrEmpty(a.Definition.OptionalFeatureName) &&
            FilterItem(a)),
        SortMode);

    public IEnumerable<AppItemViewModel> CapabilitiesFiltered => !IsInitialized ? [] : AppSortHelper.Order(
        Items.Where(a =>
            !string.IsNullOrEmpty(a.Definition.CapabilityName) &&
            FilterItem(a)),
        SortMode);

    public IEnumerable<AppItemViewModel> OptionalFeaturesFiltered => !IsInitialized ? [] : AppSortHelper.Order(
        Items.Where(a =>
            !string.IsNullOrEmpty(a.Definition.OptionalFeatureName) &&
            FilterItem(a)),
        SortMode);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentReady))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentReady))]
    public partial bool IsInitialized { get; set; }

    // The tab shows only its loader until this is true, on the first load and on every refresh.
    public bool IsContentReady => IsInitialized && !IsLoading;

    [ObservableProperty]
    public partial string StatusText { get; set; }

    // The enable runs in its own window, where Winhance cannot see how it ends.
    [ObservableProperty]
    public partial bool IsEnableHandedOffNoteVisible { get; set; }

    public string EnableHandedOffNoteText => _localizationService.GetString("WindowsApps_EnableHandedOff");

    [ObservableProperty]
    public partial bool IsRemovalDeferredNoteVisible { get; set; }

    public string RemovalDeferredNoteText => _localizationService.GetString("WindowsApps_RemovalDeferred");

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial AppSortMode SortMode { get; set; } = AppSortMode.NameAscInstalledFirst;

    partial void OnSortModeChanged(AppSortMode value)
    {
        AppSortHelper.ApplySortDescriptions(ItemsView, value);
        NotifyCardViewProperties();
    }

    // UpdatesAvailable is External Software's filter; here it lists everything.
    [ObservableProperty]
    public partial AppFilter Filter { get; set; }

    partial void OnFilterChanged(AppFilter value)
    {
        using (ItemsView.DeferRefresh())
        {
            ItemsView.RefreshFilter();
        }
        NotifyVisibleUpdatesChanged();
        NotifyCardViewProperties();
    }

    public string SectionAppsHeader => _localizationService.GetStringOrDefault("WindowsApps_Section_Apps", "Windows Apps");
    public string SectionCapabilitiesHeader => _localizationService.GetStringOrDefault("WindowsApps_Section_Capabilities", "Windows Capabilities");
    public string SectionOptionalFeaturesHeader => _localizationService.GetStringOrDefault("WindowsApps_Section_OptionalFeatures", "Windows Optional Features");

    public string SelectAllLabel => _localizationService.GetStringOrDefault("Common_SelectAll", "Select All");
    public string SelectAllInstalledLabel => _localizationService.GetStringOrDefault("Common_SelectAll_Installed", "Select All Installed");
    public string SelectAllNotInstalledLabel => _localizationService.GetStringOrDefault("Common_SelectAll_NotInstalled", "Select All Not Installed");
    public string SelectAllWithUpdatesLabel => _localizationService.GetString("Common_SelectAll_WithUpdates", UpdatesCount);

    // Only apps are updated here; capabilities and optional features have no package manager version.
    private IEnumerable<AppItemViewModel> UpdatableItems => Items.Where(a =>
        string.IsNullOrEmpty(a.Definition.CapabilityName) && string.IsNullOrEmpty(a.Definition.OptionalFeatureName));

    // The count and Select All With Updates cover the rows the filter shows, so they match what the user sees.
    private IEnumerable<AppItemViewModel> VisibleUpdatableItems => UpdatableItems.Where(a => FilterItem(a));

    public bool IsAllSelectedWithUpdates => UpdateSelection.IsAllSelected(VisibleUpdatableItems);
    public int UpdatesCount => VisibleUpdatableItems.Count(a => a.HasUpdate);
    public bool HasUpdates => VisibleUpdatableItems.Any(a => a.HasUpdate);

    public bool IsAllSelected =>
        Items.Count > 0 && Items.All(a => a.IsSelected);
    public bool IsAllSelectedInstalled =>
        Items.Any(a => a.IsInstalled) && Items.Where(a => a.IsInstalled).All(a => a.IsSelected);
    public bool IsAllSelectedNotInstalled =>
        Items.Any(a => !a.IsInstalled) && Items.Where(a => !a.IsInstalled).All(a => a.IsSelected);

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var newValue = !IsAllSelected;
        foreach (var item in Items)
            item.IsSelected = newValue;
        NotifySelectionStateChanged();
    }

    [RelayCommand]
    private void ToggleSelectAllInstalled()
    {
        var newValue = !IsAllSelectedInstalled;
        foreach (var item in Items.Where(a => a.IsInstalled))
            item.IsSelected = newValue;
        NotifySelectionStateChanged();
    }

    [RelayCommand]
    private void ToggleSelectAllNotInstalled()
    {
        var newValue = !IsAllSelectedNotInstalled;
        foreach (var item in Items.Where(a => !a.IsInstalled))
            item.IsSelected = newValue;
        NotifySelectionStateChanged();
    }

    [RelayCommand]
    private void ToggleSelectAllWithUpdates()
    {
        UpdateSelection.Toggle(VisibleUpdatableItems);
        NotifySelectionStateChanged();
    }

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsAllSelectedInstalled));
        OnPropertyChanged(nameof(IsAllSelectedNotInstalled));
        OnPropertyChanged(nameof(IsAllSelectedWithUpdates));
        OnPropertyChanged(nameof(HasSelectedItems));
        SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    [ObservableProperty]
    public partial bool IsTaskRunning { get; set; }

    public event EventHandler? SelectedItemsChanged;

    public bool HasSelectedItems => Items.Any(a => a.IsSelected);

    partial void OnSearchTextChanged(string value)
    {
        using (ItemsView.DeferRefresh())
        {
            ItemsView.RefreshFilter();
        }
        NotifyVisibleUpdatesChanged();
        NotifyCardViewProperties();
    }

    private void NotifyCardViewProperties()
    {
        OnPropertyChanged(nameof(WindowsAppsFiltered));
        OnPropertyChanged(nameof(CapabilitiesFiltered));
        OnPropertyChanged(nameof(OptionalFeaturesFiltered));
    }

    private bool FilterItem(object obj)
    {
        if (obj is AppItemViewModel app)
        {
            return (Filter != AppFilter.Installed || app.IsInstalled)
                && SearchHelper.MatchesSearchTerm(SearchText, app.Name, app.Description, app.Id);
        }
        return true;
    }

    public Task LoadItemsAsync() => LoadAppsAndCheckInstallationStatusAsync();

    private readonly object _loadGate = new();
    private Task? _loadTask;

    // Exactly once, idempotent: startup triggers this from two independent paths (StartupUiCoordinator, and
    // first-run backup-config creation via StartupOrchestrator). An `if (IsInitialized)` guard is not enough -
    // IsInitialized is only set when the load FINISHES - so two full passes would clobber Items and race over
    // icon-cache .tmp files. The first caller starts the load and every other caller awaits the same Task, on
    // the UI thread with a SynchronizationContext so continuations stay UI-affine.
    [RelayCommand]
    public Task LoadAppsAndCheckInstallationStatusAsync()
    {
        lock (_loadGate)
        {
            return _loadTask ??= _dispatcherService.RunOnUIThreadWithContextAsync(
                LoadAppsAndCheckInstallationStatusCoreAsync);
        }
    }

    private async Task LoadAppsAndCheckInstallationStatusCoreAsync()
    {
        IsLoading = true;
        StatusText = _localizationService.GetString("Progress_LoadingWindowsApps");

        try
        {
            foreach (var item in Items)
                item.Dispose();
            Items.Clear();

            var allItems = await _windowsAppsService.GetAppsAsync();
            var apps = allItems.Where(x => x.AppxPackageName?.Length > 0 || (x.WinGetPackageId != null && x.WinGetPackageId.Length > 0));
            var capabilities = allItems.Where(x => !string.IsNullOrEmpty(x.CapabilityName));
            var features = allItems.Where(x => !string.IsNullOrEmpty(x.OptionalFeatureName));

            LoadAppsIntoItems(apps.Concat(capabilities).Concat(features));
        }
        catch (Exception ex)
        {
            _logService.LogError("Error loading app definitions", ex);
        }

        try
        {
            await ScanAsync(Items.Select(item => item.Definition).ToList());
        }
        catch (Exception ex)
        {
            _logService.LogWarning(
                $"Install status check failed, items loaded without status ({ex.GetType().FullName}, HRESULT=0x{ex.HResult:X8}): {ex.Message}");
        }

        // IsInitialized first: a handler that throws below must not leave the tab behind its loader for good.
        try
        {
            IsInitialized = true;
            NotifyCardViewProperties();
            NotifySelectionStateChanged();
        }
        catch (Exception ex)
        {
            _logService.LogWarning(
                $"Error finalizing ({ex.GetType().FullName}, HRESULT=0x{ex.HResult:X8}): {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }

        // Icons fill in after the loader clears: a cold icon cache downloads them, which can take far longer than detection.
        ResolveIconsAsync(Items.ToList()).FireAndForget(_logService);
    }

    private void LoadAppsIntoItems(IEnumerable<ItemDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            var viewModel = new AppItemViewModel(
                definition,
                _localizationService,
                _dispatcherService,
                _themeService);
            viewModel.PropertyChanged += Item_PropertyChanged;
            Items.Add(viewModel);
        }
    }

    // A ticked row gaining or losing its update changes what the Install button will do, like a tick does.
    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppItemViewModel.HasUpdate))
        {
            OnPropertyChanged(nameof(IsAllSelectedWithUpdates));
            NotifyUpdateCountChanged();
        }
        if (e.PropertyName == nameof(AppItemViewModel.IsSelected)
            || (e.PropertyName == nameof(AppItemViewModel.HasUpdate) && sender is AppItemViewModel { IsSelected: true }))
        {
            NotifySelectionStateChanged();
        }
    }

    private void NotifyVisibleUpdatesChanged()
    {
        OnPropertyChanged(nameof(IsAllSelectedWithUpdates));
        NotifyUpdateCountChanged();
    }

    private void NotifyUpdateCountChanged()
    {
        OnPropertyChanged(nameof(UpdatesCount));
        OnPropertyChanged(nameof(HasUpdates));
        OnPropertyChanged(nameof(SelectAllWithUpdatesLabel));
    }

    // No-op when no resolver was injected. Failures are logged and swallowed - icon resolution must never block the load flow.
    private async Task ResolveIconsAsync(IReadOnlyList<AppItemViewModel> items)
    {
        if (_iconResolver is null) return;

        try
        {
            var definitions = items.Select(item => item.Definition).ToList();
            // Windows Apps get theme adaptation — backplate crop + light/dark
            // variant synthesis for monochrome system icons.
            await _iconResolver.ResolveBatchAsync(definitions, applyThemeAdaptation: true);

            foreach (var item in items)
                item.NotifyIconChanged();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Icon resolution failed: {ex.Message}");
        }
    }

    // The install status of the given rows and the updates of the apps among them, from one read of the PC. Callers
    // that arrive together share that read; the apply is synchronous, so two of them cannot interleave. False when
    // the scan failed or could not read updates, which leaves each row the update it showed before.
    private async Task<bool> ScanAsync(IReadOnlyList<ItemDefinition> definitions)
    {
        try
        {
            var scan = await _installedAppsService.GetInstalledAppsAsync(definitions, StatusRules.Windows);

            // NotificationDeferrer.Dispose() fires VectorChanged events the bound DataGrid handles by reading its
            // ItemsSource DependencyProperty, and DPs throw WinRT HRESULT off the UI thread.
            await _dispatcherService.RunOnUIThreadAsync(() =>
            {
                Apply(scan);
                return Task.CompletedTask;
            });
            return scan.UpdatesKnown;
        }
        catch (Exception ex)
        {
            _logService.LogError(
                $"Error scanning Windows apps ({ex.GetType().FullName}, HRESULT=0x{ex.HResult:X8}): {ex.Message}", ex);
            return false;
        }
    }

    private void Apply(InstalledAppsScan scan)
    {
        using (ItemsView.DeferRefresh())
        {
            foreach (var item in Items)
            {
                if (scan.Status.TryGetValue(item.Definition.Id, out var isInstalled))
                    item.IsInstalled = isInstalled;
            }
        }

        if (scan.UpdatesKnown)
        {
            var byCatalogId = scan.Apps.Where(app => app.CatalogId is not null).ToLookup(app => app.CatalogId!);
            foreach (var item in UpdatableItems.ToList())
            {
                var found = WindowsAppUpdate.Pick(byCatalogId[item.Id]);
                item.Installed = WindowsAppUpdate.Applies(found) ? found : null;
            }
        }

        NotifySelectionStateChanged();
    }

    // The apps only: no capability or optional feature is asked for, so DISM does not run. The tray's refresh.
    public Task<bool> RefreshUpdatesAsync() => ScanAsync(UpdatableItems.Select(item => item.Definition).ToList());

    private bool IsBusy => IsTaskRunning || _progressService.IsTaskRunning;

    // No confirmation: an update keeps the app and its settings. Not started when there was nothing to update or
    // another task holds the progress bar. Ticks are the caller's to change.
    public async Task<AppUpdateRun> UpdateAppsAsync(IReadOnlyList<AppItemViewModel> apps, bool askToCloseOpenApps = false)
    {
        if (apps.Count == 0 || IsBusy)
            return default;

        IsTaskRunning = true;
        bool cancelled;
        try
        {
            cancelled = await AppUpdateRunner.RunAsync(apps, _installedAppsService, _progressService, _localizationService, _logService,
                askToCloseOpenApps ? _dialogService : null);
        }
        finally
        {
            IsTaskRunning = false;
        }

        return new AppUpdateRun(Started: true, cancelled, await RefreshUpdatesAsync());
    }

    // False before the first load finishes. The updates come from AppUpdateMonitor, which scans once for both tabs.
    public async Task<bool> RecheckInstallationStatusAsync()
    {
        if (!IsInitialized)
        {
            StatusText = _localizationService.GetString("Progress_WaitForInitialLoad");
            return false;
        }

        IsLoading = true;

        try
        {
            _installedAppsService.Invalidate();
            await ScanAsync(Items.Select(item => item.Definition).ToList());
            NotifyCardViewProperties();
        }
        catch (Exception ex)
        {
            _logService.LogError("Error refreshing status", ex);
        }
        finally
        {
            IsLoading = false;
        }

        return true;
    }

    [RelayCommand]
    public async Task InstallAppsAsync()
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WindowsApps_NoSelection_Install"),
                _localizationService.GetString("WindowsApps_NoSelection_Title"));
            return;
        }

        var toUpdate = selectedItems.Where(a => a.HasUpdate).ToList();
        var toInstall = selectedItems.Except(toUpdate).ToList();
        if (toUpdate.Count > 0 && IsBusy)
        {
            await _dialogService.ShowWarningAsync(_localizationService.GetString("ExternalApps_UpdateBusy_Message"));
            return;
        }

        if (toInstall.Count > 0)
        {
            var r = await _dialogService.ShowConfirmationAsync(
                AppOperationConfirmation.Build(AppOperation.Install, toInstall.Select(a => a.Name).ToList(), null, _localizationService));
            if (!r.Confirmed) return;
        }

        var run = await UpdateAppsAsync(toUpdate, askToCloseOpenApps: true);
        if (run.UpdatesKnown)
        {
            // An app the fresh scan still shows an update for did not update, and stays ticked for another try.
            foreach (var app in toUpdate)
                app.IsSelected = app.HasUpdate;
        }

        if (run.Cancelled || toInstall.Count == 0)
            return;

        await InstallSelectedAsync(toInstall);
    }

    private async Task InstallSelectedAsync(List<AppItemViewModel> selectedItems)
    {
        var servicingApps = selectedItems
            .Where(a => !string.IsNullOrEmpty(a.Definition.CapabilityName) || !string.IsNullOrEmpty(a.Definition.OptionalFeatureName))
            .ToList();
        var installApps = selectedItems.Except(servicingApps).ToList();
        int queueTotal = installApps.Count + (servicingApps.Count > 0 ? 1 : 0);

        var failedNames = new List<string>();
        IsTaskRunning = true;
        IsEnableHandedOffNoteVisible = false;

        try
        {
            var cancellation = _progressService.StartTask(_localizationService.GetStringOrDefault("Progress_Task_InstallingWindowsApps", "Installing Windows Apps"), false);
            var progress = _progressService.CreateDetailedProgress();

            for (int i = 0; i < installApps.Count; i++)
            {
                if (_progressService.ConsumeSkipNextRequest())
                    continue;

                var app = installApps[i];
                var nextName = i + 1 < installApps.Count ? installApps[i + 1].Name : null;
                progress.Report(new TaskProgressDetail
                {
                    StatusText = _localizationService.GetString("Progress_Installing", app.Name),
                    QueueTotal = queueTotal,
                    QueueCurrent = i + 1,
                    QueueNextItemName = nextName
                });

                var result = await _appInstallationService.InstallAppAsync(app.Definition, progress, shouldRemoveFromBloatScript: true);
                if (result.Success && result.Result)
                    app.IsInstalled = true;
                else if (!cancellation.IsCancellationRequested)
                    failedNames.Add(app.Name);
            }

            IsEnableHandedOffNoteVisible = servicingApps.Count > 0
                && await DispatchEnableBatchAsync(servicingApps, progress, queueTotal);
        }
        catch (Exception ex)
        {
            _progressService.FailTask();
            _logService.LogError("Error installing apps", ex);
        }
        finally
        {
            if (_progressService.IsTaskRunning)
            {
                _progressService.CompleteTask();
            }
            IsTaskRunning = false;
        }

        await RefreshAfterOperationAsync();

        if (failedNames.Count > 0)
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WindowsApps_InstallFailed", string.Join("\n", failedNames)));
    }

    // Features and capabilities go into ONE window for the whole batch, named in the progress line, and
    // it is always the last queue slot. Nothing is marked installed here: the enable runs where Winhance
    // cannot see it, so only the detection pass in RefreshAfterOperationAsync may flip a pill.
    private async Task<bool> DispatchEnableBatchAsync(
        List<AppItemViewModel> apps,
        IProgress<TaskProgressDetail> progress,
        int queueTotal)
    {
        var names = string.Join(", ", apps.Select(a => a.Name));
        progress.Report(new TaskProgressDetail
        {
            StatusText = _localizationService.GetString("Progress_Installing", names),
            QueueTotal = queueTotal,
            QueueCurrent = queueTotal
        });

        var result = await _appInstallationService.EnableServicingBatchAsync(
            apps.Select(a => a.Definition).ToList(), progress);
        if (result.Success)
            return true;

        _logService.LogError($"Failed to start the enable for: {names}");
        return false;
    }

    public async Task<(bool Confirmed, bool SaveScripts)> ShowRemovalSummaryAndConfirm()
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (selectedItems.Count == 0) return (true, true);

        var checkboxText = _localizationService.GetString("Dialog_SaveRemovalScripts");
        var r = await _dialogService.ShowConfirmationAsync(
            AppOperationConfirmation.Build(AppOperation.Remove, selectedItems.Select(a => a.Definition), checkboxText, _localizationService, _localizationService.GetString("Dialog_SaveRemovalScripts_Description")));
        bool confirmed = r.Confirmed;
        bool checkboxChecked = r.CheckboxChecked;
        return (confirmed, checkboxChecked);
    }

    public async Task RemoveApps(bool skipConfirmation = false, bool saveRemovalScripts = true)
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (selectedItems.Count == 0) return;

        if (!skipConfirmation)
        {
            var checkboxText = _localizationService.GetString("Dialog_SaveRemovalScripts");
            var r = await _dialogService.ShowConfirmationAsync(
                AppOperationConfirmation.Build(AppOperation.Remove, selectedItems.Select(a => a.Definition), checkboxText, _localizationService, _localizationService.GetString("Dialog_SaveRemovalScripts_Description")));
            bool confirmed = r.Confirmed;
            bool checkboxChecked = r.CheckboxChecked;
            if (!confirmed) return;
            saveRemovalScripts = checkboxChecked;
        }

        await RemoveAppsInternalAsync(selectedItems, saveRemovalScripts);
    }

    [RelayCommand]
    public async Task RemoveAppsAsync()
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WindowsApps_NoSelection_Remove"),
                _localizationService.GetString("WindowsApps_NoSelection_Title"));
            return;
        }

        var checkboxText = _localizationService.GetString("Dialog_SaveRemovalScripts");
        var r = await _dialogService.ShowConfirmationAsync(
            AppOperationConfirmation.Build(AppOperation.Remove, selectedItems.Select(a => a.Definition), checkboxText, _localizationService, _localizationService.GetString("Dialog_SaveRemovalScripts_Description")));
        bool confirmed = r.Confirmed;
        bool saveScripts = r.CheckboxChecked;
        if (!confirmed) return;

        await RemoveAppsInternalAsync(selectedItems, saveScripts);
    }

    private async Task RemoveAppsInternalAsync(List<AppItemViewModel> selectedItems, bool saveRemovalScripts = true)
    {
        IsTaskRunning = true;
        IsRemovalDeferredNoteVisible = false;

        try
        {
            var definitions = selectedItems.Select(a => a.Definition).ToList();
            var result = await _windowsAppUninstallService.UninstallAppsInParallelAsync(definitions, saveRemovalScripts);

            if (result.Success)
            {
                // A deferred removal has not happened yet, so nothing is marked uninstalled.
                IsRemovalDeferredNoteVisible = !string.IsNullOrEmpty(result.InfoMessage);
                if (!IsRemovalDeferredNoteVisible)
                {
                    foreach (var item in selectedItems)
                    {
                        item.IsInstalled = false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("Error removing apps", ex);
        }
        finally
        {
            IsTaskRunning = false;
        }

        await RefreshAfterOperationAsync();
    }

    private async Task RefreshAfterOperationAsync()
    {
        _installedAppsService.Invalidate();
        await ScanAsync(Items.Select(item => item.Definition).ToList());
        NotifyCardViewProperties();
        ClearSelections();
    }

    [RelayCommand]
    public void ClearSelections()
    {
        foreach (var item in Items)
        {
            item.IsSelected = false;
        }
        NotifySelectionStateChanged();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SectionAppsHeader));
        OnPropertyChanged(nameof(SectionCapabilitiesHeader));
        OnPropertyChanged(nameof(SectionOptionalFeaturesHeader));
        OnPropertyChanged(nameof(SelectAllLabel));
        OnPropertyChanged(nameof(SelectAllInstalledLabel));
        OnPropertyChanged(nameof(SelectAllNotInstalledLabel));
        OnPropertyChanged(nameof(SelectAllWithUpdatesLabel));
        OnPropertyChanged(nameof(EnableHandedOffNoteText));
        OnPropertyChanged(nameof(RemovalDeferredNoteText));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            foreach (var item in Items)
            {
                item.PropertyChanged -= Item_PropertyChanged;
                item.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
