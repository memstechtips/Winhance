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
    public partial class ExternalAppsViewModel : SearchableViewModel<ExternalApp>
    {
        [ObservableProperty]
        private bool _isInitialized = false;
        
        private readonly IAppInstallationService? _appInstallationService;
        private readonly IAppService? _appDiscoveryService;
        private readonly IConfigurationService? _configurationService;

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
            IConfigurationService configurationService
        )
            : base(progressService, searchService, packageManager)
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
                    _categories.Add(new ExternalAppsCategoryViewModel(categoryName, appsCollection));
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

            IsLoading = true;
            StatusText = $"Installing {app.Name}...";

            try
            {
                var progress = _progressService.CreateDetailedProgress();
                await _appInstallationService.InstallAppAsync(app.ToAppInfo(), progress);
                app.IsInstalled = true;
                StatusText = $"Successfully installed {app.Name}";
                
                // Show success dialog
                CustomDialog.ShowInformation(
                    "Installation Complete",
                    $"{app.Name} was successfully installed.",
                    new[] { app.Name },
                    "The application has been installed successfully."
                );
            }
            catch (System.Exception ex)
            {
                StatusText = $"Error installing {app.Name}: {ex.Message}";
                
                // Show error dialog
                CustomDialog.ShowInformation(
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
            
            // Get selected apps that are not already installed
            var selectedApps = Items.Where(a => a.IsSelected && !a.IsInstalled).ToList();
            
            if (!selectedApps.Any())
            {
                StatusText = "No apps selected for installation";
                CustomDialog.ShowInformation(
                    "No Apps Selected",
                    "No apps were selected for installation.",
                    new[] { "Please select at least one app to install." },
                    "Check the boxes next to the apps you want to install and try again."
                );
                return;
            }
            
            // Show confirmation dialog
            bool? dialogResult = CustomDialog.ShowConfirmation(
                "Confirm Installation",
                "The following apps will be installed:",
                selectedApps.Select(a => a.Name),
                $"Do you want to install {selectedApps.Count} app(s)?"
            );
            
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
                int totalSelected = selectedApps.Count;
                
                foreach (var app in selectedApps)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    currentItem++;
                    progress.Report(new TaskProgressDetail
                    {
                        Progress = (currentItem * 100.0) / totalSelected,
                        StatusText = $"Installing {app.Name}... ({currentItem}/{totalSelected})",
                        DetailedMessage = $"Installing app: {app.Name}",
                        AdditionalInfo = new Dictionary<string, string>
                        {
                            { "AppName", app.Name },
                            { "PackageName", app.PackageName },
                            { "OperationType", "Install" },
                            { "ItemNumber", currentItem.ToString() },
                            { "TotalItems", totalSelected.ToString() }
                        }
                    });
                    
                    try
                    {
                        await _appInstallationService.InstallAppAsync(app.ToAppInfo(), progress, cancellationToken);
                        app.IsInstalled = true;
                        successCount++;
                        
                        progress.Report(new TaskProgressDetail
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
                                { "TotalItems", totalSelected.ToString() }
                            }
                        });
                    }
                    catch (System.Exception ex)
                    {
                        progress.Report(new TaskProgressDetail
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
                                { "TotalItems", totalSelected.ToString() }
                            }
                        });
                    }
                }
                
                // Final report
                progress.Report(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = $"Successfully installed {successCount} of {totalSelected} apps",
                    DetailedMessage = $"Task completed: {successCount} of {totalSelected} apps installed successfully",
                    LogLevel = successCount == totalSelected ? LogLevel.Success : LogLevel.Warning,
                    AdditionalInfo = new Dictionary<string, string>
                    {
                        { "OperationType", "Install" },
                        { "OperationStatus", successCount == totalSelected ? "Complete" : "PartialSuccess" },
                        { "SuccessCount", successCount.ToString() },
                        { "TotalItems", totalSelected.ToString() },
                        { "SuccessRate", $"{(successCount * 100.0 / totalSelected):F1}%" },
                        { "CompletionTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
                    }
                });
                
                // Collect success and failure information for the result dialog
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
                
                // Show result dialog
                CustomDialog.ShowInformation(
                    "Installation Results",
                    successCount > 0 
                        ? $"The following apps were successfully installed:"
                        : "Installation operation completed.",
                    successItems.Any() ? successItems : new[] { "No apps were successfully installed." },
                    successCount == totalSelected
                        ? "All apps were successfully installed."
                        : $"Some apps could not be installed. {successCount} of {totalSelected} apps were installed successfully."
                );
                
                return successCount;
            }, $"Installing {selectedApps.Count} apps", false);
        }

        public async Task LoadAppsAndCheckInstallationStatusAsync()
        {
            if (IsInitialized)
            {
                System.Diagnostics.Debug.WriteLine("ExternalAppsViewModel already initialized, skipping LoadAppsAndCheckInstallationStatusAsync");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine("Starting ExternalAppsViewModel LoadAppsAndCheckInstallationStatusAsync");
            await LoadItemsAsync();
            await CheckInstallationStatusAsync();
            
            // Mark as initialized after loading is complete
            IsInitialized = true;
            System.Diagnostics.Debug.WriteLine("Completed ExternalAppsViewModel LoadAppsAndCheckInstallationStatusAsync");
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
    }
}
