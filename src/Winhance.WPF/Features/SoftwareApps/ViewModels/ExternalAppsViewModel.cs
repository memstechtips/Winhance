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
        IInternetConnectivityService connectivityService)
        : BaseAppFeatureViewModel<AppItemViewModel>(progressService, logService, eventBus, dialogService, connectivityService)
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

                    var appsByCategory = filteredItems.GroupBy(app => app.Category).OrderBy(group => group.Key);

                    foreach (var group in appsByCategory)
                    {
                        var categoryApps = new ObservableCollection<AppItemViewModel>(group);
                        var categoryViewModel = new ExternalAppsCategoryViewModel(group.Key, categoryApps);
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
                    "Installing External Apps"
                );
            }
            catch (OperationCanceledException)
            {
                CurrentCancellationReason = CancellationReason.UserCancelled;
                // No dialog needed - user knows they cancelled and task progress control disappears
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
                        logService);
                    Items.Add(viewModel);
                    viewModel.PropertyChanged += Item_PropertyChanged;
                }

                StatusText = $"Loaded {Items.Count} external apps";
                UpdateAllItemsCollection();
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
            await Task.CompletedTask;
        }

        public async Task LoadAppsAndCheckInstallationStatusAsync()
        {
            if (IsInitialized) return;

            await LoadItemsAsync();
            IsAllSelected = false;
            IsInitialized = true;
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
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading apps: {ex.Message}";
                IsLoading = false;
            }
        }

        protected override string[] GetSearchableFields(AppItemViewModel item)
        {
            return new[] { item.Name, item.Description, item.Category };
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


        private async Task<bool> ShowConfirmationAsync(string operationType, IEnumerable<AppItemViewModel> items)
        {
            var itemNames = items.Select(a => a.Name);
            return await ShowConfirmItemsDialogAsync(operationType, itemNames, items.Count()) == true;
        }

        private async Task<int> ExecuteInstallOperation(List<AppItemViewModel> selectedApps, IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken = default, bool skipResultDialog = false)
        {
            var results = new OperationResultAggregator();

            foreach (var app in selectedApps)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var result = await appOperationService.InstallAppAsync(app.Definition, progress, shouldRemoveFromBloatScript: false);
                    if (result.IsCancelled)
                    {
                        CurrentCancellationReason = CancellationReason.UserCancelled;
                        break;
                    }
                    results.Add(app.Name, result.Success && result.Result, result.ErrorMessage);
                    if (result.Success && result.Result) app.IsInstalled = true;
                }
                catch (OperationCanceledException)
                {
                    CurrentCancellationReason = CancellationReason.UserCancelled;
                    break;
                }
                catch (Exception ex)
                {
                    results.Add(app.Name, false, ex.Message);
                }
            }

            if (!skipResultDialog)
                await ShowOperationResultDialogAsync("Install", results.SuccessCount, results.TotalCount,
                    results.SuccessItems, results.FailedItems);

            return results.SuccessCount;
        }

        private void SetAllItemsSelection(bool value)
        {
            foreach (var item in Items)
                item.IsSelected = value;
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