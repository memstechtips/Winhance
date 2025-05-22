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
    public partial class WindowsAppsViewModel : BaseInstallationViewModel<WindowsApp>
    {
        private readonly IAppInstallationService _appInstallationService;
        private readonly ICapabilityInstallationService _capabilityService;
        private readonly IFeatureInstallationService _featureService;
        private readonly IFeatureRemovalService _featureRemovalService;
        private readonly IAppService _appDiscoveryService;
        private readonly IConfigurationService _configurationService;
        private readonly IScriptDetectionService _scriptDetectionService;
        private readonly IInternetConnectivityService _connectivityService;
        private readonly IAppInstallationCoordinatorService _appInstallationCoordinatorService;

        [ObservableProperty]
        private string _statusText = "Ready";

        [ObservableProperty]
        private bool _isRemovingApps;

        [ObservableProperty]
        private ObservableCollection<ScriptInfo> _activeScripts = new();

        // Flag to prevent duplicate initialization
        [ObservableProperty]
        private bool _isInitialized = false;

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
            Services.SoftwareAppsDialogService dialogService
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
            _capabilityService = capabilityService;
            _featureService = featureService;
            _featureRemovalService = featureRemovalService;
            _appDiscoveryService = packageManager?.AppDiscoveryService;
            _configurationService = configurationService;
            _scriptDetectionService = scriptDetectionService;
            _connectivityService = connectivityService;
            _appInstallationCoordinatorService = appInstallationCoordinatorService;
        }

        // SaveConfig and ImportConfig methods removed as part of unified configuration cleanup

        /// <summary>
        /// Applies the current search text to filter items.
        /// </summary>
        protected override void ApplySearch()
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

        public override async Task LoadItemsAsync()
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
                }

                foreach (var capability in capabilities)
                {
                    var windowsApp = WindowsApp.FromCapabilityInfo(capability);
                    // AppType is already set in FromCapabilityInfo
                    Items.Add(windowsApp);
                    Capabilities.Add(windowsApp);
                }

                foreach (var feature in features)
                {
                    var windowsApp = WindowsApp.FromFeatureInfo(feature);
                    // AppType is already set in FromFeatureInfo
                    Items.Add(windowsApp);
                    OptionalFeatures.Add(windowsApp);
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
            if (string.IsNullOrEmpty(operationType))
                return string.Empty;

            return operationType.Equals("Remove", StringComparison.OrdinalIgnoreCase)
                ? "removed"
                : $"{operationType.ToLower()}ed";
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
            // Use the base class implementation that uses the dialog service
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
            // Use the base class implementation that uses the dialog service
            base.ShowOperationResultDialog(
                operationType,
                successCount,
                totalCount,
                successItems,
                failedItems,
                skippedItems
            );
        }

        #endregion

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

            // Set Select All to true by default when view loads
            IsAllSelected = true;

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

            // Set Select All to true by default when view loads
            IsAllSelected = true;
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
            if (app == null || _packageManager == null)
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
                    // Regular app removal
                    bool isCapability =
                        app.AppType
                        == Winhance.WPF.Features.SoftwareApps.Models.WindowsAppType.Capability;
                    success = await _packageManager.RemoveAppAsync(app.PackageName, isCapability);
                    if (success)
                    {
                        app.IsInstalled = false;
                        StatusText = $"Successfully removed {app.Name}";
                    }
                    else
                    {
                        StatusText = $"Failed to remove {app.Name}";
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
                }
            }
        }

        [RelayCommand]
        public async Task RemoveApps()
        {
            if (_packageManager == null)
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

                    // Process standard Windows apps
                    foreach (var app in selectedApps)
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
                                    $"Removing app {app.Name}... ({currentItem}/{totalSelected})",
                                DetailedMessage = $"Removing Windows App: {app.Name}",
                                AdditionalInfo = new Dictionary<string, string>
                                {
                                    { "AppType", app.AppType.ToString() },
                                    { "PackageName", app.PackageName },
                                    { "IsSystemProtected", app.IsSystemProtected.ToString() },
                                    { "CanBeReinstalled", app.CanBeReinstalled.ToString() },
                                    { "OperationType", "Remove" },
                                    { "ItemNumber", currentItem.ToString() },
                                    { "TotalItems", totalSelected.ToString() },
                                },
                            }
                        );

                        try
                        {
                            // Check if this is a special app that requires special handling
                            if (
                                app.IsSpecialHandler
                                && !string.IsNullOrEmpty(app.SpecialHandlerType)
                            )
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
                                }
                            }
                            else
                            {
                                // Regular app removal
                                bool success = await _packageManager.RemoveAppAsync(
                                    app.PackageName,
                                    false
                                );
                                if (success)
                                {
                                    app.IsInstalled = false;
                                    successCount++;
                                }
                            }

                            progress.Report(
                                new TaskProgressDetail
                                {
                                    Progress = (currentItem * 100.0) / totalSelected,
                                    StatusText = $"Successfully removed {app.Name}",
                                    DetailedMessage =
                                        $"Successfully removed Windows App: {app.Name}",
                                    LogLevel = LogLevel.Success,
                                    AdditionalInfo = new Dictionary<string, string>
                                    {
                                        { "AppType", app.AppType.ToString() },
                                        { "PackageName", app.PackageName },
                                        { "OperationType", "Remove" },
                                        { "OperationStatus", "Success" },
                                        { "ItemNumber", currentItem.ToString() },
                                        { "TotalItems", totalSelected.ToString() },
                                    },
                                }
                            );
                        }
                        catch (System.Exception ex)
                        {
                            progress.Report(
                                new TaskProgressDetail
                                {
                                    Progress = (currentItem * 100.0) / totalSelected,
                                    StatusText = $"Error removing {app.Name}",
                                    DetailedMessage = $"Error removing {app.Name}: {ex.Message}",
                                    LogLevel = LogLevel.Error,
                                    AdditionalInfo = new Dictionary<string, string>
                                    {
                                        { "AppType", app.AppType.ToString() },
                                        { "PackageName", app.PackageName },
                                        { "OperationType", "Remove" },
                                        { "OperationStatus", "Error" },
                                        { "ErrorMessage", ex.Message },
                                        { "ErrorType", ex.GetType().Name },
                                        { "ItemNumber", currentItem.ToString() },
                                        { "TotalItems", totalSelected.ToString() },
                                    },
                                }
                            );
                        }
                    }

                    // Process capabilities
                    foreach (var capability in selectedCapabilities)
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
                                    $"Removing capability {capability.Name}... ({currentItem}/{totalSelected})",
                                DetailedMessage = $"Removing Windows Capability: {capability.Name}",
                                AdditionalInfo = new Dictionary<string, string>
                                {
                                    { "AppType", capability.AppType.ToString() },
                                    { "PackageName", capability.PackageName },
                                    {
                                        "IsSystemProtected",
                                        capability.IsSystemProtected.ToString()
                                    },
                                    { "CanBeReinstalled", capability.CanBeReinstalled.ToString() },
                                    { "OperationType", "Remove" },
                                    { "ItemNumber", currentItem.ToString() },
                                    { "TotalItems", totalSelected.ToString() },
                                },
                            }
                        );

                        try
                        {
                            bool success = await _packageManager.RemoveAppAsync(
                                capability.PackageName,
                                true
                            );

                            if (success)
                            {
                                capability.IsInstalled = false;
                                successCount++;

                                progress.Report(
                                    new TaskProgressDetail
                                    {
                                        Progress = (currentItem * 100.0) / totalSelected,
                                        StatusText =
                                            $"Successfully removed capability {capability.Name}",
                                        DetailedMessage =
                                            $"Successfully removed Windows Capability: {capability.Name}",
                                        LogLevel = LogLevel.Success,
                                        AdditionalInfo = new Dictionary<string, string>
                                        {
                                            { "AppType", capability.AppType.ToString() },
                                            { "PackageName", capability.PackageName },
                                            { "OperationType", "Remove" },
                                            { "OperationStatus", "Success" },
                                            { "ItemNumber", currentItem.ToString() },
                                            { "TotalItems", totalSelected.ToString() },
                                        },
                                    }
                                );
                            }
                            else
                            {
                                progress.Report(
                                    new TaskProgressDetail
                                    {
                                        Progress = (currentItem * 100.0) / totalSelected,
                                        StatusText =
                                            $"Failed to remove capability {capability.Name}",
                                        DetailedMessage =
                                            $"Failed to remove Windows Capability: {capability.Name}",
                                        LogLevel = LogLevel.Error,
                                        AdditionalInfo = new Dictionary<string, string>
                                        {
                                            { "AppType", capability.AppType.ToString() },
                                            { "PackageName", capability.PackageName },
                                            { "OperationType", "Remove" },
                                            { "OperationStatus", "Error" },
                                            { "ItemNumber", currentItem.ToString() },
                                            { "TotalItems", totalSelected.ToString() },
                                        },
                                    }
                                );
                            }
                        }
                        catch (System.Exception ex)
                        {
                            progress.Report(
                                new TaskProgressDetail
                                {
                                    Progress = (currentItem * 100.0) / totalSelected,
                                    StatusText = $"Error removing capability {capability.Name}",
                                    DetailedMessage =
                                        $"Error removing capability {capability.Name}: {ex.Message}",
                                    LogLevel = LogLevel.Error,
                                    AdditionalInfo = new Dictionary<string, string>
                                    {
                                        { "AppType", capability.AppType.ToString() },
                                        { "PackageName", capability.PackageName },
                                        { "OperationType", "Remove" },
                                        { "OperationStatus", "Error" },
                                        { "ErrorMessage", ex.Message },
                                        { "ErrorType", ex.GetType().Name },
                                        { "ItemNumber", currentItem.ToString() },
                                        { "TotalItems", totalSelected.ToString() },
                                    },
                                }
                            );
                        }
                    }

                    // Process optional features
                    foreach (var feature in selectedFeatures)
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
                                    $"Removing feature {feature.Name}... ({currentItem}/{totalSelected})",
                                DetailedMessage = $"Removing Windows Feature: {feature.Name}",
                                AdditionalInfo = new Dictionary<string, string>
                                {
                                    { "AppType", feature.AppType.ToString() },
                                    { "PackageName", feature.PackageName },
                                    { "IsSystemProtected", feature.IsSystemProtected.ToString() },
                                    { "CanBeReinstalled", feature.CanBeReinstalled.ToString() },
                                    { "OperationType", "Remove" },
                                    { "ItemNumber", currentItem.ToString() },
                                    { "TotalItems", totalSelected.ToString() },
                                },
                            }
                        );

                        try
                        {
                            if (_featureRemovalService != null)
                            {
                                bool success = await _featureRemovalService.RemoveFeatureAsync(
                                    feature.ToFeatureInfo()
                                );
                                if (success)
                                {
                                    feature.IsInstalled = false;
                                    successCount++;

                                    progress.Report(
                                        new TaskProgressDetail
                                        {
                                            Progress = (currentItem * 100.0) / totalSelected,
                                            StatusText =
                                                $"Successfully removed feature {feature.Name}",
                                            DetailedMessage =
                                                $"Successfully removed Windows Feature: {feature.Name}",
                                            LogLevel = LogLevel.Success,
                                            AdditionalInfo = new Dictionary<string, string>
                                            {
                                                { "AppType", feature.AppType.ToString() },
                                                { "PackageName", feature.PackageName },
                                                { "OperationType", "Remove" },
                                                { "OperationStatus", "Success" },
                                                { "ItemNumber", currentItem.ToString() },
                                                { "TotalItems", totalSelected.ToString() },
                                            },
                                        }
                                    );
                                }
                                else
                                {
                                    progress.Report(
                                        new TaskProgressDetail
                                        {
                                            Progress = (currentItem * 100.0) / totalSelected,
                                            StatusText = $"Failed to remove feature {feature.Name}",
                                            DetailedMessage =
                                                $"Failed to remove Windows Feature: {feature.Name}",
                                            LogLevel = LogLevel.Error,
                                            AdditionalInfo = new Dictionary<string, string>
                                            {
                                                { "AppType", feature.AppType.ToString() },
                                                { "PackageName", feature.PackageName },
                                                { "OperationType", "Remove" },
                                                { "OperationStatus", "Error" },
                                                { "ItemNumber", currentItem.ToString() },
                                                { "TotalItems", totalSelected.ToString() },
                                            },
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
                                    Progress = (currentItem * 100.0) / totalSelected,
                                    StatusText = $"Error removing feature {feature.Name}",
                                    DetailedMessage =
                                        $"Error removing feature {feature.Name}: {ex.Message}",
                                    LogLevel = LogLevel.Error,
                                    AdditionalInfo = new Dictionary<string, string>
                                    {
                                        { "AppType", feature.AppType.ToString() },
                                        { "PackageName", feature.PackageName },
                                        { "OperationType", "Remove" },
                                        { "OperationStatus", "Error" },
                                        { "ErrorMessage", ex.Message },
                                        { "ErrorType", ex.GetType().Name },
                                        { "ItemNumber", currentItem.ToString() },
                                        { "TotalItems", totalSelected.ToString() },
                                    },
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
                                $"Successfully removed {successCount} of {totalSelected} items",
                            DetailedMessage =
                                $"Task completed: {successCount} of {totalSelected} items removed successfully",
                            LogLevel =
                                successCount == totalSelected ? LogLevel.Success : LogLevel.Warning,
                            AdditionalInfo = new Dictionary<string, string>
                            {
                                { "OperationType", "Remove" },
                                {
                                    "OperationStatus",
                                    successCount == totalSelected ? "Complete" : "PartialSuccess"
                                },
                                { "SuccessCount", successCount.ToString() },
                                { "TotalItems", totalSelected.ToString() },
                                { "SuccessRate", $"{(successCount * 100.0 / totalSelected):F1}%" },
                                { "StandardAppsCount", selectedApps.Count.ToString() },
                                { "CapabilitiesCount", selectedCapabilities.Count.ToString() },
                                { "FeaturesCount", selectedFeatures.Count.ToString() },
                                {
                                    "CompletionTime",
                                    System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                },
                            },
                        }
                    );

                    // Refresh the UI
                    SortCollections();

                    // Check if the operation was cancelled by the user or due to connectivity issues
                    if (CurrentCancellationReason == CancellationReason.UserCancelled)
                    {
                        // Log the user cancellation
                        System.Diagnostics.Debug.WriteLine("[DEBUG] Installation cancelled by user - showing user cancellation dialog");
                        
                        // Show the cancellation dialog
                        await ShowCancellationDialogAsync(true, false);
                        
                        // Reset cancellation reason after showing dialog
                        CurrentCancellationReason = CancellationReason.None;
                        return successCount;
                    }
                    else if (CurrentCancellationReason == CancellationReason.InternetConnectivityLost)
                    {
                        // Log the connectivity loss
                        System.Diagnostics.Debug.WriteLine("[DEBUG] Installation stopped due to connectivity loss - showing connectivity loss dialog");
                        
                        // Show the connectivity loss dialog
                        await ShowCancellationDialogAsync(false, true);
                        
                        // Reset cancellation reason after showing dialog
                        CurrentCancellationReason = CancellationReason.None;
                        return successCount;
                    }
                    
                    // Only proceed with normal completion reporting if not cancelled
                    // For normal completion (not cancelled), collect success and failure information
                    var successItems = new List<string>();
                    var failedItems = new List<string>();

                    // Add successful items to the list
                    foreach (var app in selectedApps.Where(a => !a.IsInstalled))
                    {
                        successItems.Add(app.Name);
                    }
                    foreach (var capability in selectedCapabilities.Where(c => !c.IsInstalled))
                    {
                        successItems.Add(capability.Name);
                    }
                    foreach (var feature in selectedFeatures.Where(f => !f.IsInstalled))
                    {
                        successItems.Add(feature.Name);
                    }

                    // Add failed items to the list
                    foreach (var app in selectedApps.Where(a => a.IsInstalled))
                    {
                        failedItems.Add(app.Name);
                    }
                    foreach (var capability in selectedCapabilities.Where(c => c.IsInstalled))
                    {
                        failedItems.Add(capability.Name);
                    }
                    foreach (var feature in selectedFeatures.Where(f => f.IsInstalled))
                    {
                        failedItems.Add(feature.Name);
                    }

                    // Show result dialog
                    ShowOperationResultDialog(
                        "Remove",
                        successCount,
                        totalSelected,
                        successItems,
                        failedItems
                    );

                    return successCount;
                },
                $"Removing {totalSelected} items",
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
                            await _capabilityService.InstallCapabilityAsync(
                                capability.ToCapabilityInfo(),
                                progress,
                                cancellationToken
                            );
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
                            await _featureService.InstallFeatureAsync(
                                feature.ToFeatureInfo(),
                                progress,
                                cancellationToken
                            );
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
        protected override string GetAppName(WindowsApp app)
        {
            return app.Name;
        }

        /// <summary>
        /// Converts a Windows app to an AppInfo object.
        /// </summary>
        /// <param name="app">The Windows app to convert.</param>
        /// <returns>The AppInfo object.</returns>
        protected override AppInfo ToAppInfo(WindowsApp app)
        {
            return app.ToAppInfo();
        }

        /// <summary>
        /// Gets the selected Windows apps.
        /// </summary>
        /// <returns>The selected Windows apps.</returns>
        protected override IEnumerable<WindowsApp> GetSelectedApps()
        {
            return Items.Where(a => a.IsSelected);
        }

        /// <summary>
        /// Sets the installation status of a Windows app.
        /// </summary>
        /// <param name="app">The Windows app.</param>
        /// <param name="isInstalled">Whether the app is installed.</param>
        protected override void SetInstallationStatus(WindowsApp app, bool isInstalled)
        {
            app.IsInstalled = isInstalled;
        }

        #endregion
    }
}
