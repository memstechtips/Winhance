using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
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
public partial class ExternalAppsViewModel : BaseViewModel
{
    private readonly IExternalAppsService _externalAppsService;
    private readonly IAppOperationService _appOperationService;
    private readonly ITaskProgressService _progressService;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IInternetConnectivityService _connectivityService;
    private readonly IDispatcherService _dispatcherService;

    public ExternalAppsViewModel(
        IExternalAppsService externalAppsService,
        IAppOperationService appOperationService,
        ITaskProgressService progressService,
        ILogService logService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IInternetConnectivityService connectivityService,
        IDispatcherService dispatcherService)
    {
        _externalAppsService = externalAppsService;
        _appOperationService = appOperationService;
        _progressService = progressService;
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _connectivityService = connectivityService;
        _dispatcherService = dispatcherService;

        Items = new ObservableCollection<AppItemViewModel>();
        ItemsView = new AdvancedCollectionView(Items, true);
        ItemsView.Filter = FilterItem;
        ItemsView.SortDescriptions.Add(new SortDescription("IsInstalled", SortDirection.Descending));
        ItemsView.SortDescriptions.Add(new SortDescription("Name", SortDirection.Ascending));
    }

    public ObservableCollection<AppItemViewModel> Items { get; }
    public AdvancedCollectionView ItemsView { get; }

    private List<AppCategory> _categories = new();
    public List<AppCategory> Categories
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
            result.Add(new AppCategory(group.Key, displayName, glyph, group.ToList()));
        }
        Categories = result;
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _searchText = string.Empty;

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
            _logService.LogInformation("[ExternalAppsViewModel] Already initialized, skipping");
            return;
        }

        IsLoading = true;
        StatusText = _localizationService.GetString("Progress_LoadingExternalApps");

        try
        {
            Items.Clear();

            var allItems = await _externalAppsService.GetAppsAsync();
            await LoadAppsIntoItemsAsync(allItems);

            StatusText = _localizationService.GetString("Progress_CheckingInstallStatus");
            await CheckInstallationStatusAsync();

            IsAllSelected = false;
            IsInitialized = true;
            StatusText = $"Loaded {Items.Count} items";
            RebuildCategories();
        }
        catch (Exception ex)
        {
            _logService.LogError("[ExternalAppsViewModel] Error loading apps", ex);
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
        if (_externalAppsService == null) return;

        try
        {
            var definitions = Items.Select(item => item.Definition).ToList();
            var statusResults = await _externalAppsService.CheckBatchInstalledAsync(definitions);

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

    /// <summary>
    /// Installs selected apps with optional confirmation skip (for ConfigurationService compatibility).
    /// </summary>
    public async Task InstallApps(bool skipConfirmation = false)
    {
        var selectedItems = Items.Where(a => a.IsSelected).ToList();
        if (!selectedItems.Any()) return;

        if (!await _connectivityService.IsInternetConnectedAsync(true))
        {
            if (!skipConfirmation)
            {
                await _dialogService.ShowWarningAsync(
                    "An internet connection is required to install apps.",
                    "No Internet Connection");
            }
            return;
        }

        if (!skipConfirmation)
        {
            var itemNames = selectedItems.Select(a => a.Name).ToList();
            var confirmed = await _dialogService.ShowAppOperationConfirmationAsync("install", itemNames, selectedItems.Count);
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

        if (!await _connectivityService.IsInternetConnectedAsync(true))
        {
            await _dialogService.ShowWarningAsync(
                "An internet connection is required to install apps.",
                "No Internet Connection");
            return;
        }

        var itemNames = selectedItems.Select(a => a.Name).ToList();
        var confirmed = await _dialogService.ShowAppOperationConfirmationAsync("install", itemNames, selectedItems.Count);
        if (!confirmed) return;

        await InstallAppsInternalAsync(selectedItems);
    }

    private async Task InstallAppsInternalAsync(List<AppItemViewModel> selectedItems)
    {

        IsTaskRunning = true;
        StatusText = _localizationService.GetString("Progress_Task_InstallingExternalApps");

        try
        {
            _progressService.StartTask("Installing External Apps", false);
            var progress = _progressService.CreateDetailedProgress();

            int successCount = 0;
            foreach (var app in selectedItems)
            {
                var result = await _externalAppsService.InstallAppAsync(app.Definition, progress);
                if (result.Success && result.Result)
                {
                    app.IsInstalled = true;
                    successCount++;
                }
            }

            StatusText = $"Installed {successCount} of {selectedItems.Count} apps";
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
        var confirmed = await _dialogService.ShowAppOperationConfirmationAsync("uninstall", itemNames, selectedItems.Count);
        if (!confirmed) return;

        IsTaskRunning = true;
        StatusText = _localizationService.GetString("Progress_Task_UninstallingExternalApps");

        try
        {
            _progressService.StartTask("Uninstalling External Apps", false);
            var progress = _progressService.CreateDetailedProgress();

            int successCount = 0;
            foreach (var app in selectedItems)
            {
                var result = await _externalAppsService.UninstallAppAsync(app.Definition, progress);
                if (result.Success && result.Result)
                {
                    app.IsInstalled = false;
                    successCount++;
                }
            }

            StatusText = $"Uninstalled {successCount} of {selectedItems.Count} apps";
            await RefreshAfterOperationAsync();
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var item in Items)
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }
        }
        base.Dispose(disposing);
    }
}
