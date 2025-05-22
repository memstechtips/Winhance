using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.UI.Interfaces;
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

        private readonly IAppInstallationService _appInstallationService;
        private readonly IAppService _appDiscoveryService;
        private readonly IConfigurationService _configurationService;

        [ObservableProperty]
        private string _statusText = "Ready";

        // ObservableCollection to store category view models
        private ObservableCollection<ExternalAppsCategoryViewModel> _categories = new();

        // Public property to expose the categories
        public ObservableCollection<ExternalAppsCategoryViewModel> Categories => _categories;

        public ExternalAppsViewModel(
            ITaskProgressService progressService,
            ISearchService searchService,
            IPackageManager packageManager,
            IAppInstallationService appInstallationService,
            IAppService appDiscoveryService,
            IConfigurationService configurationService,
            Services.SoftwareAppsDialogService dialogService,
            IInternetConnectivityService connectivityService,
            IAppInstallationCoordinatorService appInstallationCoordinatorService
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
        }

        // SaveConfig and ImportConfig methods removed as part of unified configuration cleanup

        /// <summary>
        /// Applies the current search text to filter items.
        /// </summary>
        protected override void ApplySearch()
        {
            if (Items == null || Items.Count == 0)
                return;

            // Filter items based on search text
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
                        || ex.Message.Contains("pipeline has been stopped", StringComparison.OrdinalIgnoreCase);

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
            // Clear all selected items
            foreach (var app in Items)
            {
                app.IsSelected = false;
            }

            // Update the UI for all categories
            foreach (var category in Categories)
            {
                foreach (var app in category.Apps)
                {
                    app.IsSelected = false;
                }
            }

            StatusText = "All selections cleared";
        }

        [RelayCommand]
        public async Task InstallApps()
        {
            if (_appInstallationService == null)
                return;

            // Get all selected apps regardless of installation status
            var selectedApps = Items.Where(a => a.IsSelected).ToList();

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

            // Show confirmation dialog
            bool? dialogResult = await ShowConfirmItemsDialogAsync(
                "install",
                selectedApps.Select(a => a.Name),
                selectedApps.Count
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
                    int totalSelected = selectedApps.Count;

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
                                        DetailedMessage = $"Successfully installed app: {app.Name}",
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
                                    operationResult.ErrorMessage ?? $"Failed to install {app.Name}";

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
                                ex.Message.Contains("internet", StringComparison.OrdinalIgnoreCase) ||
                                ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                                ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                                ex.Message.Contains("pipeline has been stopped", StringComparison.OrdinalIgnoreCase);

                            if (isConnectivityIssue && CurrentCancellationReason == CancellationReason.None)
                            {
                                // Set the cancellation reason to connectivity issue
                                await HandleCancellationAsync(true); // Connectivity-related cancellation
                            }
                            
                            progress.Report(
                                new TaskProgressDetail
                                {
                                    Progress = (currentItem * 100.0) / totalSelected,
                                    StatusText = $"Error installing {app.Name}",
                                    DetailedMessage = $"Error installing {app.Name}: {ex.Message}",
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
                                        { "IsConnectivityIssue", isConnectivityIssue.ToString() },
                                    },
                                }
                            );
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
                    if (failedItems != null && failedItems.Any(item => item.Contains("cancelled by user", StringComparison.OrdinalIgnoreCase)))
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
