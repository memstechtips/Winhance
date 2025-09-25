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
                    OnPropertyChanged(nameof(Categories));
                }
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
            if (_allItemsView != null)
            {
                _allItemsView.Refresh();
            }
        }



        [RelayCommand]
        public async Task InstallApps()
        {
            var selectedApps = GetSelectedItems();
            if (!selectedApps.Any())
            {
                await ShowNoItemsSelectedDialogAsync("installation");
                return;
            }

            if (!await CheckConnectivityAsync()) return;
            if (!await ShowConfirmationAsync("install", selectedApps)) return;

            await ExecuteWithProgressAsync(
                progressService => ExecuteInstallOperation(selectedApps.ToList(), progressService.CreateDetailedProgress()),
                "Installing External Apps"
            );
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

        private void SetupCollectionChangeHandlers()
        {
            Items.CollectionChanged += OnCollectionChanged;
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
            if (_allItemsView != null)
            {
                _allItemsView.Refresh();
            }
            UpdateAllItemsCollection();
            OnPropertyChanged(nameof(Categories));
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

        private async Task<int> ExecuteInstallOperation(List<AppItemViewModel> selectedApps, IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken = default)
        {
            var results = new OperationResultAggregator();

            foreach (var app in selectedApps)
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

            ShowOperationResultDialog("Install", results.SuccessCount, results.TotalCount,
                results.SuccessItems, results.FailedItems);

            return results.SuccessCount;
        }

        private void SetAllItemsSelection(bool value)
        {
            foreach (var item in Items)
                item.IsSelected = value;
        }


        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppItemViewModel.IsSelected))
            {
                InvalidateHasSelectedItemsCache();
                OnPropertyChanged(nameof(HasSelectedItems));
                OnPropertyChanged(nameof(IsAllSelected));
                SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}