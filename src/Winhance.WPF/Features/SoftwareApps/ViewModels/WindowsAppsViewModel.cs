using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Utils;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.SoftwareApps.Models;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    public partial class WindowsAppsViewModel(
        ITaskProgressService progressService,
        ILogService logService,
        IEventBus eventBus,
        IWindowsAppsService windowsAppsService,
        IAppOperationService appOperationService,
        IAppStatusDiscoveryService appStatusDiscoveryService,
        IConfigurationService configurationService,
        IScriptDetectionService scriptDetectionService,
        IInternetConnectivityService connectivityService,
        IDialogService dialogService)
        : BaseAppFeatureViewModel<AppItemViewModel>(progressService, logService, eventBus, dialogService, connectivityService)
    {
        private const string PowerShellOperationMessage =
            "PowerShell is now handling this operation.\n\n" +
            "You can see the real-time progress in the PowerShell window that just opened. " +
            "Feel free to minimize it and let it run in the background - Winhance will continue working normally.";

        public override string ModuleId => FeatureIds.WindowsApps;
        public override string DisplayName => "Windows Apps";

        public event EventHandler SelectedItemsChanged;

        [ObservableProperty] private bool _isUpdatingButtonStates = false;
        [ObservableProperty] private bool _isRemovingApps;
        [ObservableProperty] private ObservableCollection<ScriptInfo> _activeScripts = new();
        [ObservableProperty] private bool _isAllSelectedOptionalFeatures;

        private ICollectionView _allItemsView;

        public ICollectionView AllItemsView
        {
            get
            {
                if (_allItemsView == null)
                {
                    InitializeCollectionView();
                }
                return _allItemsView;
            }
        }

        public IEnumerable<AppItemViewModel> WindowsAppsFiltered =>
            GetSortedFilteredItems(item => !string.IsNullOrEmpty(item.Definition.AppxPackageName));

        public IEnumerable<AppItemViewModel> CapabilitiesFiltered =>
            GetSortedFilteredItems(item => !string.IsNullOrEmpty(item.Definition.CapabilityName));

        public IEnumerable<AppItemViewModel> OptionalFeaturesFiltered =>
            GetSortedFilteredItems(item => !string.IsNullOrEmpty(item.Definition.OptionalFeatureName));

        private IEnumerable<AppItemViewModel> GetSortedFilteredItems(Func<AppItemViewModel, bool> typeFilter)
        {
            if (Items == null) return Enumerable.Empty<AppItemViewModel>();

            var filtered = Items.Where(typeFilter);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(w => SearchHelper.MatchesSearchTerm(SearchText, w.Name, w.Description, w.Id));
            }

            return filtered
                .OrderByDescending(w => w.IsInstalled)
                .ThenBy(w => w.Name);
        }

        private bool _isAllSelected;
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (SetProperty(ref _isAllSelected, value))
                {
                    SetAllItemsSelection(value);
                    UpdateSpecializedCheckboxStates(value);
                }
            }
        }

        private bool _isAllSelectedInstalled;
        public bool IsAllSelectedInstalled
        {
            get => _isAllSelectedInstalled;
            set
            {
                if (SetProperty(ref _isAllSelectedInstalled, value))
                {
                    SetInstalledItemsSelection(value);
                    UpdateIsAllSelectedState();
                }
            }
        }

        private bool _isAllSelectedNotInstalled;
        public bool IsAllSelectedNotInstalled
        {
            get => _isAllSelectedNotInstalled;
            set
            {
                if (SetProperty(ref _isAllSelectedNotInstalled, value))
                {
                    SetNotInstalledItemsSelection(value);
                    UpdateIsAllSelectedState();
                }
            }
        }

        private bool _hasSelectedItems;
        private bool _hasSelectedItemsCacheValid;

        public bool HasSelectedItems
        {
            get
            {
                if (!_hasSelectedItemsCacheValid)
                {
                    _hasSelectedItems = Items?.Any(a => a.IsSelected) == true;
                    _hasSelectedItemsCacheValid = true;
                }
                return _hasSelectedItems;
            }
        }

        protected override void OnTableViewModeChanged()
        {
            if (IsTableViewMode)
            {
                InitializeCollectionView();
            }
            UpdateAllItemsCollection();
        }

        [RelayCommand]
        public void UpdateAllItemsCollectionExplicit()
        {
            UpdateAllItemsCollection();
        }

        public void UpdateAllItemsCollection()
        {
            if (_allItemsView != null)
            {
                _allItemsView.Refresh();
            }

            OnPropertyChanged(nameof(WindowsAppsFiltered));
            OnPropertyChanged(nameof(CapabilitiesFiltered));
            OnPropertyChanged(nameof(OptionalFeaturesFiltered));
        }



        public override async Task LoadItemsAsync()
        {
            if (windowsAppsService == null) return;

            IsLoading = true;

            try
            {
                Items.Clear();
                UnsubscribeFromItemPropertyChangedEvents();

                var allItems = await windowsAppsService.GetAppsAsync();
                var apps = allItems.Where(x => !string.IsNullOrEmpty(x.AppxPackageName) || !string.IsNullOrEmpty(x.WinGetPackageId));
                var capabilities = allItems.Where(x => !string.IsNullOrEmpty(x.CapabilityName));
                var features = allItems.Where(x => !string.IsNullOrEmpty(x.OptionalFeatureName));

                LoadAppsIntoItems(apps, capabilities, features);
                StatusText = $"Loaded {Items.Count} total items";
                UpdateAllItemsCollection();
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading Windows apps: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                RefreshScriptStatus();
            }
        }

        public override async Task CheckInstallationStatusAsync()
        {
            if (appStatusDiscoveryService == null) return;

            IsLoading = true;

            try
            {
                var definitions = Items.Select(item => item.Definition).ToList();
                var statusResults = await appStatusDiscoveryService.GetInstallationStatusBatchAsync(definitions);

                foreach (var item in Items)
                {
                    if (statusResults.TryGetValue(item.Definition.Id, out bool isInstalled))
                    {
                        item.Definition.IsInstalled = isInstalled;
                    }
                }

                OnPropertyChanged(nameof(WindowsAppsFiltered));
                OnPropertyChanged(nameof(CapabilitiesFiltered));
                OnPropertyChanged(nameof(OptionalFeaturesFiltered));

                StatusText = $"Installation status checked for {Items.Count} items";
            }
            catch (Exception ex)
            {
                StatusText = $"Error checking installation status: {ex.Message}";
                logService.LogError("Error checking installation status", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task InstallApps()
        {
            var selectedItems = GetSelectedItemsForOperation();
            if (!selectedItems.HasItems)
            {
                await ShowNoItemsSelectedDialogAsync("installation");
                return;
            }

            if (!await CheckConnectivityAsync()) return;
            if (!await ShowConfirmationAsync("install", selectedItems.AllItems)) return;

            await ExecuteWithProgressAsync(
                progressService => ExecuteInstallOperation(selectedItems, progressService.CreateDetailedProgress()),
                "Installing Windows Apps"
            );
        }

        [RelayCommand]
        public async Task RemoveApps()
        {
            var selectedItems = GetSelectedItemsForOperation();
            if (!selectedItems.HasItems)
            {
                await ShowNoItemsSelectedDialogAsync("removal");
                return;
            }

            if (!await ShowConfirmationAsync("remove", selectedItems.AllItems)) return;

            await ExecuteWithProgressAsync(
                progressService => ExecuteRemoveOperation(selectedItems, progressService.CreateDetailedProgress()),
                "Removing Windows Apps"
            );
        }

        [RelayCommand]
        public async Task InstallApp(AppItemViewModel app)
        {
            if (app == null) return;

            if (!await CheckConnectivityAsync()) return;
            if (ShowOperationConfirmationDialog("Install", new[] { app }) != true) return;

            IsLoading = true;
            StatusText = $"Installing {app.Name}...";

            try
            {
                var progress = progressService.CreateDetailedProgress();
                var result = await appOperationService.InstallAppAsync(
                    app.Definition, progress);

                if (result.Success)
                {
                    app.IsInstalled = true;
                    StatusText = $"Successfully installed {app.Name}";

                    // Trigger UI refresh immediately after operation completes
                    RefreshUIAfterOperation();

                    // Check if this is a PowerShell operation (capability or optional feature)
                    bool isPowerShellOperation = !string.IsNullOrEmpty(app.Definition.CapabilityName) ||
                                               !string.IsNullOrEmpty(app.Definition.OptionalFeatureName);

                    if (isPowerShellOperation)
                    {
                        // Show PowerShell information dialog for capabilities and features
                        await dialogService.ShowInformationAsync(PowerShellOperationMessage, "PowerShell Operation");
                    }
                    else
                    {
                        // Show standard success dialog for regular WinGet apps
                        ShowOperationResultDialog("Install", 1, 1, new[] { app.Name }, Array.Empty<string>());
                    }
                }
                else
                {
                    StatusText = result.ErrorMessage ?? $"Failed to install {app.Name}";
                    RefreshUIAfterOperation();
                    ShowOperationResultDialog("Install", 0, 1, Array.Empty<string>(),
                        new[] { $"{app.Name}: {result.ErrorMessage}" });
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error installing {app.Name}: {ex.Message}";
                RefreshUIAfterOperation();
                ShowOperationResultDialog("Install", 0, 1, Array.Empty<string>(),
                    new[] { $"{app.Name}: {ex.Message}" });
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task RemoveApp(AppItemViewModel app)
        {
            if (app == null) return;

            if (ShowOperationConfirmationDialog("Remove", new[] { app }) != true) return;

            IsLoading = true;
            StatusText = $"Removing {app.Name}...";

            try
            {
                bool success = await ExecuteSingleRemovalOperation(app);
                StatusText = success ? $"Successfully removed {app.Name}" : $"Failed to remove {app.Name}";

                // Trigger UI refresh immediately after operation completes
                RefreshUIAfterOperation();

                ShowOperationResultDialog("Remove", success ? 1 : 0, 1,
                    success ? new[] { app.Name } : Array.Empty<string>(),
                    success ? Array.Empty<string>() : new[] { app.Name });
            }
            catch (Exception ex)
            {
                StatusText = $"Error removing {app.Name}: {ex.Message}";
                RefreshUIAfterOperation();
                ShowOperationResultDialog("Remove", 0, 1, Array.Empty<string>(),
                    new[] { $"{app.Name}: {ex.Message}" });
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadAppsAndCheckInstallationStatusAsync()
        {
            if (IsInitialized) return;

            await LoadItemsAsync();
            await CheckInstallationStatusAsync();
            IsAllSelected = false;
            IsInitialized = true;
            RefreshScriptStatus();
        }

        private async void RefreshUIAfterOperation()
        {
            await CheckInstallationStatusAsync();
            ClearAllSelections();
            UpdateAllItemsCollection();
        }

        private void ClearAllSelections()
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

        public override async void OnNavigatedTo(object parameter)
        {
            try
            {
                if (!IsInitialized)
                {
                    CurrentSortProperty = "IsInstalled";
                    SortDirection = ListSortDirection.Descending;
                    await LoadAppsAndCheckInstallationStatusAsync();
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading apps: {ex.Message}";
                IsLoading = false;
            }
        }

        protected override string[] GetSearchableFields(AppItemViewModel item)
        {
            return new[] { item.Name, item.Description, item.Id };
        }

        protected override string GetItemName(AppItemViewModel item)
        {
            return item.Name;
        }


        private void InitializeCollectionView()
        {
            if (_allItemsView != null) return;

            _allItemsView = CollectionViewSource.GetDefaultView(Items);
            _allItemsView.Filter = FilterPredicate;
            OnPropertyChanged(nameof(AllItemsView));
            ApplySorting();
            SetupCollectionChangeHandlers();
        }

        private bool FilterPredicate(object obj)
        {
            if (obj is AppItemViewModel app)
            {
                return SearchHelper.MatchesSearchTerm(SearchText, app.Name, app.Description, app.Id);
            }
            return true;
        }

        private void SetupCollectionChangeHandlers()
        {
            Items.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateAllItemsCollection();
        }

        private void ApplySorting()
        {
            if (_allItemsView?.SortDescriptions == null || string.IsNullOrEmpty(CurrentSortProperty)) return;

            _allItemsView.SortDescriptions.Clear();
            _allItemsView.SortDescriptions.Add(new SortDescription(CurrentSortProperty, SortDirection));

            switch (CurrentSortProperty)
            {
                case "IsInstalled":
                    _allItemsView.SortDescriptions.Add(new SortDescription("AppType", ListSortDirection.Ascending));
                    _allItemsView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                    break;
                case "AppType":
                    _allItemsView.SortDescriptions.Add(new SortDescription("IsInstalled", ListSortDirection.Descending));
                    _allItemsView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                    break;
                default:
                    _allItemsView.SortDescriptions.Add(new SortDescription("IsInstalled", ListSortDirection.Descending));
                    _allItemsView.SortDescriptions.Add(new SortDescription("AppType", ListSortDirection.Ascending));
                    break;
            }
        }

        protected override void OnOptimizedSelectionChanged()
        {
            InvalidateHasSelectedItemsCache();
            OnPropertyChanged(nameof(HasSelectedItems));
            SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void ApplyOptimizedSorting()
        {
            ApplySorting();
        }

        protected override void ApplySearch()
        {
            if (_allItemsView != null)
            {
                _allItemsView.Refresh();
            }
            UpdateAllItemsCollection();
        }

        private IEnumerable<AppItemViewModel> FilterItems(IEnumerable<AppItemViewModel> items)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return items;

            return items.Where(item => SearchHelper.MatchesSearchTerm(SearchText, item.Name, item.Description, item.Id));
        }


        private void LoadAppsIntoItems(IEnumerable<ItemDefinition> apps, IEnumerable<ItemDefinition> capabilities, IEnumerable<ItemDefinition> features)
        {
            foreach (var app in apps)
            {
                var viewModel = new AppItemViewModel(
                    app,
                    appOperationService,
                    dialogService,
                    logService);
                Items.Add(viewModel);
                viewModel.PropertyChanged += Item_PropertyChanged;
            }

            foreach (var capability in capabilities)
            {
                var viewModel = new AppItemViewModel(
                    capability,
                    appOperationService,
                    dialogService,
                    logService);
                Items.Add(viewModel);
                viewModel.PropertyChanged += Item_PropertyChanged;
            }

            foreach (var feature in features)
            {
                var viewModel = new AppItemViewModel(
                    feature,
                    appOperationService,
                    dialogService,
                    logService);
                Items.Add(viewModel);
                viewModel.PropertyChanged += Item_PropertyChanged;
            }
        }


        private SelectedItemsCollection GetSelectedItemsForOperation()
        {
            var selectedItems = Items.Where(a => a.IsSelected).ToList();
            return new SelectedItemsCollection
            {
                Apps = selectedItems.Where(a => !string.IsNullOrEmpty(a.Definition.AppxPackageName)).ToList(),
                Capabilities = selectedItems.Where(a => !string.IsNullOrEmpty(a.Definition.CapabilityName)).ToList(),
                Features = selectedItems.Where(a => !string.IsNullOrEmpty(a.Definition.OptionalFeatureName)).ToList()
            };
        }


        private async Task<bool> ShowConfirmationAsync(string operationType, IEnumerable<AppItemViewModel> items)
        {
            var itemNames = items.Select(a => a.Name);
            return await ShowConfirmItemsDialogAsync(operationType, itemNames, items.Count()) == true;
        }

        private async Task<int> ExecuteInstallOperation(SelectedItemsCollection selectedItems, IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken = default)
        {
            int totalSuccessCount = 0;

            // 1. Process Capabilities first (if any)
            if (selectedItems.Capabilities.Any())
            {
                var capabilityResults = new OperationResultAggregator();
                await ProcessCapabilityInstallations(selectedItems.Capabilities, capabilityResults, progress, cancellationToken);
                totalSuccessCount += capabilityResults.SuccessCount;
            }

            // 2. Process Optional Features second (if any)
            if (selectedItems.Features.Any())
            {
                var featureResults = new OperationResultAggregator();
                await ProcessFeatureInstallations(selectedItems.Features, featureResults, progress, cancellationToken);
                totalSuccessCount += featureResults.SuccessCount;
            }

            // 3. Process WinGet Apps last (if any)
            if (selectedItems.Apps.Any())
            {
                var appResults = new OperationResultAggregator();
                await ProcessAppInstallations(selectedItems.Apps, appResults, progress, cancellationToken);
                totalSuccessCount += appResults.SuccessCount;
            }

            // Trigger UI refresh immediately after operations complete
            RefreshUIAfterOperation();

            // Now show dialogs
            if (selectedItems.Capabilities.Any())
            {
                await dialogService.ShowInformationAsync(PowerShellOperationMessage, "PowerShell Operations - Capabilities");
            }

            if (selectedItems.Features.Any())
            {
                await dialogService.ShowInformationAsync(PowerShellOperationMessage, "PowerShell Operations - Optional Features");
            }

            if (selectedItems.Apps.Any())
            {
                var appResults = new OperationResultAggregator();
                // Re-calculate for dialog display only
                foreach (var app in selectedItems.Apps)
                {
                    appResults.Add(app.Name, app.IsInstalled, "");
                }

                ShowOperationResultDialog("Install", appResults.SuccessCount, selectedItems.Apps.Count,
                    appResults.SuccessItems, appResults.FailedItems);
            }

            return totalSuccessCount;
        }

        private async Task<int> ExecuteRemoveOperation(SelectedItemsCollection selectedItems, IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken = default)
        {
            var allDefinitions = selectedItems.AllItems.Select(item => item.Definition).ToList();

            var result = await appOperationService.UninstallAppsAsync(allDefinitions, progress);

            if (result.Success)
            {
                foreach (var item in selectedItems.AllItems)
                {
                    item.IsInstalled = false;
                }

                // Trigger UI refresh immediately after operation completes
                RefreshUIAfterOperation();

                var successCount = result.Result;
                var totalCount = selectedItems.TotalCount;
                var succeededItems = selectedItems.AllNames.Take(successCount).ToList();
                var failedItems = selectedItems.AllNames.Skip(successCount).ToList();

                ShowOperationResultDialog("Remove", successCount, totalCount, succeededItems, failedItems);
                return successCount;
            }

            // Trigger UI refresh even if operation failed
            RefreshUIAfterOperation();

            ShowOperationResultDialog("Remove", 0, selectedItems.TotalCount, new List<string>(), selectedItems.AllNames.ToList());
            return 0;
        }

        private async Task ProcessAppInstallations(List<AppItemViewModel> apps, OperationResultAggregator results, IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken)
        {
            foreach (var app in apps)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var result = await appOperationService.InstallAppAsync(app.Definition, progress);
                    results.Add(app.Name, result.Success && result.Result, result.ErrorMessage);
                    if (result.Success && result.Result) app.IsInstalled = true;
                }
                catch (Exception ex)
                {
                    results.Add(app.Name, false, ex.Message);
                }
            }
        }

        private async Task ProcessCapabilityInstallations(List<AppItemViewModel> capabilities, OperationResultAggregator results, IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken)
        {
            foreach (var capability in capabilities)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var result = await appOperationService.InstallAppAsync(capability.Definition, progress);
                    results.Add(capability.Name, result.Success && result.Result, result.ErrorMessage);
                    if (result.Success && result.Result) capability.IsInstalled = true;
                }
                catch (Exception ex)
                {
                    results.Add(capability.Name, false, ex.Message);
                }
            }
        }

        private async Task ProcessFeatureInstallations(List<AppItemViewModel> features, OperationResultAggregator results, IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken)
        {
            foreach (var feature in features)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var result = await appOperationService.InstallAppAsync(feature.Definition, progress);
                    results.Add(feature.Name, result.Success && result.Result, result.ErrorMessage);
                    if (result.Success && result.Result) feature.IsInstalled = true;
                }
                catch (Exception ex)
                {
                    results.Add(feature.Name, false, ex.Message);
                }
            }
        }

        private async Task<bool> ExecuteSingleRemovalOperation(AppItemViewModel app)
        {
            var result = await appOperationService.UninstallAppAsync(app.Definition.Id);
            if (result.Success) app.Definition.IsInstalled = false;
            return result.Success;
        }

        private static string GetKeyForApp(AppItemViewModel app)
        {
            return app.Definition.CapabilityName ?? app.Definition.OptionalFeatureName ?? app.Definition.AppxPackageName ?? app.Definition.Id;
        }

        private void SetAllItemsSelection(bool value)
        {
            Items.ToList().ForEach(app => app.IsSelected = value);

            OnPropertyChanged(nameof(WindowsAppsFiltered));
            OnPropertyChanged(nameof(CapabilitiesFiltered));
            OnPropertyChanged(nameof(OptionalFeaturesFiltered));
        }

        private void UpdateSpecializedCheckboxStates(bool value)
        {
            _isAllSelectedInstalled = value;
            _isAllSelectedNotInstalled = value;
            OnPropertyChanged(nameof(IsAllSelectedInstalled));
            OnPropertyChanged(nameof(IsAllSelectedNotInstalled));
        }

        private void SetInstalledItemsSelection(bool value)
        {
            Items.Where(a => a.IsInstalled).ToList().ForEach(app => app.IsSelected = value);

            OnPropertyChanged(nameof(WindowsAppsFiltered));
            OnPropertyChanged(nameof(CapabilitiesFiltered));
            OnPropertyChanged(nameof(OptionalFeaturesFiltered));
        }

        private void SetNotInstalledItemsSelection(bool value)
        {
            Items.Where(a => !a.IsInstalled).ToList().ForEach(app => app.IsSelected = value);

            OnPropertyChanged(nameof(WindowsAppsFiltered));
            OnPropertyChanged(nameof(CapabilitiesFiltered));
            OnPropertyChanged(nameof(OptionalFeaturesFiltered));
        }

        private void UpdateIsAllSelectedState()
        {
            bool allItemsSelected = Items.All(app => app.IsSelected);

            if (_isAllSelected != allItemsSelected)
            {
                _isAllSelected = allItemsSelected;
                OnPropertyChanged(nameof(IsAllSelected));
            }

            UpdateSpecializedCheckboxStates();
        }

        private void UpdateSpecializedCheckboxStates()
        {
            var installedItems = Items.Where(a => a.IsInstalled);
            var notInstalledItems = Items.Where(a => !a.IsInstalled);

            bool allInstalledSelected = !installedItems.Any() || installedItems.All(a => a.IsSelected);
            bool allNotInstalledSelected = !notInstalledItems.Any() || notInstalledItems.All(a => a.IsSelected);

            if (_isAllSelectedInstalled != allInstalledSelected)
            {
                _isAllSelectedInstalled = allInstalledSelected;
                OnPropertyChanged(nameof(IsAllSelectedInstalled));
            }

            if (_isAllSelectedNotInstalled != allNotInstalledSelected)
            {
                _isAllSelectedNotInstalled = allNotInstalledSelected;
                OnPropertyChanged(nameof(IsAllSelectedNotInstalled));
            }
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppItemViewModel.IsSelected))
            {
                UpdateIsAllSelectedState();
                InvalidateHasSelectedItemsCache();
                OnPropertyChanged(nameof(HasSelectedItems));

                OnPropertyChanged(nameof(WindowsAppsFiltered));
                OnPropertyChanged(nameof(CapabilitiesFiltered));
                OnPropertyChanged(nameof(OptionalFeaturesFiltered));
            }
        }

        private void InvalidateHasSelectedItemsCache()
        {
            _hasSelectedItemsCacheValid = false;
        }

        private void UnsubscribeFromItemPropertyChangedEvents()
        {
            Items.ToList().ForEach(app => app.PropertyChanged -= Item_PropertyChanged);
        }


        private bool? ShowOperationConfirmationDialog(string operationType, IEnumerable<AppItemViewModel> selectedApps)
        {
            return ShowOperationConfirmationDialogAsync(operationType, selectedApps).GetAwaiter().GetResult() ? true : (bool?)false;
        }

        private async Task<bool> ShowOperationConfirmationDialogAsync(string operationType, IEnumerable<AppItemViewModel> selectedApps)
        {
            string title = $"Confirm {operationType}";
            string headerText = $"The following items will be {GetPastTense(operationType)}:";
            var appNames = selectedApps.Select(a => a.Name).ToList();
            string message = $"{headerText}\n{string.Join("\n", appNames)}\n\nDo you want to continue?";

            return await dialogService.ShowConfirmationAsync(message, title);
        }


        public void RefreshScriptStatus()
        {
            if (scriptDetectionService == null) return;

            IsRemovingApps = scriptDetectionService.AreRemovalScriptsPresent();
            ActiveScripts.Clear();
            foreach (var script in scriptDetectionService.GetActiveScripts())
                ActiveScripts.Add(script);
        }

        public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);
    }
}