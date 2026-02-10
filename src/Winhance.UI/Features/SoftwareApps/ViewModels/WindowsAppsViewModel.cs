using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using Winhance.Core.Features.Common.Enums;
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
public partial class WindowsAppsViewModel : BaseViewModel
{
    private readonly IWindowsAppsService _windowsAppsService;
    private readonly IAppOperationService _appOperationService;
    private readonly ITaskProgressService _progressService;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IInternetConnectivityService _connectivityService;
    private readonly IDispatcherService _dispatcherService;

    public WindowsAppsViewModel(
        IWindowsAppsService windowsAppsService,
        IAppOperationService appOperationService,
        ITaskProgressService progressService,
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IInternetConnectivityService connectivityService,
        IDispatcherService dispatcherService)
    {
        _windowsAppsService = windowsAppsService;
        _appOperationService = appOperationService;
        _progressService = progressService;
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _connectivityService = connectivityService;
        _dispatcherService = dispatcherService;

        _localizationService.LanguageChanged += OnLanguageChanged;

        Items = new ObservableCollection<AppItemViewModel>();
        ItemsView = new AdvancedCollectionView(Items, true);
        ItemsView.Filter = FilterItem;
        ItemsView.SortDescriptions.Add(new SortDescription("IsInstalled", SortDirection.Descending));
        ItemsView.SortDescriptions.Add(new SortDescription("Name", SortDirection.Ascending));
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
    private bool _isLoading;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _searchText = string.Empty;

    public string SectionAppsHeader => _localizationService.GetString("WindowsApps_Section_Apps") ?? "Windows Apps";
    public string SectionCapabilitiesHeader => _localizationService.GetString("WindowsApps_Section_Capabilities") ?? "Windows Capabilities";
    public string SectionOptionalFeaturesHeader => _localizationService.GetString("WindowsApps_Section_OptionalFeatures") ?? "Windows Optional Features";

    public string SelectAllLabel => _localizationService.GetString("Common_SelectAll") ?? "Select All";
    public string SelectAllInstalledLabel => _localizationService.GetString("Common_SelectAll_Installed") ?? "Select All Installed";
    public string SelectAllNotInstalledLabel => _localizationService.GetString("Common_SelectAll_NotInstalled") ?? "Select All Not Installed";

    [ObservableProperty]
    private bool _isAllSelected;

    [ObservableProperty]
    private bool _isAllSelectedInstalled;

    [ObservableProperty]
    private bool _isAllSelectedNotInstalled;

    [ObservableProperty]
    private bool _isTaskRunning;

    public event EventHandler? SelectedItemsChanged;

    public bool HasSelectedItems => Items.Any(a => a.IsSelected);

    partial void OnSearchTextChanged(string value)
    {
        ItemsView.RefreshFilter();
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

    partial void OnIsAllSelectedChanged(bool value)
    {
        foreach (var item in Items)
        {
            item.IsSelected = value;
        }
        IsAllSelectedInstalled = value;
        IsAllSelectedNotInstalled = value;
        OnPropertyChanged(nameof(HasSelectedItems));
        SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsAllSelectedInstalledChanged(bool value)
    {
        foreach (var item in Items.Where(a => a.IsInstalled))
        {
            item.IsSelected = value;
        }
        UpdateIsAllSelectedState();
        OnPropertyChanged(nameof(HasSelectedItems));
        SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsAllSelectedNotInstalledChanged(bool value)
    {
        foreach (var item in Items.Where(a => !a.IsInstalled))
        {
            item.IsSelected = value;
        }
        UpdateIsAllSelectedState();
        OnPropertyChanged(nameof(HasSelectedItems));
        SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateIsAllSelectedState()
    {
        var allSelected = Items.All(a => a.IsSelected);
        if (_isAllSelected != allSelected)
        {
            _isAllSelected = allSelected;
            OnPropertyChanged(nameof(IsAllSelected));
        }
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

            await LoadAppsIntoItemsAsync(apps.Concat(capabilities).Concat(features));

            StatusText = _localizationService.GetString("Progress_CheckingInstallStatus");
            await CheckInstallationStatusAsync();

            IsAllSelected = false;
            IsInitialized = true;
            StatusText = $"Loaded {Items.Count} items";
            NotifyCardViewProperties();
        }
        catch (Exception ex)
        {
            _logService.LogError("[WindowsAppsViewModel] Error loading apps", ex);
            StatusText = $"Error loading apps: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadAppsIntoItemsAsync(IEnumerable<ItemDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            var viewModel = new AppItemViewModel(
                definition,
                _appOperationService,
                _dialogService,
                _logService,
                _localizationService,
                _dispatcherService);
            viewModel.PropertyChanged += Item_PropertyChanged;
            Items.Add(viewModel);
        }

        await Task.CompletedTask;
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppItemViewModel.IsSelected))
        {
            UpdateIsAllSelectedState();
            OnPropertyChanged(nameof(HasSelectedItems));
            SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
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

            foreach (var item in Items)
            {
                if (statusResults.TryGetValue(item.Definition.Id, out bool isInstalled))
                {
                    item.IsInstalled = isInstalled;
                }
            }

            ItemsView.RefreshSorting();
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
            foreach (var app in selectedItems)
            {
                var result = await _appOperationService.InstallAppAsync(app.Definition, progress, shouldRemoveFromBloatScript: true);
                if (result.Success && result.Result)
                {
                    app.IsInstalled = true;
                    successCount++;
                }
            }

            StatusText = $"Installed {successCount} of {selectedItems.Count} items";
            await RefreshAfterOperationAsync();
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
            _progressService.StartTask(_localizationService.GetString("Progress_Task_RemovingWindowsApps") ?? "Removing Windows Apps", false);
            var progress = _progressService.CreateDetailedProgress();

            var definitions = selectedItems.Select(a => a.Definition).ToList();
            var result = await _appOperationService.UninstallAppsAsync(definitions, progress, saveRemovalScripts);

            if (result.Success)
            {
                foreach (var item in selectedItems)
                {
                    item.IsInstalled = false;
                }
                StatusText = $"Removed {result.Result} items";
            }
            else
            {
                StatusText = result.ErrorMessage ?? "Removal failed";
            }

            await RefreshAfterOperationAsync();
        }
        catch (Exception ex)
        {
            _logService.LogError("Error removing apps", ex);
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
    }

    private async Task RefreshAfterOperationAsync()
    {
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
        _isAllSelected = false;
        _isAllSelectedInstalled = false;
        _isAllSelectedNotInstalled = false;
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsAllSelectedInstalled));
        OnPropertyChanged(nameof(IsAllSelectedNotInstalled));
        OnPropertyChanged(nameof(HasSelectedItems));
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
