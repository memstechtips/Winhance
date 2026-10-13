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

public record AppCategory(string GroupName, string DisplayName, string IconGlyph, IReadOnlyList<AppItemViewModel> Apps);

// How an update run ended. UpdatesKnown is the scan after it: only then does a row without an update mean it updated.
public readonly record struct AppUpdateRun(bool Started, bool Cancelled, bool UpdatesKnown);

public partial class ExternalAppsViewModel : BaseViewModel, IExternalAppsItemsProvider
{
    private readonly IExternalAppsService _externalAppsService;
    private readonly IInstalledAppsService _installedAppsService;
    private readonly IAppInstallationService _appInstallationService;
    private readonly IStagedAppInstaller _stagedAppInstaller;
    private readonly ITaskProgressService _progressService;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IThemeService _themeService;
    private readonly IAppIconResolver? _iconResolver;

    public ExternalAppsViewModel(
        IExternalAppsService externalAppsService,
        IInstalledAppsService installedAppsService,
        IAppInstallationService appInstallationService,
        IStagedAppInstaller stagedAppInstaller,
        ITaskProgressService progressService,
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IThemeService themeService,
        IAppIconResolver? iconResolver = null)
    {
        _externalAppsService = externalAppsService;
        _installedAppsService = installedAppsService;
        _appInstallationService = appInstallationService;
        _stagedAppInstaller = stagedAppInstaller;
        _progressService = progressService;
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _dispatcherService = dispatcherService;
        _themeService = themeService;
        _iconResolver = iconResolver;

        _localizationService.LanguageChanged += OnLanguageChanged;
        _externalAppsService.WinGetReady += OnWinGetInstalled;

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

    private IReadOnlyList<AppCategory> _categories = new List<AppCategory>();
    public IReadOnlyList<AppCategory> Categories
    {
        get => _categories;
        private set => SetProperty(ref _categories, value);
    }

    private static readonly Dictionary<string, string> CategoryGlyphs = new()
    {
        ["Browsers"] = "\uE774",
        ["Compression"] = "\uE8B7",
        ["Customization Utilities"] = "\uE790",
        ["Development Apps"] = "\uE943",
        ["Document Viewers"] = "\uE8A5",
        ["File & Disk Management"] = "\uEDA2",
        ["Gaming"] = "\uE7FC",
        ["Imaging"] = "\uE722",
        ["Messaging, Email & Calendar"] = "\uE715",
        ["Multimedia (Audio & Video)"] = "\uE8B2",
        ["Online Storage & Backup"] = "\uE753",
        ["Optical Disc Tools"] = "\uE958",
        ["Other Utilities"] = "\uE74C",
        ["Privacy & Security"] = "\uE72E",
        ["Remote Access"] = "\uE8AF",
        ["Runtimes & Dependencies"] = "\uE756",
        [OtherInstalledItem.GroupName] = "\uE71D",
        [WinGetPackageItem.GroupName] = "\uE721",
        [ChocoPackageItem.GroupName] = "\uE7B8",
    };

    private void RebuildCategories()
    {
        var filtered = Items.Where(a => FilterItem(a)).ToList();
        var groups = filtered
            .GroupBy(a => a.GroupName)
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .OrderBy(g => GroupRank(g.Key))
            .ThenBy(g => g.Key);

        Categories = groups
            .Select(group => new AppCategory(group.Key, group.First().CategoryDisplayName,
                CategoryGlyphs.GetValueOrDefault(group.Key, "\uE74C"), AppSortHelper.Order(group, SortMode).ToList()))
            .ToList();
    }

    // The catalog's categories, then the apps it does not list, then search results.
    private static int GroupRank(string group) => group switch
    {
        OtherInstalledItem.GroupName => 1,
        WinGetPackageItem.GroupName => 2,
        ChocoPackageItem.GroupName => 3,
        _ => 0,
    };

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

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkingInBackground))]
    public partial bool IsSearchingPackages { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkingInBackground))]
    public partial bool IsScanningInstalledApps { get; set; }

    public bool IsWorkingInBackground => IsSearchingPackages || IsScanningInstalledApps;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPackageSearchStatus))]
    public partial string PackageSearchStatus { get; set; } = string.Empty;

    public bool HasPackageSearchStatus => !string.IsNullOrEmpty(PackageSearchStatus);

    // Search covers Chocolatey only through choco itself, so without it the results are winget's alone.
    [ObservableProperty]
    public partial bool IsChocolateyNoteVisible { get; set; }

    public string ChocolateyNoteText => _localizationService.GetString("ExternalApps_ChocolateyNotInstalled");

    private const int PackageSearchMinLength = 3;
    private static readonly TimeSpan PackageSearchDelay = TimeSpan.FromMilliseconds(500);
    private CancellationTokenSource? _packageSearchCts;

    [ObservableProperty]
    public partial AppSortMode SortMode { get; set; } = AppSortMode.NameAscInstalledFirst;

    partial void OnSortModeChanged(AppSortMode value)
    {
        AppSortHelper.ApplySortDescriptions(ItemsView, value);
        RebuildCategories();
    }

    [ObservableProperty]
    public partial AppFilter Filter { get; set; }

    partial void OnFilterChanged(AppFilter value) => RefreshFilter();

    private void RefreshFilter()
    {
        using (ItemsView.DeferRefresh())
        {
            ItemsView.RefreshFilter();
        }
        RebuildCategories();
    }

    private IEnumerable<AppItemViewModel> SelectableItems => Items.Where(a => a.IsSelectable);

    public bool IsAllSelected =>
        SelectableItems.Any() && SelectableItems.All(a => a.IsSelected);
    public bool IsAllSelectedInstalled =>
        SelectableItems.Any(a => a.IsInstalled) && SelectableItems.Where(a => a.IsInstalled).All(a => a.IsSelected);
    public bool IsAllSelectedNotInstalled =>
        SelectableItems.Any(a => !a.IsInstalled) && SelectableItems.Where(a => !a.IsInstalled).All(a => a.IsSelected);

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var newValue = !IsAllSelected;
        foreach (var item in SelectableItems)
            item.IsSelected = newValue;
        NotifySelectionStateChanged();
    }

    [RelayCommand]
    private void ToggleSelectAllInstalled()
    {
        var newValue = !IsAllSelectedInstalled;
        foreach (var item in SelectableItems.Where(a => a.IsInstalled))
            item.IsSelected = newValue;
        NotifySelectionStateChanged();
    }

    [RelayCommand]
    private void ToggleSelectAllNotInstalled()
    {
        var newValue = !IsAllSelectedNotInstalled;
        foreach (var item in SelectableItems.Where(a => !a.IsInstalled))
            item.IsSelected = newValue;
        NotifySelectionStateChanged();
    }

    [ObservableProperty]
    public partial bool AuthorsIntent { get; set; }

    partial void OnAuthorsIntentChanged(bool value)
    {
        foreach (var item in Items)
            item.AuthorsIntent = value;
        NotifySelectionStateChanged();
    }

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsAllSelectedInstalled));
        OnPropertyChanged(nameof(IsAllSelectedNotInstalled));
        OnPropertyChanged(nameof(HasSelectedItems));
        SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public string SelectAllLabel => _localizationService.GetStringOrDefault("Common_SelectAll", "Select All");
    public string SelectAllInstalledLabel => _localizationService.GetStringOrDefault("Common_SelectAll_Installed", "Select All Installed");
    public string SelectAllNotInstalledLabel => _localizationService.GetStringOrDefault("Common_SelectAll_NotInstalled", "Select All Not Installed");

    [ObservableProperty]
    public partial bool IsTaskRunning { get; set; }

    public event EventHandler? SelectedItemsChanged;

    public bool HasSelectedItems => Items.Any(a => a.IsSelected);

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilter();
        StartPackageSearch(value);
    }

    // Search picks are the current results plus earlier ticked ones, so the text filter never hides them.
    private bool FilterItem(object obj)
    {
        if (obj is AppItemViewModel app)
        {
            var passesFilter = Filter switch
            {
                AppFilter.Installed => app.IsInstalled,
                AppFilter.UpdatesAvailable => app.HasUpdate,
                _ => true,
            };
            return passesFilter
                && (PackageSearchItem.Is(app.Id)
                    || SearchHelper.MatchesSearchTerm(SearchText, app.Name, app.Description, app.Id));
        }
        return true;
    }

    private void StartPackageSearch(string query)
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _packageSearchCts, cts);
        previous?.Cancel();
        previous?.Dispose();

        if (!IsInitialized)
            return;

        query = query.Trim();
        if (query.Length < PackageSearchMinLength)
        {
            IsSearchingPackages = false;
            IsChocolateyNoteVisible = false;
            PackageSearchStatus = string.Empty;
            ShowSearchResults([]);
            return;
        }

        SearchPackagesAfterDelayAsync(query, cts.Token).FireAndForget(_logService);
    }

    private async Task SearchPackagesAfterDelayAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(PackageSearchDelay, cancellationToken);
            await SearchPackagesAsync(query, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer keystroke started its own search.
        }
    }

    internal async Task SearchPackagesAsync(string query, CancellationToken cancellationToken)
    {
        IsSearchingPackages = true;
        PackageSearchStatus = _localizationService.GetString("ExternalApps_PackageSearch_Searching");
        try
        {
            var result = await _externalAppsService.SearchPackagesAsync(query, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            IsChocolateyNoteVisible = !result.SearchedChocolatey;
            var shown = ShowSearchResults(result.Items);
            PackageSearchStatus = shown == 0
                ? _localizationService.GetString("ExternalApps_PackageSearch_None")
                : _localizationService.GetString("ExternalApps_PackageSearch_Found", shown);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
                IsSearchingPackages = false;
        }
    }

    // Ticked results stay so a pick survives the next search; unticked ones make way for the new results. A result
    // the PC already has as another installed app is that app, so it is not listed twice. Returns how many show.
    private int ShowSearchResults(IReadOnlyList<ItemDefinition> results)
    {
        using (ItemsView.DeferRefresh())
        {
            foreach (var stale in Items.Where(item => PackageSearchItem.Is(item.Id) && !item.IsSelected).ToList())
                RemoveItem(stale);
        }

        var installed = Items.Where(item => item.IsOtherInstalled).Select(item => item.Installed?.WinGetId).OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var shown = results.Where(result => result.WinGetPackageId?.FirstOrDefault() is not { } id || !installed.Contains(id)).ToList();
        AddItems(shown);
        return shown.Count;
    }

    private void RemoveItem(AppItemViewModel item)
    {
        item.PropertyChanged -= Item_PropertyChanged;
        item.Dispose();
        Items.Remove(item);
    }

    // A config file or answer file saves a search pick by its id alone; this rebuilds it so selection can find it. An
    // id an installed app already carries is that app.
    public void AddSearchItems(IEnumerable<string> ids) =>
        AddItems(ids.Where(id => Items.All(item => item.PackageId != id))
            .Select(PackageSearchItem.FromId).OfType<ItemDefinition>().ToList());

    private void AddItems(IReadOnlyList<ItemDefinition> definitions)
    {
        var newDefinitions = definitions
            .Where(definition => Items.All(item => item.Id != definition.Id))
            .DistinctBy(definition => definition.Id)
            .ToList();
        var added = LoadAppsIntoItems(newDefinitions);
        RebuildCategories();
        if (added.Count == 0)
            return;

        NotifySelectionStateChanged();
        CheckStatusAsync(added).FireAndForget(_logService);
        ResolveIconsAsync(added).FireAndForget(_logService);
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
        StatusText = _localizationService.GetString("Progress_LoadingExternalApps");

        try
        {
            foreach (var item in Items)
                item.Dispose();
            Items.Clear();

            var allItems = await _externalAppsService.GetAppsAsync();
            LoadAppsIntoItems(allItems);
        }
        catch (Exception ex)
        {
            _logService.LogError("Error loading app definitions", ex);
        }

        try
        {
            StatusText = _localizationService.GetString("Progress_CheckingInstallStatus");
            await CheckInstallationStatusAsync();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Install status check failed, items loaded without status: {ex.Message}");
        }

        // IsInitialized first: a handler that throws below must not leave the tab behind its loader for good.
        try
        {
            IsInitialized = true;
            RebuildCategories();
            NotifySelectionStateChanged();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Error finalizing: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }

        // Resolve icons in the background — never block the load flow. Callers such
        // as config-import review-mode setup await LoadItemsAsync, and the icon
        // pipeline is network-bound (rate-limit retries can run for over a minute).
        // Awaiting it here kept the import command's task pending that whole time,
        // leaving the Import Config button greyed out. ResolveIconsAsync logs and
        // swallows its own failures and refreshes each item's icon as it completes.
        ResolveIconsAsync(Items.ToList()).FireAndForget(_logService);
        HideUnavailableAsync().FireAndForget(_logService);
        RefreshInstalledAppsAsync().FireAndForget(_logService);
    }

    private Task<bool>? _installedAppsScan;

    // The installed-apps scan gives each catalog app its version and update, and lists the installed apps the catalog
    // does not. A scan already running read the PC before this call, so a fresh one queues behind it. False when the
    // scan failed, which leaves the rows as they were, or when it listed the apps without knowing their updates, which
    // leaves each row the update it showed before.
    public Task<bool> RefreshInstalledAppsAsync()
    {
        var previous = _installedAppsScan;
        return _installedAppsScan = ScanInstalledAppsAsync(previous);
    }

    private async Task<bool> ScanInstalledAppsAsync(Task? previous)
    {
        if (previous is not null)
            await previous;

        IsScanningInstalledApps = true;
        try
        {
            var scan = await _installedAppsService.GetInstalledAppsAsync();
            MergeInstalledApps(scan.Apps, scan.UpdatesKnown);
            return scan.UpdatesKnown;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error scanning installed apps", ex);
            return false;
        }
        finally
        {
            IsScanningInstalledApps = false;
        }
    }

    private void MergeInstalledApps(IReadOnlyList<InstalledApp> apps, bool updatesKnown)
    {
        var byCatalogId = apps.Where(app => app.CatalogId is not null).DistinctBy(app => app.CatalogId).ToDictionary(app => app.CatalogId!);
        var others = apps.Where(app => app.CatalogId is null).DistinctBy(app => app.Key).ToDictionary(app => app.Key);

        // An app a catalog row or a search pick already shows stays on that row and gets no row of its own.
        var shownElsewhere = new Dictionary<AppItemViewModel, InstalledApp>();
        foreach (var item in Items.Where(item => !item.IsOtherInstalled && !byCatalogId.ContainsKey(item.Id)))
        {
            if (others.Values.FirstOrDefault(other => IsSameApp(item, other)) is { } match)
            {
                shownElsewhere[item] = match;
                others.Remove(match.Key);
            }
        }

        using (ItemsView.DeferRefresh())
        {
            foreach (var gone in Items.Where(item => item.IsOtherInstalled && !others.ContainsKey(item.Id)).ToList())
                RemoveItem(gone);
        }

        var listed = Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        LoadAppsIntoItems(others.Values.Where(app => !listed.Contains(app.Key)).Select(OtherInstalledItem.Create));
        foreach (var item in Items)
        {
            var installed = item.IsOtherInstalled ? others[item.Id]
                : byCatalogId.GetValueOrDefault(item.Id) ?? shownElsewhere.GetValueOrDefault(item);
            item.Installed = updatesKnown ? installed : KeepKnownUpdate(item.Installed, installed);
        }

        RefreshFilter();
        NotifySelectionStateChanged();
    }

    // A scan that could not read the updates says nothing about them, so the app keeps the update it had. A changed
    // version means the app was updated since, and its update is gone.
    private static InstalledApp? KeepKnownUpdate(InstalledApp? previous, InstalledApp? scanned) =>
        previous is { HasUpdate: true } && scanned is { HasUpdate: false }
            && scanned.Key == previous.Key && scanned.Version == previous.Version
            ? previous
            : scanned;

    // The scan left this app without a catalog id. A shared package id says it is the row's app whatever the row's
    // status reads; a shared name only counts for a row already detected as installed.
    private static bool IsSameApp(AppItemViewModel item, InstalledApp app)
    {
        var definition = item.Definition;
        return (app.WinGetId is { } winGetId && definition.WinGetPackageId?.Contains(winGetId, StringComparer.OrdinalIgnoreCase) == true)
            || (app.MsStoreId is { } storeId && string.Equals(definition.MsStoreId, storeId, StringComparison.OrdinalIgnoreCase))
            || (app.PackageName is { } packageName && definition.AppxPackageName?.Contains(packageName, StringComparer.OrdinalIgnoreCase) == true)
            || (item.IsInstalled && string.Equals(definition.Name, app.Name, StringComparison.OrdinalIgnoreCase));
    }

    // An installed app stays: it can still be uninstalled, and this PC is proof it once installed.
    private async Task HideUnavailableAsync()
    {
        var unavailable = await _externalAppsService.GetUnavailableAppIdsAsync(
            Items.Where(item => !item.IsOtherInstalled).Select(item => item.Definition).ToList());
        if (unavailable is not { Count: > 0 })
            return;

        using (ItemsView.DeferRefresh())
        {
            foreach (var item in Items.Where(item => unavailable.Contains(item.Id) && !item.IsInstalled).ToList())
                RemoveItem(item);
        }

        RebuildCategories();
        NotifySelectionStateChanged();
    }

    private List<AppItemViewModel> LoadAppsIntoItems(IEnumerable<ItemDefinition> definitions)
    {
        var added = new List<AppItemViewModel>();
        foreach (var definition in definitions)
        {
            var viewModel = new AppItemViewModel(
                definition,
                _localizationService,
                _dispatcherService,
                _themeService) { AuthorsIntent = AuthorsIntent };
            viewModel.PropertyChanged += Item_PropertyChanged;
            Items.Add(viewModel);
            added.Add(viewModel);
        }
        return added;
    }

    // A ticked row gaining or losing its update changes what the Install button will do, like a tick does.
    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppItemViewModel.IsSelected)
            || (e.PropertyName == nameof(AppItemViewModel.HasUpdate) && sender is AppItemViewModel { IsSelected: true }))
        {
            NotifySelectionStateChanged();
        }
    }

    // No-op when no resolver was injected. Failures are logged and swallowed - icon resolution must never block the load flow.
    private async Task ResolveIconsAsync(IReadOnlyList<AppItemViewModel> items)
    {
        if (_iconResolver is null) return;

        try
        {
            var definitions = items.Select(item => item.Definition).ToList();
            // External Apps are vendor brand logos — cache them exactly as
            // shipped (no backplate crop, no light/dark variant synthesis).
            await _iconResolver.ResolveBatchAsync(definitions, applyThemeAdaptation: false);

            foreach (var item in items)
                item.NotifyIconChanged();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Icon resolution failed: {ex.Message}");
        }
    }

    // Serializes the install-status re-check. It runs from the Refresh button,
    // the WinGetReady event (OnWinGetInstalled) and post-install/remove
    // (RefreshKeepingFailedCheckedAsync / RefreshAfterOperationAsync, including config-apply); none are mutually
    // exclusive. Overlapping callers queue here instead of interleaving access
    // to the shared Items collection and the detection cache.
    private readonly SemaphoreSlim _statusCheckGate = new(1, 1);

    [RelayCommand]
    public Task CheckInstallationStatusAsync() => CheckStatusAsync(null);

    private async Task CheckStatusAsync(IReadOnlyList<AppItemViewModel>? items)
    {
        await _statusCheckGate.WaitAsync();
        try
        {
            // An installed app the catalog does not list takes its status from the installed-apps scan.
            items ??= Items.Where(item => !item.IsOtherInstalled).ToList();
            var definitions = items.Select(item => item.Definition).ToList();
            var statusResults = await _externalAppsService.CheckBatchInstalledAsync(definitions);

            using (ItemsView.DeferRefresh())
            {
                foreach (var item in items)
                {
                    if (statusResults.TryGetValue(item.Definition.Id, out bool isInstalled))
                    {
                        item.IsInstalled = isInstalled;
                    }
                }
            }
            if (Filter != AppFilter.All)
                RebuildCategories();
        }
        catch (Exception ex)
        {
            _logService.LogError("Error checking installation status", ex);
        }
        finally
        {
            _statusCheckGate.Release();
        }
    }

    [RelayCommand]
    public async Task RefreshInstallationStatusAsync()
    {
        if (!IsInitialized)
        {
            StatusText = _localizationService.GetString("Progress_WaitForInitialLoad");
            return;
        }

        IsLoading = true;
        StatusText = _localizationService.GetString("Progress_RefreshingStatus");

        try
        {
            _externalAppsService.InvalidateStatusCache();
            await CheckInstallationStatusAsync();
        }
        catch (Exception ex)
        {
            _logService.LogError("Error refreshing status", ex);
        }
        finally
        {
            IsLoading = false;
        }

        RefreshInstalledAppsAsync().FireAndForget(_logService);
    }

    private void OnWinGetInstalled(object? sender, EventArgs e)
    {
        _dispatcherService.RunOnUIThreadAsync(async () =>
        {
            if (IsInitialized)
            {
                _logService.LogInformation("WinGet installed — refreshing External Apps installation status");
                _externalAppsService.InvalidateStatusCache();
                await CheckInstallationStatusAsync();
            }
        }).FireAndForget(_logService);
    }

    public async Task InstallApps(bool skipConfirmation = false)
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (selectedItems.Count == 0) return;

        if (!skipConfirmation && !await ConfirmInstallAsync(selectedItems))
            return;

        await InstallAppsInternalAsync(selectedItems);
    }

    [RelayCommand]
    public async Task InstallAppsAsync()
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("ExternalApps_NoSelection_Install"),
                _localizationService.GetString("ExternalApps_NoSelection_Title"));
            return;
        }

        if (!await ConfirmInstallAsync(selectedItems))
            return;

        await InstallAppsInternalAsync(selectedItems);
    }

    // What the Install button runs: ticked apps with an update are updated, the rest go through install. Updates run
    // first so the install's own refresh is the last word on which rows stay ticked. Only installs are confirmed.
    // Cancelling the updates cancels the installs too.
    public async Task InstallOrUpdateSelectedAsync()
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("ExternalApps_NoSelection_Install"),
                _localizationService.GetString("ExternalApps_NoSelection_Title"));
            return;
        }

        var toUpdate = selectedItems.Where(a => a.HasUpdate).ToList();
        var toInstall = selectedItems.Except(toUpdate).ToList();
        if (toUpdate.Count > 0 && IsBusy)
        {
            await _dialogService.ShowWarningAsync(_localizationService.GetString("ExternalApps_UpdateBusy_Message"));
            return;
        }

        if (toInstall.Count > 0 && !await ConfirmInstallAsync(toInstall))
            return;

        var run = await UpdateAppsAsync(toUpdate);
        if (run.UpdatesKnown)
        {
            // An app the fresh scan still shows an update for did not update, and stays ticked for another try.
            foreach (var app in toUpdate)
                app.IsSelected = app.HasUpdate;
        }

        if (run.Cancelled)
            return;

        if (toInstall.Count > 0)
            await InstallAppsInternalAsync(toInstall);
    }

    private async Task<bool> ConfirmInstallAsync(List<AppItemViewModel> apps)
    {
        var response = await _dialogService.ShowConfirmationAsync(
            AppOperationConfirmation.Build(AppOperation.Install, apps.Select(a => a.Name), null, _localizationService));
        return response.Confirmed;
    }

    private async Task InstallAppsInternalAsync(List<AppItemViewModel> selectedItems)
    {
        var notInstalled = await InstallQueueAsync(
            selectedItems,
            (app, progress) => _appInstallationService.InstallAppAsync(app.Definition, progress, shouldRemoveFromBloatScript: false),
            []);

        await RefreshKeepingFailedCheckedAsync(selectedItems, notInstalled.Apps);
    }

    public async Task InstallOnNewPcAsync(IReadOnlyList<string> ids, string? ownInstallersFolder)
    {
        if (!IsInitialized)
            await LoadItemsAsync();

        AddSearchItems(ids);
        foreach (var item in Items)
            item.IsSelected = ids.Contains(item.Id);

        var apps = Items.Where(a => a.IsSelected).ToList();
        var unknownIds = ids.Where(id => Items.All(item => item.Id != id)).ToList();
        IReadOnlyList<StagedInstaller> ownInstallers = ownInstallersFolder is null
            ? []
            : _stagedAppInstaller.FindAll(ownInstallersFolder);

        var notInstalled = await InstallQueueAsync(
            apps,
            (app, progress) => _appInstallationService.InstallOnNewPcAsync(app.Definition, progress),
            ownInstallers);
        await RefreshKeepingFailedCheckedAsync(apps, notInstalled.Apps);

        var notInstalledNames = notInstalled.Apps.Select(a => a.Name)
            .Concat(notInstalled.OwnInstallers.Select(i => i.Name))
            .Concat(unknownIds)
            .ToList();
        var total = apps.Count + unknownIds.Count + ownInstallers.Count;
        var title = _localizationService.GetString("ExternalApps_NewPc_Title");
        if (notInstalledNames.Count == 0)
        {
            await _dialogService.ShowInformationAsync(
                _localizationService.GetString("ExternalApps_NewPc_Done"), title);
        }
        else
        {
            var message = _localizationService.GetString(
                "ExternalApps_NewPc_Failed", total - notInstalledNames.Count, total, string.Join("\n", notInstalledNames));
            await _dialogService.ShowWarningAsync(message, title);
        }
    }

    // Failed apps stay checked so Install Selected Items retries them.
    private async Task RefreshKeepingFailedCheckedAsync(List<AppItemViewModel> apps, List<AppItemViewModel> notInstalled)
    {
        _externalAppsService.InvalidateStatusCache();
        await CheckInstallationStatusAsync();

        foreach (var app in apps)
            app.IsSelected = notInstalled.Contains(app);

        NotifySelectionStateChanged();
        RefreshInstalledAppsAsync().FireAndForget(_logService);
    }

    // Returns what did not install. Rows, not names: two rows can share a name.
    private async Task<(List<AppItemViewModel> Apps, List<StagedInstaller> OwnInstallers)> InstallQueueAsync(
        List<AppItemViewModel> apps,
        Func<AppItemViewModel, IProgress<TaskProgressDetail>, Task<OperationResult<bool>>> installApp,
        IReadOnlyList<StagedInstaller> ownInstallers)
    {
        var names = apps.Select(a => a.Name).Concat(ownInstallers.Select(i => i.Name)).ToList();
        var failedApps = new List<AppItemViewModel>(apps);
        var failedInstallers = new List<StagedInstaller>(ownInstallers);

        IsTaskRunning = true;

        try
        {
            var cancellation = _progressService.StartTask(_localizationService.GetStringOrDefault("Progress_Task_InstallingExternalApps", "Installing External Apps"), false);
            var progress = _progressService.CreateDetailedProgress();

            for (int i = 0; i < names.Count; i++)
            {
                if (_progressService.ConsumeSkipNextRequest())
                    continue;

                progress.Report(new TaskProgressDetail
                {
                    StatusText = _localizationService.GetString("Progress_Installing", names[i]),
                    QueueTotal = names.Count,
                    QueueCurrent = i + 1,
                    QueueNextItemName = i + 1 < names.Count ? names[i + 1] : null
                });

                if (i < apps.Count)
                {
                    var result = await installApp(apps[i], progress);
                    if (result.Success && result.Result)
                    {
                        apps[i].IsInstalled = true;
                        failedApps.Remove(apps[i]);
                    }
                }
                else
                {
                    var installer = ownInstallers[i - apps.Count];
                    if (await _stagedAppInstaller.InstallAsync(installer, progress, cancellation.Token))
                        failedInstallers.Remove(installer);
                }
            }
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

        return (failedApps, failedInstallers);
    }

    [RelayCommand]
    public async Task UninstallAppsAsync()
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("ExternalApps_NoSelection_Uninstall"),
                _localizationService.GetString("ExternalApps_NoSelection_Title"));
            return;
        }

        var itemNames = selectedItems.Select(a => a.Name).ToList();
        var r = await _dialogService.ShowConfirmationAsync(
            AppOperationConfirmation.Build(AppOperation.Remove, itemNames, null, _localizationService));
        bool confirmed = r.Confirmed;
        if (!confirmed) return;

        IsTaskRunning = true;

        try
        {
            _progressService.StartTask(_localizationService.GetStringOrDefault("Progress_Task_UninstallingExternalApps", "Uninstalling External Apps"), false);
            var progress = _progressService.CreateDetailedProgress();

            for (int i = 0; i < selectedItems.Count; i++)
            {
                if (_progressService.ConsumeSkipNextRequest())
                    continue;

                var app = selectedItems[i];
                var nextName = i + 1 < selectedItems.Count ? selectedItems[i + 1].Name : null;
                progress.Report(new TaskProgressDetail
                {
                    StatusText = _localizationService.GetString("Progress_Uninstalling", app.Name),
                    QueueTotal = selectedItems.Count,
                    QueueCurrent = i + 1,
                    QueueNextItemName = nextName
                });

                var result = await _externalAppsService.UninstallAppAsync(app.Definition, progress);
                if (result.Success && result.Result)
                    app.IsInstalled = false;
            }
        }
        catch (Exception ex)
        {
            _progressService.FailTask();
            _logService.LogError("Error uninstalling apps", ex);
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
    }

    private async Task RefreshAfterOperationAsync()
    {
        _externalAppsService.InvalidateStatusCache();
        await CheckInstallationStatusAsync();
        ClearSelections();
        RefreshInstalledAppsAsync().FireAndForget(_logService);
    }

    private bool IsBusy => IsTaskRunning || _progressService.IsTaskRunning;

    // No confirmation: an update keeps the app and its settings, the way Store's Update all works. Not started when
    // there was nothing to update or another task holds the progress bar. Ticks are the caller's to change.
    public async Task<AppUpdateRun> UpdateAppsAsync(IReadOnlyList<AppItemViewModel> apps)
    {
        if (apps.Count == 0 || IsBusy)
            return default;

        IsTaskRunning = true;
        CancellationTokenSource? cancellation = null;
        bool cancelled;
        try
        {
            cancellation = _progressService.StartTask(_localizationService.GetString("Progress_Task_UpdatingApps"), false);
            var progress = _progressService.CreateDetailedProgress();

            for (int i = 0; i < apps.Count; i++)
            {
                if (_progressService.ConsumeSkipNextRequest())
                    continue;

                var app = apps[i];
                progress.Report(new TaskProgressDetail
                {
                    StatusText = _localizationService.GetString("Progress_Updating", app.Name),
                    QueueTotal = apps.Count,
                    QueueCurrent = i + 1,
                    QueueNextItemName = i + 1 < apps.Count ? apps[i + 1].Name : null,
                });

                var result = await _installedAppsService.UpdateAsync(app.Installed!, cancellation.Token);
                if (!result.Success)
                    _logService.LogWarning($"Update of {app.Name} failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _progressService.FailTask();
            _logService.LogError("Error updating apps", ex);
        }
        finally
        {
            cancelled = cancellation?.IsCancellationRequested == true;
            if (_progressService.IsTaskRunning)
                _progressService.CompleteTask();
            IsTaskRunning = false;
        }

        return new AppUpdateRun(Started: true, cancelled, await RefreshInstalledAppsAsync());
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
        OnPropertyChanged(nameof(SelectAllLabel));
        OnPropertyChanged(nameof(SelectAllInstalledLabel));
        OnPropertyChanged(nameof(SelectAllNotInstalledLabel));
        OnPropertyChanged(nameof(ChocolateyNoteText));
        RebuildCategories();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            _externalAppsService.WinGetReady -= OnWinGetInstalled;
            var search = Interlocked.Exchange(ref _packageSearchCts, null);
            search?.Cancel();
            search?.Dispose();
            foreach (var item in Items)
            {
                item.PropertyChanged -= Item_PropertyChanged;
                item.Dispose();
            }
            _statusCheckGate.Dispose();
        }
        base.Dispose(disposing);
    }
}
