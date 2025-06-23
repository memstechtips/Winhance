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
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
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
    public partial class ExternalAppsViewModel : BaseInstallationViewModel<ExternalApp>
    {
        [ObservableProperty]
        private bool _isInitialized = false;

        [ObservableProperty]
        private bool _isPackageManagerViewMode = false;

        [ObservableProperty]
        private bool _isAllSelected = false;

        private readonly IAppInstallationService _appInstallationService;
        private readonly IAppService _appDiscoveryService;
        private readonly IConfigurationService _configurationService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private WinGetSearchResultsViewModel _winGetSearchResultsViewModel;
        
        [ObservableProperty]
        private ExternalAppsPackageManagerViewModel _externalAppsPackageManagerViewModel;

        private ObservableCollection<ExternalAppWithTableInfo> _allItems = new();
        private ICollectionView _allItemsView;

        /// <summary>
        /// Gets the collection view for all external apps in table view mode
        /// </summary>
        public ICollectionView AllItemsView =>
            _allItemsView ??= CollectionViewSource.GetDefaultView(_allItems);

        /// <summary>
        /// Command to toggle between list view and package manager view modes
        /// </summary>
        [RelayCommand]
        private void ToggleViewMode(object parameter = null)
        {
            // If parameter is provided, use it to set the view mode directly
            if (parameter is string strParam)
            {
                IsPackageManagerViewMode = bool.Parse(strParam);
            }
            else if (parameter is bool boolParam)
            {
                IsPackageManagerViewMode = boolParam;
            }
            else
            {
                // Toggle the view mode if no parameter is provided
                IsPackageManagerViewMode = !IsPackageManagerViewMode;
            }

            // If switching to package manager view, ensure search is applied
            if (IsPackageManagerViewMode && !string.IsNullOrWhiteSpace(SearchText))
            {
                // Apply search to the package manager view
                ExternalAppsPackageManagerViewModel?.ApplySearch(SearchText);
            }
            else if (!IsPackageManagerViewMode)
            {
                // Clear search in package manager view when switching back to list view
                ExternalAppsPackageManagerViewModel?.ApplySearch(string.Empty);
            }
        }

        /// <summary>
        /// Command to sort the table view by the specified property
        /// </summary>
        [RelayCommand]
        private void SortBy(string propertyName)
        {
            if (_allItemsView == null)
                return;

            var direction = ListSortDirection.Ascending;

            // If already sorting by this property, toggle the direction
            if (
                _allItemsView.SortDescriptions.Count > 0
                && _allItemsView.SortDescriptions[0].PropertyName == propertyName
            )
            {
                direction =
                    _allItemsView.SortDescriptions[0].Direction == ListSortDirection.Ascending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending;
            }

            _allItemsView.SortDescriptions.Clear();
            _allItemsView.SortDescriptions.Add(new SortDescription(propertyName, direction));
        }

        /// <summary>
        /// Updates the collection of all external apps for the table view
        /// </summary>
        private void UpdateAllItemsCollection()
        {
            _allItems.Clear();

            // Add all apps from all categories to the flat list
            foreach (var category in Categories)
            {
                foreach (var app in category.Apps)
                {
                    _allItems.Add(new ExternalAppWithTableInfo(app));
                }
            }

            // Refresh the view
            _allItemsView?.Refresh();
        }

        /// <summary>
        /// Handles changes to the IsAllSelected property
        /// </summary>
        partial void OnIsAllSelectedChanged(bool value)
        {
            // Apply the selection state to all items in the table view
            foreach (var item in _allItems)
            {
                item.IsSelected = value;
            }
        }

        [ObservableProperty]
        private string _statusText = "Ready";

        // ObservableCollection to store category view models
        private ObservableCollection<ExternalAppsCategoryViewModel> _categories = new();

        // Public property to expose the categories
        public ObservableCollection<ExternalAppsCategoryViewModel> Categories => _categories;

        // Property to indicate if any items are selected
        public bool HasSelectedItems
        {
            get
            {
                // Check if any regular items are selected
                bool regularItemsSelected = Items?.Any(a => a.IsSelected) == true;

                // Check if any WinGet search results are selected
                bool winGetItemsSelected = WinGetSearchResultsViewModel?.HasSelectedItems == true;

                // Return true if either regular items or WinGet items are selected
                return regularItemsSelected || winGetItemsSelected;
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
        )
            : base(
                progressService,
                searchService,
                packageManager,
                appInstallationService,
                appInstallationCoordinatorService,
                connectivityService,
                dialogService
            )
        {
            _appInstallationService = appInstallationService;
            _appDiscoveryService = appDiscoveryService;
            _configurationService = configurationService;
            _serviceProvider = serviceProvider;

            // Initialize WinGetSearchResultsViewModel
            WinGetSearchResultsViewModel =
                _serviceProvider.GetRequiredService<WinGetSearchResultsViewModel>();

            // Initialize ExternalAppsPackageManagerViewModel
            ExternalAppsPackageManagerViewModel =
                _serviceProvider.GetRequiredService<ExternalAppsPackageManagerViewModel>();

            // Subscribe to property changes in the WinGetSearchResultsViewModel
            WinGetSearchResultsViewModel.PropertyChanged +=
                WinGetSearchResultsViewModel_PropertyChanged;

            // Subscribe to collection changed events to track item selection changes
            Items.CollectionChanged += Items_CollectionChanged;
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
                OnPropertyChanged(nameof(HasSelectedItems));
            }
        }

        private void WinGetSearchResultsViewModel_PropertyChanged(
            object sender,
            System.ComponentModel.PropertyChangedEventArgs e
        )
        {
            // When the HasSelectedItems property changes in the WinGetSearchResultsViewModel, update our HasSelectedItems property
            if (e.PropertyName == nameof(WinGetSearchResultsViewModel.HasSelectedItems))
            {
                OnPropertyChanged(nameof(HasSelectedItems));
            }
        }

        // SaveConfig and ImportConfig methods removed as part of unified configuration cleanup

        /// <summary>
        /// Applies the current search text to filter items.
        /// </summary>
        protected override void ApplySearch()
        {
            if (Items == null || Items.Count == 0)
                return;

            // Only apply search to the package manager view if in package manager view mode
            if (IsPackageManagerViewMode)
            {
                // If search is active
                if (IsSearchActive)
                {
                    // Execute WinGet search asynchronously through the package manager view model
                    // No minimum character requirement to allow for specific short searches
                    ExternalAppsPackageManagerViewModel?.ApplySearch(SearchText);
                }
                else
                {
                    // Clear in the package manager view model if search text is cleared
                    ExternalAppsPackageManagerViewModel?.ApplySearch(string.Empty);
                }
                
                // Don't filter categories in package manager mode
                return;
            }

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

            // Update the collection if in package manager view mode
            if (IsPackageManagerViewMode)
            {
                UpdateAllItemsCollection();
            }
        }

        public override async Task LoadItemsAsync()
        {
            if (_packageManager == null)
                return;

            IsLoading = true;
            StatusText = "Loading external apps...";

            try
            {
                Items.Clear();
                _categories.Clear();

                var apps = await _packageManager.GetInstallableAppsAsync();

                // Group apps by category
                var appsByCategory = new Dictionary<string, List<ExternalApp>>();

                foreach (var app in apps)
                {
                    var externalApp = ExternalApp.FromAppInfo(app);
                    Items.Add(externalApp);

                    // Group by category
                    string category = app.Category;
                    if (string.IsNullOrEmpty(category))
                    {
                        category = "Other";
                    }

                    if (!appsByCategory.ContainsKey(category))
                    {
                        appsByCategory[category] = new List<ExternalApp>();
                    }

                    appsByCategory[category].Add(externalApp);
                }

                // Sort categories alphabetically
                var sortedCategories = appsByCategory.Keys.OrderBy(c => c).ToList();

                // Create category view models with sorted apps
                foreach (var categoryName in sortedCategories)
                {
                    // Sort apps alphabetically within the category
                    var sortedApps = appsByCategory[categoryName].OrderBy(a => a.Name).ToList();

                    // Create observable collection for the category
                    var appsCollection = new ObservableCollection<ExternalApp>(sortedApps);

                    // Create and add the category view model
                    _categories.Add(
                        new ExternalAppsCategoryViewModel(categoryName, appsCollection)
                    );
                }

                StatusText = $"Loaded {Items.Count} external apps";

                // Update the collection if in package manager view mode
                if (IsPackageManagerViewMode)
                {
                    UpdateAllItemsCollection();
                }
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

        public override async Task CheckInstallationStatusAsync()
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

                    if (operationResult.Success)
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
                // Log any unexpected errors but don't disrupt the installation process
                System.Diagnostics.Debug.WriteLine($"Error in connectivity check: {ex.Message}");
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

            // Also clear WinGet search results selections
            WinGetSearchResultsViewModel.ClearSelection();

            StatusText = "All selections cleared";

            // Explicitly notify that HasSelectedItems has changed
            OnPropertyChanged(nameof(HasSelectedItems));
        }

        [RelayCommand]
        public async Task InstallApps()
        {
            if (_appInstallationService == null)
                return;

            // Get all selected apps regardless of installation status
            var selectedApps = Items.Where(a => a.IsSelected).ToList();

            // Check if there are any selected WinGet search results
            var selectedWinGetItems = WinGetSearchResultsViewModel
                .SearchResults.Where(item => item.IsSelected)
                .ToList();

            // If no apps or WinGet items are selected, show a message
            if (!selectedApps.Any() && !selectedWinGetItems.Any())
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

            // Add WinGet package names if any are selected
            if (selectedWinGetItems.Any())
            {
                itemsToInstall.AddRange(selectedWinGetItems.Select(w => $"WinGet: {w.Name}"));
                totalItemsCount += selectedWinGetItems.Count;
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

                                if (operationResult.Success)
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

                    // Next, install WinGet packages if any are selected
                    if (selectedWinGetItems.Any())
                    {
                        foreach (var winGetItem in selectedWinGetItems)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                await HandleCancellationAsync(false); // User-initiated cancellation
                                cts.Cancel(); // Ensure all tasks are cancelled
                                return successCount; // Exit the method immediately
                            }

                            try
                            {
                                StatusText = $"Installing {winGetItem.Name}...";

                                progress.Report(
                                    new TaskProgressDetail
                                    {
                                        Progress = (currentItem * 100.0) / totalSelected,
                                        StatusText = $"Installing {winGetItem.Name}",
                                        DetailedMessage =
                                            $"Installing WinGet package: {winGetItem.Name}",
                                        LogLevel = LogLevel.Info,
                                        AdditionalInfo = new Dictionary<string, string>
                                        {
                                            { "PackageName", winGetItem.Name },
                                            { "PackageId", winGetItem.Id },
                                            { "OperationType", "Install" },
                                            { "ItemNumber", currentItem.ToString() },
                                            { "TotalItems", totalSelected.ToString() },
                                        },
                                    }
                                );

                                // Use the WinGet installer service to install the package directly
                                var result =
                                    await WinGetSearchResultsViewModel.InstallWinGetPackageAsync(
                                        winGetItem.Id,
                                        winGetItem.Name,
                                        cancellationToken
                                    );

                                bool success = result.Success;

                                if (success)
                                {
                                    successCount++;

                                    progress.Report(
                                        new TaskProgressDetail
                                        {
                                            Progress = (currentItem * 100.0) / totalSelected,
                                            StatusText =
                                                $"Successfully installed {winGetItem.Name}",
                                            DetailedMessage =
                                                $"Successfully installed WinGet package: {winGetItem.Name}",
                                            LogLevel = LogLevel.Success,
                                            AdditionalInfo = new Dictionary<string, string>
                                            {
                                                { "PackageName", winGetItem.Name },
                                                { "PackageId", winGetItem.Id },
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
                                        result.Message ?? $"Failed to install {winGetItem.Name}";

                                    progress.Report(
                                        new TaskProgressDetail
                                        {
                                            Progress = (currentItem * 100.0) / totalSelected,
                                            StatusText = $"Error installing {winGetItem.Name}",
                                            DetailedMessage = errorMessage,
                                            LogLevel = LogLevel.Error,
                                            AdditionalInfo = new Dictionary<string, string>
                                            {
                                                { "PackageName", winGetItem.Name },
                                                { "PackageId", winGetItem.Id },
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
                            catch (OperationCanceledException)
                            {
                                progress.Report(
                                    new TaskProgressDetail
                                    {
                                        Progress = (currentItem * 100.0) / totalSelected,
                                        StatusText =
                                            $"Installation of {winGetItem.Name} was cancelled",
                                        DetailedMessage =
                                            $"The installation of {winGetItem.Name} was cancelled.",
                                        LogLevel = LogLevel.Warning,
                                        AdditionalInfo = new Dictionary<string, string>
                                        {
                                            { "PackageName", winGetItem.Name },
                                            { "PackageId", winGetItem.Id },
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
                                        StatusText = $"Error installing {winGetItem.Name}",
                                        DetailedMessage =
                                            $"Error installing {winGetItem.Name}: {ex.Message}",
                                        LogLevel = LogLevel.Error,
                                        AdditionalInfo = new Dictionary<string, string>
                                        {
                                            { "PackageName", winGetItem.Name },
                                            { "PackageId", winGetItem.Id },
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
                            finally
                            {
                                currentItem++;
                            }
                        }

                        // Clear WinGet search result selections after installation
                        WinGetSearchResultsViewModel.ClearSelection();
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
                // Log any unexpected errors but don't disrupt the installation process
                System.Diagnostics.Debug.WriteLine(
                    $"Error in batch connectivity check: {ex.Message}"
                );
            }
        }

        public async Task LoadAppsAndCheckInstallationStatusAsync()
        {
            if (IsInitialized)
            {
                System.Diagnostics.Debug.WriteLine(
                    "ExternalAppsViewModel already initialized, skipping LoadAppsAndCheckInstallationStatusAsync"
                );
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                "Starting ExternalAppsViewModel LoadAppsAndCheckInstallationStatusAsync"
            );
            await LoadItemsAsync();
            await CheckInstallationStatusAsync();

            // Mark as initialized after loading is complete
            IsInitialized = true;
            System.Diagnostics.Debug.WriteLine(
                "Completed ExternalAppsViewModel LoadAppsAndCheckInstallationStatusAsync"
            );
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
        protected override string GetAppName(ExternalApp app)
        {
            return app.Name;
        }

        /// <summary>
        /// Converts an external app to an AppInfo object.
        /// </summary>
        /// <param name="app">The external app to convert.</param>
        /// <returns>The AppInfo object.</returns>
        protected override AppInfo ToAppInfo(ExternalApp app)
        {
            return app.ToAppInfo();
        }

        /// <summary>
        /// Gets the selected external apps.
        /// </summary>
        /// <returns>The selected external apps.</returns>
        protected override IEnumerable<ExternalApp> GetSelectedApps()
        {
            return Items.Where(a => a.IsSelected);
        }

        /// <summary>
        /// Sets the installation status of an external app.
        /// </summary>
        /// <param name="app">The external app.</param>
        /// <param name="isInstalled">Whether the app is installed.</param>
        protected override void SetInstallationStatus(ExternalApp app, bool isInstalled)
        {
            app.IsInstalled = isInstalled;
        }

        #endregion
    }
}
