using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.WPF.Features.SoftwareApps.Services;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.SoftwareApps;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.UI.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.SoftwareApps.Models;
using ToastType = Winhance.Core.Features.UI.Interfaces.ToastType;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    public partial class ExternalAppsViewModel : BaseViewModel
    {
        [ObservableProperty]
        private bool _isInitialized = false;
        
        [ObservableProperty]
        private string _searchText = string.Empty;
        
        private readonly ITaskProgressService _progressService;
        private bool _isLoading;

        /// <summary>
        /// Gets or sets a value indicating whether the view model is loading data.
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether search is active.
        /// </summary>
        public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);

        /// <summary>
        /// Gets the collection of items.
        /// </summary>
        public ObservableCollection<ExternalApp> Items { get; } = new();

        private readonly AppViewModeManager _viewModeManager;

        /// <summary>
        /// Gets or sets the current cancellation reason.
        /// </summary>
        protected CancellationReason CurrentCancellationReason { get; set; } = CancellationReason.None;

        /// <summary>
        /// Gets or sets whether the view is in table view mode
        /// </summary>
        public bool IsTableViewMode
        {
            get => _viewModeManager.IsTableViewMode;
            set 
            { 
                if (_viewModeManager.IsTableViewMode != value)
                {
                    _viewModeManager.IsTableViewMode = value;
                    if (value)
                    {
                        UpdateAllItemsCollection();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the visibility for the grid view
        /// </summary>
        public Visibility GridViewVisibility => _viewModeManager.GridViewVisibility;

        /// <summary>
        /// Gets the visibility for the table view
        /// </summary>
        public Visibility TableViewVisibility => _viewModeManager.TableViewVisibility;

        [ObservableProperty]
        private bool _isAllSelected = false;
        
        /// <summary>
        /// Current sort property for table view
        /// </summary>
        [ObservableProperty]
        private string _currentSortProperty = "Name";
        
        /// <summary>
        /// Current sort direction for table view
        /// </summary>
        [ObservableProperty]
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;

        private readonly IAppInstallationService _appInstallationService;
        private readonly IAppService _appDiscoveryService;
        private readonly IConfigurationService _configurationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPackageManager _packageManager;
        private readonly ISearchService _searchService;
        private readonly IAppInstallationCoordinatorService _appInstallationCoordinatorService;
        private readonly IInternetConnectivityService _connectivityService;
        private readonly SoftwareAppsDialogService _dialogService;
        
        // Optimized services for performance
        private readonly OptimizedCollectionManager<ExternalApp, OptimizedExternalAppWrapper> _collectionManager;
        private readonly DebouncedSearchService _debouncedSearchService;
        
        /// <summary>
        /// Event raised when selected items change to notify parent view models
        /// </summary>
        public event EventHandler SelectedItemsChanged;

        /// <summary>
        /// Gets the collection view for all items in the table view
        /// </summary>
        public ICollectionView AllItemsView 
        {
            get 
            {
                var collectionView = _collectionManager?.CollectionView;
                return collectionView;
            }
        }

        /// <summary>
        /// Command to toggle between list view and table view modes
        /// </summary>
        [RelayCommand]
        private void ToggleViewMode(object parameter = null)
        {
            // If parameter is provided, use it to set the view mode directly
            if (parameter is string strParam)
            {
                IsTableViewMode = bool.Parse(strParam);
            }
            else if (parameter is bool boolParam)
            {
                IsTableViewMode = boolParam;
            }
            else
            {
                // Toggle the view mode if no parameter is provided
                IsTableViewMode = !IsTableViewMode;
            }

            // Update the collection if in table view mode
            if (IsTableViewMode)
            {
                UpdateAllItemsCollection();
            }

        }

        /// <summary>
        /// Updates the collection of all external apps for the table view
        /// Optimized for performance with batch updates and UI thread binding management
        /// </summary>
        public void UpdateAllItemsCollection()
        {
            // Use the optimized collection update method
            UpdateOptimizedAllItemsCollection();
        }

        /// <summary>
        /// Command to sort the table view by the specified property
        /// </summary>
        [RelayCommand]
        public void SortBy(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return;
                
            // If clicking the same column, toggle sort direction
            if (propertyName == CurrentSortProperty)
            {
                SortDirection = SortDirection == ListSortDirection.Ascending 
                    ? ListSortDirection.Descending 
                    : ListSortDirection.Ascending;
            }
            else
            {
                // New column, set as current and default to ascending
                CurrentSortProperty = propertyName;
                SortDirection = ListSortDirection.Ascending;
            }
            
            // Apply the optimized sorting
            ApplyOptimizedSorting();
        }
        
        /// <summary>
        /// Applies optimized filtering using debounced search service
        /// </summary>
        private void ApplyOptimizedFilter()
        {
            if (_collectionManager == null || _debouncedSearchService == null) return;
            
            var filter = _debouncedSearchService.CreateFilterPredicate<OptimizedExternalAppWrapper>(
                SearchText,
                wrapper => new[] { wrapper.Name, wrapper.Description, wrapper.Publisher }
            );
            
            _collectionManager.ApplyFilter(filter);
        }

        /// <summary>
        /// Handles view mode changes from AppViewModeManager
        /// </summary>
        private void OnViewModeChanged(bool isTableViewMode)
        {
            // When switching to table view, update the combined collection
            if (isTableViewMode)
            {
                UpdateOptimizedAllItemsCollection();
            }
        }
        

        
        /// <summary>
        /// Command for handling selection changes from DataGrid behavior
        /// </summary>
        [RelayCommand]
        public void HandleSelectionChanged()
        {
            OnOptimizedSelectionChanged();
        }
        
        /// <summary>
        /// Command for handling checkbox selection changes from behavior
        /// </summary>
        [RelayCommand]
        public void HandleCheckboxSelectionChanged()
        {
            OnOptimizedSelectionChanged();
        }
        
        /// <summary>
        /// Updates the combined collection using optimized incremental updates
        /// </summary>
        private void UpdateOptimizedAllItemsCollection()
        {            
            if (_collectionManager == null)
            {
                return;
            }
            
            // Update collection with incremental changes (pass source items, not wrappers)
            _collectionManager.UpdateCollectionImmediate(Items);
            
            // Apply current sorting and filtering
            ApplyOptimizedSorting();
            ApplyOptimizedFilter();            
        }
        private void OnOptimizedSelectionChanged()
        {
            // Debounce selection change notifications
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                OnPropertyChanged(nameof(HasSelectedItems));
                OnPropertyChanged(nameof(IsAllSelected));
                SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
        
        /// <summary>
        /// Applies optimized sorting using collection manager
        /// </summary>
        private void ApplyOptimizedSorting()
        {
            if (_collectionManager == null) return;
            
            _collectionManager.ApplySort(CurrentSortProperty, SortDirection);
        }
        
        /// <summary>
        /// Helper class to defer ICollectionView refresh until disposed
        /// </summary>
        private class DeferRefresh : IDisposable
        {
            private readonly ICollectionView _view;
            
            public DeferRefresh(ICollectionView view)
            {
                _view = view;
                // No direct way to suspend binding, but we can defer refresh
            }
            
            public void Dispose()
            {
                // Refresh the view when disposed
                _view.Refresh();
            }
        }
        
        /// <summary>
        /// Filters items based on the current search text.
        /// </summary>
        /// <param name="items">The items to filter.</param>
        /// <returns>The filtered items.</returns>
        protected IEnumerable<ExternalApp> FilterItems(IEnumerable<ExternalApp> items)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return items;

            var searchTerms = SearchText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return items.Where(item =>
            {
                // If any search term matches, include the item
                return searchTerms.All(term =>
                    item.Name?.ToLower().Contains(term) == true ||
                    item.Description?.ToLower().Contains(term) == true ||
                    item.PackageName?.ToLower().Contains(term) == true ||
                    item.Category?.ToLower().Contains(term) == true);
            });
        }
        
        /// <summary>
        /// Handles the SearchText property change with optimized debounced search
        /// </summary>
        /// <param name="value">The new search text value.</param>
        partial void OnSearchTextChanged(string value)
        {
            // Apply search for list view (categories)
            ApplySearch();
            
            // Apply optimized debounced search for table view
            if (_debouncedSearchService != null)
            {
                _debouncedSearchService.Search(value, _ => ApplyOptimizedFilter());
            }
            
            // Notify that IsSearchActive may have changed
            OnPropertyChanged(nameof(IsSearchActive));
        }
        
        /// <summary>
        /// Applies the current search text to filter items in the table view.
        /// </summary>
        private void ApplyTableViewFilter()
        {
            // Use the optimized filter method
            ApplyOptimizedFilter();
        }

        /// <summary>
        /// Handles changes to the IsAllSelected property
        /// </summary>
        partial void OnIsAllSelectedChanged(bool value)
        {
            // Apply the selection state to all items in the underlying collection
            foreach (var item in Items)
            {
                item.IsSelected = value;
            }
            
            // If in table view mode, also notify wrapper objects to update their UI
            if (IsTableViewMode && _collectionManager != null)
            {
                foreach (var wrapperItem in _collectionManager.Collection)
                {
                    wrapperItem.NotifyIsSelectedChanged();
                }
            }
        }

        [ObservableProperty]
        private string _statusText = "Ready";

        // ObservableCollection to store category view models
        private ObservableCollection<ExternalAppsCategoryViewModel> _categories = new();

        // Public property to expose the categories
        public ObservableCollection<ExternalAppsCategoryViewModel> Categories => _categories;

        // Cached value for HasSelectedItems to improve performance
        private bool _hasSelectedItems;
        private bool _hasSelectedItemsCacheValid;

        // Property to indicate if any items are selected
        public bool HasSelectedItems
        {
            get
            {
                if (!_hasSelectedItemsCacheValid)
                {
                    if (IsTableViewMode && _collectionManager != null)
                    {
                        // Check if any table view wrapper items are selected
                        _hasSelectedItems = _collectionManager.Collection.Any(a => a.IsSelected);
                    }
                    else
                    {
                        // Check if any regular items are selected
                        _hasSelectedItems = Items?.Any(a => a.IsSelected) == true;
                    }
                    
                    _hasSelectedItemsCacheValid = true;
                }
                
                return _hasSelectedItems;
            }
        }

        // Method to invalidate the HasSelectedItems cache
        private void InvalidateHasSelectedItemsCache()
        {

            _hasSelectedItemsCacheValid = false;

        }
        
        /// <summary>
        /// Public method to invalidate the selection state and notify that HasSelectedItems has changed
        /// Used by the view to notify the viewmodel of selection changes
        /// </summary>
        public void InvalidateSelectionState()
        {

            
            // Invalidate the cached value
            InvalidateHasSelectedItemsCache();
            
            // Trigger property changed notification
            OnPropertyChanged(nameof(HasSelectedItems));
            
            // Ensure parent viewmodel updates button states by raising selection changed event
            if (SelectedItemsChanged != null)
            {

                SelectedItemsChanged.Invoke(this, EventArgs.Empty);
            }
        }

        public ExternalAppsViewModel(
            ITaskProgressService progressService,
            ISearchService searchService,
            IPackageManager packageManager,
            IAppInstallationService appInstallationService,
            IAppService appDiscoveryService,
            IConfigurationService configurationService,
            Services.SoftwareAppsDialogService dialogService,
            IInternetConnectivityService connectivityService,
            IAppInstallationCoordinatorService appInstallationCoordinatorService,
            IServiceProvider serviceProvider
        ) : base(
            progressService,
            serviceProvider.GetRequiredService<ILogService>(),
            serviceProvider.GetRequiredService<IEventBus>()
        )
        {
            
            _progressService = progressService;
            _appInstallationService = appInstallationService;
            _appDiscoveryService = appDiscoveryService;
            _configurationService = configurationService;
            _serviceProvider = serviceProvider;
            _packageManager = packageManager;
            _searchService = searchService;
            _dialogService = dialogService;
            _connectivityService = connectivityService;
            _appInstallationCoordinatorService = appInstallationCoordinatorService;
            
            // Initialize optimized services
            var logService = serviceProvider.GetRequiredService<ILogService>();
            _collectionManager = new OptimizedCollectionManager<ExternalApp, OptimizedExternalAppWrapper>(
                app => new OptimizedExternalAppWrapper(app, OnOptimizedSelectionChanged),
                logService
            );
            _debouncedSearchService = new DebouncedSearchService(TimeSpan.FromMilliseconds(300));
            
            // Initialize view mode manager
            _viewModeManager = new AppViewModeManager();
            _viewModeManager.ViewModeChanged += OnViewModeChanged;

            // Subscribe to collection changed events to track item selection changes
            Items.CollectionChanged += Items_CollectionChanged;
            Items.CollectionChanged += (s, e) => {
            };

            // Notify that AllItemsView is now available
            OnPropertyChanged(nameof(AllItemsView));

            // Set up a loaded event handler to ensure the collection is populated
            this.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(IsInitialized) && IsInitialized)
                {
                    // If we're in table view mode, update the collection
                    if (IsTableViewMode)
                    {
                        UpdateOptimizedAllItemsCollection();
                    }
                }
            };
            
        }

        /// <summary>
        /// Handles selection changes from TableView wrapper objects
        /// </summary>
        private void OnTableViewSelectionChanged()
        {



            
            // Log selections in the optimized collection
            if (_collectionManager != null)
            {
                var selectedItems = _collectionManager.Collection.Where(a => a.IsSelected).ToList();

                foreach (var item in selectedItems.Take(5))
                {
                    // Log item selection if needed
                }
            }
            
            // Force IsTableViewMode to be true since we're getting selection changes from table view
            if (!IsTableViewMode && _collectionManager != null && _collectionManager.Collection.Any())
            {
                IsTableViewMode = true;
            }
            
            InvalidateHasSelectedItemsCache();

            
            var hasSelected = HasSelectedItems;

            

            OnPropertyChanged(nameof(HasSelectedItems));
            
            // Ensure the parent view model is notified of changes by forcing an update to the property
            // This is crucial for ensuring the buttons in the parent view model are enabled/disabled properly
            Application.Current.Dispatcher.BeginInvoke(new Action(() => 
            {

                OnPropertyChanged(nameof(HasSelectedItems));
            }));
            


        }

        private void Items_CollectionChanged(
            object sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e
        )
        {
            // Subscribe to property changed events for new items
            if (e.NewItems != null)
            {
                foreach (ExternalApp item in e.NewItems)
                {
                    // Avoid duplicate subscriptions
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }

            // Unsubscribe from property changed events for removed items
            if (e.OldItems != null)
            {
                foreach (ExternalApp item in e.OldItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }

            // If the collection changed, we might need to update HasSelectedItems
            InvalidateHasSelectedItemsCache();
            OnPropertyChanged(nameof(HasSelectedItems));
        }

        private void Item_PropertyChanged(
            object sender,
            System.ComponentModel.PropertyChangedEventArgs e
        )
        {
            // When an item's IsSelected property changes, notify that HasSelectedItems may have changed
            if (e.PropertyName == nameof(ExternalApp.IsSelected))
            {
                InvalidateHasSelectedItemsCache();
                OnPropertyChanged(nameof(HasSelectedItems));
            }
        }

        // SaveConfig and ImportConfig methods removed as part of unified configuration cleanup

        /// <summary>
        /// Initialize the table view collection
        /// </summary>
        /// <returns></returns>
        public async Task InitializeTableViewAsync()
        {
            // Initialize the table view collection
            UpdateAllItemsCollection();
            
        }
        
        /// <summary>
        /// Applies the current search text to filter items.
        /// </summary>
        protected void ApplySearch()
        {
            if (Items == null || Items.Count == 0)
                return;

            // For category view mode, filter items based on search text
            var filteredItems = FilterItems(Items);
            
            // Clear all categories
            foreach (var category in Categories)
            {
                category.Apps.Clear();
            }

            // Group filtered items by category
            var appsByCategory = new Dictionary<string, List<ExternalApp>>();

            foreach (var app in filteredItems)
            {
                string category = app.Category;
                if (string.IsNullOrEmpty(category))
                {
                    category = "Other";
                }

                if (!appsByCategory.ContainsKey(category))
                {
                    appsByCategory[category] = new List<ExternalApp>();
                }

                appsByCategory[category].Add(app);
            }

            // Update categories with filtered items
            foreach (var category in Categories)
            {
                if (appsByCategory.TryGetValue(category.Name, out var apps))
                {
                    // Sort apps alphabetically within the category
                    var sortedApps = apps.OrderBy(a => a.Name);

                    foreach (var app in sortedApps)
                    {
                        category.Apps.Add(app);
                    }
                }
            }

            // Hide empty categories if search is active
            if (IsSearchActive)
            {
                foreach (var category in Categories)
                {
                    category.IsExpanded = category.Apps.Count > 0;
                }
            }

            // Always update the AllItemsCollection to ensure it's populated
            UpdateAllItemsCollection();
        }

        public async Task LoadItemsAsync()
        {
            
            if (_packageManager == null)
            {
                return;
            }

            IsLoading = true;
            StatusText = "Loading external apps...";

            try
            {
                Items.Clear();
                _categories.Clear();

                var apps = await _packageManager.GetInstallableAppsAsync();

                if (apps == null)
                {
                    return;
                }

                // Convert AppInfo objects to ExternalApp objects and group by category
                var appsByCategory = new Dictionary<string, List<ExternalApp>>();
                
                foreach (var appInfo in apps)
                {
                    var externalApp = ExternalApp.FromAppInfo(appInfo);
                    Items.Add(externalApp);
                    
                    // Group by category for the grid view
                    string category = externalApp.Category ?? "Other";
                    if (!appsByCategory.ContainsKey(category))
                    {
                        appsByCategory[category] = new List<ExternalApp>();
                    }
                    appsByCategory[category].Add(externalApp);
                    
                }
                
                // Create category view models
                foreach (var categoryGroup in appsByCategory.OrderBy(x => x.Key))
                {
                    var categoryViewModel = new ExternalAppsCategoryViewModel(
                        categoryGroup.Key, 
                        new ObservableCollection<ExternalApp>(categoryGroup.Value.OrderBy(a => a.Name))
                    );
                    _categories.Add(categoryViewModel);
                }

                StatusText = $"Loaded {Items.Count} external apps in {_categories.Count} categories";

                // Update optimized collection
                UpdateOptimizedAllItemsCollection();
            }
            catch (System.Exception ex)
            {
                StatusText = $"Error loading external apps: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
            
        }

        public async Task CheckInstallationStatusAsync()
        {
            if (_appDiscoveryService == null)
                return;

            IsLoading = true;
            StatusText = "Checking installation status...";

            try
            {
                var statusResults = await _appDiscoveryService.GetBatchInstallStatusAsync(
                    Items.Select(a => a.PackageName)
                );

                foreach (var app in Items)
                {
                    if (statusResults.TryGetValue(app.PackageName, out bool isInstalled))
                    {
                        app.IsInstalled = isInstalled;
                    }
                }
                StatusText = "Installation status check complete";
            }
            catch (System.Exception ex)
            {
                StatusText = $"Error checking installation status: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task InstallApp(ExternalApp app)
        {
            if (app == null || _appInstallationService == null)
                return;

            // Check for internet connectivity before starting installation
            bool isInternetConnected =
                await _packageManager.SystemServices.IsInternetConnectedAsync(true);
            if (!isInternetConnected)
            {
                StatusText = "No internet connection available. Installation cannot proceed.";

                // Show dialog informing the user about the connectivity issue
                await ShowNoInternetConnectionDialogAsync();
                return;
            }

            IsLoading = true;
            StatusText = $"Installing {app.Name}...";

            // Setup cancellation for the installation process
            using var cts = new System.Threading.CancellationTokenSource();
            var cancellationToken = cts.Token;

            // Start a background task to periodically check internet connectivity during installation
            var connectivityCheckTask = StartPeriodicConnectivityCheck(app.Name, cts);

            try
            {
                var progress = _progressService.CreateDetailedProgress();

                try
                {
                    var operationResult = await _appInstallationService.InstallAppAsync(
                        app.ToAppInfo(),
                        progress,
                        cancellationToken
                    );

                    // Cancel the connectivity check task as installation is complete
                    cts.Cancel();

                    // Wait for the connectivity check task to complete
                    try
                    {
                        await connectivityCheckTask;
                    }
                    catch (OperationCanceledException)
                    { /* Expected when we cancel */
                    }

                    // Only mark as successful if the operation actually succeeded
                    if (operationResult.Success && operationResult.Result)
                    {
                        app.IsInstalled = true;
                        StatusText = $"Successfully installed {app.Name}";

                        // Show success dialog
                        _dialogService.ShowInformationAsync(
                            "Installation Complete",
                            $"{app.Name} was successfully installed.",
                            new[] { app.Name },
                            "The application has been installed successfully."
                        );
                    }
                    else
                    {
                        string errorMessage =
                            operationResult.ErrorMessage
                            ?? $"Failed to install {app.Name}. Please try again.";
                        
                        // Check if this is a cancellation rather than a failure
                        // Handle multiple cancellation message patterns from different services
                        bool isCancellation = errorMessage != null && (
                            errorMessage.Contains("cancelled by the user", StringComparison.OrdinalIgnoreCase) ||
                            errorMessage.Contains("cancelled by user", StringComparison.OrdinalIgnoreCase) ||
                            errorMessage.Contains("operation was canceled", StringComparison.OrdinalIgnoreCase) ||
                            errorMessage.Contains("operation was cancelled", StringComparison.OrdinalIgnoreCase) ||
                            errorMessage.Contains("script returned no result", StringComparison.OrdinalIgnoreCase)
                        );
                        
                        if (isCancellation)
                        {
                            // Set cancellation reason to user cancelled
                            CurrentCancellationReason = CancellationReason.UserCancelled;
                            
                            StatusText = $"Installation of {app.Name} was cancelled";
                            
                            // Show cancellation dialog
                            await ShowCancellationDialogAsync(true, false); // User-initiated cancellation
                            
                            // Reset cancellation reason after showing dialog
                            CurrentCancellationReason = CancellationReason.None;
                        }
                        else
                        {
                            StatusText = errorMessage;
                            
                            // Store the error message for later reference
                            app.LastOperationError = errorMessage;

                            // Show error dialog
                            _dialogService.ShowInformationAsync(
                                "Installation Failed",
                                $"Failed to install {app.Name}.",
                                new[] { $"{app.Name}: {errorMessage}" },
                                "There was an error during installation. Please try again later."
                            );
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Set cancellation reason to UserCancelled
                    CurrentCancellationReason = CancellationReason.UserCancelled;

                    // Cancel the connectivity check task
                    cts.Cancel();

                    // Wait for the connectivity check task to complete
                    try
                    {
                        await connectivityCheckTask;
                    }
                    catch (OperationCanceledException)
                    { /* Expected when we cancel */
                    }

                    // For single app installations, use the ShowCancellationDialogAsync method directly
                    // which will use CustomDialog with a simpler message
                    await ShowCancellationDialogAsync(true, false); // User-initiated cancellation

                    // Reset cancellation reason after showing dialog
                    CurrentCancellationReason = CancellationReason.None;

                    StatusText = $"Installation of {app.Name} was cancelled";
                }
                catch (System.Exception ex)
                {
                    // Cancel the connectivity check task
                    cts.Cancel();

                    // Wait for the connectivity check task to complete
                    try
                    {
                        await connectivityCheckTask;
                    }
                    catch (OperationCanceledException)
                    { /* Expected when we cancel */
                    }

                    // Check if the exception is related to internet connectivity
                    bool isConnectivityIssue =
                        ex.Message.Contains("internet", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains(
                            "pipeline has been stopped",
                            StringComparison.OrdinalIgnoreCase
                        );

                    if (isConnectivityIssue && CurrentCancellationReason == CancellationReason.None)
                    {
                        // Use the centralized cancellation handling for connectivity issues
                        await HandleCancellationAsync(true); // Connectivity-related cancellation
                    }

                    string errorMessage = isConnectivityIssue
                        ? "Internet connection lost during installation. Please check your network connection and try again."
                        : ex.Message;

                    // Store the error message for later reference
                    app.LastOperationError = errorMessage;

                    StatusText = $"Error installing {app.Name}: {errorMessage}";

                    // Only show error dialog if it's not a connectivity issue (which is already handled by HandleCancellationAsync)
                    if (!isConnectivityIssue)
                    {
                        _dialogService.ShowInformationAsync(
                            "Installation Failed",
                            $"Failed to install {app.Name}.",
                            new[] { $"{app.Name}: {errorMessage}" },
                            "There was an error during installation. Please try again later."
                        );
                    }
                }
            }
            catch (System.Exception ex)
            {
                // This is a fallback catch-all to ensure the application doesn't crash
                // Cancel the connectivity check task
                cts.Cancel();

                // Wait for the connectivity check task to complete
                try
                {
                    await connectivityCheckTask;
                }
                catch (OperationCanceledException)
                { /* Expected when we cancel */
                }

                // Store the error message for later reference
                app.LastOperationError = ex.Message;

                StatusText = $"Error installing {app.Name}: {ex.Message}";

                // Show error dialog
                _dialogService.ShowInformationAsync(
                    "Installation Failed",
                    $"Failed to install {app.Name}.",
                    new[] { $"{app.Name}: {ex.Message}" },
                    "There was an error during installation. Please try again later."
                );
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Starts a periodic check for internet connectivity during installation.
        /// </summary>
        /// <param name="appName">The name of the app being installed</param>
        /// <param name="cts">Cancellation token source to cancel the task</param>
        /// <returns>A task that completes when the installation is done or cancelled</returns>
        private async Task StartPeriodicConnectivityCheck(
            string appName,
            System.Threading.CancellationTokenSource cts
        )
        {
            try
            {
                // Check connectivity every 5 seconds during installation
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(5000, cts.Token); // 5 seconds delay between checks

                    if (cts.Token.IsCancellationRequested)
                        break;

                    bool isConnected =
                        await _packageManager.SystemServices.IsInternetConnectedAsync(
                            false,
                            cts.Token
                        );

                    if (!isConnected)
                    {
                        // Only set connectivity loss if no other cancellation reason is set
                        if (CurrentCancellationReason == CancellationReason.None)
                        {
                            // Update status to inform user about connectivity issue
                            StatusText =
                                $"Error: Internet connection lost while installing {appName}. Installation stopped.";

                            // Show a non-blocking toast notification
                            if (_packageManager.NotificationService != null)
                            {
                                _packageManager.NotificationService.ShowToast(
                                    "Internet Connection Lost",
                                    "Internet connection has been lost during installation. Installation has been stopped.",
                                    ToastType.Error
                                );
                            }

                            // Use the centralized cancellation handling instead of showing dialog directly
                            await HandleCancellationAsync(true); // Connectivity-related cancellation
                        }

                        // Cancel the installation process
                        cts.Cancel();
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled, which is expected when installation completes or is stopped
            }
            catch (Exception ex)
            {
                // Don't disrupt the installation process
            }
        }

        [RelayCommand]
        public void ClearSelectedItems()
        {

            
            // Set all items to not selected
            foreach (var app in Items)
            {
                app.IsSelected = false;
            }

            foreach (var category in Categories)
            {
                foreach (var app in category.Apps)
                {
                    app.IsSelected = false;
                }
            }

            // If in table view mode, also clear selections in the table view collection
            if (IsTableViewMode && _collectionManager != null)
            {
                foreach (var wrapperItem in _collectionManager.Collection)
                {
                    wrapperItem.IsSelected = false;
                }
            }

            StatusText = "All selections cleared";

            // Explicitly notify that HasSelectedItems has changed
            InvalidateHasSelectedItemsCache();
            OnPropertyChanged(nameof(HasSelectedItems));
            

        }

        [RelayCommand]
        public async Task InstallApps()
        {
            if (_appInstallationService == null)
                return;



            // Get all selected apps regardless of installation status
            List<ExternalApp> selectedApps;
            if (IsTableViewMode && _collectionManager != null)
            {
                // In table view mode, get selected apps from the wrapper objects
                selectedApps = _collectionManager.Collection.Where(wrapper => wrapper.IsSelected)
                                      .Select(wrapper => Items.FirstOrDefault(item => item.PackageName == wrapper.PackageName))
                                      .Where(app => app != null)
                                      .ToList();

            }
            else
            {
                // In list view mode, get selected apps from the Items collection
                selectedApps = Items.Where(a => a.IsSelected).ToList();

            }

            // If no apps or WinGet items are selected, show a message
            if (!selectedApps.Any())
            {
                StatusText = "No apps selected for installation";
                await ShowNoItemsSelectedDialogAsync("installation");
                return;
            }

            // Check for internet connectivity before starting batch installation
            bool isInternetConnected =
                await _packageManager.SystemServices.IsInternetConnectedAsync(true);
            if (!isInternetConnected)
            {
                StatusText = "No internet connection available. Installation cannot proceed.";

                // Show dialog informing the user about the connectivity issue
                await ShowNoInternetConnectionDialogAsync();
                return;
            }

            // Prepare the list of items to install for confirmation dialog
            var itemsToInstall = new List<string>();
            int totalItemsCount = 0;

            // Add regular app names if any are selected
            if (selectedApps.Any())
            {
                itemsToInstall.AddRange(selectedApps.Select(a => a.Name));
                totalItemsCount += selectedApps.Count;
            }

            // Show confirmation dialog
            bool? dialogResult = await ShowConfirmItemsDialogAsync(
                "install",
                itemsToInstall,
                totalItemsCount
            );

            // If user didn't confirm, exit
            if (dialogResult != true)
            {
                return;
            }

            // Setup cancellation for the installation process
            using var cts = new System.Threading.CancellationTokenSource();

            // Start a background task to periodically check internet connectivity during installation
            var connectivityCheckTask = StartBatchConnectivityCheck(cts);

            // Use the ExecuteWithProgressAsync method from BaseViewModel to handle progress reporting
            await ExecuteWithProgressAsync(
                async (progress, cancellationToken) =>
                {
                    int successCount = 0;
                    int currentItem = 0;
                    int totalSelected = totalItemsCount;

                    // First install regular apps if any are selected
                    if (selectedApps.Any())
                    {
                        foreach (var app in selectedApps)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                await HandleCancellationAsync(false); // User-initiated cancellation
                                cts.Cancel(); // Ensure all tasks are cancelled
                                return successCount; // Exit the method immediately
                            }

                            try
                            {
                                var operationResult = await _appInstallationService.InstallAppAsync(
                                    app.ToAppInfo(),
                                    progress,
                                    cancellationToken
                                );

                                // Only mark as successful if the operation actually succeeded
                                if (operationResult.Success && operationResult.Result)
                                {
                                    app.IsInstalled = true;
                                    successCount++;

                                    progress.Report(
                                        new TaskProgressDetail
                                        {
                                            Progress = (currentItem * 100.0) / totalSelected,
                                            StatusText = $"Successfully installed {app.Name}",
                                            DetailedMessage =
                                                $"Successfully installed app: {app.Name}",
                                            LogLevel = LogLevel.Success,
                                            AdditionalInfo = new Dictionary<string, string>
                                            {
                                                { "AppName", app.Name },
                                                { "PackageName", app.PackageName },
                                                { "OperationType", "Install" },
                                                { "OperationStatus", "Success" },
                                                { "ItemNumber", currentItem.ToString() },
                                                { "TotalItems", totalSelected.ToString() },
                                            },
                                        }
                                    );
                                }
                                else
                                {
                                    string errorMessage =
                                        operationResult.ErrorMessage
                                        ?? $"Failed to install {app.Name}";

                                    // Check if this is a cancellation rather than a failure
                                    // Handle multiple cancellation message patterns from different services
                                    bool isCancellation = errorMessage != null && (
                                        errorMessage.Contains("cancelled by the user", StringComparison.OrdinalIgnoreCase) ||
                                        errorMessage.Contains("cancelled by user", StringComparison.OrdinalIgnoreCase) ||
                                        errorMessage.Contains("operation was canceled", StringComparison.OrdinalIgnoreCase) ||
                                        errorMessage.Contains("operation was cancelled", StringComparison.OrdinalIgnoreCase) ||
                                        errorMessage.Contains("script returned no result", StringComparison.OrdinalIgnoreCase)
                                    );
                                    
                                    if (isCancellation)
                                    {
                                        // Set cancellation reason to user cancelled
                                        CurrentCancellationReason = CancellationReason.UserCancelled;
                                        
                                        progress.Report(
                                            new TaskProgressDetail
                                            {
                                                Progress = (currentItem * 100.0) / totalSelected,
                                                StatusText = $"Installation of {app.Name} was cancelled",
                                                DetailedMessage = $"Installation of app {app.Name} was cancelled",
                                                LogLevel = LogLevel.Warning,
                                                AdditionalInfo = new Dictionary<string, string>
                                                {
                                                    { "AppName", app.Name },
                                                    { "PackageName", app.PackageName },
                                                    { "OperationType", "Install" },
                                                    { "OperationStatus", "Cancelled" },
                                                    { "ItemNumber", currentItem.ToString() },
                                                    { "TotalItems", totalSelected.ToString() },
                                                },
                                            }
                                        );
                                    }
                                    else
                                    {
                                        // Store the error message for later reference
                                        app.LastOperationError = errorMessage;

                                        progress.Report(
                                            new TaskProgressDetail
                                            {
                                                Progress = (currentItem * 100.0) / totalSelected,
                                                StatusText = $"Error installing {app.Name}",
                                                DetailedMessage = errorMessage,
                                                LogLevel = LogLevel.Error,
                                                AdditionalInfo = new Dictionary<string, string>
                                                {
                                                    { "AppName", app.Name },
                                                    { "PackageName", app.PackageName },
                                                    { "OperationType", "Install" },
                                                    { "OperationStatus", "Error" },
                                                    { "ErrorMessage", errorMessage },
                                                    { "ItemNumber", currentItem.ToString() },
                                                    { "TotalItems", totalSelected.ToString() },
                                                },
                                            }
                                        );
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                progress.Report(
                                    new TaskProgressDetail
                                    {
                                        Progress = (currentItem * 100.0) / totalSelected,
                                        StatusText = $"Installation of {app.Name} was cancelled",
                                        DetailedMessage =
                                            $"The installation of {app.Name} was cancelled.",
                                        LogLevel = LogLevel.Warning,
                                        AdditionalInfo = new Dictionary<string, string>
                                        {
                                            { "AppName", app.Name },
                                            { "PackageName", app.PackageName },
                                            { "OperationType", "Install" },
                                            { "OperationStatus", "Cancelled" },
                                            { "ItemNumber", currentItem.ToString() },
                                            { "TotalItems", totalSelected.ToString() },
                                        },
                                    }
                                );

                                // Use the centralized cancellation handling
                                await HandleCancellationAsync(false); // User-initiated cancellation
                                cts.Cancel();
                                return successCount; // Exit the method immediately
                            }
                            catch (System.Exception ex)
                            {
                                // Check if the exception is related to internet connectivity
                                bool isConnectivityIssue =
                                    ex.Message.Contains(
                                        "internet",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                    || ex.Message.Contains(
                                        "connection",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                    || ex.Message.Contains(
                                        "network",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                    || ex.Message.Contains(
                                        "pipeline has been stopped",
                                        StringComparison.OrdinalIgnoreCase
                                    );

                                if (
                                    isConnectivityIssue
                                    && CurrentCancellationReason == CancellationReason.None
                                )
                                {
                                    // Set the cancellation reason to connectivity issue
                                    await HandleCancellationAsync(true); // Connectivity-related cancellation
                                }

                                progress.Report(
                                    new TaskProgressDetail
                                    {
                                        Progress = (currentItem * 100.0) / totalSelected,
                                        StatusText = $"Error installing {app.Name}",
                                        DetailedMessage =
                                            $"Error installing {app.Name}: {ex.Message}",
                                        LogLevel = LogLevel.Error,
                                        AdditionalInfo = new Dictionary<string, string>
                                        {
                                            { "AppName", app.Name },
                                            { "PackageName", app.PackageName },
                                            { "OperationType", "Install" },
                                            { "OperationStatus", "Error" },
                                            { "ErrorMessage", ex.Message },
                                            { "ErrorType", ex.GetType().Name },
                                            { "ItemNumber", currentItem.ToString() },
                                            { "TotalItems", totalSelected.ToString() },
                                            {
                                                "IsConnectivityIssue",
                                                isConnectivityIssue.ToString()
                                            },
                                        },
                                    }
                                );
                            }
                        }
                    }

                    // Cancel the connectivity check task as installation is complete
                    cts.Cancel();

                    // Wait for the connectivity check task to complete
                    try
                    {
                        await connectivityCheckTask;
                    }
                    catch (OperationCanceledException)
                    { /* Expected when we cancel */
                    }

                    // Only proceed with normal completion reporting if not cancelled
                    // Final report
                    // Check if any failures were due to internet connectivity issues
                    bool hasInternetIssues = selectedApps.Any(a =>
                        !a.IsInstalled
                        && (
                            a.LastOperationError?.Contains("Internet connection") == true
                            || a.LastOperationError?.Contains("No internet") == true
                        )
                    );

                    string statusText =
                        successCount == totalSelected
                            ? $"Successfully installed {successCount} of {totalSelected} apps"
                        : hasInternetIssues ? $"Installation incomplete: Internet connection issues"
                        : $"Installation incomplete: {successCount} of {totalSelected} apps installed";

                    string detailedMessage =
                        successCount == totalSelected
                            ? $"Task completed: {successCount} of {totalSelected} apps installed successfully"
                        : hasInternetIssues
                            ? $"Task not completed: {successCount} of {totalSelected} apps installed. Installation failed due to internet connection issues"
                        : $"Task completed: {successCount} of {totalSelected} apps installed successfully";

                    progress.Report(
                        new TaskProgressDetail
                        {
                            Progress = 100,
                            StatusText = statusText,
                            DetailedMessage = detailedMessage,
                            LogLevel =
                                successCount == totalSelected ? LogLevel.Success : LogLevel.Warning,
                            AdditionalInfo = new Dictionary<string, string>
                            {
                                { "OperationType", "Install" },
                                {
                                    "OperationStatus",
                                    successCount == totalSelected ? "Complete" : "PartialSuccess"
                                },
                                { "SuccessCount", successCount.ToString() },
                                { "TotalItems", totalSelected.ToString() },
                                { "SuccessRate", $"{(successCount * 100.0 / totalSelected):F1}%" },
                                { "CompletionTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                            },
                        }
                    );

                    // For normal completion (not cancelled), collect success and failure information
                    var successItems = new List<string>();
                    var failedItems = new List<string>();

                    // Add successful items to the list
                    foreach (var app in selectedApps.Where(a => a.IsInstalled))
                    {
                        successItems.Add(app.Name);
                    }

                    // Add failed items to the list
                    foreach (var app in selectedApps.Where(a => !a.IsInstalled))
                    {
                        failedItems.Add(app.Name);
                    }

                    // Check if any failures are due to internet connectivity issues
                    bool hasConnectivityIssues = hasInternetIssues;
                    bool isFailure = successCount < totalSelected;

                    // Important: Check if the operation was cancelled by the user
                    // This ensures we show the correct dialog even if the cancellation happened in a different part of the code
                    if (
                        failedItems != null
                        && failedItems.Any(item =>
                            item.Contains("cancelled by user", StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        // Set the cancellation reason to UserCancelled if it's not already set
                        if (CurrentCancellationReason == CancellationReason.None)
                        {
                            CurrentCancellationReason = CancellationReason.UserCancelled;
                        }
                    }

                    // Show result dialog using the base class method which handles cancellation scenarios properly
                    ShowOperationResultDialog(
                        "Install",
                        successCount,
                        totalSelected,
                        successItems,
                        failedItems,
                        null // no skipped items
                    );

                    return successCount;
                },
                $"Installing {selectedApps.Count} apps",
                false
            );
        }

        /// <summary>
        /// Starts a periodic check for internet connectivity during batch installation.
        /// </summary>
        /// <param name="cts">Cancellation token source to cancel the task</param>
        /// <returns>A task that completes when the installation is done or cancelled</returns>
        private async Task StartBatchConnectivityCheck(System.Threading.CancellationTokenSource cts)
        {
            try
            {
                // Check connectivity every 15 seconds during batch installation (reduced frequency)
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(15000, cts.Token); // 15 seconds delay between checks (increased from 5)

                    // If cancellation was already requested (e.g., by the user), don't proceed with connectivity check
                    if (cts.Token.IsCancellationRequested)
                    {
                        // Important: Do NOT set cancellation reason here, as it might overwrite UserCancelled
                        break;
                    }

                    // Check if user cancellation has already been set
                    if (CurrentCancellationReason == CancellationReason.UserCancelled)
                    {
                        // User already cancelled, don't change the reason
                        break;
                    }

                    bool isConnected =
                        await _packageManager.SystemServices.IsInternetConnectedAsync(
                            false,
                            cts.Token
                        );

                    if (!isConnected)
                    {
                        // Only set connectivity loss if no other cancellation reason is set
                        if (CurrentCancellationReason == CancellationReason.None)
                        {
                            // Update status to inform user about connectivity issue
                            StatusText =
                                "Error: Internet connection lost during installation. Installation stopped.";

                            // Show a non-blocking toast notification
                            if (_packageManager.NotificationService != null)
                            {
                                _packageManager.NotificationService.ShowToast(
                                    "Internet Connection Lost",
                                    "Internet connection has been lost during installation. Installation has been stopped.",
                                    ToastType.Error
                                );
                            }

                            // Use the centralized cancellation handling
                            await HandleCancellationAsync(true); // Connectivity-related cancellation
                        }

                        cts.Cancel();
                        return; // Exit the method immediately
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled, which is expected when installation completes or is stopped
                // Do NOT set cancellation reason here, as it might have been set by the main task
            }
            catch (Exception ex)
            {
                // Don't disrupt the installation process
            }
        }

        public async Task LoadAppsAndCheckInstallationStatusAsync()
        {
            
            if (IsInitialized)
            {
                return;
            }

            await LoadItemsAsync();
            
            await CheckInstallationStatusAsync();

            // Mark as initialized after loading is complete
            IsInitialized = true;
        }

        public override async void OnNavigatedTo(object parameter)
        {
            try
            {
                // Only load data if not already initialized
                if (!IsInitialized)
                {
                    await LoadAppsAndCheckInstallationStatusAsync();
                }
            }
            catch (System.Exception ex)
            {
                // Handle any exceptions
                StatusText = $"Error loading apps: {ex.Message}";
                IsLoading = false;
            }
        }

        #region BaseInstallationViewModel Abstract Method Implementations

        /// <summary>
        /// Gets the name of an external app.
        /// </summary>
        /// <param name="app">The external app.</param>
        /// <returns>The name of the app.</returns>
        protected string GetAppName(ExternalApp app)
        {
            return app.Name;
        }

        /// <summary>
        /// Converts an external app to an AppInfo object.
        /// </summary>
        /// <param name="app">The external app to convert.</param>
        /// <returns>The AppInfo object.</returns>
        protected AppInfo ToAppInfo(ExternalApp app)
        {
            return app.ToAppInfo();
        }

        /// <summary>
        /// Gets the selected external apps.
        /// </summary>
        /// <returns>The selected external apps.</returns>
        protected IEnumerable<ExternalApp> GetSelectedApps()
        {
            return Items.Where(a => a.IsSelected);
        }

        /// <summary>
        /// Sets the installation status of an external app.
        /// </summary>
        /// <param name="app">The external app.</param>
        /// <param name="isInstalled">Whether the app is installed.</param>
        protected void SetInstallationStatus(ExternalApp app, bool isInstalled)
        {
            app.IsInstalled = isInstalled;
        }

        #endregion

        #region Helper Methods Previously Inherited from BaseInstallationViewModel

        /// <summary>
        /// Gets a string representing the past tense of an operation type.
        /// </summary>
        /// <param name="operationType">The operation type (e.g. Install, Remove).</param>
        /// <returns>The past tense of the operation.</returns>
        private string GetPastTense(string operationType)
        {
            return operationType.ToLowerInvariant() switch
            {
                "install" => "installed",
                "remove" => "removed",
                _ => $"{operationType.ToLowerInvariant()}ed"
            };
        }

        /// <summary>
        /// Shows a dialog when no items are selected for an operation.
        /// </summary>
        /// <param name="operationType">The type of operation (e.g., "install" or "uninstall").</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private Task ShowNoItemsSelectedDialogAsync(string operationType)
        {
            return _dialogService.ShowInformationAsync(
                $"No items selected",
                $"Please select at least one item to {operationType}."
            );
        }

        /// <summary>
        /// Shows a dialog when no internet connection is available.
        /// </summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private Task ShowNoInternetConnectionDialogAsync()
        {
            return _dialogService.ShowInformationAsync(
                "No Internet Connection",
                "Internet connection is required for this operation. Please check your connection and try again."
            );
        }

        /// <summary>
        /// Shows a confirmation dialog for items to be processed.
        /// </summary>
        /// <param name="operationType">The type of operation (Install/Remove).</param>
        /// <param name="selectedItems">The selected items.</param>
        /// <param name="totalCount">Total number of items.</param>
        /// <returns>True if confirmed, otherwise false.</returns>
        private async Task<bool> ShowConfirmItemsDialogAsync(string operationType, List<string> selectedItems, int totalCount)
        {
            string title = $"Confirm {operationType}";
            string headerText = $"The following items will be {GetPastTense(operationType)}:";

            // Create footer text
            string footerText = "Do you want to continue?";

            // Build the message
            string message = $"{headerText}\n";
            foreach (var name in selectedItems)
            {
                message += $"{name}\n";
            }
            message += $"\n{footerText}";

            // Show the confirmation dialog
            return await _dialogService.ShowConfirmationAsync(message, title);
        }
        
        /// <summary>
        /// Shows a confirmation dialog for items to be processed.
        /// </summary>
        /// <param name="operationType">The type of operation (Install/Remove).</param>
        /// <param name="selectedItems">The selected items.</param>
        /// <param name="skippedItems">Items that will be skipped (optional).</param>
        /// <returns>True if confirmed, otherwise false.</returns>
        private async Task<bool> ShowConfirmItemsDialogAsync(string operationType, IEnumerable<ExternalApp> selectedItems, IEnumerable<ExternalApp>? skippedItems = null)
        {
            string title = $"Confirm {operationType}";
            string headerText = $"The following items will be {GetPastTense(operationType)}:";

            // Create list of app names for the dialog
            var appNames = selectedItems.Select(a => GetAppName(a)).ToList();

            // Create footer text
            string footerText = "Do you want to continue?";

            // If there are skipped apps, add information about them
            if (skippedItems != null && skippedItems.Any())
            {
                var skippedNames = skippedItems.Select(a => GetAppName(a)).ToList();
                footerText =
                    $"Note: The following {skippedItems.Count()} item(s) cannot be {GetPastTense(operationType)} and will be skipped:\n";
                footerText += string.Join(", ", skippedNames);
                footerText +=
                    $"\n\nDo you want to continue with the remaining {selectedItems.Count()} item(s)?";
            }

            // Build the message
            string message = $"{headerText}\n";
            foreach (var name in appNames)
            {
                message += $"{name}\n";
            }
            message += $"\n{footerText}";

            // Show the confirmation dialog
            return await _dialogService.ShowConfirmationAsync(message, title);
        }
        
        /// <summary>
        /// Handles the cancellation process.
        /// </summary>
        /// <param name="isConnectivityIssue">True if the cancellation was due to connectivity issues, false if user-initiated.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task HandleCancellationAsync(bool isConnectivityIssue)
        {
            // Set the appropriate cancellation reason
            CurrentCancellationReason = isConnectivityIssue
                ? CancellationReason.InternetConnectivityLost
                : CancellationReason.UserCancelled;

            // Show the appropriate dialog
            await ShowCancellationDialogAsync(!isConnectivityIssue, isConnectivityIssue);

            // Reset cancellation reason after showing dialog
            CurrentCancellationReason = CancellationReason.None;
        }

        /// <summary>
        /// Shows a cancellation dialog.
        /// </summary>
        /// <param name="isUserInitiated">Whether the cancellation was initiated by the user.</param>
        /// <param name="isConnectivityIssue">Whether the cancellation was due to connectivity issues.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private async Task ShowCancellationDialogAsync(bool isUserInitiated, bool isConnectivityIssue)
        {
            if (isUserInitiated)
            {
                await _dialogService.ShowInformationAsync(
                    "Installation Cancelled",
                    "The installation process was cancelled by the user."
                );
            }
            else if (isConnectivityIssue)
            {
                await _dialogService.ShowInformationAsync(
                    "Installation Stopped",
                    "The installation process was stopped due to connectivity issues. Please check your internet connection and try again."
                );
            }
        }

        /// <summary>
        /// Shows an operation result dialog after operations complete.
        /// </summary>
        /// <param name="operationType">Type of operation (Install/Remove)</param>
        /// <param name="successCount">Number of successful operations</param>
        /// <param name="totalCount">Total number of operations attempted</param>
        /// <param name="successItems">List of successfully processed items</param>
        /// <param name="failedItems">List of failed items (optional)</param>
        /// <param name="skippedItems">List of skipped items (optional)</param>
        private void ShowOperationResultDialog(
            string operationType,
            int successCount,
            int totalCount,
            IEnumerable<string> successItems,
            IEnumerable<string>? failedItems = null,
            IEnumerable<string>? skippedItems = null
        )
        {
            // Determine if this was a user-initiated cancellation or connectivity issue
            bool isUserCancelled = CurrentCancellationReason == CancellationReason.UserCancelled;
            bool isConnectivityIssue =
                CurrentCancellationReason == CancellationReason.InternetConnectivityLost;

            // If the operation was cancelled by the user, use CustomDialog for a simpler message
            if (isUserCancelled)
            {
                string title = "Installation Aborted by User";
                string headerText = "Installation aborted by user";
                string message = "The installation process was cancelled by the user.";
                string footerText =
                    successCount > 0
                        ? $"Some items were successfully {GetPastTense(operationType)} before cancellation."
                        : $"No items were {GetPastTense(operationType)} before cancellation.";

                // Use CustomDialog directly instead of SoftwareAppsDialog
                Winhance.WPF.Features.Common.Views.CustomDialog.ShowInformation(title, headerText, message, footerText);

                // Reset cancellation reason after showing dialog
                CurrentCancellationReason = CancellationReason.None;
                return;
            }
            else if (isConnectivityIssue)
            {
                // Use the dialog service with the connectivity issue flag
                _dialogService.ShowOperationResult(
                    operationType,
                    successCount,
                    totalCount,
                    successItems,
                    failedItems,
                    skippedItems,
                    true, // Connectivity issue flag
                    false // Not a user cancellation
                );

                // Reset cancellation reason after showing dialog
                CurrentCancellationReason = CancellationReason.None;
                return;
            }

            // For normal completion (no cancellation), show standard result dialog
            _dialogService.ShowOperationResult(
                operationType,
                successCount,
                totalCount,
                successItems,
                failedItems,
                skippedItems,
                false, // Not a connectivity issue
                false  // Not a user cancellation
            );
        }

        #endregion
        
        #region IDisposable
        
        /// <summary>
        /// Disposes of optimized services and cleans up resources
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose optimized services
                _collectionManager?.Dispose();
                _debouncedSearchService?.Dispose();
                
                // Unsubscribe from view mode manager events
                if (_viewModeManager != null)
                {
                    _viewModeManager.ViewModeChanged -= OnViewModeChanged;
                }
            }
            
            base.Dispose(disposing);
        }
        
        #endregion
    }
}
