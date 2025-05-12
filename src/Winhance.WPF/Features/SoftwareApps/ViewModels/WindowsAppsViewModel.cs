using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.WPF.Features.SoftwareApps.Models;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.Common.ViewModels;
using System.Windows;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    public partial class WindowsAppsViewModel : SearchableViewModel<WindowsApp>
    {
        private readonly IAppInstallationService? _appInstallationService;
        private readonly ICapabilityInstallationService? _capabilityService;
        private readonly IFeatureInstallationService? _featureService;
        private readonly IFeatureRemovalService? _featureRemovalService;
        private readonly IAppService? _appDiscoveryService;
        private readonly IConfigurationService? _configurationService;
        private readonly IScriptDetectionService? _scriptDetectionService;

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
        public System.Collections.ObjectModel.ObservableCollection<WindowsApp> WindowsApps { get; } = new();
        
        // Windows Capabilities
        public System.Collections.ObjectModel.ObservableCollection<WindowsApp> Capabilities { get; } = new();
        
        // Optional Features
        public System.Collections.ObjectModel.ObservableCollection<WindowsApp> OptionalFeatures { get; } = new();

        public WindowsAppsViewModel(
            ITaskProgressService progressService,
            ISearchService searchService,
            IPackageManager packageManager,
            IAppInstallationService appInstallationService,
            ICapabilityInstallationService capabilityService,
            IFeatureInstallationService featureService,
            IFeatureRemovalService featureRemovalService,
            IConfigurationService configurationService,
            IScriptDetectionService scriptDetectionService)
            : base(progressService, searchService, packageManager)
        {
            _appInstallationService = appInstallationService;
            _capabilityService = capabilityService;
            _featureService = featureService;
            _featureRemovalService = featureRemovalService;
            _appDiscoveryService = packageManager?.AppDiscoveryService;
            _configurationService = configurationService;
            _scriptDetectionService = scriptDetectionService;
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
            if (_appDiscoveryService == null) return;

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
                
                StatusText = $"Loaded {WindowsApps.Count} apps, {Capabilities.Count} capabilities, and {OptionalFeatures.Count} features";
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
            if (_appDiscoveryService == null) return;

            IsLoading = true;
            StatusText = "Checking installation status...";
            
            try
            {
                var statusResults = await _appDiscoveryService
                    .GetBatchInstallStatusAsync(Items.Select(a => a.PackageName));

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
        
        private void SortCollection(System.Collections.ObjectModel.ObservableCollection<WindowsApp> collection)
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
            IEnumerable<WindowsApp> skippedApps = null)
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
                footerText = $"Note: The following {skippedApps.Count()} item(s) cannot be {GetPastTense(operationType)} and will be skipped:\n";
                footerText += string.Join(", ", skippedNames);
                footerText += $"\n\nDo you want to continue with the remaining {selectedApps.Count()} item(s)?";
            }
            
            // Show the confirmation dialog
            return CustomDialog.ShowConfirmation(title, headerText, appNames, footerText);
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
            IEnumerable<string> failedItems = null,
            IEnumerable<string> skippedItems = null)
        {
            string title = $"{operationType} Results";
            string headerText = successCount > 0 
                ? $"The following items were successfully {GetPastTense(operationType)}:"
                : $"{operationType} operation completed.";
            
            // Create list of items for the dialog
            var resultItems = new List<string>();
            
            // Add successful items directly to the list
            if (successItems != null && successItems.Any())
            {
                foreach (var item in successItems)
                {
                    resultItems.Add(item);
                }
            }
            else
            {
                resultItems.Add($"No items were successfully {GetPastTense(operationType)}.");
            }
            
            // Add skipped items if any
            if (skippedItems != null && skippedItems.Any())
            {
                resultItems.Add($"Skipped items: {skippedItems.Count()}");
                foreach (var item in skippedItems.Take(5))
                {
                    resultItems.Add($"  - {item}");
                }
                if (skippedItems.Count() > 5)
                {
                    resultItems.Add($"  - ... and {skippedItems.Count() - 5} more");
                }
            }
            
            // Add failed items if any
            if (failedItems != null && failedItems.Any())
            {
                resultItems.Add($"Failed items: {failedItems.Count()}");
                foreach (var item in failedItems.Take(5))
                {
                    resultItems.Add($"  - {item}");
                }
                if (failedItems.Count() > 5)
                {
                    resultItems.Add($"  - ... and {failedItems.Count() - 5} more");
                }
            }
            
            // Create footer text
            string footerText = successCount == totalCount
                ? $"All items were successfully {GetPastTense(operationType)}."
                : $"Some items could not be {GetPastTense(operationType)}. Check the log for details.";
            
            // Show the information dialog
            CustomDialog.ShowInformation(title, headerText, resultItems, footerText);
        }
        
        #endregion

        public async Task LoadAppsAndCheckInstallationStatusAsync()
        {
            if (IsInitialized)
            {
                System.Diagnostics.Debug.WriteLine("WindowsAppsViewModel already initialized, skipping LoadAppsAndCheckInstallationStatusAsync");
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
                return;
            
            // Check if the app can be reinstalled
            if (!app.CanBeReinstalled)
            {
                CustomDialog.ShowInformation(
                    "Cannot Install Item",
                    $"{app.Name} cannot be reinstalled.",
                    new[] { app.Name },
                    "This item cannot be reinstalled once removed.");
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

            try
            {
                var progress = _progressService.CreateDetailedProgress();
                await _appInstallationService.InstallAppAsync(app.ToAppInfo(), progress);
                app.IsInstalled = true;
                StatusText = $"Successfully installed {app.Name}";
                
                // Show result dialog
                ShowOperationResultDialog(
                    "Install",
                    1,
                    1,
                    new[] { app.Name });
            }
            catch (System.Exception ex)
            {
                StatusText = $"Error installing {app.Name}: {ex.Message}";
                
                // Show error dialog
                ShowOperationResultDialog(
                    "Install",
                    0,
                    1,
                    Array.Empty<string>(),
                    new[] { $"{app.Name}: {ex.Message}" });
            }
            finally
            {
                IsLoading = false;
            }
        }
        
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
                        default:
                            success = await _packageManager.RemoveSpecialAppAsync(app.SpecialHandlerType);
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
                    bool isCapability = app.AppType == Winhance.WPF.Features.SoftwareApps.Models.WindowsAppType.Capability;
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
                    success ? Array.Empty<string>() : new[] { app.Name });
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
                    new[] { $"{app.Name}: {ex.Message}" });
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
            if (_packageManager == null) return;
            
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
            await ExecuteWithProgressAsync(async (progress, cancellationToken) =>
            {
                int successCount = 0;
                int currentItem = 0;
                
                // Process standard Windows apps
                foreach (var app in selectedApps)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    currentItem++;
                    progress.Report(new TaskProgressDetail
                    {
                        Progress = (currentItem * 100.0) / totalSelected,
                        StatusText = $"Removing app {app.Name}... ({currentItem}/{totalSelected})",
                        DetailedMessage = $"Removing Windows App: {app.Name}",
                        AdditionalInfo = new Dictionary<string, string>
                        {
                            { "AppType", app.AppType.ToString() },
                            { "PackageName", app.PackageName },
                            { "IsSystemProtected", app.IsSystemProtected.ToString() },
                            { "CanBeReinstalled", app.CanBeReinstalled.ToString() },
                            { "OperationType", "Remove" },
                            { "ItemNumber", currentItem.ToString() },
                            { "TotalItems", totalSelected.ToString() }
                        }
                    });
                    
                    try
                    {
                        // Check if this is a special app that requires special handling
                        if (app.IsSpecialHandler && !string.IsNullOrEmpty(app.SpecialHandlerType))
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
                                default:
                                    success = await _packageManager.RemoveSpecialAppAsync(app.SpecialHandlerType);
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
                            bool success = await _packageManager.RemoveAppAsync(app.PackageName, false);
                            if (success)
                            {
                                app.IsInstalled = false;
                                successCount++;
                            }
                        }
                        
                        progress.Report(new TaskProgressDetail
                        {
                            Progress = (currentItem * 100.0) / totalSelected,
                            StatusText = $"Successfully removed {app.Name}",
                            DetailedMessage = $"Successfully removed Windows App: {app.Name}",
                            LogLevel = LogLevel.Success,
                            AdditionalInfo = new Dictionary<string, string>
                            {
                                { "AppType", app.AppType.ToString() },
                                { "PackageName", app.PackageName },
                                { "OperationType", "Remove" },
                                { "OperationStatus", "Success" },
                                { "ItemNumber", currentItem.ToString() },
                                { "TotalItems", totalSelected.ToString() }
                            }
                        });
                    }
                    catch (System.Exception ex)
                    {
                        progress.Report(new TaskProgressDetail
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
                                { "TotalItems", totalSelected.ToString() }
                            }
                        });
                    }
                }
                
                // Process capabilities 
                foreach (var capability in selectedCapabilities)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    currentItem++;
                    progress.Report(new TaskProgressDetail
                    {
                        Progress = (currentItem * 100.0) / totalSelected,
                        StatusText = $"Removing capability {capability.Name}... ({currentItem}/{totalSelected})",
                        DetailedMessage = $"Removing Windows Capability: {capability.Name}",
                        AdditionalInfo = new Dictionary<string, string>
                        {
                            { "AppType", capability.AppType.ToString() },
                            { "PackageName", capability.PackageName },
                            { "IsSystemProtected", capability.IsSystemProtected.ToString() },
                            { "CanBeReinstalled", capability.CanBeReinstalled.ToString() },
                            { "OperationType", "Remove" },
                            { "ItemNumber", currentItem.ToString() },
                            { "TotalItems", totalSelected.ToString() }
                        }
                    });
                    
                    try 
                    {
                        bool success = await _packageManager.RemoveAppAsync(capability.PackageName, true);
                        
                        if (success)
                        {
                            capability.IsInstalled = false;
                            successCount++;
                            
                            progress.Report(new TaskProgressDetail
                            {
                                Progress = (currentItem * 100.0) / totalSelected,
                                StatusText = $"Successfully removed capability {capability.Name}",
                                DetailedMessage = $"Successfully removed Windows Capability: {capability.Name}",
                                LogLevel = LogLevel.Success,
                                AdditionalInfo = new Dictionary<string, string>
                                {
                                    { "AppType", capability.AppType.ToString() },
                                    { "PackageName", capability.PackageName },
                                    { "OperationType", "Remove" },
                                    { "OperationStatus", "Success" },
                                    { "ItemNumber", currentItem.ToString() },
                                    { "TotalItems", totalSelected.ToString() }
                                }
                            });
                        }
                        else
                        {
                            progress.Report(new TaskProgressDetail
                            {
                                Progress = (currentItem * 100.0) / totalSelected,
                                StatusText = $"Failed to remove capability {capability.Name}",
                                DetailedMessage = $"Failed to remove Windows Capability: {capability.Name}",
                                LogLevel = LogLevel.Error,
                                AdditionalInfo = new Dictionary<string, string>
                                {
                                    { "AppType", capability.AppType.ToString() },
                                    { "PackageName", capability.PackageName },
                                    { "OperationType", "Remove" },
                                    { "OperationStatus", "Error" },
                                    { "ItemNumber", currentItem.ToString() },
                                    { "TotalItems", totalSelected.ToString() }
                                }
                            });
                        }
                    }
                    catch (System.Exception ex)
                    {
                        progress.Report(new TaskProgressDetail
                        {
                            Progress = (currentItem * 100.0) / totalSelected,
                            StatusText = $"Error removing capability {capability.Name}",
                            DetailedMessage = $"Error removing capability {capability.Name}: {ex.Message}",
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
                                { "TotalItems", totalSelected.ToString() }
                            }
                        });
                    }
                }
                
                // Process optional features
                foreach (var feature in selectedFeatures)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    currentItem++;
                    progress.Report(new TaskProgressDetail
                    {
                        Progress = (currentItem * 100.0) / totalSelected,
                        StatusText = $"Removing feature {feature.Name}... ({currentItem}/{totalSelected})",
                        DetailedMessage = $"Removing Windows Feature: {feature.Name}",
                        AdditionalInfo = new Dictionary<string, string>
                        {
                            { "AppType", feature.AppType.ToString() },
                            { "PackageName", feature.PackageName },
                            { "IsSystemProtected", feature.IsSystemProtected.ToString() },
                            { "CanBeReinstalled", feature.CanBeReinstalled.ToString() },
                            { "OperationType", "Remove" },
                            { "ItemNumber", currentItem.ToString() },
                            { "TotalItems", totalSelected.ToString() }
                        }
                    });
                    
                    try 
                    {
                        if (_featureRemovalService != null)
                        {
                            bool success = await _featureRemovalService.RemoveFeatureAsync(feature.ToFeatureInfo());
                            if (success)
                            {
                                feature.IsInstalled = false;
                                successCount++;
                                
                                progress.Report(new TaskProgressDetail
                                {
                                    Progress = (currentItem * 100.0) / totalSelected,
                                    StatusText = $"Successfully removed feature {feature.Name}",
                                    DetailedMessage = $"Successfully removed Windows Feature: {feature.Name}",
                                    LogLevel = LogLevel.Success,
                                    AdditionalInfo = new Dictionary<string, string>
                                    {
                                        { "AppType", feature.AppType.ToString() },
                                        { "PackageName", feature.PackageName },
                                        { "OperationType", "Remove" },
                                        { "OperationStatus", "Success" },
                                        { "ItemNumber", currentItem.ToString() },
                                        { "TotalItems", totalSelected.ToString() }
                                    }
                                });
                            }
                            else
                            {
                                progress.Report(new TaskProgressDetail
                                {
                                    Progress = (currentItem * 100.0) / totalSelected,
                                    StatusText = $"Failed to remove feature {feature.Name}",
                                    DetailedMessage = $"Failed to remove Windows Feature: {feature.Name}",
                                    LogLevel = LogLevel.Error,
                                    AdditionalInfo = new Dictionary<string, string>
                                    {
                                        { "AppType", feature.AppType.ToString() },
                                        { "PackageName", feature.PackageName },
                                        { "OperationType", "Remove" },
                                        { "OperationStatus", "Error" },
                                        { "ItemNumber", currentItem.ToString() },
                                        { "TotalItems", totalSelected.ToString() }
                                    }
                                });
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        progress.Report(new TaskProgressDetail
                        {
                            Progress = (currentItem * 100.0) / totalSelected,
                            StatusText = $"Error removing feature {feature.Name}",
                            DetailedMessage = $"Error removing feature {feature.Name}: {ex.Message}",
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
                                { "TotalItems", totalSelected.ToString() }
                            }
                        });
                    }
                }
                
                // Final report
                progress.Report(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = $"Successfully removed {successCount} of {totalSelected} items",
                    DetailedMessage = $"Task completed: {successCount} of {totalSelected} items removed successfully",
                    LogLevel = successCount == totalSelected ? LogLevel.Success : LogLevel.Warning,
                    AdditionalInfo = new Dictionary<string, string>
                    {
                        { "OperationType", "Remove" },
                        { "OperationStatus", successCount == totalSelected ? "Complete" : "PartialSuccess" },
                        { "SuccessCount", successCount.ToString() },
                        { "TotalItems", totalSelected.ToString() },
                        { "SuccessRate", $"{(successCount * 100.0 / totalSelected):F1}%" },
                        { "StandardAppsCount", selectedApps.Count.ToString() },
                        { "CapabilitiesCount", selectedCapabilities.Count.ToString() },
                        { "FeaturesCount", selectedFeatures.Count.ToString() },
                        { "CompletionTime", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
                    }
                });
                
                // Refresh the UI
                SortCollections();
                
                // Collect success and failure information for the result dialog
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
                    failedItems);
                
                return successCount;
            }, $"Removing {totalSelected} items", false);
        }
        
        [RelayCommand]
        public async Task InstallApps()
        {
            if (_packageManager == null || _appInstallationService == null ||
                _capabilityService == null || _featureService == null) return;
            
            // Get all selected items
            var selectedApps = WindowsApps.Where(a => a.IsSelected).ToList();
            var selectedCapabilities = Capabilities.Where(a => a.IsSelected).ToList();
            var selectedFeatures = OptionalFeatures.Where(a => a.IsSelected).ToList();
            
            // Check if anything is selected at all
            int totalSelected = selectedApps.Count + selectedCapabilities.Count + selectedFeatures.Count;
            
            if (totalSelected == 0)
            {
                StatusText = "No items selected for installation";
                CustomDialog.ShowInformation(
                    "No Items Selected",
                    "No items were selected for installation.",
                    new[] { "Please select at least one item to install." },
                    "Check the boxes next to the items you want to install and try again."
                );
                return;
            }
            
            // Identify items that cannot be reinstalled
            var nonReinstallableApps = selectedApps.Where(a => !a.CanBeReinstalled).ToList();
            var nonReinstallableCapabilities = selectedCapabilities.Where(c => !c.CanBeReinstalled).ToList();
            var nonReinstallableFeatures = selectedFeatures.Where(f => !f.CanBeReinstalled).ToList();
            
            var allNonReinstallableItems = nonReinstallableApps
                .Concat(nonReinstallableCapabilities)
                .Concat(nonReinstallableFeatures)
                .ToList();
            
            // Remove non-reinstallable items from the selected items
            var installableApps = selectedApps.Except(nonReinstallableApps).ToList();
            var installableCapabilities = selectedCapabilities.Except(nonReinstallableCapabilities).ToList();
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
                    CustomDialog.ShowInformation(
                        "Cannot Install Items",
                        "None of the selected items can be reinstalled.",
                        allNonReinstallableItems.Select(i => i.Name),
                        "These items cannot be reinstalled once removed. Please select different items.");
                }
                else
                {
                    StatusText = "No items selected for installation";
                }
                return;
            }
            
            // Show confirmation dialog, including information about skipped items
            bool? dialogResult = ShowOperationConfirmationDialog(
                "Install",
                allInstallableItems,
                allNonReinstallableItems);
            
            // If user didn't confirm, exit
            if (dialogResult != true)
            {
                return;
            }
            
            // Use the ExecuteWithProgressAsync method from BaseViewModel to handle progress reporting
            await ExecuteWithProgressAsync(async (progress, cancellationToken) =>
            {
                int successCount = 0;
                int currentItem = 0;
                
                // Process standard Windows apps
                foreach (var app in installableApps)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    currentItem++;
                    progress.Report(new TaskProgressDetail
                    {
                        Progress = (currentItem * 100.0) / totalSelected,
                        StatusText = $"Installing app {app.Name}... ({currentItem}/{totalSelected})",
                        DetailedMessage = $"Installing Windows App: {app.Name}"
                    });
                    
                    try 
                    {
                        await _appInstallationService.InstallAppAsync(app.ToAppInfo(), progress, cancellationToken);
                        app.IsInstalled = true;
                        successCount++;
                        
                        progress.Report(new TaskProgressDetail
                        {
                            DetailedMessage = $"Successfully installed {app.Name}",
                            LogLevel = LogLevel.Success
                        });
                    }
                    catch (System.Exception ex)
                    {
                        progress.Report(new TaskProgressDetail
                        {
                            DetailedMessage = $"Error installing {app.Name}: {ex.Message}",
                            LogLevel = LogLevel.Error
                        });
                    }
                }
                
                // Process capabilities
                foreach (var capability in installableCapabilities)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    currentItem++;
                    progress.Report(new TaskProgressDetail
                    {
                        Progress = (currentItem * 100.0) / totalSelected,
                        StatusText = $"Installing capability {capability.Name}... ({currentItem}/{totalSelected})",
                        DetailedMessage = $"Installing Windows Capability: {capability.Name}"
                    });
                    
                    try 
                    {
                        await _capabilityService.InstallCapabilityAsync(capability.ToCapabilityInfo(), progress, cancellationToken);
                        capability.IsInstalled = true;
                        successCount++;
                        
                        progress.Report(new TaskProgressDetail
                        {
                            DetailedMessage = $"Successfully installed capability {capability.Name}",
                            LogLevel = LogLevel.Success
                        });
                    }
                    catch (System.Exception ex)
                    {
                        progress.Report(new TaskProgressDetail
                        {
                            DetailedMessage = $"Error installing capability {capability.Name}: {ex.Message}",
                            LogLevel = LogLevel.Error
                        });
                    }
                }
                
                // Process optional features
                foreach (var feature in installableFeatures)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    currentItem++;
                    progress.Report(new TaskProgressDetail
                    {
                        Progress = (currentItem * 100.0) / totalSelected,
                        StatusText = $"Installing feature {feature.Name}... ({currentItem}/{totalSelected})",
                        DetailedMessage = $"Installing Windows Feature: {feature.Name}"
                    });
                    
                    try 
                    {
                        await _featureService.InstallFeatureAsync(feature.ToFeatureInfo(), progress, cancellationToken);
                        feature.IsInstalled = true;
                        successCount++;
                        
                        progress.Report(new TaskProgressDetail
                        {
                            DetailedMessage = $"Successfully installed feature {feature.Name}",
                            LogLevel = LogLevel.Success
                        });
                    }
                    catch (System.Exception ex)
                    {
                        progress.Report(new TaskProgressDetail
                        {
                            DetailedMessage = $"Error installing feature {feature.Name}: {ex.Message}",
                            LogLevel = LogLevel.Error
                        });
                    }
                }
                
                // Final report
                progress.Report(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = $"Successfully installed {successCount} of {totalSelected} items",
                    DetailedMessage = $"Task completed: {successCount} of {totalSelected} items installed successfully",
                    LogLevel = successCount == totalSelected ? LogLevel.Success : LogLevel.Warning
                });
                
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
                    skippedItems);
                
                return successCount;
            }, $"Installing {totalSelected} items", false);
        }
    }
}
