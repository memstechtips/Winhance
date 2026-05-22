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

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

public record AppCategory(string GroupName, string DisplayName, string IconGlyph, IReadOnlyList<AppItemViewModel> Apps);

/// <summary>
/// ViewModel for the External Apps tab in the SoftwareApps feature.
/// </summary>
public partial class ExternalAppsViewModel : BaseViewModel, IExternalAppsItemsProvider
{
    private readonly IExternalAppsService _externalAppsService;
    private readonly IAppInstallationService _appInstallationService;
    private readonly ITaskProgressService _progressService;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IThemeService _themeService;
    private readonly IAppIconResolver? _iconResolver;

    public ExternalAppsViewModel(
        IExternalAppsService externalAppsService,
        IAppInstallationService appInstallationService,
        ITaskProgressService progressService,
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IThemeService themeService,
        IAppIconResolver? iconResolver = null)
    {
        _externalAppsService = externalAppsService;
        _appInstallationService = appInstallationService;
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
        ItemsView.SortDescriptions.Add(new SortDescription("IsInstalled", SortDirection.Descending));
        ItemsView.SortDescriptions.Add(new SortDescription("Name", SortDirection.Ascending));

        // Initialize partial property defaults (after Items/ItemsView,
        // since OnSearchTextChanged uses ItemsView)
        StatusText = "Ready";
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
    };

    private void RebuildCategories()
    {
        var filtered = Items.Where(a => FilterItem(a)).ToList();
        var groups = filtered
            .GroupBy(a => a.GroupName)
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .OrderBy(g => g.Key);

        var result = new List<AppCategory>();
        foreach (var group in groups)
        {
            var glyph = CategoryGlyphs.GetValueOrDefault(group.Key, "\uE74C");
            var locKey = "ExternalApps_Category_" + group.Key.Replace(" ", "").Replace("&", "").Replace(",", "").Replace("(", "").Replace(")", "");
            var displayName = _localizationService.GetString(locKey);
            if (string.IsNullOrEmpty(displayName))
                displayName = group.Key;
            result.Add(new AppCategory(group.Key, displayName, glyph, group.OrderBy(a => a.Name).ToList()));
        }
        Categories = result;
    }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsInitialized { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

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

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsAllSelectedInstalled));
        OnPropertyChanged(nameof(IsAllSelectedNotInstalled));
        OnPropertyChanged(nameof(HasSelectedItems));
        SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public string SelectAllLabel => _localizationService.GetString("Common_SelectAll") ?? "Select All";
    public string SelectAllInstalledLabel => _localizationService.GetString("Common_SelectAll_Installed") ?? "Select All Installed";
    public string SelectAllNotInstalledLabel => _localizationService.GetString("Common_SelectAll_NotInstalled") ?? "Select All Not Installed";

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
        RebuildCategories();
    }

    private bool FilterItem(object obj)
    {
        if (obj is AppItemViewModel app)
        {
            return SearchHelper.MatchesSearchTerm(SearchText, app.Name, app.Description, app.Id);
        }
        return true;
    }

    /// <summary>
    /// Loads items - alias for LoadAppsAndCheckInstallationStatusAsync for ConfigurationService compatibility.
    /// </summary>
    public Task LoadItemsAsync() => LoadAppsAndCheckInstallationStatusAsync();

    private readonly object _loadGate = new();
    private Task? _loadTask;

    /// <summary>
    /// Loads app definitions, checks installation status and resolves icons — exactly once.
    /// Idempotent: startup triggers this from two independent paths — the UI init
    /// (StartupUiCoordinator) and first-run backup-config creation (ConfigExportService,
    /// via StartupOrchestrator phase 2). Both used to sail past an `if (IsInitialized)`
    /// guard — IsInitialized is only set when the load *finishes* — so two full passes
    /// ran concurrently, clobbering the shared Items collection and racing over the
    /// icon-cache .tmp files. Now the first caller starts the load; every other caller
    /// awaits the same Task. The core runs on the UI thread with a SynchronizationContext
    /// so its continuations stay UI-thread-affine regardless of which path triggered it.
    /// </summary>
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
            _logService.LogError("[ExternalAppsViewModel] Error loading app definitions", ex);
            StatusText = $"Error loading apps: {ex.Message}";
        }

        try
        {
            StatusText = _localizationService.GetString("Progress_CheckingInstallStatus");
            await CheckInstallationStatusAsync();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"[ExternalAppsViewModel] Install status check failed, items loaded without status: {ex.Message}");
        }

        try
        {
            NotifySelectionStateChanged();
            IsInitialized = true;
            StatusText = $"Loaded {Items.Count} items";
            RebuildCategories();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"[ExternalAppsViewModel] Error finalizing: {ex.Message}");
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
        ResolveIconsAsync().FireAndForget(_logService);
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

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppItemViewModel.IsSelected))
        {
            NotifySelectionStateChanged();
        }
    }

    /// <summary>
    /// Resolves icons for all current entries via the unified icon pipeline
    /// (Layer 1a AppX local → Layer 1b Win32 binary → Layer 2a Store CDN →
    /// Layer 2b WinGet manifest) and notifies their ViewModels so the bound
    /// Image / FontIcon refresh.
    /// No-op when no resolver was injected. Failures are logged and swallowed
    /// — icon resolution must never block the load flow.
    /// </summary>
    private async Task ResolveIconsAsync()
    {
        if (_iconResolver is null) return;

        try
        {
            var definitions = Items.Select(item => item.Definition).ToList();
            // External Apps are vendor brand logos — cache them exactly as
            // shipped (no backplate crop, no light/dark variant synthesis).
            await _iconResolver.ResolveBatchAsync(definitions, applyThemeAdaptation: false);

            foreach (var item in Items)
                item.NotifyIconChanged();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"[ExternalAppsViewModel] Icon resolution failed: {ex.Message}");
        }
    }

    // Serializes the install-status re-check. It runs from the Refresh button,
    // the WinGetReady event (OnWinGetInstalled) and post-install/remove
    // (RefreshAfterOperationAsync — including config-apply); none are mutually
    // exclusive. Overlapping callers queue here instead of interleaving access
    // to the shared Items collection and the detection cache.
    private readonly SemaphoreSlim _statusCheckGate = new(1, 1);

    [RelayCommand]
    public async Task CheckInstallationStatusAsync()
    {
        if (_externalAppsService == null) return;

        await _statusCheckGate.WaitAsync();
        try
        {
            var definitions = Items.Select(item => item.Definition).ToList();
            var statusResults = await _externalAppsService.CheckBatchInstalledAsync(definitions);

            using (ItemsView.DeferRefresh())
            {
                foreach (var item in Items)
                {
                    if (statusResults.TryGetValue(item.Definition.Id, out bool isInstalled))
                    {
                        item.IsInstalled = isInstalled;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("Error checking installation status", ex);
            StatusText = $"Error checking status: {ex.Message}";
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
            StatusText = $"Refreshed status for {Items.Count} items";
        }
        catch (Exception ex)
        {
            _logService.LogError("Error refreshing status", ex);
            StatusText = $"Error refreshing: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
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

    /// <summary>
    /// Installs selected apps with optional confirmation skip (for ConfigurationService compatibility).
    /// </summary>
    public async Task InstallApps(bool skipConfirmation = false)
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (!selectedItems.Any()) return;

        if (!skipConfirmation)
        {
            var itemNames = selectedItems.Select(a => a.Name).ToList();
            var (confirmed, _) = await _dialogService.ShowAppOperationConfirmationAsync("install", itemNames, selectedItems.Count);
            if (!confirmed) return;
        }

        await InstallAppsInternalAsync(selectedItems);
    }

    [RelayCommand]
    public async Task InstallAppsAsync()
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (!selectedItems.Any())
        {
            await _dialogService.ShowWarningAsync(
                "Please select at least one app for installation.",
                "No Apps Selected");
            return;
        }

        var itemNames = selectedItems.Select(a => a.Name).ToList();
        var (confirmed, _) = await _dialogService.ShowAppOperationConfirmationAsync("install", itemNames, selectedItems.Count);
        if (!confirmed) return;

        await InstallAppsInternalAsync(selectedItems);
    }

    private async Task InstallAppsInternalAsync(List<AppItemViewModel> selectedItems)
    {

        IsTaskRunning = true;
        StatusText = _localizationService.GetString("Progress_Task_InstallingExternalApps");

        try
        {
            _progressService.StartTask(_localizationService.GetString("Progress_Task_InstallingExternalApps") ?? "Installing External Apps", false);
            var progress = _progressService.CreateDetailedProgress();

            int successCount = 0;
            for (int i = 0; i < selectedItems.Count; i++)
            {
                if (_progressService.ConsumeSkipNextRequest())
                    continue;

                var app = selectedItems[i];
                var nextName = i + 1 < selectedItems.Count ? selectedItems[i + 1].Name : null;
                progress.Report(new TaskProgressDetail
                {
                    StatusText = _localizationService.GetString("Progress_Installing", app.Name),
                    QueueTotal = selectedItems.Count,
                    QueueCurrent = i + 1,
                    QueueNextItemName = nextName
                });

                var result = await _appInstallationService.InstallAppAsync(app.Definition, progress, shouldRemoveFromBloatScript: false);
                if (result.Success && result.Result)
                {
                    app.IsInstalled = true;
                    successCount++;
                }
            }

            StatusText = $"Installed {successCount} of {selectedItems.Count} apps";
        }
        catch (Exception ex)
        {
            _logService.LogError("Error installing apps", ex);
            StatusText = $"Error: {ex.Message}";
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

    [RelayCommand]
    public async Task UninstallAppsAsync()
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (!selectedItems.Any())
        {
            await _dialogService.ShowWarningAsync(
                "Please select at least one app for uninstallation.",
                "No Apps Selected");
            return;
        }

        var itemNames = selectedItems.Select(a => a.Name).ToList();
        var (confirmed, _) = await _dialogService.ShowAppOperationConfirmationAsync("uninstall", itemNames, selectedItems.Count);
        if (!confirmed) return;

        IsTaskRunning = true;
        StatusText = _localizationService.GetString("Progress_Task_UninstallingExternalApps");

        try
        {
            _progressService.StartTask(_localizationService.GetString("Progress_Task_UninstallingExternalApps") ?? "Uninstalling External Apps", false);
            var progress = _progressService.CreateDetailedProgress();

            int successCount = 0;
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
                {
                    app.IsInstalled = false;
                    successCount++;
                }
            }

            StatusText = $"Uninstalled {successCount} of {selectedItems.Count} apps";
        }
        catch (Exception ex)
        {
            _logService.LogError("Error uninstalling apps", ex);
            StatusText = $"Error: {ex.Message}";
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
        RebuildCategories();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            _externalAppsService.WinGetReady -= OnWinGetInstalled;
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
