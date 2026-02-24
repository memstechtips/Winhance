using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Utils;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

/// <summary>
/// ViewModel for the Windows Apps tab in the SoftwareApps feature.
/// </summary>
public partial class WindowsAppsViewModel : BaseViewModel, IWindowsAppsItemsProvider
{
    private readonly IWindowsAppsService _windowsAppsService;
    private readonly IAppInstallationService _appInstallationService;
    private readonly IAppUninstallationService _appUninstallationService;
    private readonly ITaskProgressService _progressService;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IInternetConnectivityService _connectivityService;
    private readonly IDispatcherService _dispatcherService;

    public WindowsAppsViewModel(
        IWindowsAppsService windowsAppsService,
        IAppInstallationService appInstallationService,
        IAppUninstallationService appUninstallationService,
        ITaskProgressService progressService,
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IInternetConnectivityService connectivityService,
        IDispatcherService dispatcherService)
    {
        _windowsAppsService = windowsAppsService;
        _appInstallationService = appInstallationService;
        _appUninstallationService = appUninstallationService;
        _progressService = progressService;
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _connectivityService = connectivityService;
        _dispatcherService = dispatcherService;

        _localizationService.LanguageChanged += OnLanguageChanged;
        _windowsAppsService.WinGetReady += OnWinGetInstalled;

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

    public IEnumerable<AppItemViewModel> WindowsAppsFiltered => Items
        .Where(a =>
            !string.IsNullOrEmpty(a.Definition.AppxPackageName) &&
            string.IsNullOrEmpty(a.Definition.CapabilityName) &&
            string.IsNullOrEmpty(a.Definition.OptionalFeatureName) &&
            FilterItem(a))
        .OrderByDescending(a => a.IsInstalled)
        .ThenBy(a => a.Name);

    public IEnumerable<AppItemViewModel> CapabilitiesFiltered => Items
        .Where(a =>
            !string.IsNullOrEmpty(a.Definition.CapabilityName) &&
            FilterItem(a))
        .OrderByDescending(a => a.IsInstalled)
        .ThenBy(a => a.Name);

    public IEnumerable<AppItemViewModel> OptionalFeaturesFiltered => Items
        .Where(a =>
            !string.IsNullOrEmpty(a.Definition.OptionalFeatureName) &&
            FilterItem(a))
        .OrderByDescending(a => a.IsInstalled)
        .ThenBy(a => a.Name);

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsInitialized { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

    public string SectionAppsHeader => _localizationService.GetString("WindowsApps_Section_Apps") ?? "Windows Apps";
    public string SectionCapabilitiesHeader => _localizationService.GetString("WindowsApps_Section_Capabilities") ?? "Windows Capabilities";
    public string SectionOptionalFeaturesHeader => _localizationService.GetString("WindowsApps_Section_OptionalFeatures") ?? "Windows Optional Features";

    public string SelectAllLabel => _localizationService.GetString("Common_SelectAll") ?? "Select All";
    public string SelectAllInstalledLabel => _localizationService.GetString("Common_SelectAll_Installed") ?? "Select All Installed";
    public string SelectAllNotInstalledLabel => _localizationService.GetString("Common_SelectAll_NotInstalled") ?? "Select All Not Installed";

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
            return SearchHelper.MatchesSearchTerm(SearchText, app.Name, app.Description, app.Id);
        }
        return true;
    }

    /// <summary>
    /// Loads items - alias for LoadAppsAndCheckInstallationStatusAsync for ConfigurationService compatibility.
    /// </summary>
    public Task LoadItemsAsync() => LoadAppsAndCheckInstallationStatusAsync();

    [RelayCommand]
    public async Task LoadAppsAndCheckInstallationStatusAsync()
    {
        if (IsInitialized)
        {
            _logService.LogInformation("[WindowsAppsViewModel] Already initialized, skipping");
            return;
        }

        IsLoading = true;
        StatusText = _localizationService.GetString("Progress_LoadingWindowsApps");

        try
        {
            Items.Clear();

            var allItems = await _windowsAppsService.GetAppsAsync();
            var apps = allItems.Where(x => !string.IsNullOrEmpty(x.AppxPackageName) || (x.WinGetPackageId != null && x.WinGetPackageId.Any()));
            var capabilities = allItems.Where(x => !string.IsNullOrEmpty(x.CapabilityName));
            var features = allItems.Where(x => !string.IsNullOrEmpty(x.OptionalFeatureName));

            LoadAppsIntoItems(apps.Concat(capabilities).Concat(features));
        }
        catch (Exception ex)
        {
            _logService.LogError("[WindowsAppsViewModel] Error loading app definitions", ex);
            StatusText = $"Error loading apps: {ex.Message}";
        }

        try
        {
            StatusText = _localizationService.GetString("Progress_CheckingInstallStatus");
            await CheckInstallationStatusAsync();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"[WindowsAppsViewModel] Install status check failed, items loaded without status: {ex.Message}");
        }

        try
        {
            NotifySelectionStateChanged();
            IsInitialized = true;
            StatusText = $"Loaded {Items.Count} items";
            NotifyCardViewProperties();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"[WindowsAppsViewModel] Error finalizing: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadAppsIntoItems(IEnumerable<ItemDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            var viewModel = new AppItemViewModel(
                definition,
                _localizationService,
                _dispatcherService);
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

    [RelayCommand]
    public async Task CheckInstallationStatusAsync()
    {
        if (_windowsAppsService == null) return;

        try
        {
            var definitions = Items.Select(item => item.Definition).ToList();
            var statusResults = await _windowsAppsService.CheckBatchInstalledAsync(definitions);

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
            _windowsAppsService.InvalidateStatusCache();
            await CheckInstallationStatusAsync();
            NotifyCardViewProperties();
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
                _logService.LogInformation("WinGet installed — refreshing Windows Apps installation status");
                _windowsAppsService.InvalidateStatusCache();
                await CheckInstallationStatusAsync();
                NotifyCardViewProperties();
            }
        }).FireAndForget(_logService);
    }

    [RelayCommand]
    public async Task InstallAppsAsync()
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (!selectedItems.Any())
        {
            await _dialogService.ShowWarningAsync(
                "Please select at least one item for installation.",
                "No Items Selected");
            return;
        }

        if (!await _connectivityService.IsInternetConnectedAsync(true))
        {
            await _dialogService.ShowWarningAsync(
                "An internet connection is required to install apps.",
                "No Internet Connection");
            return;
        }

        var itemNames = selectedItems.Select(a => a.Name).ToList();
        var (confirmed, _) = await _dialogService.ShowAppOperationConfirmationAsync("install", itemNames, selectedItems.Count);
        if (!confirmed) return;

        IsTaskRunning = true;
        StatusText = _localizationService.GetString("Progress_Task_InstallingWindowsApps");

        try
        {
            _progressService.StartTask(_localizationService.GetString("Progress_Task_InstallingWindowsApps") ?? "Installing Windows Apps", false);
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

                var result = await _appInstallationService.InstallAppAsync(app.Definition, progress, shouldRemoveFromBloatScript: true);
                if (result.Success && result.Result)
                {
                    app.IsInstalled = true;
                    successCount++;
                }
            }

            StatusText = $"Installed {successCount} of {selectedItems.Count} items";
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

    /// <summary>
    /// Shows removal summary and asks for confirmation.
    /// </summary>
    /// <returns>Tuple of (Confirmed, SaveScripts).</returns>
    public async Task<(bool Confirmed, bool SaveScripts)> ShowRemovalSummaryAndConfirm()
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (!selectedItems.Any()) return (true, true);

        var itemNames = selectedItems.Select(a => a.Name).ToList();
        var checkboxText = _localizationService.GetString("Dialog_SaveRemovalScripts");
        var (confirmed, checkboxChecked) = await _dialogService.ShowAppOperationConfirmationAsync("remove", itemNames, selectedItems.Count, checkboxText);
        return (confirmed, checkboxChecked);
    }

    /// <summary>
    /// Removes selected apps with optional confirmation skip (for ConfigurationService compatibility).
    /// </summary>
    public async Task RemoveApps(bool skipConfirmation = false, bool saveRemovalScripts = true)
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (!selectedItems.Any()) return;

        if (!skipConfirmation)
        {
            var itemNames = selectedItems.Select(a => a.Name).ToList();
            var checkboxText = _localizationService.GetString("Dialog_SaveRemovalScripts");
            var (confirmed, checkboxChecked) = await _dialogService.ShowAppOperationConfirmationAsync("remove", itemNames, selectedItems.Count, checkboxText);
            if (!confirmed) return;
            saveRemovalScripts = checkboxChecked;
        }

        await RemoveAppsInternalAsync(selectedItems, saveRemovalScripts);
    }

    [RelayCommand]
    public async Task RemoveAppsAsync()
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (!selectedItems.Any())
        {
            await _dialogService.ShowWarningAsync(
                "Please select at least one item for removal.",
                "No Items Selected");
            return;
        }

        var itemNames = selectedItems.Select(a => a.Name).ToList();
        var checkboxText = _localizationService.GetString("Dialog_SaveRemovalScripts");
        var (confirmed, saveScripts) = await _dialogService.ShowAppOperationConfirmationAsync("remove", itemNames, selectedItems.Count, checkboxText);
        if (!confirmed) return;

        await RemoveAppsInternalAsync(selectedItems, saveScripts);
    }

    private async Task RemoveAppsInternalAsync(List<AppItemViewModel> selectedItems, bool saveRemovalScripts = true)
    {
        IsTaskRunning = true;
        StatusText = _localizationService.GetString("Progress_Task_RemovingWindowsApps");

        try
        {
            var definitions = selectedItems.Select(a => a.Definition).ToList();
            var result = await _appUninstallationService.UninstallAppsInParallelAsync(definitions, saveRemovalScripts);

            if (result.Success)
            {
                if (result.InfoMessage != null)
                {
                    // Deferred: don't mark as uninstalled — they haven't been removed yet
                    StatusText = result.InfoMessage;
                }
                else
                {
                    foreach (var item in selectedItems)
                    {
                        item.IsInstalled = false;
                    }
                    StatusText = $"Removed {result.Result} items";
                }
            }
            else
            {
                StatusText = result.ErrorMessage ?? "Removal failed";
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("Error removing apps", ex);
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsTaskRunning = false;
        }

        await RefreshAfterOperationAsync();
    }

    private async Task RefreshAfterOperationAsync()
    {
        _windowsAppsService.InvalidateStatusCache();
        await CheckInstallationStatusAsync();
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
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            foreach (var item in Items)
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }
        }
        base.Dispose(disposing);
    }
}
