using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.UI.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.SoftwareApps.Models;
using ToastType = Winhance.Core.Features.UI.Interfaces.ToastType;
using System.Windows.Input;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// Wrapper class for items in the combined table view that adds a Type property
    /// </summary>
    public class ItemWithType : INotifyPropertyChanged
    {        
        private readonly WindowsApp _item;
        private readonly Action _selectionChangedCallback;
        
        public ItemWithType(WindowsApp item, string itemType, Action selectionChangedCallback = null)
        {
            _item = item;
            _selectionChangedCallback = selectionChangedCallback;
            ItemType = itemType;
            
            // Set TypeOrder based on the item type for custom sorting
            switch (itemType)
            {
                case "Windows App":
                    TypeOrder = 1; // Windows Apps first
                    break;
                case "Capability":
                    TypeOrder = 2; // Capabilities second
                    break;
                case "Optional Feature":
                    TypeOrder = 3; // Optional Features last
                    break;
                default:
                    TypeOrder = 99; // Unknown types at the end
                    break;
            }
            
            // Forward property change events from the wrapped item
            // Skip properties that are handled locally to prevent double notifications
            if (_item is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged += (sender, args) =>
                {
                    // Don't forward property changes for properties we handle locally
                    if (args.PropertyName != nameof(IsSelected))
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(args.PropertyName));
                    }
                };
            }
        }
        
        public string Name => _item.Name;
        public string Description => _item.Description;
        public bool IsInstalled => _item.IsInstalled;
        public bool CanBeReinstalled => _item.CanBeReinstalled;
        public string ItemType { get; }
        
        // Property for custom type ordering
        public int TypeOrder { get; }
        
        public bool IsSelected
        {
            get => _item.IsSelected;
            set
            {
                if (_item.IsSelected != value)
                {
                    _item.IsSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                    
                    // Notify ViewModel that selection has changed - this updates the button states
                    _selectionChangedCallback?.Invoke();
                    
                    // Log the selection change for debugging
                    DebugLogger.Log($"[DEBUG] ItemWithType: Selection changed for {Name} to {value}");
                }
            }
        }
        
        /// <summary>
        /// Public method to notify that IsSelected property has changed (for ViewModel use)
        /// </summary>
        public void NotifyIsSelectedChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
    }
    
    public partial class WindowsAppsViewModel : BaseViewModel
    {
        /// <summary>
        /// Event raised when selected items change to notify parent view models
        /// </summary>
        public event EventHandler SelectedItemsChanged;

        /// <summary>
        /// Gets or sets a value indicating whether the view model is initialized.
        /// </summary>
        [ObservableProperty]
        private bool _isInitialized = false;

        /// <summary>
        /// Gets or sets a value indicating whether the view model is updating button states.
        /// </summary>
        [ObservableProperty]
        private bool _isUpdatingButtonStates = false;
        
        /// <summary>
        /// Gets or sets the search text for filtering items.
        /// </summary>
        [ObservableProperty]
        private string _searchText = string.Empty;

        // IsAllSelected is already defined elsewhere in the class

        /// <summary>
        /// Gets the collection of all items.
        /// </summary>
        public ObservableCollection<WindowsApp> Items { get; } = new();
        private readonly ITaskProgressService _progressService;
        private readonly IAppInstallationService _appInstallationService;
        private readonly ICapabilityInstallationService _capabilityService;
        private readonly IFeatureInstallationService _featureService;
        private readonly IFeatureRemovalService _featureRemovalService;
        private readonly IAppService _appDiscoveryService;
        private readonly IConfigurationService _configurationService;
        private readonly IScriptDetectionService _scriptDetectionService;
        private readonly IInternetConnectivityService _connectivityService;
        private readonly IAppInstallationCoordinatorService _appInstallationCoordinatorService;
        private readonly IBloatRemovalCoordinatorService _bloatRemovalCoordinatorService;
        private readonly IPackageManager _packageManager;
        private readonly AppViewModeManager _viewModeManager;
        private readonly Services.SoftwareAppsDialogService _dialogService;
        
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
            set => _viewModeManager.IsTableViewMode = value;
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
        private string _statusText = "Ready";

        [ObservableProperty]
        private bool _isRemovingApps;

        [ObservableProperty]
        private ObservableCollection<ScriptInfo> _activeScripts = new();

        // Flag to prevent duplicate initialization
        // IsTableViewMode is now inherited from BaseAppViewModel
        
        [ObservableProperty]
        private bool _isAllSelectedOptionalFeatures;
        
        // Combined collection for table view
        private ObservableCollection<ItemWithType> _allItems = new();
        public ICollectionView AllItemsView { get; private set; }
        
        // Sorting properties
        [ObservableProperty]
        private string _currentSortProperty;
        
        [ObservableProperty]
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;
        
        // HasSelectedItems and related fields/methods are defined elsewhere in the file
        
        // Command to toggle between views
        [RelayCommand]
        private void ToggleViewMode(object parameter = null)
        {
            // If a parameter is provided, use it to set the view mode directly
            if (parameter != null)
            {
                // Handle both bool and string parameters
                if (parameter is bool tableViewMode)
                {
                    IsTableViewMode = tableViewMode;
                }
                else if (parameter is string stringParam)
                {
                    // Parse string parameter ("True" or "False")
                    if (bool.TryParse(stringParam, out bool result))
                    {
                        IsTableViewMode = result;
                    }
                }
            }
            // Otherwise toggle the current mode
            else
            {
                IsTableViewMode = !IsTableViewMode;
            }
            
            OnPropertyChanged(nameof(GridViewVisibility));
            OnPropertyChanged(nameof(TableViewVisibility));
            
            if (IsTableViewMode)
            {
                UpdateAllItemsCollection();
            }
        }
        
        /// <summary>
        /// Command to explicitly update the AllItems collection for table view
        /// </summary>
        [RelayCommand]
        public void UpdateAllItemsCollectionExplicit()
        {
            UpdateAllItemsCollection();
        }
        
        /// <summary>
        /// Updates the AllItems collection with all items from WindowsApps, Capabilities, and OptionalFeatures
        /// Uses batch processing for better performance
        /// </summary>
        public void UpdateAllItemsCollection()
        {
            // Use batch operations to minimize UI updates
            using (var deferRefresh = new DeferRefresh(AllItemsView))
            {
                _allItems.Clear();
                
                // Pre-allocate capacity for better performance
                int totalItemCount = WindowsApps.Count + Capabilities.Count + OptionalFeatures.Count;
                var newItems = new List<ItemWithType>(totalItemCount);
                
                // Prepare all items before adding to collection
                // Add Windows Apps
                foreach (var app in WindowsApps)
                {
                    newItems.Add(new ItemWithType(app, "Windows App", OnTableViewSelectionChanged));
                }
                
                // Add Capabilities
                foreach (var capability in Capabilities)
                {
                    newItems.Add(new ItemWithType(capability, "Capability", OnTableViewSelectionChanged));
                }
                
                // Add Optional Features
                foreach (var feature in OptionalFeatures)
                {
                    newItems.Add(new ItemWithType(feature, "Optional Feature", OnTableViewSelectionChanged));
                }
                
                // Batch add all items to minimize UI updates
                foreach (var item in newItems)
                {
                    _allItems.Add(item);
                }
                
                // Apply sorting to the refreshed collection
                ApplySorting();
                
                // Apply search filter if there's search text
                ApplyTableViewFilter();
            }
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
        /// Sorts the AllItemsView based on the current sort property and direction
        /// </summary>
        private void ApplySorting()
        {
            if (AllItemsView != null && !string.IsNullOrEmpty(CurrentSortProperty))
            {
                AllItemsView.SortDescriptions.Clear();
                
                // Add primary sort description based on current sort property
                AllItemsView.SortDescriptions.Add(new SortDescription(CurrentSortProperty, SortDirection));
                
                // Add secondary sort descriptions based on the primary sort property
                if (CurrentSortProperty == "IsInstalled")
                {
                    // If sorting by installation status, add secondary sort by TypeOrder
                    AllItemsView.SortDescriptions.Add(new SortDescription("TypeOrder", ListSortDirection.Ascending));
                    
                    // Add tertiary sort by Name for consistent ordering within each type
                    AllItemsView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                }
                else if (CurrentSortProperty == "ItemType" || CurrentSortProperty == "TypeOrder")
                {
                    // If sorting by type, add secondary sort by installation status
                    AllItemsView.SortDescriptions.Add(new SortDescription("IsInstalled", ListSortDirection.Descending));
                    
                    // Add tertiary sort by Name
                    AllItemsView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                }
                else
                {
                    // For other primary sorts, add secondary sort by installation status
                    AllItemsView.SortDescriptions.Add(new SortDescription("IsInstalled", ListSortDirection.Descending));
                    
                    // Add tertiary sort by TypeOrder
                    AllItemsView.SortDescriptions.Add(new SortDescription("TypeOrder", ListSortDirection.Ascending));
                }
            }
        }
        
        /// <summary>
        /// Handles sorting when a column header is clicked
        /// </summary>
        [RelayCommand]
        private void SortBy(string propertyName)
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
            
            // Apply the sorting
            ApplySorting();
        }
        
        /// <summary>
        /// Sets up collection change notifications for the individual collections
        /// </summary>
        private void SetupCollectionChangeHandlers()
        {
            // Monitor changes to the individual collections
            WindowsApps.CollectionChanged += OnCollectionChanged;
            Capabilities.CollectionChanged += OnCollectionChanged;
            OptionalFeatures.CollectionChanged += OnCollectionChanged;
        }
        
        /// <summary>
        /// Handles collection changes and updates the AllItems collection if in table view mode
        /// </summary>
        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsTableViewMode)
            {
                // Refresh the entire collection for simplicity
                UpdateAllItemsCollection();
            }
        }

        /// <summary>
        /// Handles view mode changes from AppViewModeManager
        /// </summary>
        private void OnViewModeChanged(bool isTableViewMode)
        {
            // When switching to table view, update the combined collection
            if (isTableViewMode)
            {
                UpdateAllItemsCollection();
            }
        }

        // For binding in the WindowsAppsView - filtered collections
        // Standard Windows Apps (Appx packages)
        public System.Collections.ObjectModel.ObservableCollection<WindowsApp> WindowsApps { get; } =
            new();

        // Windows Capabilities
        public System.Collections.ObjectModel.ObservableCollection<WindowsApp> Capabilities { get; } =
            new();

        // Optional Features
        public System.Collections.ObjectModel.ObservableCollection<WindowsApp> OptionalFeatures { get; } =
            new();

        public WindowsAppsViewModel(
            ITaskProgressService progressService,
            ISearchService searchService,
            IPackageManager packageManager,
            IAppInstallationService appInstallationService,
            ICapabilityInstallationService capabilityService,
            IFeatureInstallationService featureService,
            IFeatureRemovalService featureRemovalService,
            IConfigurationService configurationService,
            IScriptDetectionService scriptDetectionService,
            IInternetConnectivityService connectivityService,
            IAppInstallationCoordinatorService appInstallationCoordinatorService,
            IBloatRemovalCoordinatorService bloatRemovalCoordinatorService,
            Services.SoftwareAppsDialogService dialogService)
            : base(progressService)
        {
            // Initialize injected services
            _progressService = progressService;
            _dialogService = dialogService;
            
            // Initialize the AllItemsView
            AllItemsView = CollectionViewSource.GetDefaultView(_allItems);
            
            // Set default sort property to show installed items first
            CurrentSortProperty = "IsInstalled";
            SortDirection = ListSortDirection.Descending;
            ApplySorting();
            
            // Set up collection change handlers
            WindowsApps.CollectionChanged += OnCollectionChanged;
            Capabilities.CollectionChanged += OnCollectionChanged;
            OptionalFeatures.CollectionChanged += OnCollectionChanged;
            
            // Note: Wrapper objects now use direct callbacks instead of static events
            
            _appInstallationService = appInstallationService;
            _capabilityService = capabilityService;
            _featureService = featureService;
            _featureRemovalService = featureRemovalService;
            _appDiscoveryService = packageManager?.AppDiscoveryService;
            _configurationService = configurationService;
            _scriptDetectionService = scriptDetectionService;
            _connectivityService = connectivityService;
            _appInstallationCoordinatorService = appInstallationCoordinatorService;
            _bloatRemovalCoordinatorService = bloatRemovalCoordinatorService;
            _packageManager = packageManager;
            
            // Initialize view mode manager
            _viewModeManager = new AppViewModeManager();
            _viewModeManager.ViewModeChanged += OnViewModeChanged;
        }

        /// <summary>
        /// Handles selection changes from TableView wrapper objects
        /// </summary>
        private void OnTableViewSelectionChanged()
        {
            DebugLogger.Log("[DEBUG] ========== WindowsAppsViewModel.OnTableViewSelectionChanged START ==========");
            DebugLogger.Log($"[DEBUG] WindowsAppsViewModel.OnTableViewSelectionChanged called - Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            DebugLogger.Log($"[DEBUG] Cache valid before invalidation: {_hasSelectedItemsCacheValid}, cached value: {_hasSelectedItems}");
            
            // Log selections in the _allItems collection
            if (_allItems != null)
            {
                var selectedItems = _allItems.Where(a => a.IsSelected).ToList();
                DebugLogger.Log($"[DEBUG] Table view selected items count: {selectedItems.Count} out of {_allItems.Count}");
                foreach (var item in selectedItems.Take(5))
                {
                    DebugLogger.Log($"[DEBUG] Selected table item: {item.Name}");
                }
            }
            
            // Force IsTableViewMode to be true since we're getting selection changes from table view
            if (!IsTableViewMode && _allItems != null && _allItems.Any())
            {
                DebugLogger.Log($"[DEBUG] Forcing IsTableViewMode to true because we received table selection changes");
                IsTableViewMode = true;
            }
            
            InvalidateHasSelectedItemsCache();
            DebugLogger.Log($"[DEBUG] Cache invalidated - Cache valid: {_hasSelectedItemsCacheValid}");
            
            var hasSelected = HasSelectedItems;
            DebugLogger.Log($"[DEBUG] HasSelectedItems evaluated to: {hasSelected}");
            
            DebugLogger.Log($"[DEBUG] Calling OnPropertyChanged for HasSelectedItems");
            OnPropertyChanged(nameof(HasSelectedItems));
            
            // Ensure the parent view model is notified of changes by forcing an update to the property
            // This is crucial for ensuring the buttons in the parent view model are enabled/disabled properly
            Application.Current.Dispatcher.BeginInvoke(new Action(() => 
            {
                DebugLogger.Log("[DEBUG] Dispatching additional property change notification for HasSelectedItems");
                OnPropertyChanged(nameof(HasSelectedItems));
            }));
            
            DebugLogger.Log($"[DEBUG] OnPropertyChanged completed");
            DebugLogger.Log("[DEBUG] ========== WindowsAppsViewModel.OnTableViewSelectionChanged END ==========");
        }

        // SaveConfig and ImportConfig methods removed as part of unified configuration cleanup

        /// <summary>
        /// Applies the current search text to filter items.
        /// </summary>
        protected void ApplySearch()
        {
            if (Items == null || Items.Count == 0)
                return;

            // Clear all collections
            WindowsApps.Clear();
            Capabilities.Clear();
            OptionalFeatures.Clear();

            // Filter items based on search text
            var filteredItems = FilterItems(Items);

            // Add filtered items to their respective collections
            foreach (var app in filteredItems)
            {
                switch (app.AppType)
                {
                    case Models.WindowsAppType.StandardApp:
                        WindowsApps.Add(app);
                        break;
                    case Models.WindowsAppType.Capability:
                        Capabilities.Add(app);
                        break;
                    case Models.WindowsAppType.OptionalFeature:
                        OptionalFeatures.Add(app);
                        break;
                }
            }

            // Sort the filtered collections
            SortCollections();
        }
        
        /// <summary>
        /// Filters items based on the current search text.
        /// </summary>
        /// <param name="items">The items to filter.</param>
        /// <returns>The filtered items.</returns>
        protected IEnumerable<WindowsApp> FilterItems(IEnumerable<WindowsApp> items)
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
                    item.PackageName?.ToLower().Contains(term) == true);
            });
        }
        
        /// <summary>
        /// Applies the current search text to filter items in the table view.
        /// </summary>
        private void ApplyTableViewFilter()
        {
            if (AllItemsView == null)
                return;
                
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // Clear filter if no search text
                AllItemsView.Filter = null;
            }
            else
            {
                // Apply filter based on search text
                var searchTerms = SearchText.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                AllItemsView.Filter = obj =>
                {
                    if (obj is ItemWithType itemWrapper)
                    {
                        // Check if all search terms match any of the searchable properties
                        return searchTerms.All(term =>
                            itemWrapper.Name?.ToLower().Contains(term) == true ||
                            itemWrapper.Description?.ToLower().Contains(term) == true);
                    }
                    return false;
                };
            }
        }
        
        /// <summary>
        /// Handles the SearchText property change to trigger search functionality.
        /// </summary>
        /// <param name="value">The new search text value.</param>
        partial void OnSearchTextChanged(string value)
        {
            // Apply search for list view (individual collections)
            ApplySearch();
            
            // Apply search for table view (AllItemsView)
            ApplyTableViewFilter();
            
            // Notify that IsSearchActive may have changed
            OnPropertyChanged(nameof(IsSearchActive));
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether search is active.
        /// </summary>
        public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);

        /// <summary>
        /// Refreshes the script status information.
        /// </summary>
        public void RefreshScriptStatus()
        {
            if (_scriptDetectionService == null)
                return;

            IsRemovingApps = _scriptDetectionService.AreRemovalScriptsPresent();

            ActiveScripts.Clear();
            foreach (var script in _scriptDetectionService.GetActiveScripts())
            {
                ActiveScripts.Add(script);
            }
        }

        public async Task LoadItemsAsync()
        {
            if (_appDiscoveryService == null)
                return;

            IsLoading = true;
            StatusText = "Loading Windows apps...";

            try
            {
                // Clear all collections
                Items.Clear();
                WindowsApps.Clear();
                Capabilities.Clear();
                OptionalFeatures.Clear();

                // Unsubscribe from existing items' property changed events
                UnsubscribeFromItemPropertyChangedEvents();

                // Load standard Windows apps (Appx packages)
                var apps = await _appDiscoveryService.GetStandardAppsAsync();

                // Load capabilities
                var capabilities = await _appDiscoveryService.GetCapabilitiesAsync();

                // Load optional features
                var features = await _appDiscoveryService.GetOptionalFeaturesAsync();

                // Convert all to WindowsApp objects and add to the main collection
                foreach (var app in apps)
                {
                    var windowsApp = WindowsApp.FromAppInfo(app);
                    // AppType is already set in FromAppInfo
                    Items.Add(windowsApp);
                    WindowsApps.Add(windowsApp);
                    // Subscribe to property changed events
                    windowsApp.PropertyChanged += Item_PropertyChanged;
                }

                foreach (var capability in capabilities)
                {
                    var windowsApp = WindowsApp.FromCapabilityInfo(capability);
                    // AppType is already set in FromCapabilityInfo
                    Items.Add(windowsApp);
                    Capabilities.Add(windowsApp);
                    // Subscribe to property changed events
                    windowsApp.PropertyChanged += Item_PropertyChanged;
                }

                foreach (var feature in features)
                {
                    var windowsApp = WindowsApp.FromFeatureInfo(feature);
                    // AppType is already set in FromFeatureInfo
                    Items.Add(windowsApp);
                    OptionalFeatures.Add(windowsApp);
                    // Subscribe to property changed events
                    windowsApp.PropertyChanged += Item_PropertyChanged;
                }

                StatusText =
                    $"Loaded {WindowsApps.Count} apps, {Capabilities.Count} capabilities, and {OptionalFeatures.Count} features";
            }
            catch (System.Exception ex)
            {
                StatusText = $"Error loading Windows apps: {ex.Message}";
            }
            finally
            {
                IsLoading = false;

                // Check script status
                RefreshScriptStatus();
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

                // Sort all collections after we have the installation status
                SortCollections();

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

        private void SortCollections()
        {
            // Sort apps within each collection by installed status (installed first) then alphabetically
            SortCollection(WindowsApps);
            SortCollection(Capabilities);
            SortCollection(OptionalFeatures);
        }

        private void SortCollection(
            System.Collections.ObjectModel.ObservableCollection<WindowsApp> collection
        )
        {
            var sorted = collection
                .OrderByDescending(app => app.IsInstalled) // Installed first
                .ThenBy(app => app.Name) // Then alphabetically
                .ToList();

            collection.Clear();
            foreach (var app in sorted)
            {
                collection.Add(app);
            }
        }

        #region Dialog Helper Methods

        /// <summary>
        /// Gets the past tense form of an operation type
        /// </summary>
        /// <param name="operationType">The operation type (e.g., "Install", "Remove")</param>
        /// <returns>The past tense form of the operation type</returns>
        private string GetPastTense(string operationType)
        {
            return operationType.ToLower() switch
            {
                "install" => "installed",
                "remove" => "removed",
                "uninstall" => "uninstalled",
                "reinstall" => "reinstalled",
                "enable" => "enabled",
                "disable" => "disabled",
                _ => $"{operationType}ed"
            };
        }

        /// <summary>
        /// Shows a confirmation dialog before performing operations.
        /// </summary>
        /// <param name="operationType">Type of operation (Install/Remove)</param>
        /// <param name="selectedApps">List of apps selected for the operation</param>
        /// <param name="skippedApps">List of apps that will be skipped (optional)</param>
        /// <returns>Dialog result (true if confirmed, false if canceled)</returns>
        private bool? ShowOperationConfirmationDialog(
            string operationType,
            IEnumerable<WindowsApp> selectedApps,
            IEnumerable<WindowsApp>? skippedApps = null
        )
        {
            // Call the async implementation and wait for the result
            var result = ShowOperationConfirmationDialogAsync(
                    operationType,
                    selectedApps,
                    skippedApps
                )
                .GetAwaiter()
                .GetResult();
            return result ? true : (bool?)false;
        }
        
        /// <summary>
        /// Shows a confirmation dialog for items to be processed.
        /// </summary>
        /// <param name="operationType">The type of operation (Install/Remove).</param>
        /// <param name="selectedApps">The selected apps.</param>
        /// <param name="skippedApps">Apps that will be skipped (optional).</param>
        /// <returns>True if confirmed, otherwise false.</returns>
        private async Task<bool> ShowOperationConfirmationDialogAsync(
            string operationType,
            IEnumerable<WindowsApp> selectedApps,
            IEnumerable<WindowsApp>? skippedApps = null
        )
        {
            string title = $"Confirm {operationType}";
            string headerText = $"The following items will be {GetPastTense(operationType)}:";

            // Create list of app names for the dialog
            var appNames = selectedApps.Select(a => a.Name).ToList();

            // Create footer text
            string footerText = "Do you want to continue?";

            // If there are skipped apps, add information about them
            if (skippedApps != null && skippedApps.Any())
            {
                var skippedNames = skippedApps.Select(a => a.Name).ToList();
                footerText =
                    $"Note: The following {skippedApps.Count()} item(s) cannot be {GetPastTense(operationType)} and will be skipped:\n";
                footerText += string.Join(", ", skippedNames);
                footerText +=
                    $"\n\nDo you want to continue with the remaining {selectedApps.Count()} item(s)?";
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
            bool isConnectivityIssue = CurrentCancellationReason == CancellationReason.InternetConnectivityLost;

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

        /// <summary>
        /// Shows a dialog informing the user that no internet connection is available.
        /// </summary>
        protected async Task ShowNoInternetConnectionDialogAsync()
        {
            await _dialogService.ShowWarningAsync(
                "An internet connection is required to install Windows apps. Please check your connection and try again.",
                "No Internet Connection"
            );
        }
        
        /// <summary>
        /// Shows a dialog informing the user that no items are selected.
        /// </summary>
        /// <param name="action">The action that requires selected items (e.g., "installation", "removal")</param>
        protected async Task ShowNoItemsSelectedDialogAsync(string action)
        {
            await _dialogService.ShowWarningAsync(
                $"Please select at least one item for {action}.",
                "No Items Selected"
            );
        }
        
        /// <summary>
        /// Shows a dialog informing the user that the selected items cannot be reinstalled.
        /// </summary>
        /// <param name="itemNames">Names of items that cannot be reinstalled</param>
        /// <param name="showDetails">Whether to show detailed item names</param>
        protected async Task ShowCannotReinstallDialogAsync(IEnumerable<string> itemNames, bool showDetails)
        {
            string message = "The selected items cannot be reinstalled. Please select different items.";
            
            if (showDetails && itemNames.Any())
            {
                message += "\n\nNon-reinstallable items:\n" + string.Join("\n", itemNames);
            }
            
            await _dialogService.ShowWarningAsync(
                message,
                "Cannot Reinstall"
            );
        }
        
        /// <summary>
        /// Shows a dialog asking the user to confirm the operation on the selected items.
        /// </summary>
        /// <param name="operation">The operation to perform (e.g., "install", "remove")</param>
        /// <param name="itemNames">Names of items to perform the operation on</param>
        /// <param name="count">Number of items</param>
        /// <returns>True if confirmed, false otherwise</returns>
        protected async Task<bool?> ShowConfirmItemsDialogAsync(string operation, IEnumerable<string> itemNames, int count)
        {
            string message = $"Are you sure you want to {operation.ToLower()} the following {count} items?";
            
            if (count <= 10)
            {
                message += $"\n\n{string.Join("\n", itemNames)}";
            }
            else
            {
                var firstItems = itemNames.Take(5).ToList();
                var lastItems = itemNames.Skip(count - 5).Take(5).ToList();
                
                message += $"\n\n{string.Join("\n", firstItems)}";
                message += $"\n\n... and {count - 10} more items ...\n\n";
                message += $"{string.Join("\n", lastItems)}";
            }
            
            var result = await _dialogService.ShowConfirmationAsync(
                message,
                $"Confirm {operation}",
                "Yes",
                "No"
            );
            return result;
        }
        
        public async Task LoadAppsAndCheckInstallationStatusAsync()
        {
            if (IsInitialized)
            {
                System.Diagnostics.Debug.WriteLine(
                    "WindowsAppsViewModel already initialized, skipping LoadAppsAndCheckInstallationStatusAsync"
                );
                return;
            }

            System.Diagnostics.Debug.WriteLine("Starting LoadAppsAndCheckInstallationStatusAsync");
            await LoadItemsAsync();
            await CheckInstallationStatusAsync();

            // Set Select All to false by default when view loads
            IsAllSelected = false;

            // Mark as initialized after loading is complete
            IsInitialized = true;

            // Check script status
            RefreshScriptStatus();
            System.Diagnostics.Debug.WriteLine("Completed LoadAppsAndCheckInstallationStatusAsync");
        }

        [RelayCommand]
        public async Task InstallApp(WindowsApp app)
        {
            if (app == null || _appInstallationService == null)
                System.Diagnostics.Debug.WriteLine(
                    "Starting LoadAppsAndCheckInstallationStatusAsync"
                );
            await LoadItemsAsync();
            await CheckInstallationStatusAsync();

            // Set Select All to false by default when view loads
            IsAllSelected = false;
            bool isInternetConnected =
                await _packageManager.SystemServices.IsInternetConnectedAsync(true);
            if (!isInternetConnected)
            {
                StatusText = "No internet connection available. Installation cannot proceed.";

                // Show dialog informing the user about the connectivity issue
                await ShowNoInternetConnectionDialogAsync();
                return;
            }

            // Show confirmation dialog
            bool? dialogResult = ShowOperationConfirmationDialog("Install", new[] { app });

            // If user didn't confirm, exit
            if (dialogResult != true)
            {
                return;
            }

            IsLoading = true;
            StatusText = $"Installing {app.Name}...";

            // Setup cancellation for the installation process
            using var cts = new System.Threading.CancellationTokenSource();
            var cancellationToken = cts.Token;

            try
            {
                var progress = _progressService.CreateDetailedProgress();

                // Subscribe to installation status changes
                void OnInstallationStatusChanged(
                    object sender,
                    InstallationStatusChangedEventArgs e
                )
                {
                    if (e.AppInfo.PackageID == app.PackageID)
                    {
                        StatusText = e.StatusMessage;
                    }
                }

                // Register for status updates
                _appInstallationCoordinatorService.InstallationStatusChanged +=
                    OnInstallationStatusChanged;

                try
                {
                    // Use the coordinator service to handle installation, connectivity monitoring, and status reporting
                    var coordinationResult =
                        await _appInstallationCoordinatorService.InstallAppAsync(
                            app.ToAppInfo(),
                            progress,
                            cancellationToken
                        );

                    if (coordinationResult.Success)
                    {
                        app.IsInstalled = true;
                        StatusText = $"Successfully installed {app.Name}";

                        // Show result dialog
                        ShowOperationResultDialog("Install", 1, 1, new[] { app.Name });
                    }
                    else
                    {
                        string errorMessage =
                            coordinationResult.ErrorMessage
                            ?? $"Failed to install {app.Name}. Please try again.";
                        StatusText = errorMessage;

                        // Determine the type of failure for the dialog
                        if (coordinationResult.WasCancelled)
                        {
                            // Show cancellation dialog
                            ShowOperationResultDialog(
                                "Install",
                                0,
                                1,
                                Array.Empty<string>(),
                                new[] { $"{app.Name}: Installation was cancelled by user" }
                            );
                        }
                        else if (coordinationResult.WasConnectivityIssue)
                        {
                            // Show connectivity issue dialog
                            ShowOperationResultDialog(
                                "Install",
                                0,
                                1,
                                Array.Empty<string>(),
                                new[]
                                {
                                    $"{app.Name}: Internet connection lost during installation. Please check your network connection and try again.",
                                }
                            );
                        }
                        else
                        {
                            // Show general error dialog
                            ShowOperationResultDialog(
                                "Install",
                                0,
                                1,
                                Array.Empty<string>(),
                                new[] { $"{app.Name}: {errorMessage}" }
                            );
                        }
                    }
                }
                finally
                {
                    // Always unregister from the event
                    _appInstallationCoordinatorService.InstallationStatusChanged -=
                        OnInstallationStatusChanged;
                }
            }
            catch (System.Exception ex)
            {
                // This should rarely happen as most exceptions are handled by the coordinator service
                StatusText = $"Error installing {app.Name}: {ex.Message}";

                // Show error dialog
                ShowOperationResultDialog(
                    "Install",
                    0,
                    1,
                    Array.Empty<string>(),
                    new[] { $"{app.Name}: {ex.Message}" }
                );
            }
            finally
            {
                IsLoading = false;
            }
        }

        // The StartPeriodicConnectivityCheck method has been removed as this functionality is now handled by the AppInstallationCoordinatorService

        [RelayCommand]
        public async Task RemoveApp(WindowsApp app)
        {
            if (app == null || _packageManager == null || _bloatRemovalCoordinatorService == null)
                return;

            // Show confirmation dialog
            bool? dialogResult = ShowOperationConfirmationDialog("Remove", new[] { app });

            // If user didn't confirm, exit
            if (dialogResult != true)
            {
                return;
            }

            IsLoading = true;
            StatusText = $"Removing {app.Name}...";
            bool success = false;

            try
            {
                // Check if this is a special app that requires special handling
                if (app.IsSpecialHandler && !string.IsNullOrEmpty(app.SpecialHandlerType))
                {
                    // Use the appropriate special handler method
                    switch (app.SpecialHandlerType)
                    {
                        case "Edge":
                            success = await _packageManager.RemoveEdgeAsync();
                            break;
                        case "OneDrive":
                            success = await _packageManager.RemoveOneDriveAsync();
                            break;
                        case "OneNote":
                            success = await _packageManager.RemoveOneNoteAsync();
                            break;
                        default:
                            success = await _packageManager.RemoveSpecialAppAsync(
                                app.SpecialHandlerType
                            );
                            break;
                    }

                    if (success)
                    {
                        app.IsInstalled = false;
                        StatusText = $"Successfully removed special app: {app.Name}";
                    }
                    else
                    {
                        StatusText = $"Failed to remove special app: {app.Name}";
                    }
                }
                else
                {
                    // Use the BloatRemovalCoordinatorService based on app type
                    var progress = new Progress<TaskProgressDetail>(detail =>
                    {
                        StatusText = detail.StatusText;
                    });

                    OperationResult<bool> result;

                    switch (app.AppType)
                    {
                        case Models.WindowsAppType.Capability:
                            var capabilityInfo = new CapabilityInfo
                            {
                                PackageName = app.PackageName,
                                Name = app.Name,
                            };
                            result = await _bloatRemovalCoordinatorService.RemoveItemsAsync(
                                null,
                                new List<CapabilityInfo> { capabilityInfo },
                                null,
                                progress
                            );
                            break;

                        case Models.WindowsAppType.OptionalFeature:
                            var featureInfo = new FeatureInfo
                            {
                                PackageName = app.PackageName,
                                Name = app.Name,
                            };
                            result = await _bloatRemovalCoordinatorService.RemoveItemsAsync(
                                null,
                                null,
                                new List<FeatureInfo> { featureInfo },
                                progress
                            );
                            break;

                        default: // StandardApp
                            var appInfo = app.ToAppInfo();
                            result = await _bloatRemovalCoordinatorService.RemoveItemsAsync(
                                new List<AppInfo> { appInfo },
                                null,
                                null,
                                progress
                            );
                            break;
                    }

                    success = result.Success;

                    if (success)
                    {
                        app.IsInstalled = false;
                        StatusText = $"Successfully removed {app.Name}";
                    }
                    else
                    {
                        StatusText = $"Failed to remove {app.Name}: {result.ErrorMessage}";
                    }
                }

                // Show result dialog
                ShowOperationResultDialog(
                    "Remove",
                    success ? 1 : 0,
                    1,
                    success ? new[] { app.Name } : Array.Empty<string>(),
                    success ? Array.Empty<string>() : new[] { app.Name }
                );
            }
            catch (System.Exception ex)
            {
                StatusText = $"Error removing {app.Name}: {ex.Message}";

                // Show error dialog
                ShowOperationResultDialog(
                    "Remove",
                    0,
                    1,
                    Array.Empty<string>(),
                    new[] { $"{app.Name}: {ex.Message}" }
                );
            }
            finally
            {
                IsLoading = false;
            }
        }

        public override async void OnNavigatedTo(object parameter)
        {
            try
            {
                // Only load data if not already initialized
                if (!IsInitialized)
                {
                    await LoadAppsAndCheckInstallationStatusAsync();
                    IsInitialized = true;
                }
            }
            catch (System.Exception ex)
            {
                StatusText = $"Error loading apps: {ex.Message}";
                IsLoading = false;
                // Log the error or handle it appropriately
            }
        }

        // Add IsAllSelected property for the "Select All" checkbox
        private bool _isAllSelected;
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (SetProperty(ref _isAllSelected, value))
                {
                    // Apply to all items across all categories
                    foreach (var app in WindowsApps)
                    {
                        app.IsSelected = value;
                    }

                    foreach (var capability in Capabilities)
                    {
                        capability.IsSelected = value;
                    }

                    foreach (var feature in OptionalFeatures)
                    {
                        feature.IsSelected = value;
                    }

                    // If in table view mode, also notify wrapper objects to update their UI
                    if (IsTableViewMode && _allItems != null)
                    {
                        foreach (var wrapperItem in _allItems)
                        {
                            wrapperItem.NotifyIsSelectedChanged();
                        }
                    }

                    // Update other checkboxes to be consistent with the same value
                    // regardless of whether we're checking or unchecking
                    _isAllSelectedInstalled = value;
                    _isAllSelectedNotInstalled = value;
                    OnPropertyChanged(nameof(IsAllSelectedInstalled));
                    OnPropertyChanged(nameof(IsAllSelectedNotInstalled));
                }
            }
        }

        // Add IsAllSelectedInstalled property for the "Select All Installed" checkbox
        private bool _isAllSelectedInstalled;
        public bool IsAllSelectedInstalled
        {
            get => _isAllSelectedInstalled;
            set
            {
                if (SetProperty(ref _isAllSelectedInstalled, value))
                {
                    // Apply to all installed items across all categories
                    foreach (var app in WindowsApps.Where(a => a.IsInstalled))
                    {
                        app.IsSelected = value;
                    }

                    foreach (var capability in Capabilities.Where(c => c.IsInstalled))
                    {
                        capability.IsSelected = value;
                    }

                    foreach (var feature in OptionalFeatures.Where(f => f.IsInstalled))
                    {
                        feature.IsSelected = value;
                    }

                    // Update IsAllSelected based on the current state
                    UpdateIsAllSelectedState();
                }
            }
        }

        // Add IsAllSelectedNotInstalled property for the "Select All Not Installed" checkbox
        private bool _isAllSelectedNotInstalled;
        public bool IsAllSelectedNotInstalled
        {
            get => _isAllSelectedNotInstalled;
            set
            {
                if (SetProperty(ref _isAllSelectedNotInstalled, value))
                {
                    // Apply to all not installed items across all categories
                    foreach (var app in WindowsApps.Where(a => !a.IsInstalled))
                    {
                        app.IsSelected = value;
                    }

                    foreach (var capability in Capabilities.Where(c => !c.IsInstalled))
                    {
                        capability.IsSelected = value;
                    }

                    foreach (var feature in OptionalFeatures.Where(f => !f.IsInstalled))
                    {
                        feature.IsSelected = value;
                    }

                    // Update IsAllSelected based on the current state
                    UpdateIsAllSelectedState();
                }
            }
        }

        // Helper method to update the IsAllSelected state based on other selections
        private void UpdateIsAllSelectedState()
        {
            bool allItemsSelected = true;

            // Check if all items are selected across all categories
            foreach (var app in WindowsApps)
            {
                if (!app.IsSelected)
                {
                    allItemsSelected = false;
                    break;
                }
            }

            if (allItemsSelected)
            {
                foreach (var capability in Capabilities)
                {
                    if (!capability.IsSelected)
                    {
                        allItemsSelected = false;
                        break;
                    }
                }
            }

            if (allItemsSelected)
            {
                foreach (var feature in OptionalFeatures)
                {
                    if (!feature.IsSelected)
                    {
                        allItemsSelected = false;
                        break;
                    }
                }
            }

            // Update the IsAllSelected property without triggering its setter
            if (_isAllSelected != allItemsSelected)
            {
                _isAllSelected = allItemsSelected;
                OnPropertyChanged(nameof(IsAllSelected));
            }

            // Update the IsAllSelectedInstalled and IsAllSelectedNotInstalled states
            UpdateSpecializedCheckboxStates();
        }

        // Helper method to update the specialized checkbox states
        private void UpdateSpecializedCheckboxStates()
        {
            // Check if all installed items are selected
            bool allInstalledSelected = true;
            bool allNotInstalledSelected = true;

            // Check Windows Apps
            foreach (var app in WindowsApps)
            {
                if (app.IsInstalled && !app.IsSelected)
                {
                    allInstalledSelected = false;
                }
                else if (!app.IsInstalled && !app.IsSelected)
                {
                    allNotInstalledSelected = false;
                }
            }

            // Check Capabilities
            foreach (var capability in Capabilities)
            {
                if (capability.IsInstalled && !capability.IsSelected)
                {
                    allInstalledSelected = false;
                }
                else if (!capability.IsInstalled && !capability.IsSelected)
                {
                    allNotInstalledSelected = false;
                }
            }

            // Check Optional Features
            foreach (var feature in OptionalFeatures)
            {
                if (feature.IsInstalled && !feature.IsSelected)
                {
                    allInstalledSelected = false;
                }
                else if (!feature.IsInstalled && !feature.IsSelected)
                {
                    allNotInstalledSelected = false;
                }
            }

            // Update the IsAllSelectedInstalled property without triggering its setter
            if (_isAllSelectedInstalled != allInstalledSelected)
            {
                _isAllSelectedInstalled = allInstalledSelected;
                OnPropertyChanged(nameof(IsAllSelectedInstalled));
            }

            // Update the IsAllSelectedNotInstalled property without triggering its setter
            if (_isAllSelectedNotInstalled != allNotInstalledSelected)
            {
                _isAllSelectedNotInstalled = allNotInstalledSelected;
                OnPropertyChanged(nameof(IsAllSelectedNotInstalled));
            }
        }

        // Event handler for item property changes
        private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WindowsApp.IsSelected))
            {
                // Update all checkbox states when an individual item's selection changes
                UpdateIsAllSelectedState();
                
                // Notify that HasSelectedItems has changed to trigger button state updates in parent ViewModel
                InvalidateHasSelectedItemsCache();
                OnPropertyChanged(nameof(HasSelectedItems));
                
                DebugLogger.Log($"[DEBUG] WindowsAppsViewModel.Item_PropertyChanged - IsSelected changed, HasSelectedItems cache invalidated");
            }
        }
        
        // Cached value for HasSelectedItems to improve performance
        private bool _hasSelectedItems;
        private bool _hasSelectedItemsCacheValid;

        /// <summary>
        /// Gets a value indicating whether any items are selected.
        /// </summary>
        public bool HasSelectedItems
        {
            get
            {
                if (!_hasSelectedItemsCacheValid)
                {
                    if (IsTableViewMode && _allItems != null)
                    {
                        // Check if any table view wrapper items are selected
                        _hasSelectedItems = _allItems.Any(a => a.IsSelected);
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
        
        /// <summary>
        /// Invalidates the HasSelectedItems cache.
        /// </summary>
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
            DebugLogger.Log($"[DEBUG] WindowsAppsViewModel.InvalidateSelectionState called");
            
            // Invalidate the cached value
            InvalidateHasSelectedItemsCache();
            
            // Trigger property changed notification
            OnPropertyChanged(nameof(HasSelectedItems));
            
            // Ensure parent viewmodel updates button states by raising selection changed event
            if (SelectedItemsChanged != null)
            {
                DebugLogger.Log($"[DEBUG] Raising SelectedItemsChanged event");
                SelectedItemsChanged.Invoke(this, EventArgs.Empty);
            }
        }

        // Unsubscribe from all item property changed events
        private void UnsubscribeFromItemPropertyChangedEvents()
        {
            foreach (var app in WindowsApps)
            {
                app.PropertyChanged -= Item_PropertyChanged;
            }

            foreach (var capability in Capabilities)
            {
                capability.PropertyChanged -= Item_PropertyChanged;
            }

            foreach (var feature in OptionalFeatures)
            {
                feature.PropertyChanged -= Item_PropertyChanged;
            }
        }

        [RelayCommand]
        public async Task RemoveApps()
        {
            if (_packageManager == null || _bloatRemovalCoordinatorService == null)
                return;

            // Get selected items from all categories
            var selectedApps = WindowsApps.Where(a => a.IsSelected).ToList();
            var selectedCapabilities = Capabilities.Where(a => a.IsSelected).ToList();
            var selectedFeatures = OptionalFeatures.Where(a => a.IsSelected).ToList();

            // Combine all selected items
            var allSelectedItems = selectedApps
                .Concat(selectedCapabilities.Cast<WindowsApp>())
                .Concat(selectedFeatures.Cast<WindowsApp>())
                .ToList();

            int totalSelected = allSelectedItems.Count;

            if (totalSelected == 0)
            {
                StatusText = "No items selected for removal";
                return;
            }

            // Show confirmation dialog
            bool? dialogResult = ShowOperationConfirmationDialog("Remove", allSelectedItems);

            // If user didn't confirm, exit
            if (dialogResult != true)
            {
                return;
            }

            // Use the ExecuteWithProgressAsync method from BaseViewModel to handle progress reporting
            await ExecuteWithProgressAsync(
                async (progress, cancellationToken) =>
                {
                    int successCount = 0;
                    int currentItem = 0;

                    // Convert WindowsApp objects to their respective domain models
                    var appInfos = new List<AppInfo>();
                    var capabilityInfos = new List<CapabilityInfo>();
                    var featureInfos = new List<FeatureInfo>();

                    // Process special apps separately using the existing methods
                    var specialApps = selectedApps
                        .Where(a =>
                            a.IsSpecialHandler && !string.IsNullOrEmpty(a.SpecialHandlerType)
                        )
                        .ToList();
                    var regularApps = selectedApps
                        .Where(a =>
                            !a.IsSpecialHandler || string.IsNullOrEmpty(a.SpecialHandlerType)
                        )
                        .ToList();

                    // Handle special apps first
                    foreach (var app in specialApps)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            CurrentCancellationReason = CancellationReason.UserCancelled;
                            break;
                        }

                        progress.Report(
                            new TaskProgressDetail
                            {
                                Progress = 0,
                                StatusText = $"Removing special app {app.Name}...",
                                DetailedMessage = $"Removing special Windows App: {app.Name}",
                                AdditionalInfo = new Dictionary<string, string>
                                {
                                    { "AppType", app.AppType.ToString() },
                                    { "PackageName", app.PackageName },
                                    { "SpecialHandlerType", app.SpecialHandlerType },
                                    { "OperationType", "Remove" },
                                },
                            }
                        );

                        try
                        {
                            // Use the appropriate special handler method
                            bool success = false;
                            switch (app.SpecialHandlerType)
                            {
                                case "Edge":
                                    success = await _packageManager.RemoveEdgeAsync();
                                    break;
                                case "OneDrive":
                                    success = await _packageManager.RemoveOneDriveAsync();
                                    break;
                                case "OneNote":
                                    success = await _packageManager.RemoveOneNoteAsync();
                                    break;
                                default:
                                    success = await _packageManager.RemoveSpecialAppAsync(
                                        app.SpecialHandlerType
                                    );
                                    break;
                            }

                            if (success)
                            {
                                app.IsInstalled = false;
                                successCount++;

                                progress.Report(
                                    new TaskProgressDetail
                                    {
                                        Progress = 0,
                                        StatusText = $"Successfully removed special app {app.Name}",
                                        DetailedMessage =
                                            $"Successfully removed special Windows App: {app.Name}",
                                        LogLevel = LogLevel.Success,
                                    }
                                );
                            }
                            else
                            {
                                progress.Report(
                                    new TaskProgressDetail
                                    {
                                        Progress = 0,
                                        StatusText = $"Failed to remove special app {app.Name}",
                                        DetailedMessage =
                                            $"Failed to remove special Windows App: {app.Name}",
                                        LogLevel = LogLevel.Error,
                                    }
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            progress.Report(
                                new TaskProgressDetail
                                {
                                    Progress = 0,
                                    StatusText =
                                        $"Error removing special app {app.Name}: {ex.Message}",
                                    DetailedMessage =
                                        $"Error removing special Windows App: {app.Name}",
                                    LogLevel = LogLevel.Error,
                                }
                            );
                        }
                    }

                    // Convert regular apps to AppInfo objects
                    foreach (var app in regularApps)
                    {
                        appInfos.Add(app.ToAppInfo());
                    }

                    // Convert capabilities to CapabilityInfo objects
                    foreach (var capability in selectedCapabilities)
                    {
                        capabilityInfos.Add(
                            new CapabilityInfo
                            {
                                PackageName = capability.PackageName,
                                Name = capability.Name,
                            }
                        );
                    }

                    // Convert features to FeatureInfo objects
                    foreach (var feature in selectedFeatures)
                    {
                        featureInfos.Add(
                            new FeatureInfo
                            {
                                PackageName = feature.PackageName,
                                Name = feature.Name,
                            }
                        );
                    }

                    // Use the BloatRemovalCoordinatorService to remove all items at once
                    progress.Report(
                        new TaskProgressDetail
                        {
                            Progress = 10,
                            StatusText = "Adding items to BloatRemoval script...",
                            DetailedMessage =
                                $"Adding {appInfos.Count} apps, {capabilityInfos.Count} capabilities, and {featureInfos.Count} features to BloatRemoval script",
                        }
                    );

                    var result = await _bloatRemovalCoordinatorService.RemoveItemsAsync(
                        appInfos,
                        capabilityInfos,
                        featureInfos,
                        progress,
                        cancellationToken
                    );

                    if (result.Success)
                    {
                        // Create lists to track successful and failed items
                        var successItems = new List<string>();
                        var failedItems = new List<string>();

                        // Mark all items as uninstalled
                        foreach (var app in regularApps)
                        {
                            app.IsInstalled = false;
                            successCount++;
                            successItems.Add(app.Name);
                        }

                        foreach (var capability in selectedCapabilities)
                        {
                            capability.IsInstalled = false;
                            successCount++;
                            successItems.Add(capability.Name);
                        }

                        foreach (var feature in selectedFeatures)
                        {
                            feature.IsInstalled = false;
                            successCount++;
                            successItems.Add(feature.Name);
                        }

                        // Add special apps to the success list
                        foreach (var app in specialApps.Where(a => !a.IsInstalled))
                        {
                            successItems.Add(app.Name);
                        }

                        // Final progress report
                        progress.Report(
                            new TaskProgressDetail
                            {
                                Progress = 100,
                                StatusText = $"Successfully removed {successCount} items",
                                LogLevel = LogLevel.Success,
                            }
                        );

                        // Show success dialog after completion
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            ShowOperationResultDialog(
                                "Remove",
                                successCount,
                                totalSelected,
                                successItems,
                                failedItems
                            );
                        });
                    }

                    // Refresh the UI
                    SortCollections();

                    return successCount;
                },
                "Removing Windows Apps",
                false
            );
        }

        [RelayCommand]
        public async Task InstallApps()
        {
            if (
                _packageManager == null
                || _appInstallationService == null
                || _capabilityService == null
                || _featureService == null
            )
                return;

            // Get all selected items
            var selectedApps = WindowsApps.Where(a => a.IsSelected).ToList();
            var selectedCapabilities = Capabilities.Where(a => a.IsSelected).ToList();
            var selectedFeatures = OptionalFeatures.Where(a => a.IsSelected).ToList();

            // Check if anything is selected at all
            int totalSelected =
                selectedApps.Count + selectedCapabilities.Count + selectedFeatures.Count;

            if (totalSelected == 0)
            {
                StatusText = "No items selected for installation";
                await ShowNoItemsSelectedDialogAsync("installation");
                return;
            }

            // Identify items that cannot be reinstalled
            var nonReinstallableApps = selectedApps.Where(a => !a.CanBeReinstalled).ToList();
            var nonReinstallableCapabilities = selectedCapabilities
                .Where(c => !c.CanBeReinstalled)
                .ToList();
            var nonReinstallableFeatures = selectedFeatures
                .Where(f => !f.CanBeReinstalled)
                .ToList();

            var allNonReinstallableItems = nonReinstallableApps
                .Concat(nonReinstallableCapabilities)
                .Concat(nonReinstallableFeatures)
                .ToList();

            // Remove non-reinstallable items from the selected items
            var installableApps = selectedApps.Except(nonReinstallableApps).ToList();
            var installableCapabilities = selectedCapabilities
                .Except(nonReinstallableCapabilities)
                .ToList();
            var installableFeatures = selectedFeatures.Except(nonReinstallableFeatures).ToList();

            var allInstallableItems = installableApps
                .Concat(installableCapabilities.Cast<WindowsApp>())
                .Concat(installableFeatures.Cast<WindowsApp>())
                .ToList();

            if (allInstallableItems.Count == 0)
            {
                if (allNonReinstallableItems.Any())
                {
                    // Show dialog explaining that all selected items cannot be reinstalled
                    await ShowCannotReinstallDialogAsync(
                        allNonReinstallableItems.Select(a => a.Name),
                        false
                    );
                }
                else
                {
                    StatusText = "No items selected for installation";
                }
                return;
            }

            // Show confirmation dialog, including information about skipped items
            // Show confirmation dialog
            bool? dialogResult = await ShowConfirmItemsDialogAsync(
                "install",
                allInstallableItems.Select(a => a.Name),
                allInstallableItems.Count
            );

            // If user didn't confirm, exit
            if (dialogResult != true)
            {
                return;
            }

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

            // Use the ExecuteWithProgressAsync method from BaseViewModel to handle progress reporting
            await ExecuteWithProgressAsync(
                async (progress, cancellationToken) =>
                {
                    int successCount = 0;
                    int currentItem = 0;

                    // Process standard Windows apps
                    foreach (var app in installableApps)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            // Set cancellation reason to user cancelled
                            CurrentCancellationReason = CancellationReason.UserCancelled;
                            break;
                        }

                        currentItem++;
                        progress.Report(
                            new TaskProgressDetail
                            {
                                Progress = (currentItem * 100.0) / totalSelected,
                                StatusText =
                                    $"Installing app {app.Name}... ({currentItem}/{totalSelected})",
                                DetailedMessage = $"Installing Windows App: {app.Name}",
                            }
                        );

                        try
                        {
                            var result = await _appInstallationService.InstallAppAsync(
                                app.ToAppInfo(),
                                progress,
                                cancellationToken
                            );

                            // Only mark as successful if the operation actually succeeded
                            if (result.Success && result.Result)
                            {
                                app.IsInstalled = true;
                                successCount++;

                                progress.Report(
                                    new TaskProgressDetail
                                    {
                                        DetailedMessage = $"Successfully installed {app.Name}",
                                        LogLevel = LogLevel.Success,
                                    }
                                );
                            }
                            else
                            {
                                // The operation returned but was not successful
                                string errorMessage =
                                    result.ErrorMessage
                                    ?? "Unknown error occurred during installation";

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
                                            DetailedMessage =
                                                $"Installation of {app.Name} was cancelled",
                                            LogLevel = LogLevel.Warning,
                                        }
                                    );
                                }
                                else
                                {
                                    // Check if it's an internet connectivity issue
                                    if (
                                        errorMessage.Contains("internet")
                                        || errorMessage.Contains("connection")
                                    )
                                    {
                                        errorMessage =
                                            $"No internet connection available. Please check your network connection and try again.";
                                    }

                                    progress.Report(
                                        new TaskProgressDetail
                                        {
                                            DetailedMessage =
                                                $"Failed to install {app.Name}: {errorMessage}",
                                            LogLevel = LogLevel.Error,
                                        }
                                    );
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            progress.Report(
                                new TaskProgressDetail
                                {
                                    DetailedMessage = $"Error installing {app.Name}: {ex.Message}",
                                    LogLevel = LogLevel.Error,
                                }
                            );
                        }
                    }

                    // Process capabilities
                    foreach (var capability in installableCapabilities)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            // Set cancellation reason to user cancelled
                            CurrentCancellationReason = CancellationReason.UserCancelled;
                            break;
                        }

                        currentItem++;
                        progress.Report(
                            new TaskProgressDetail
                            {
                                Progress = (currentItem * 100.0) / totalSelected,
                                StatusText =
                                    $"Installing capability {capability.Name}... ({currentItem}/{totalSelected})",
                                DetailedMessage =
                                    $"Installing Windows Capability: {capability.Name}",
                            }
                        );

                        try
                        {
                            var result = await _capabilityService.InstallCapabilityAsync(
                                capability.ToCapabilityInfo(),
                                progress,
                                cancellationToken
                            );
                            
                            // Only mark as successful if the operation actually succeeded
                            if (result.Success && result.Result)
                            {
                                capability.IsInstalled = true;
                                successCount++;

                                progress.Report(
                                    new TaskProgressDetail
                                    {
                                        DetailedMessage =
                                            $"Successfully installed capability {capability.Name}",
                                        LogLevel = LogLevel.Success,
                                    }
                                );
                            }
                            else
                            {
                                // Check if this is a cancellation rather than a failure
                                // Handle multiple cancellation message patterns from different services
                                bool isCancellation = result.ErrorMessage != null && (
                                    result.ErrorMessage.Contains("cancelled by the user", StringComparison.OrdinalIgnoreCase) ||
                                    result.ErrorMessage.Contains("operation was canceled", StringComparison.OrdinalIgnoreCase) ||
                                    result.ErrorMessage.Contains("script returned no result", StringComparison.OrdinalIgnoreCase)
                                );
                                
                                if (isCancellation)
                                {
                                    // Set cancellation reason to user cancelled
                                    CurrentCancellationReason = CancellationReason.UserCancelled;
                                    
                                    progress.Report(
                                        new TaskProgressDetail
                                        {
                                            DetailedMessage = $"Installation of capability {capability.Name} was cancelled",
                                            LogLevel = LogLevel.Warning,
                                        }
                                    );
                                }
                                else
                                {
                                    progress.Report(
                                        new TaskProgressDetail
                                        {
                                            DetailedMessage =
                                                $"Failed to install capability {capability.Name}: {result.ErrorMessage ?? "Unknown error"}",
                                            LogLevel = LogLevel.Error,
                                        }
                                    );
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            progress.Report(
                                new TaskProgressDetail
                                {
                                    DetailedMessage =
                                        $"Error installing capability {capability.Name}: {ex.Message}",
                                    LogLevel = LogLevel.Error,
                                }
                            );
                        }
                    }

                    // Process optional features
                    foreach (var feature in installableFeatures)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            // Set cancellation reason to user cancelled
                            CurrentCancellationReason = CancellationReason.UserCancelled;
                            break;
                        }

                        currentItem++;
                        progress.Report(
                            new TaskProgressDetail
                            {
                                Progress = (currentItem * 100.0) / totalSelected,
                                StatusText =
                                    $"Installing feature {feature.Name}... ({currentItem}/{totalSelected})",
                                DetailedMessage = $"Installing Windows Feature: {feature.Name}",
                            }
                        );

                        try
                        {
                            var result = await _featureService.InstallFeatureAsync(
                                feature.ToFeatureInfo(),
                                progress,
                                cancellationToken
                            );
                            
                            // Only mark as successful if the operation actually succeeded
                            if (result.Success && result.Result)
                            {
                                feature.IsInstalled = true;
                                successCount++;

                                progress.Report(
                                    new TaskProgressDetail
                                    {
                                        DetailedMessage =
                                            $"Successfully installed feature {feature.Name}",
                                        LogLevel = LogLevel.Success,
                                    }
                                );
                            }
                            else
                            {
                                // Check if this is a cancellation rather than a failure
                                // Handle multiple cancellation message patterns from different services
                                bool isCancellation = result.ErrorMessage != null && (
                                    result.ErrorMessage.Contains("cancelled by the user", StringComparison.OrdinalIgnoreCase) ||
                                    result.ErrorMessage.Contains("operation was canceled", StringComparison.OrdinalIgnoreCase) ||
                                    result.ErrorMessage.Contains("script returned no result", StringComparison.OrdinalIgnoreCase)
                                );
                                
                                if (isCancellation)
                                {
                                    // Set cancellation reason to user cancelled
                                    CurrentCancellationReason = CancellationReason.UserCancelled;
                                    
                                    progress.Report(
                                        new TaskProgressDetail
                                        {
                                            DetailedMessage = $"Installation of feature {feature.Name} was cancelled",
                                            LogLevel = LogLevel.Warning,
                                        }
                                    );
                                }
                                else
                                {
                                    progress.Report(
                                        new TaskProgressDetail
                                        {
                                            DetailedMessage =
                                                $"Failed to install feature {feature.Name}: {result.ErrorMessage ?? "Unknown error"}",
                                            LogLevel = LogLevel.Error,
                                        }
                                    );
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            progress.Report(
                                new TaskProgressDetail
                                {
                                    DetailedMessage =
                                        $"Error installing feature {feature.Name}: {ex.Message}",
                                    LogLevel = LogLevel.Error,
                                }
                            );
                        }
                    }

                    // Final report
                    progress.Report(
                        new TaskProgressDetail
                        {
                            Progress = 100,
                            StatusText =
                                $"Successfully installed {successCount} of {totalSelected} items",
                            DetailedMessage =
                                $"Task completed: {successCount} of {totalSelected} items installed successfully",
                            LogLevel =
                                successCount == totalSelected ? LogLevel.Success : LogLevel.Warning,
                        }
                    );

                    // Refresh the UI
                    SortCollections();

                    // Collect success, failure, and skipped information for the result dialog
                    var successItems = new List<string>();
                    var failedItems = new List<string>();
                    var skippedItems = allNonReinstallableItems.Select(i => i.Name).ToList();

                    // Add successful items to the list
                    foreach (var app in installableApps.Where(a => a.IsInstalled))
                    {
                        successItems.Add(app.Name);
                    }
                    foreach (var capability in installableCapabilities.Where(c => c.IsInstalled))
                    {
                        successItems.Add(capability.Name);
                    }
                    foreach (var feature in installableFeatures.Where(f => f.IsInstalled))
                    {
                        successItems.Add(feature.Name);
                    }

                    // Add failed items to the list
                    foreach (var app in installableApps.Where(a => !a.IsInstalled))
                    {
                        failedItems.Add(app.Name);
                    }
                    foreach (var capability in installableCapabilities.Where(c => !c.IsInstalled))
                    {
                        failedItems.Add(capability.Name);
                    }
                    foreach (var feature in installableFeatures.Where(f => !f.IsInstalled))
                    {
                        failedItems.Add(feature.Name);
                    }

                    // Show result dialog
                    ShowOperationResultDialog(
                        "Install",
                        successCount,
                        totalSelected + skippedItems.Count,
                        successItems,
                        failedItems,
                        skippedItems
                    );

                    return successCount;
                },
                $"Installing {totalSelected} items",
                false
            );
        }

        #region BaseInstallationViewModel Abstract Method Implementations

        /// <summary>
        /// Gets the name of a Windows app.
        /// </summary>
        /// <param name="app">The Windows app.</param>
        /// <returns>The name of the app.</returns>
        protected string GetAppName(WindowsApp app)
        {
            return app.Name;
        }

        /// <summary>
        /// Converts a Windows app to an AppInfo object.
        /// </summary>
        /// <param name="app">The Windows app to convert.</param>
        /// <returns>The AppInfo object.</returns>
        protected AppInfo ToAppInfo(WindowsApp app)
        {
            return app.ToAppInfo();
        }

        /// <summary>
        /// Gets the selected Windows apps.
        /// </summary>
        /// <returns>The selected Windows apps.</returns>
        protected IEnumerable<WindowsApp> GetSelectedApps()
        {
            return Items.Where(a => a.IsSelected);
        }

        /// <summary>
        /// Sets the installation status of a Windows app.
        /// </summary>
        /// <param name="app">The Windows app.</param>
        /// <param name="isInstalled">Whether the app is installed.</param>
        protected void SetInstallationStatus(WindowsApp app, bool isInstalled)
        {
            app.IsInstalled = isInstalled;
        }

        #endregion
    }
}
