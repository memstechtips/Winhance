using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.SoftwareApps.Models;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    public partial class ExternalAppsViewModel(
        ITaskProgressService progressService,
        ILogService logService,
        IEventBus eventBus,
        IExternalAppsService externalAppsService,
        IAppOperationService appOperationService,
        IConfigurationService configurationService,
        IDialogService dialogService,
        IInternetConnectivityService connectivityService,
        ILocalizationService localizationService)
        : BaseAppFeatureViewModel<AppItemViewModel>(progressService, logService, eventBus, dialogService, connectivityService, localizationService)
    {
        private System.Threading.Timer? _refreshTimer;
        private CancellationTokenSource? _refreshCts;
        public override string ModuleId => FeatureIds.ExternalApps;
        public override string DisplayName => "External Apps";

        public new bool IsTableViewMode
        {
            get => base.IsTableViewMode;
            set
            {
                if (base.IsTableViewMode != value)
                {
                    base.IsTableViewMode = value;
                    if (value)
                    {
                        InitializeCollectionView();
                        UpdateAllItemsCollection();
                    }
                    else
                    {
                        CleanupTableView();
                        OnPropertyChanged(nameof(Categories));
                    }
                }
            }
        }

        public override void OnNavigatedFrom()
        {
            CleanupTableView();
            base.OnNavigatedFrom();
        }

        public void CleanupTableView()
        {
            if (!IsTableViewMode) return;

            _refreshTimer?.Dispose();
            _refreshTimer = null;

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = null;

            if (_allItemsView != null)
            {
                _allItemsView.Filter = null;
                CleanupCollectionHandlers();
                _allItemsView = null;
            }
        }

        private bool _isAllSelected = false;

        private ICollectionView _allItemsView;

        public event EventHandler SelectedItemsChanged;

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

        public ObservableCollection<ExternalAppsCategoryViewModel> Categories
        {
            get
            {
                if (!IsTableViewMode)
                {
                    var filteredItems = GetFilteredItems();
                    var categories = new ObservableCollection<ExternalAppsCategoryViewModel>();

                    var appsByGroup = filteredItems.GroupBy(app => app.GroupName).OrderBy(group => group.Key);

                    foreach (var group in appsByGroup)
                    {
                        var categoryApps = new ObservableCollection<AppItemViewModel>(group);
                        var localizedCategoryName = LocalizeCategoryName(group.Key);
                        var categoryViewModel = new ExternalAppsCategoryViewModel(group.Key, categoryApps, localizedCategoryName);
                        categories.Add(categoryViewModel);
                    }

                    return categories;
                }
                return new ObservableCollection<ExternalAppsCategoryViewModel>();
            }
        }

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

        public void UpdateAllItemsCollection()
        {
            if (!IsTableViewMode || _allItemsView == null) return;

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();

            var token = _refreshCts.Token;

            Task.Delay(150, token).ContinueWith(_ =>
            {
                if (!token.IsCancellationRequested && IsTableViewMode)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (_allItemsView != null && IsTableViewMode && !token.IsCancellationRequested)
                        {
                            _allItemsView.Refresh();
                        }
                    });
                }
            }, TaskScheduler.Default);
        }

        private string LocalizeCategoryName(string englishCategory)
        {
            var keyName = englishCategory
                .Replace(" ", "")
                .Replace("&", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("/", "")
                .Replace(",", "");
            var key = $"ExternalApps_Category_{keyName}";
            return localizationService.GetString(key);
        }

        [RelayCommand]
        public async Task InstallApps(bool skipConfirmation = false)
        {
            var selectedApps = GetSelectedItems();
            if (!selectedApps.Any())
            {
                await ShowNoItemsSelectedDialogAsync("installation");
                return;
            }

            if (!await CheckConnectivityAsync()) return;
            if (!skipConfirmation && !await ShowConfirmationAsync("install", selectedApps))
                return;

            try
            {
                await ExecuteWithProgressAsync(
                    progressService => ExecuteInstallOperation(selectedApps.ToList(), progressService.CreateDetailedProgress(), skipResultDialog: skipConfirmation),
                    localizationService.GetString("Progress_Task_InstallingExternalApps")
                );
            }
            catch (OperationCanceledException)
            {
                CurrentCancellationReason = CancellationReason.UserCancelled;
            }
        }

        [RelayCommand]
        public async Task RemoveApps(bool skipConfirmation = false)
        {
            var selectedItems = GetSelectedItemsForOperation();
            if (!selectedItems.HasItems)
            {
                await ShowNoItemsSelectedDialogAsync("removal");
                return;
            }

            if (!skipConfirmation && !await ShowConfirmationAsync("uninstall", selectedItems.AllItems))
                return;

            try
            {
                await ExecuteWithProgressAsync(
                    progressService => ExecuteRemoveOperation(selectedItems, progressService.CreateDetailedProgress(), skipResultDialog: skipConfirmation),
                    localizationService.GetString("Progress_Task_UninstallingExternalApps")
                );
            }
            catch (OperationCanceledException)
            {
                CurrentCancellationReason = CancellationReason.UserCancelled;
            }
        }

        public override async Task LoadItemsAsync()
        {
            IsLoading = true;

            try
            {
                Items.Clear();

                var itemGroup = ExternalAppDefinitions.GetExternalApps();

                foreach (var itemDef in itemGroup.Items)
                {
                    var viewModel = new AppItemViewModel(
                        itemDef,
                        appOperationService,
                        dialogService,
                        logService,
                        localizationService);
                    Items.Add(viewModel);
                    viewModel.PropertyChanged += Item_PropertyChanged;
                }

                StatusText = $"Loaded {Items.Count} external apps";
                OnPropertyChanged(nameof(Categories));
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading external apps: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }

            await Task.CompletedTask;
        }

        public override async Task CheckInstallationStatusAsync()
        {
            await CheckInstallationStatusAsync(showLoadingOverlay: true);
        }

        public async Task CheckInstallationStatusAsync(bool showLoadingOverlay)
        {
            if (Items == null || !Items.Any())
                return;

            if (showLoadingOverlay)
            {
                IsLoading = true;
                StatusText = localizationService.GetString("Progress_CheckingInstallStatus");
            }

            try
            {
                var appsWithWinGetId = Items
                    .Where(item => !string.IsNullOrEmpty(item.Definition.WinGetPackageId))
                    .ToList();

                var appsWithoutWinGetId = Items
                    .Where(item => string.IsNullOrEmpty(item.Definition.WinGetPackageId))
                    .ToList();

                int checkedCount = 0;

                if (appsWithWinGetId.Any())
                {
                    var definitions = appsWithWinGetId.Select(item => item.Definition).ToList();
                    var statusResults = await externalAppsService.CheckBatchInstalledAsync(definitions).ConfigureAwait(false);

                    foreach (var item in appsWithWinGetId)
                    {
                        if (statusResults.TryGetValue(item.Definition.Id, out bool isInstalled))
                        {
                            item.IsInstalled = isInstalled;
                            checkedCount++;
                        }
                    }
                }

                if (appsWithoutWinGetId.Any())
                {
                    var displayNames = appsWithoutWinGetId.Select(item => item.Definition.Name).ToList();
                    var statusResults = await externalAppsService.CheckInstalledByDisplayNameAsync(displayNames).ConfigureAwait(false);

                    foreach (var item in appsWithoutWinGetId)
                    {
                        if (statusResults.TryGetValue(item.Definition.Name, out bool isInstalled))
                        {
                            item.IsInstalled = isInstalled;
                            checkedCount++;
                        }
                    }
                }

                StatusText = $"Status checked for {checkedCount} apps";
            }
            catch (Exception ex)
            {
                StatusText = $"Error checking status: {ex.Message}";
                logService.LogError("Error checking installation status", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task RefreshInstallationStatus()
        {
            if (!IsInitialized)
            {
                StatusText = localizationService.GetString("Progress_WaitForInitialLoad");
                return;
            }

            IsLoading = true;
            StatusText = localizationService.GetString("Progress_RefreshingStatus");

            try
            {
                await CheckInstallationStatusAsync();
                StatusText = $"Installation status refreshed for {Items.Count} items";
            }
            catch (Exception ex)
            {
                StatusText = $"Error refreshing status: {ex.Message}";
                logService.LogError("Error refreshing installation status", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadAppsAndCheckInstallationStatusAsync()
        {
            if (IsInitialized)
            {
                logService.LogInformation("[ExternalAppsViewModel] Already initialized, skipping");
                return;
            }

            logService.LogInformation("[ExternalAppsViewModel] LoadItemsAsync starting");
            await LoadItemsAsync().ConfigureAwait(false);
            logService.LogInformation("[ExternalAppsViewModel] LoadItemsAsync completed");
            
            logService.LogInformation("[ExternalAppsViewModel] CheckInstallationStatusAsync starting");
            await CheckInstallationStatusAsync().ConfigureAwait(false);
            logService.LogInformation("[ExternalAppsViewModel] CheckInstallationStatusAsync completed");
            
            IsAllSelected = false;
            IsInitialized = true;
            logService.LogInformation("[ExternalAppsViewModel] LoadAppsAndCheckInstallationStatusAsync fully completed");
        }

        public override async void OnNavigatedTo(object parameter)
        {
            try
            {
                if (!IsInitialized)
                {
                    CurrentSortProperty = "Name";
                    SortDirection = ListSortDirection.Ascending;
                    await LoadAppsAndCheckInstallationStatusAsync();
                }
                else
                {
                    await CheckInstallationStatusAsync();
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
            return new[] { item.Name, item.Description, item.GroupName };
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
                return MatchesSearchTerm(SearchText, app);
            }
            return true;
        }

        private bool _collectionHandlersSetup = false;

        private void SetupCollectionChangeHandlers()
        {
            if (_collectionHandlersSetup) return;
            Items.CollectionChanged += OnCollectionChanged;
            _collectionHandlersSetup = true;
        }

        private void CleanupCollectionHandlers()
        {
            if (!_collectionHandlersSetup) return;
            Items.CollectionChanged -= OnCollectionChanged;
            _collectionHandlersSetup = false;
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (IsTableViewMode) UpdateAllItemsCollection();
        }

        private void ApplySorting()
        {
            if (_allItemsView?.SortDescriptions == null || string.IsNullOrEmpty(CurrentSortProperty)) return;

            _allItemsView.SortDescriptions.Clear();
            _allItemsView.SortDescriptions.Add(new SortDescription(CurrentSortProperty, SortDirection));

            if (CurrentSortProperty != "Name")
            {
                _allItemsView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            }
        }

        protected override void OnOptimizedSelectionChanged()
        {
            InvalidateHasSelectedItemsCache();
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(IsAllSelected));
            SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void ApplyOptimizedSorting()
        {
            ApplySorting();
        }

        protected override void ApplySearch()
        {
            if (IsTableViewMode)
            {
                if (_allItemsView != null)
                {
                    _allItemsView.Refresh();
                }
                UpdateAllItemsCollection();
            }
            else
            {
                OnPropertyChanged(nameof(Categories));
            }
        }

        private IEnumerable<AppItemViewModel> GetFilteredItems()
        {
            return FilterItems(Items);
        }

        private IEnumerable<AppItemViewModel> GetSelectedItems()
        {
            return Items.Where(a => a.IsSelected);
        }

        private SelectedItemsCollection GetSelectedItemsForOperation()
        {
            var selectedItems = Items.Where(a => a.IsSelected).ToList();
            return new SelectedItemsCollection
            {
                Apps = selectedItems
            };
        }

        private async Task RefreshUIAfterOperation()
        {
            await CheckInstallationStatusAsync(showLoadingOverlay: false);
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
        }


        private async Task<bool> ShowConfirmationAsync(string operationType, IEnumerable<AppItemViewModel> items)
        {
            var itemNames = items.Select(a => a.Name);
            return await ShowConfirmItemsDialogAsync(operationType, itemNames, items.Count()) == true;
        }

        private async Task<int> ExecuteInstallOperation(List<AppItemViewModel> selectedApps, IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken = default, bool skipResultDialog = false)
        {
            var results = new OperationResultAggregator();
            bool wasCancelled = false;

            foreach (var app in selectedApps)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var result = await appOperationService.InstallAppAsync(app.Definition, progress, shouldRemoveFromBloatScript: false);
                    if (result.IsCancelled)
                    {
                        CurrentCancellationReason = CancellationReason.UserCancelled;
                        wasCancelled = true;
                        break;
                    }
                    results.Add(app.Name, result.Success && result.Result, result.ErrorMessage);
                    if (result.Success && result.Result) app.IsInstalled = true;
                }
                catch (OperationCanceledException)
                {
                    CurrentCancellationReason = CancellationReason.UserCancelled;
                    wasCancelled = true;
                    break;
                }
                catch (Exception ex)
                {
                    results.Add(app.Name, false, ex.Message);
                }
            }

            await CheckInstallationStatusAsync(showLoadingOverlay: false);

            if (!skipResultDialog && !wasCancelled)
                await ShowOperationResultDialogAsync("Install", results.SuccessCount, results.TotalCount,
                    results.SuccessItems, results.FailedItems);

            return results.SuccessCount;
        }

        private async Task<int> ExecuteRemoveOperation(SelectedItemsCollection selectedItems, IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken = default, bool skipResultDialog = false)
        {
            var allDefinitions = selectedItems.AllItems.Select(item => item.Definition).ToList();

            var result = await appOperationService.UninstallExternalAppsAsync(allDefinitions, progress);

            if (result.IsCancelled)
            {
                CurrentCancellationReason = CancellationReason.UserCancelled;
                ClearAllSelections();
                UpdateAllItemsCollection();
                if (!skipResultDialog)
                    await ShowOperationResultDialogAsync("Uninstall", 0, selectedItems.TotalCount, new List<string>(), selectedItems.AllNames.ToList());
                return 0;
            }

            if (result.Success)
            {
                foreach (var item in selectedItems.AllItems)
                {
                    item.IsInstalled = false;
                }

                ClearAllSelections();
                UpdateAllItemsCollection();

                var successCount = result.Result;
                var totalCount = selectedItems.TotalCount;
                var succeededItems = selectedItems.AllNames.Take(successCount).ToList();
                var failedItems = selectedItems.AllNames.Skip(successCount).ToList();

                if (!skipResultDialog)
                    await ShowOperationResultDialogAsync("Uninstall", successCount, totalCount, succeededItems, failedItems);

                return successCount;
            }

            ClearAllSelections();
            UpdateAllItemsCollection();

            if (!skipResultDialog)
                await ShowOperationResultDialogAsync("Uninstall", 0, selectedItems.TotalCount, new List<string>(), selectedItems.AllNames.ToList());

            return 0;
        }

        private void SetAllItemsSelection(bool value)
        {
            foreach (var item in Items)
                item.IsSelected = value;
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
            foreach (var item in Items.Where(a => a.IsInstalled))
                item.IsSelected = value;

            if (!IsTableViewMode)
            {
                OnPropertyChanged(nameof(Categories));
            }
        }

        private void SetNotInstalledItemsSelection(bool value)
        {
            foreach (var item in Items.Where(a => !a.IsInstalled))
                item.IsSelected = value;

            if (!IsTableViewMode)
            {
                OnPropertyChanged(nameof(Categories));
            }
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

            bool allInstalledSelected = installedItems.Any() && installedItems.All(a => a.IsSelected);
            bool allNotInstalledSelected = notInstalledItems.Any() && notInstalledItems.All(a => a.IsSelected);

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

        private bool _isUpdatingSelection = false;

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppItemViewModel.IsSelected))
            {
                if (_isUpdatingSelection) return;

                try
                {
                    _isUpdatingSelection = true;
                    InvalidateHasSelectedItemsCache();
                    OnPropertyChanged(nameof(HasSelectedItems));
                    OnPropertyChanged(nameof(IsAllSelected));
                    UpdateSpecializedCheckboxStates();
                    SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
                }
                finally
                {
                    _isUpdatingSelection = false;
                }
            }
        }

        private void UnsubscribeFromItemPropertyChangedEvents()
        {
            foreach (var item in Items)
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Dispose();
                _refreshCts?.Cancel();
                _refreshCts?.Dispose();
                CleanupCollectionHandlers();
                UnsubscribeFromItemPropertyChangedEvents();
            }
            base.Dispose(disposing);
        }
    }
}