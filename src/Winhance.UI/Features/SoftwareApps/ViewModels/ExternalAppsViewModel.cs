using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using FluentIcons.Common;
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

public record AppCategory(string GroupName, string DisplayName, Icon SectionIcon, IReadOnlyList<AppItemViewModel> Apps)
{
    // A group's accessible name is its ToString.
    public override string ToString() => DisplayName;

    // A new list makes a view rebuild every row it shows and jump back to the top. Rows update in place, so a view
    // swaps its list only when a row came, went or moved.
    public static bool SameLayout(IReadOnlyList<AppCategory> current, IReadOnlyList<AppCategory> rebuilt) =>
        current.Count == rebuilt.Count
        && current.Zip(rebuilt).All(pair => pair.First.GroupName == pair.Second.GroupName
            && pair.First.DisplayName == pair.Second.DisplayName
            && pair.First.Apps.SequenceEqual(pair.Second.Apps));
}

// How an update run ended. UpdatesKnown is the scan after it: only then does a row without an update mean it updated.
public readonly record struct AppUpdateRun(bool Started, bool Cancelled, bool UpdatesKnown);

public partial class ExternalAppsViewModel : BaseViewModel, IExternalAppsItemsProvider, ISelectAllSource
{
    private static readonly string[] VisibleRowProperties =
    [
        nameof(IsAllSelected), nameof(IsAllSelectedInstalled), nameof(IsAllSelectedNotInstalled), nameof(IsAllSelectedWithUpdates),
        nameof(UpdatesCount), nameof(HasUpdates), nameof(SelectAllWithUpdatesLabel),
    ];

    private static readonly string[] LocalizedProperties =
    [
        nameof(SelectAllWithUpdatesLabel), nameof(SelectAllLabel), nameof(SelectAllInstalledLabel), nameof(SelectAllNotInstalledLabel),
        nameof(ChocolateyNoteText), nameof(StatusText), nameof(PackageSearchStatus),
    ];

    private readonly IExternalAppsService _externalAppsService;
    private readonly IInstalledAppsService _installedAppsService;
    private readonly IAppInstallationService _appInstallationService;
    private readonly IStagedAppInstaller _stagedAppInstaller;
    private readonly IInstallOptionsService _installOptionsService;
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
        IInstallOptionsService installOptionsService,
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
        _installOptionsService = installOptionsService;
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

        StatusKey = string.Empty;
        // Must come after ItemsView: OnSearchTextChanged uses it.
        SearchText = string.Empty;
    }

    public ObservableCollection<AppItemViewModel> Items { get; }
    public AdvancedCollectionView ItemsView { get; }

    [ObservableProperty]
    public partial IReadOnlyList<AppCategory> Categories { get; private set; } = [];

    private static readonly Dictionary<string, Icon> CategoryIcons = new()
    {
        ["Browsers"] = Icon.Globe,
        ["Compression"] = Icon.FolderZip,
        ["Customization Utilities"] = Icon.Color,
        ["Development Apps"] = Icon.Code,
        ["Document Viewers"] = Icon.DocumentText,
        ["File & Disk Management"] = Icon.HardDrive,
        ["Gaming"] = Icon.Games,
        ["Imaging"] = Icon.Camera,
        ["Messaging, Email & Calendar"] = Icon.Mail,
        ["Multimedia (Audio & Video)"] = Icon.Video,
        ["Online Storage & Backup"] = Icon.Cloud,
        ["Optical Disc Tools"] = Icon.Target,
        ["Other Utilities"] = Icon.Toolbox,
        ["Privacy & Security"] = Icon.LockClosed,
        ["Remote Access"] = Icon.DesktopArrowRight,
        ["Runtimes & Dependencies"] = Icon.WindowConsole,
        [OtherInstalledItem.GroupName] = Icon.Apps,
        [WinGetPackageItem.GroupName] = Icon.Search,
        [ChocoPackageItem.GroupName] = Icon.Box,
    };

    private void RebuildCategories()
    {
        var filtered = Items.Where(a => FilterItem(a)).ToList();
        var groups = filtered
            .GroupBy(a => a.GroupName)
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .OrderBy(g => GroupRank(g.Key))
            .ThenBy(g => g.Key);

        var rebuilt = groups
            .Select(group => new AppCategory(group.Key, group.First().CategoryDisplayName,
                CategoryIcons.GetValueOrDefault(group.Key, Icon.Toolbox), AppSortHelper.Order(group, SortMode).ToList()))
            .ToList();

        if (!AppCategory.SameLayout(Categories, rebuilt))
            Categories = rebuilt;
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

    // When a scan last read every installed app's update; a scheduled check skips a read this recent.
    public DateTime? UpdatesReadUtc { get; private set; }

    // The tab shows only its loader until this is true, on the first load and on every refresh.
    public bool IsContentReady => IsInitialized && !IsLoading;

    // Held as a key so a language switch re-reads it.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial string StatusKey { get; private set; }

    public string StatusText => StatusKey.Length == 0 ? string.Empty : _localizationService.GetString(StatusKey);

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkingInBackground))]
    public partial bool IsSearchingPackages { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkingInBackground))]
    public partial bool IsScanningInstalledApps { get; set; }

    public bool IsWorkingInBackground => IsSearchingPackages || IsScanningInstalledApps;

    // Held as a key and a count so a language switch re-reads it.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PackageSearchStatus))]
    [NotifyPropertyChangedFor(nameof(HasPackageSearchStatus))]
    public partial string PackageSearchStatusKey { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PackageSearchStatus))]
    public partial int PackageSearchFoundCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PackageSearchStatus))]
    public partial int PackageSearchPickCount { get; private set; }

    public string PackageSearchStatus => PackageSearchStatusKey switch
    {
        "" => string.Empty,
        "ExternalApps_PackageSearch_Found" => _localizationService.GetString(
            PackageSearchFoundCount == 1 ? "ExternalApps_PackageSearch_Found_One" : "ExternalApps_PackageSearch_Found",
            PackageSearchFoundCount),
        "ExternalApps_PackageSearch_FoundWithPicks" => _localizationService.GetString(
            (PackageSearchFoundCount, PackageSearchPickCount) switch
            {
                (1, 1) => "ExternalApps_PackageSearch_FoundWithPicks_One_OnePick",
                (1, _) => "ExternalApps_PackageSearch_FoundWithPicks_One",
                (_, 1) => "ExternalApps_PackageSearch_FoundWithPicks_OnePick",
                _ => "ExternalApps_PackageSearch_FoundWithPicks",
            },
            PackageSearchFoundCount,
            PackageSearchPickCount),
        "ExternalApps_PackageSearch_NoneWithPicks" => _localizationService.GetString(
            PackageSearchPickCount == 1 ? "ExternalApps_PackageSearch_NoneWithPicks_One" : "ExternalApps_PackageSearch_NoneWithPicks",
            PackageSearchPickCount),
        var key => _localizationService.GetString(key),
    };

    public bool HasPackageSearchStatus => PackageSearchStatusKey.Length > 0;

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
        NotifyVisibleRowsChanged();
    }

    // The select-all boxes and the update count cover the rows the filter and search show, so they match what the
    // user sees.
    private IEnumerable<AppItemViewModel> VisibleSelectableItems => Items.Where(a => a.IsSelectable && FilterItem(a));

    public bool IsAllSelected => SelectAllRows.AreAllSelected(VisibleSelectableItems);
    public bool IsAllSelectedInstalled => SelectAllRows.AreAllSelected(VisibleSelectableItems.Where(a => a.IsInstalled));
    public bool IsAllSelectedNotInstalled => SelectAllRows.AreAllSelected(VisibleSelectableItems.Where(a => !a.IsInstalled));
    public bool IsAllSelectedWithUpdates => SelectAllRows.AreAllSelected(VisibleSelectableItems.Where(a => a.HasUpdate));

    public int UpdatesCount => Items.Count(a => a.HasUpdate && FilterItem(a));
    public bool HasUpdates => Items.Any(a => a.HasUpdate && FilterItem(a));

    [RelayCommand]
    private void ToggleSelectAll() => ChangeSelection(() => SelectAllRows.Toggle(VisibleSelectableItems));

    [RelayCommand]
    private void ToggleSelectAllInstalled() =>
        ChangeSelection(() => SelectAllRows.Toggle(VisibleSelectableItems.Where(a => a.IsInstalled)));

    [RelayCommand]
    private void ToggleSelectAllNotInstalled() =>
        ChangeSelection(() => SelectAllRows.Toggle(VisibleSelectableItems.Where(a => !a.IsInstalled)));

    [RelayCommand]
    private void ToggleSelectAllWithUpdates() =>
        ChangeSelection(() => SelectAllRows.Toggle(VisibleSelectableItems.Where(a => a.HasUpdate)));

    [ObservableProperty]
    public partial bool AuthorsIntent { get; set; }

    partial void OnAuthorsIntentChanged(bool value) => ChangeSelection(() =>
    {
        foreach (var item in Items)
            item.AuthorsIntent = value;
    });

    // Each tick or status change re-scans every row for the select-all boxes, so a change across the list notifies
    // once, at the end.
    public void ChangeSelection(Action change)
    {
        _changingSelection = true;
        try
        {
            change();
        }
        finally
        {
            _changingSelection = false;
        }
        NotifySelectionStateChanged();
    }

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(HasSelectedItems));
        NotifyVisibleRowsChanged();
        SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public string SelectAllLabel => _localizationService.GetStringOrDefault("Common_SelectAll", "Select All");
    public string SelectAllInstalledLabel => _localizationService.GetStringOrDefault("Common_SelectAll_Installed", "Select All Installed");
    public string SelectAllWithUpdatesLabel => _localizationService.GetString("Common_SelectAll_WithUpdates", UpdatesCount);
    public string SelectAllNotInstalledLabel => _localizationService.GetStringOrDefault("Common_SelectAll_NotInstalled", "Select All Not Installed");

    [ObservableProperty]
    public partial bool IsTaskRunning { get; set; }

    public event EventHandler? SelectedItemsChanged;

    private bool _changingSelection;

    public bool HasSelectedItems => Items.Any(a => a.IsSelected);
    public bool HasSelectedInstalledItems => Items.Any(a => a.IsSelected && a.IsInstalled);

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
            PackageSearchStatusKey = string.Empty;
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
        PackageSearchStatusKey = "ExternalApps_PackageSearch_Searching";
        try
        {
            var result = await _externalAppsService.SearchPackagesAsync(query, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            IsChocolateyNoteVisible = !result.SearchedChocolatey;
            var (found, picks) = ShowSearchResults(result.Items);
            PackageSearchFoundCount = found;
            PackageSearchPickCount = picks;
            PackageSearchStatusKey = (found, picks) switch
            {
                (0, 0) => "ExternalApps_PackageSearch_None",
                (0, _) => "ExternalApps_PackageSearch_NoneWithPicks",
                (_, 0) => "ExternalApps_PackageSearch_Found",
                _ => "ExternalApps_PackageSearch_FoundWithPicks",
            };
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
                IsSearchingPackages = false;
        }
    }

    // Ticked results stay so a pick survives the next search; unticked ones make way for the new results. A result
    // the PC already has as another installed app is that app, so it is not listed twice. Returns how many results
    // show and how many earlier picks show beside them.
    private (int Found, int Picks) ShowSearchResults(IReadOnlyList<ItemDefinition> results)
    {
        var stale = Items.Where(item => PackageSearchItem.Is(item.Id) && !item.IsSelected).ToList();
        using (ItemsView.DeferRefresh())
        {
            foreach (var item in stale)
                RemoveItem(item);
        }
        if (stale.Count > 0)
            NotifyVisibleRowsChanged();

        var installed = Items.Where(item => item.IsOtherInstalled).Select(item => item.Installed?.WinGetId).OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var shown = results.Where(result => result.WinGetPackageId?.FirstOrDefault() is not { } id || !installed.Contains(id)).ToList();
        AddItems(shown);
        var shownIds = shown.Select(result => result.Id).ToHashSet();
        return (shown.Count, Items.Count(item => PackageSearchItem.Is(item.Id) && !shownIds.Contains(item.Id)));
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
        CheckAddedAsync(added).FireAndForget(_logService);
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
        StatusKey = "Progress_LoadingExternalApps";

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

        // The unavailable-app lookup runs beside the scan, and the rows are hidden once the scan says which are installed.
        try
        {
            var unavailable = FindUnavailableAsync();
            await RefreshInstalledAppsAsync();
            HideUnavailable(await unavailable);
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
        // Awaiting it here would keep the import command's task pending that whole time,
        // leaving the Import Config button greyed out.
        ResolveIconsAsync(Items.ToList()).FireAndForget(_logService);
    }

    // One read of the PC gives every catalog app its status, version and update, and lists the installed apps the
    // catalog does not. Callers that arrive together share that read; the apply is synchronous, so two of them cannot
    // interleave. False when the scan failed, which leaves the rows as they were, or when it listed the apps without
    // knowing their updates, which leaves each row the update it showed before.
    public async Task<bool> RefreshInstalledAppsAsync()
    {
        IsScanningInstalledApps = true;
        try
        {
            // An installed app the catalog does not list takes its status from the scan's app list.
            var definitions = Items.Where(item => !item.IsOtherInstalled).Select(item => item.Definition).ToList();
            var scan = await _installedAppsService.GetInstalledAppsAsync(definitions, StatusRules.External);
            ApplyStatus(Items, scan.Status);
            MergeInstalledApps(scan.Apps, scan.UpdatesKnown);
            if (scan.UpdatesKnown)
                UpdatesReadUtc = DateTime.UtcNow;
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

    // Rows added after the load (search picks, a config's apps) take their status from the same kind of read.
    private async Task CheckAddedAsync(IReadOnlyList<AppItemViewModel> added)
    {
        try
        {
            var scan = await _installedAppsService.GetInstalledAppsAsync(added.Select(item => item.Definition).ToList(), StatusRules.External);
            ApplyStatus(added, scan.Status);
            RefreshFilter();
        }
        catch (Exception ex)
        {
            _logService.LogError("Error checking installation status", ex);
        }
    }

    private void ApplyStatus(IEnumerable<AppItemViewModel> items, IReadOnlyDictionary<string, bool> status) => ChangeSelection(() =>
    {
        using (ItemsView.DeferRefresh())
        {
            foreach (var item in items)
            {
                if (status.TryGetValue(item.Definition.Id, out var isInstalled))
                    item.IsInstalled = isInstalled;
            }
        }
    });

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

    private Task<IReadOnlyCollection<string>> FindUnavailableAsync() => _externalAppsService.GetUnavailableAppIdsAsync(
        Items.Where(item => !item.IsOtherInstalled).Select(item => item.Definition).ToList());

    // An installed app stays: it can still be uninstalled, and this PC is proof it once installed.
    private void HideUnavailable(IReadOnlyCollection<string>? unavailable)
    {
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

    // A ticked row gaining or losing its update or its install changes what Install and Uninstall will do, like a tick
    // does. A row that changes status or stops being selectable moves between the boxes even while unticked.
    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_changingSelection)
            return;
        if (e.PropertyName == nameof(AppItemViewModel.IsSelected)
            || (e.PropertyName is nameof(AppItemViewModel.HasUpdate) or nameof(AppItemViewModel.IsInstalled)
                && sender is AppItemViewModel { IsSelected: true }))
        {
            NotifySelectionStateChanged();
        }
        else if (e.PropertyName is nameof(AppItemViewModel.HasUpdate) or nameof(AppItemViewModel.IsInstalled)
                 or nameof(AppItemViewModel.IsSelectable))
        {
            NotifyVisibleRowsChanged();
        }
    }

    // No-op when no resolver was injected. Started with FireAndForget, which logs a failure: icon resolution must
    // never block the load flow.
    private async Task ResolveIconsAsync(IReadOnlyList<AppItemViewModel> items)
    {
        if (_iconResolver is null) return;

        var definitions = items.Select(item => item.Definition).ToList();
        // External Apps are vendor brand logos — cache them exactly as
        // shipped (no backplate crop, no light/dark variant synthesis).
        await _iconResolver.ResolveBatchAsync(definitions, applyThemeAdaptation: false);

        foreach (var item in items)
            item.NotifyIconChanged();
    }

    // False before the first load finishes. The updates come from AppUpdateMonitor, which scans once for both tabs.
    public async Task<bool> RecheckInstallationStatusAsync()
    {
        if (!IsInitialized)
        {
            StatusKey = "Progress_WaitForInitialLoad";
            return false;
        }

        StatusKey = "Progress_LoadingExternalApps";
        IsLoading = true;
        try
        {
            _installedAppsService.Invalidate();
            await RefreshInstalledAppsAsync();
        }
        finally
        {
            IsLoading = false;
        }

        return true;
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

        var run = await UpdateAppsAsync(toUpdate, askToCloseOpenApps: true);
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

        if (notInstalled.Failed.Count > 0)
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("ExternalApps_InstallFailed", string.Join("\n", notInstalled.Failed)));
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
        _installedAppsService.Invalidate();
        await RefreshInstalledAppsAsync();

        foreach (var app in apps)
            app.IsSelected = notInstalled.Contains(app);

        NotifySelectionStateChanged();
    }

    // Returns what did not install. Rows, not names: two rows can share a name. Failed names only what was tried
    // and failed, not what a skip or a cancel left behind.
    private async Task<(List<AppItemViewModel> Apps, List<StagedInstaller> OwnInstallers, List<string> Failed)> InstallQueueAsync(
        List<AppItemViewModel> apps,
        Func<AppItemViewModel, IProgress<TaskProgressDetail>, Task<OperationResult<bool>>> installApp,
        IReadOnlyList<StagedInstaller> ownInstallers)
    {
        var names = apps.Select(a => a.Name).Concat(ownInstallers.Select(i => i.Name)).ToList();
        var failedApps = new List<AppItemViewModel>(apps);
        var failedInstallers = new List<StagedInstaller>(ownInstallers);
        var triedAndFailed = new List<string>();
        var current = string.Empty;

        IsTaskRunning = true;

        try
        {
            var finished = await _dialogService.RunOrShowErrorAsync(_logService, async () =>
            {
                var cancellation = _progressService.StartTask(_localizationService.GetStringOrDefault("Progress_Task_InstallingExternalApps", "Installing External Apps"), false);
                var progress = _progressService.CreateDetailedProgress();

                for (int i = 0; i < names.Count; i++)
                {
                    if (_progressService.ConsumeSkipNextRequest())
                        continue;

                    current = names[i];
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
                        else if (!cancellation.IsCancellationRequested)
                        {
                            triedAndFailed.Add(names[i]);
                        }
                    }
                    else
                    {
                        var installer = ownInstallers[i - apps.Count];
                        if (await _stagedAppInstaller.InstallAsync(installer, progress, cancellation.Token))
                            failedInstallers.Remove(installer);
                        else if (!cancellation.IsCancellationRequested)
                            triedAndFailed.Add(names[i]);
                    }
                }
            }, ex => _localizationService.GetString("Progress_WinGet_InstallationError", current, ex.Message));
            if (!finished)
                _progressService.FailTask();
        }
        finally
        {
            if (_progressService.IsTaskRunning)
            {
                _progressService.CompleteTask();
            }
            IsTaskRunning = false;
        }

        return (failedApps, failedInstallers, triedAndFailed);
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
        string current = string.Empty;
        var failed = new List<AppItemViewModel>();
        var finished = false;

        try
        {
            finished = await _dialogService.RunOrShowErrorAsync(_logService, async () =>
            {
                var cancellation = _progressService.StartTask(_localizationService.GetStringOrDefault("Progress_Task_UninstallingExternalApps", "Uninstalling External Apps"), false);
                var progress = _progressService.CreateDetailedProgress();

                for (int i = 0; i < selectedItems.Count; i++)
                {
                    if (_progressService.ConsumeSkipNextRequest())
                        continue;

                    var app = selectedItems[i];
                    current = app.Name;
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
                    else if (!cancellation.IsCancellationRequested)
                        failed.Add(app);
                }
            }, ex => _localizationService.GetString("Progress_WinGet_UninstallationError", current, ex.Message));
            if (!finished)
                _progressService.FailTask();
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

        // A run that threw has already shown its error dialog.
        if (finished && failed.Count > 0)
            await _dialogService.ShowWarningAsync(UninstallFailedMessage(failed));
    }

    // The rescan tells an app that was never on this PC apart from one whose uninstaller ran and left it in place.
    private string UninstallFailedMessage(List<AppItemViewModel> failed)
    {
        var notFound = failed.Where(a => !a.IsInstalled).Select(a => a.Name).ToList();
        var stillInstalled = failed.Where(a => a.IsInstalled).Select(a => a.Name).ToList();
        var sections = new List<string>();
        if (notFound.Count > 0)
            sections.Add(_localizationService.GetString("ExternalApps_UninstallNotFound", string.Join("\n", notFound)));
        if (stillInstalled.Count > 0)
            sections.Add(_localizationService.GetString("ExternalApps_UninstallFailed", string.Join("\n", stillInstalled)));
        return string.Join("\n\n", sections);
    }

    private async Task RefreshAfterOperationAsync()
    {
        _installedAppsService.Invalidate();
        await RefreshInstalledAppsAsync();
        ClearSelections();
    }

    private bool IsBusy => IsTaskRunning || _progressService.IsTaskRunning;

    // No confirmation: an update keeps the app and its settings, the way Store's Update all works. Not started when
    // there was nothing to update or another task holds the progress bar. Ticks are the caller's to change.
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

        return new AppUpdateRun(Started: true, cancelled, await RefreshInstalledAppsAsync());
    }

    [RelayCommand]
    public async Task EditDefaultInstallOptionsAsync()
    {
        var choice = await _dialogService.ShowInstallOptionsDialogAsync(null, new InstallOptionsChoice(false, _installOptionsService.Defaults));
        if (choice is not null)
            await _installOptionsService.SetDefaultsAsync(choice.Options);
    }

    [RelayCommand(CanExecute = nameof(CanEditInstallOptions))]
    public async Task EditInstallOptionsAsync(AppItemViewModel? app)
    {
        if (app is null)
            return;

        var current = _installOptionsService.ForApp(app.Id) is { } own
            ? new InstallOptionsChoice(false, own)
            : new InstallOptionsChoice(true, _installOptionsService.Defaults);
        var choice = await _dialogService.ShowInstallOptionsDialogAsync(app.Name, current);
        if (choice is not null)
            await _installOptionsService.SetForAppAsync(app.Id, choice.FollowsDefaults ? null : choice.Options);
    }

    // The options only reach winget and Chocolatey; a Store-only app installs the one way the Store allows.
    public static bool CanEditInstallOptions(AppItemViewModel? app) =>
        app?.Definition is { } definition
        && (definition.WinGetPackageId is { Length: > 0 } || !string.IsNullOrEmpty(definition.ChocoPackageId));

    [RelayCommand]
    public void ClearSelections() => ChangeSelection(() =>
    {
        foreach (var item in Items)
            item.IsSelected = false;
    });

    private void NotifyVisibleRowsChanged()
    {
        foreach (var name in VisibleRowProperties)
            OnPropertyChanged(name);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var name in LocalizedProperties)
            OnPropertyChanged(name);
        RebuildCategories();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            var search = Interlocked.Exchange(ref _packageSearchCts, null);
            search?.Cancel();
            search?.Dispose();
            foreach (var item in Items)
            {
                item.PropertyChanged -= Item_PropertyChanged;
                item.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
