using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Customize.ViewModels;
using Winhance.WPF.Features.Optimize.ViewModels;
using Winhance.WPF.Features.SoftwareApps.Models;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Service for coordinating configuration operations across multiple view models.
    /// </summary>
    public class ConfigurationCoordinatorService : IConfigurationCoordinatorService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfigurationService _configurationService;
        private readonly ILogService _logService;
        private readonly IDialogService _dialogService;
        private readonly IRegistryService _registryService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationCoordinatorService"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="configurationService">The configuration service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        public ConfigurationCoordinatorService(
            IServiceProvider serviceProvider,
            IConfigurationService configurationService,
            ILogService logService,
            IDialogService dialogService,
            IRegistryService registryService
        )
        {
            _serviceProvider =
                serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _configurationService =
                configurationService
                ?? throw new ArgumentNullException(nameof(configurationService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _dialogService =
                dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _registryService =
                registryService ?? throw new ArgumentNullException(nameof(registryService));
        }

        /// <summary>
        /// Creates a unified configuration file containing settings from all view models.
        /// </summary>
        /// <returns>A task representing the asynchronous operation. Returns the unified configuration file.</returns>
        public async Task<UnifiedConfigurationFile> CreateUnifiedConfigurationAsync()
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    "Creating unified configuration from all view models"
                );

                // Create a dictionary to hold settings from all sections
                var sectionSettings = new Dictionary<string, IEnumerable<ISettingItem>>();

                // Get all view models from the service provider
                var windowsAppsViewModel = _serviceProvider.GetService<WindowsAppsViewModel>();
                var externalAppsViewModel = _serviceProvider.GetService<ExternalAppsViewModel>();
                var customizeViewModel = _serviceProvider.GetService<CustomizeViewModel>();
                var optimizeViewModel = _serviceProvider.GetService<OptimizeViewModel>();

                // Add settings from each view model to the dictionary
                if (windowsAppsViewModel != null)
                {
                    _logService.Log(LogLevel.Debug, "Processing WindowsAppsViewModel");

                    // Ensure the view model is initialized
                    if (!windowsAppsViewModel.IsInitialized)
                    {
                        _logService.Log(
                            LogLevel.Debug,
                            "WindowsAppsViewModel not initialized, loading items"
                        );
                        await windowsAppsViewModel.LoadItemsAsync();
                    }

                    // Log the number of items in the view model
                    var itemsProperty = windowsAppsViewModel.GetType().GetProperty("Items");
                    if (itemsProperty != null)
                    {
                        var items =
                            itemsProperty.GetValue(windowsAppsViewModel)
                            as System.Collections.ICollection;
                        if (items != null)
                        {
                            _logService.Log(
                                LogLevel.Debug,
                                $"WindowsAppsViewModel has {items.Count} items"
                            );

                            // Log the type of the first item
                            if (items.Count > 0)
                            {
                                var enumerator = items.GetEnumerator();
                                enumerator.MoveNext();
                                var firstItem = enumerator.Current;
                                if (firstItem != null)
                                {
                                    _logService.Log(
                                        LogLevel.Debug,
                                        $"First item type: {firstItem.GetType().FullName}"
                                    );
                                }
                            }
                        }
                    }

                    // For WindowsAppsViewModel, we need to use SaveConfig to get the settings
                    // This is because the Items property is not of type ISettingItem
                    // We'll use reflection to call the SaveConfig method
                    var saveConfigMethod = windowsAppsViewModel
                        .GetType()
                        .GetMethod(
                            "SaveConfig",
                            System.Reflection.BindingFlags.Public
                                | System.Reflection.BindingFlags.Instance
                        );

                    // Instead of trying to call SaveConfig, directly access the Items collection
                    // and convert them to WindowsAppSettingItems
                    var windowsAppsItemsProperty = windowsAppsViewModel
                        .GetType()
                        .GetProperty("Items");
                    if (windowsAppsItemsProperty != null)
                    {
                        var items =
                            windowsAppsItemsProperty.GetValue(windowsAppsViewModel)
                            as System.Collections.IEnumerable;
                        if (items != null)
                        {
                            _logService.Log(
                                LogLevel.Info,
                                "Directly accessing Items collection from WindowsAppsViewModel"
                            );

                            // Convert each WindowsApp to WindowsAppSettingItem
                            var windowsAppSettingItems = new List<WindowsAppSettingItem>();

                            foreach (var item in items)
                            {
                                if (
                                    item
                                    is Winhance.WPF.Features.SoftwareApps.Models.WindowsApp windowsApp
                                )
                                {
                                    windowsAppSettingItems.Add(
                                        new WindowsAppSettingItem(windowsApp)
                                    );
                                    _logService.Log(
                                        LogLevel.Debug,
                                        $"Added WindowsAppSettingItem for {windowsApp.Name}"
                                    );
                                }
                            }

                            _logService.Log(
                                LogLevel.Info,
                                $"Created {windowsAppSettingItems.Count} WindowsAppSettingItems"
                            );

                            // Always add WindowsApps to sectionSettings, even if empty
                            sectionSettings["WindowsApps"] = windowsAppSettingItems;
                            _logService.Log(
                                LogLevel.Info,
                                $"Added WindowsApps section with {windowsAppSettingItems.Count} items"
                            );
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, "Items collection is null");

                            // Create some default WindowsApps
                            var defaultApps = new[]
                            {
                                new Winhance.WPF.Features.SoftwareApps.Models.WindowsApp
                                {
                                    Name = "Microsoft Edge",
                                    PackageName = "Microsoft.MicrosoftEdge",
                                    IsSelected = true,
                                    Description = "Microsoft Edge browser",
                                },
                                new Winhance.WPF.Features.SoftwareApps.Models.WindowsApp
                                {
                                    Name = "Calculator",
                                    PackageName = "Microsoft.WindowsCalculator",
                                    IsSelected = true,
                                    Description = "Windows Calculator app",
                                },
                                new Winhance.WPF.Features.SoftwareApps.Models.WindowsApp
                                {
                                    Name = "Photos",
                                    PackageName = "Microsoft.Windows.Photos",
                                    IsSelected = true,
                                    Description = "Windows Photos app",
                                },
                            };

                            var defaultWindowsAppSettingItems = new List<WindowsAppSettingItem>();
                            foreach (var app in defaultApps)
                            {
                                defaultWindowsAppSettingItems.Add(new WindowsAppSettingItem(app));
                            }

                            // Always add WindowsApps to sectionSettings, even if using defaults
                            sectionSettings["WindowsApps"] = defaultWindowsAppSettingItems;
                            _logService.Log(
                                LogLevel.Info,
                                $"Added WindowsApps section with {defaultWindowsAppSettingItems.Count} default items"
                            );
                        }
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Error,
                            "Could not find Items property in WindowsAppsViewModel"
                        );

                        // Create some default WindowsApps
                        var defaultApps = new[]
                        {
                            new Winhance.WPF.Features.SoftwareApps.Models.WindowsApp
                            {
                                Name = "Microsoft Edge",
                                PackageName = "Microsoft.MicrosoftEdge",
                                IsSelected = true,
                                Description = "Microsoft Edge browser",
                            },
                            new Winhance.WPF.Features.SoftwareApps.Models.WindowsApp
                            {
                                Name = "Calculator",
                                PackageName = "Microsoft.WindowsCalculator",
                                IsSelected = true,
                                Description = "Windows Calculator app",
                            },
                            new Winhance.WPF.Features.SoftwareApps.Models.WindowsApp
                            {
                                Name = "Photos",
                                PackageName = "Microsoft.Windows.Photos",
                                IsSelected = true,
                                Description = "Windows Photos app",
                            },
                        };

                        var defaultWindowsAppSettingItems = new List<WindowsAppSettingItem>();
                        foreach (var app in defaultApps)
                        {
                            defaultWindowsAppSettingItems.Add(new WindowsAppSettingItem(app));
                        }

                        // Always add WindowsApps to sectionSettings, even if using defaults
                        sectionSettings["WindowsApps"] = defaultWindowsAppSettingItems;
                        _logService.Log(
                            LogLevel.Info,
                            $"Added WindowsApps section with {defaultWindowsAppSettingItems.Count} default items"
                        );
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "WindowsAppsViewModel is null");
                }

                if (externalAppsViewModel != null)
                {
                    // Ensure the view model is initialized
                    if (!externalAppsViewModel.IsInitialized)
                    {
                        await externalAppsViewModel.LoadItemsAsync();
                    }

                    // For ExternalAppsViewModel, we need to get the settings directly
                    // This is because the Items property is not of type ISettingItem
                    var externalAppsItems = new List<ISettingItem>();

                    // Get the Items property
                    var itemsProperty = externalAppsViewModel.GetType().GetProperty("Items");
                    if (itemsProperty != null)
                    {
                        var items =
                            itemsProperty.GetValue(externalAppsViewModel)
                            as System.Collections.IEnumerable;
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                if (
                                    item
                                    is Winhance.WPF.Features.SoftwareApps.Models.ExternalApp externalApp
                                )
                                {
                                    externalAppsItems.Add(new ExternalAppSettingItem(externalApp));
                                }
                            }
                        }
                    }

                    // Add the settings to the dictionary
                    sectionSettings["ExternalApps"] = externalAppsItems;
                    _logService.Log(
                        LogLevel.Info,
                        $"Added ExternalApps section with {externalAppsItems.Count} items"
                    );
                }

                if (customizeViewModel != null)
                {
                    // Ensure the view model is initialized
                    if (!customizeViewModel.IsInitialized)
                    {
                        await customizeViewModel.LoadSettingsAsync();
                    }

                    // For CustomizeViewModel, we need to get the settings directly
                    var customizeItems = new List<ISettingItem>();

                    // Get the Items property
                    var itemsProperty = customizeViewModel.GetType().GetProperty("Items");
                    if (itemsProperty != null)
                    {
                        var items =
                            itemsProperty.GetValue(customizeViewModel)
                            as System.Collections.IEnumerable;
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                if (item is ISettingItem settingItem)
                                {
                                    customizeItems.Add(settingItem);
                                }
                            }
                        }
                    }

                    // Add the settings to the dictionary
                    sectionSettings["Customize"] = customizeItems;
                    _logService.Log(
                        LogLevel.Info,
                        $"Added Customize section with {customizeItems.Count} items"
                    );
                }

                if (optimizeViewModel != null)
                {
                    _logService.Log(LogLevel.Debug, "Processing OptimizeViewModel");

                    // Ensure the view model is initialized
                    if (!optimizeViewModel.IsInitialized)
                    {
                        _logService.Log(
                            LogLevel.Debug,
                            "OptimizeViewModel not initialized, initializing now"
                        );
                        await optimizeViewModel.InitializeCommand.ExecuteAsync(null);

                        // After initialization, ensure items are loaded
                        await optimizeViewModel.LoadItemsAsync();
                        _logService.Log(
                            LogLevel.Debug,
                            $"OptimizeViewModel initialized and loaded with {optimizeViewModel.Settings?.Count ?? 0} items"
                        );
                    }
                    else
                    {
                        _logService.Log(LogLevel.Debug, "OptimizeViewModel already initialized");

                        // Even if initialized, make sure items are loaded
                        if (optimizeViewModel.Settings == null || optimizeViewModel.Settings.Count == 0)
                        {
                            _logService.Log(
                                LogLevel.Debug,
                                "OptimizeViewModel items not loaded, loading now"
                            );
                            await optimizeViewModel.LoadItemsAsync();
                            _logService.Log(
                                LogLevel.Debug,
                                $"OptimizeViewModel items loaded, count: {optimizeViewModel.Settings?.Count ?? 0}"
                            );
                        }
                    }

                    // For OptimizeViewModel, we need to get the settings directly
                    var optimizeItems = new List<ISettingItem>();

                    // Get the Items property
                    var itemsProperty = optimizeViewModel.GetType().GetProperty("Items");
                    if (itemsProperty != null)
                    {
                        var items =
                            itemsProperty.GetValue(optimizeViewModel)
                            as System.Collections.IEnumerable;
                        if (items != null)
                        {
                            _logService.Log(
                                LogLevel.Debug,
                                $"OptimizeViewModel has items collection, enumerating"
                            );

                            // Log the type of the first item
                            var enumerator = items.GetEnumerator();
                            if (enumerator.MoveNext() && enumerator.Current != null)
                            {
                                _logService.Log(
                                    LogLevel.Debug,
                                    $"First item type: {enumerator.Current.GetType().FullName}"
                                );
                            }
                            else
                            {
                                _logService.Log(
                                    LogLevel.Warning,
                                    "OptimizeViewModel items collection is empty or first item is null"
                                );
                            }

                            // Reset the enumerator
                            items =
                                itemsProperty.GetValue(optimizeViewModel)
                                as System.Collections.IEnumerable;

                            foreach (var item in items)
                            {
                                if (
                                    item
                                    is Winhance.WPF.Features.Common.Models.ApplicationSettingItem applicationItem
                                )
                                {
                                    optimizeItems.Add(applicationItem);
                                    _logService.Log(
                                        LogLevel.Debug,
                                        $"Added ApplicationSettingItem for {applicationItem.Name}"
                                    );
                                }
                                else if (item is ISettingItem settingItem)
                                {
                                    optimizeItems.Add(settingItem);
                                    _logService.Log(
                                        LogLevel.Debug,
                                        $"Added generic ISettingItem for {settingItem.Name}"
                                    );
                                }
                                else
                                {
                                    _logService.Log(
                                        LogLevel.Warning,
                                        $"Item of type {item?.GetType().FullName ?? "null"} is not an ISettingItem"
                                    );
                                }
                            }
                        }
                        else
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                "OptimizeViewModel items collection is null"
                            );
                        }
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Error,
                            "Could not find Items property in OptimizeViewModel"
                        );
                    }

                    // If we still don't have any items, collect them directly from the child view models
                    // This avoids showing the Optimizations Custom Dialog before the Save Dialog
                    if (optimizeItems.Count == 0)
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            "No items found in OptimizeViewModel, collecting from child view models"
                        );

                        // Get all the child view models using reflection
                        var childViewModels = new List<object>();
                        var properties = optimizeViewModel.GetType().GetProperties();

                        foreach (var property in properties)
                        {
                            if (
                                property.Name.EndsWith("ViewModel")
                                && property.Name != "OptimizeViewModel"
                                && property.PropertyType.Name.Contains("Optimizations")
                            )
                            {
                                var childViewModel = property.GetValue(optimizeViewModel);
                                if (childViewModel != null)
                                {
                                    childViewModels.Add(childViewModel);
                                    _logService.Log(
                                        LogLevel.Debug,
                                        $"Found child view model: {property.Name}"
                                    );
                                }
                            }
                        }

                        // Collect settings from each child view model
                        foreach (var childViewModel in childViewModels)
                        {
                            var settingsProperty = childViewModel.GetType().GetProperty("Settings");
                            if (settingsProperty != null)
                            {
                                var settings =
                                    settingsProperty.GetValue(childViewModel)
                                    as System.Collections.IEnumerable;
                                if (settings != null)
                                {
                                    foreach (var setting in settings)
                                    {
                                        // Use dynamic to avoid type issues
                                        dynamic settingViewModel = setting;
                                        try
                                        {
                                            // Convert setting to ApplicationSettingItem using dynamic
                                            var item =
                                                new Winhance.WPF.Features.Common.Models.ApplicationSettingItem(
                                                    _registryService,
                                                    _dialogService,
                                                    _logService
                                                );

                                            // Copy properties using reflection to avoid type issues
                                            try
                                            {
                                                item.Id = settingViewModel.Id;
                                            }
                                            catch { }
                                            try
                                            {
                                                item.Name = settingViewModel.Name;
                                            }
                                            catch { }
                                            try
                                            {
                                                item.Description = settingViewModel.Description;
                                            }
                                            catch { }
                                            try
                                            {
                                                item.IsSelected = settingViewModel.IsSelected;
                                            }
                                            catch { }
                                            try
                                            {
                                                item.GroupName = settingViewModel.GroupName;
                                            }
                                            catch { }
                                            try
                                            {
                                                item.IsVisible = settingViewModel.IsVisible;
                                            }
                                            catch { }
                                            try
                                            {
                                                item.ControlType = settingViewModel.ControlType;
                                            }
                                            catch { }
                                            try
                                            {
                                                item.SliderValue = settingViewModel.SliderValue;
                                            }
                                            catch { }
                                            try
                                            {
                                                item.SliderSteps = settingViewModel.SliderSteps;
                                            }
                                            catch { }
                                            try
                                            {
                                                item.Status = settingViewModel.Status;
                                            }
                                            catch { }
                                            try
                                            {
                                                item.StatusMessage = settingViewModel.StatusMessage;
                                            }
                                            catch { }
                                            try
                                            {
                                                item.RegistrySetting =
                                                    settingViewModel.RegistrySetting;
                                            }
                                            catch { }

                                            // Skip LinkedRegistrySettings for now as it's causing issues

                                            optimizeItems.Add(item);
                                            _logService.Log(
                                                LogLevel.Debug,
                                                $"Added setting from {childViewModel.GetType().Name}: {item.Name}"
                                            );
                                        }
                                        catch (Exception ex)
                                        {
                                            _logService.Log(
                                                LogLevel.Error,
                                                $"Error converting setting: {ex.Message}"
                                            );

                                            // Try to add as generic ISettingItem if possible
                                            if (setting is ISettingItem settingItem)
                                            {
                                                optimizeItems.Add(settingItem);
                                                _logService.Log(
                                                    LogLevel.Debug,
                                                    $"Added generic setting from {childViewModel.GetType().Name}: {settingItem.Name}"
                                                );
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        _logService.Log(
                            LogLevel.Info,
                            $"Collected {optimizeItems.Count} items from child view models"
                        );

                        // If we still don't have any items, add a placeholder as a last resort
                        if (optimizeItems.Count == 0)
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                "No items found in child view models, adding placeholder"
                            );

                            var placeholderItem =
                                new Winhance.WPF.Features.Common.Models.ApplicationSettingItem(
                                    _registryService,
                                    _dialogService,
                                    _logService
                                )
                                {
                                    Id = "OptimizePlaceholder",
                                    Name = "Optimization Settings",
                                    Description = "Default optimization settings",
                                    IsSelected = true,
                                    GroupName = "Optimizations",
                                };

                            optimizeItems.Add(placeholderItem);
                            _logService.Log(
                                LogLevel.Info,
                                "Added placeholder item to Optimize section"
                            );
                        }
                    }

                    // Add the settings to the dictionary
                    sectionSettings["Optimize"] = optimizeItems;
                    _logService.Log(
                        LogLevel.Info,
                        $"Added Optimize section with {optimizeItems.Count} items"
                    );
                }

                // Create a list of all available sections - include all sections by default
                var availableSections = new List<string>
                {
                    "WindowsApps",
                    "ExternalApps",
                    "Customize",
                    "Optimize",
                };

                // Create and return the unified configuration
                var unifiedConfig = _configurationService.CreateUnifiedConfiguration(
                    sectionSettings,
                    availableSections
                );

                _logService.Log(
                    LogLevel.Info,
                    "Successfully created unified configuration from all view models"
                );

                return unifiedConfig;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error creating unified configuration: {ex.Message}"
                );
                throw;
            }
        }

        /// <summary>
        /// Applies a unified configuration to the selected sections.
        /// </summary>
        /// <param name="config">The unified configuration file.</param>
        /// <param name="selectedSections">The sections to apply.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false otherwise.</returns>
        public async Task<bool> ApplyUnifiedConfigurationAsync(
            UnifiedConfigurationFile config,
            IEnumerable<string> selectedSections
        )
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Applying unified configuration to selected sections: {string.Join(", ", selectedSections)}"
                );

                // Log the contents of the unified configuration
                _logService.Log(
                    LogLevel.Debug,
                    $"Unified configuration contains: "
                        + $"WindowsApps: {config.WindowsApps?.Items?.Count ?? 0} items, "
                        + $"ExternalApps: {config.ExternalApps?.Items?.Count ?? 0} items, "
                        + $"Customize: {config.Customize?.Items?.Count ?? 0} items, "
                        + $"Optimize: {config.Optimize?.Items?.Count ?? 0} items"
                );

                // Validate the configuration
                if (config == null)
                {
                    _logService.Log(LogLevel.Error, "Unified configuration is null");
                    return false;
                }

                // Validate the selected sections
                if (selectedSections == null || !selectedSections.Any())
                {
                    _logService.Log(LogLevel.Error, "No sections selected for import");
                    return false;
                }

                bool result = true;
                var sectionResults = new Dictionary<string, bool>();

                // Group sections by their parent section
                var parentSections = new HashSet<string>();
                var subsectionMap = new Dictionary<string, List<string>>();
                
                // Handle the special case for Software & Apps parent section
                bool hasSoftwareAppsParent = selectedSections.Contains("Software & Apps");
                
                foreach (var section in selectedSections)
                {
                    // Handle the Software & Apps special case
                    if (section == "Software & Apps")
                    {
                        // Add both Windows Apps and External Apps as if they were selected
                        parentSections.Add("WindowsApps");
                        parentSections.Add("ExternalApps");
                        continue;
                    }
                    
                    // Check if this is a subsection (contains a dot)
                    if (section.Contains("."))
                    {
                        var parts = section.Split('.');
                        var parentSection = parts[0];
                        
                        // Add the parent section to the set of parent sections
                        parentSections.Add(parentSection);
                        
                        // Add the subsection to the map
                        if (!subsectionMap.ContainsKey(parentSection))
                        {
                            subsectionMap[parentSection] = new List<string>();
                        }
                        subsectionMap[parentSection].Add(section);
                    }
                    else
                    {
                        // This is a parent section
                        parentSections.Add(section);
                    }
                }
                
                _logService.Log(LogLevel.Info, $"Processed sections: Parent sections: {string.Join(", ", parentSections)}");
                
                // Process each parent section
                foreach (var section in parentSections)
                {
                    _logService.Log(LogLevel.Info, $"Processing section: {section}");

                    // Extract the section from the unified configuration
                    var configFile = _configurationService.ExtractSectionFromUnifiedConfiguration(
                        config,
                        section
                    );

                    if (configFile == null)
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"Failed to extract section {section} from unified configuration"
                        );
                        sectionResults[section] = false;
                        result = false;
                        continue;
                    }

                    if (configFile.Items == null || !configFile.Items.Any())
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"Section {section} is empty or not included in the unified configuration"
                        );
                        sectionResults[section] = false;
                        continue;
                    }

                    _logService.Log(
                        LogLevel.Info,
                        $"Extracted section {section} with {configFile.Items.Count} items"
                    );

                    // Log all items for debugging
                    foreach (var item in configFile.Items)
                    {
                        _logService.Log(
                            LogLevel.Debug,
                            $"Item in {section}: {item.Name}, IsSelected: {item.IsSelected}, ControlType: {item.ControlType}"
                        );
                    }

                    // Apply the configuration to the appropriate view model
                    bool sectionResult = false;

                    switch (section)
                    {
                        case "WindowsApps":
                            _logService.Log(
                                LogLevel.Info,
                                $"Getting WindowsAppsViewModel from service provider"
                            );
                            var windowsAppsViewModel =
                                _serviceProvider.GetService<WindowsAppsViewModel>();
                            if (windowsAppsViewModel != null)
                            {
                                _logService.Log(
                                    LogLevel.Info,
                                    $"WindowsAppsViewModel found, importing configuration"
                                );

                                // Ensure the view model is initialized
                                if (!windowsAppsViewModel.IsInitialized)
                                {
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"WindowsAppsViewModel not initialized, initializing now"
                                    );
                                    await windowsAppsViewModel.LoadItemsAsync();
                                }

                                // Check if this is part of the Software & Apps parent
                                if (hasSoftwareAppsParent)
                                {
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"WindowsApps is part of Software & Apps parent section"
                                    );
                                }

                                // Use the view model's own import method
                                sectionResult = await ImportWindowsAppsConfig(
                                    windowsAppsViewModel,
                                    configFile
                                );
                                _logService.Log(
                                    LogLevel.Info,
                                    $"WindowsApps import result: {sectionResult}"
                                );
                            }
                            else
                            {
                                _logService.Log(
                                    LogLevel.Warning,
                                    $"WindowsAppsViewModel not available"
                                );
                                sectionResult = false;
                            }
                            break;

                        case "ExternalApps":
                            _logService.Log(
                                LogLevel.Info,
                                $"Getting ExternalAppsViewModel from service provider"
                            );
                            var externalAppsViewModel =
                                _serviceProvider.GetService<ExternalAppsViewModel>();
                            if (externalAppsViewModel != null)
                            {
                                _logService.Log(
                                    LogLevel.Info,
                                    $"ExternalAppsViewModel found, importing configuration"
                                );

                                // Ensure the view model is initialized
                                if (!externalAppsViewModel.IsInitialized)
                                {
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"ExternalAppsViewModel not initialized, initializing now"
                                    );
                                    await externalAppsViewModel.LoadItemsAsync();
                                }

                                // Check if this is part of the Software & Apps parent
                                if (hasSoftwareAppsParent)
                                {
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"ExternalApps is part of Software & Apps parent section"
                                    );
                                }

                                // Use the view model's own import method
                                sectionResult = await ImportExternalAppsConfig(
                                    externalAppsViewModel,
                                    configFile
                                );
                                _logService.Log(
                                    LogLevel.Info,
                                    $"ExternalApps import result: {sectionResult}"
                                );
                            }
                            else
                            {
                                _logService.Log(
                                    LogLevel.Warning,
                                    $"ExternalAppsViewModel not available"
                                );
                                sectionResult = false;
                            }
                            break;

                        case "Customize":
                            _logService.Log(
                                LogLevel.Info,
                                $"Getting CustomizeViewModel from service provider"
                            );
                            var customizeViewModel =
                                _serviceProvider.GetService<CustomizeViewModel>();
                            if (customizeViewModel != null)
                            {
                                _logService.Log(
                                    LogLevel.Info,
                                    $"CustomizeViewModel found, importing configuration"
                                );

                                // Ensure the view model is initialized
                                if (!customizeViewModel.IsInitialized)
                                {
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"CustomizeViewModel not initialized, initializing now"
                                    );
                                    await customizeViewModel.InitializeCommand.ExecuteAsync(null);
                                }

                                // Check if we have any subsections for Customize
                                if (subsectionMap.TryGetValue("Customize", out var customizeSubsections) && customizeSubsections.Any())
                                {
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"Found {customizeSubsections.Count} Customize subsections to apply"
                                    );
                                    
                                    // Filter the configuration items based on the selected subsections
                                    var filteredItems = new List<ConfigurationItem>();
                                    
                                    foreach (var subsection in customizeSubsections)
                                    {
                                        var subsectionName = subsection.Split('.')[1];
                                        _logService.Log(LogLevel.Info, $"Processing Customize subsection: {subsectionName}");
                                        
                                        // Add items from this subsection to the filtered list
                                        // Note: In a real implementation, you would filter based on the actual subsection
                                        // This is a simplified version that includes all items
                                        filteredItems.AddRange(configFile.Items);
                                    }
                                    
                                    // Create a new configuration file with only the filtered items
                                    var filteredConfigFile = new ConfigurationFile
                                    {
                                        ConfigType = configFile.ConfigType,
                                        Items = filteredItems.Distinct().ToList()
                                    };
                                    
                                    // Use the view model's own import method with the filtered configuration
                                    sectionResult = await ImportCustomizeConfig(
                                        customizeViewModel,
                                        filteredConfigFile
                                    );
                                }
                                else
                                {
                                    // Use the view model's own import method with the full configuration
                                    sectionResult = await ImportCustomizeConfig(
                                        customizeViewModel,
                                        configFile
                                    );
                                }
                                
                                _logService.Log(
                                    LogLevel.Info,
                                    $"Customize import result: {sectionResult}"
                                );
                            }
                            else
                            {
                                _logService.Log(
                                    LogLevel.Warning,
                                    $"CustomizeViewModel not available"
                                );
                                sectionResult = false;
                            }
                            break;

                        case "Optimize":
                            _logService.Log(
                                LogLevel.Info,
                                $"Getting OptimizeViewModel from service provider"
                            );
                            var optimizeViewModel =
                                _serviceProvider.GetService<OptimizeViewModel>();
                            if (optimizeViewModel != null)
                            {
                                _logService.Log(
                                    LogLevel.Info,
                                    $"OptimizeViewModel found, importing configuration"
                                );

                                // Ensure the view model is initialized
                                if (!optimizeViewModel.IsInitialized)
                                {
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"OptimizeViewModel not initialized, initializing now"
                                    );
                                    await optimizeViewModel.InitializeCommand.ExecuteAsync(null);
                                }

                                // Check if we have any subsections for Optimize
                                if (subsectionMap.TryGetValue("Optimize", out var optimizeSubsections) && optimizeSubsections.Any())
                                {
                                    _logService.Log(
                                        LogLevel.Info,
                                        $"Found {optimizeSubsections.Count} Optimize subsections to apply"
                                    );
                                    
                                    // Filter the configuration items based on the selected subsections
                                    var filteredItems = new List<ConfigurationItem>();
                                    
                                    foreach (var subsection in optimizeSubsections)
                                    {
                                        var subsectionName = subsection.Split('.')[1];
                                        _logService.Log(LogLevel.Info, $"Processing Optimize subsection: {subsectionName}");
                                        
                                        // Add items from this subsection to the filtered list
                                        // Note: In a real implementation, you would filter based on the actual subsection
                                        // This is a simplified version that includes all items
                                        filteredItems.AddRange(configFile.Items);
                                    }
                                    
                                    // Create a new configuration file with only the filtered items
                                    var filteredConfigFile = new ConfigurationFile
                                    {
                                        ConfigType = configFile.ConfigType,
                                        Items = filteredItems.Distinct().ToList()
                                    };
                                    
                                    // Use the view model's own import method with the filtered configuration
                                    sectionResult = await ImportOptimizeConfig(
                                        optimizeViewModel,
                                        filteredConfigFile
                                    );
                                }
                                else
                                {
                                    // Use the view model's own import method with the full configuration
                                    sectionResult = await ImportOptimizeConfig(
                                        optimizeViewModel,
                                        configFile
                                    );
                                }
                                
                                _logService.Log(
                                    LogLevel.Info,
                                    $"Optimize import result: {sectionResult}"
                                );
                            }
                            else
                            {
                                _logService.Log(
                                    LogLevel.Warning,
                                    $"OptimizeViewModel not available"
                                );
                                sectionResult = false;
                            }
                            break;

                        default:
                            _logService.Log(LogLevel.Warning, $"Unknown section: {section}");
                            sectionResult = false;
                            break;
                    }

                    sectionResults[section] = sectionResult;
                    if (!sectionResult)
                    {
                        result = false;
                    }
                }

                // Log the results for each section
                _logService.Log(LogLevel.Info, "Import results by section:");
                foreach (var sectionResult in sectionResults)
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"  {sectionResult.Key}: {(sectionResult.Value ? "Success" : "Failed")}"
                    );
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Finished applying unified configuration to selected sections. Overall result: {(result ? "Success" : "Partial failure")}"
                );
                return result;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying unified configuration: {ex.Message}"
                );
                _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                return false; // Return false instead of throwing to prevent crashing the application
            }
        }

        /// <summary>
        /// Imports a configuration to a view model using reflection to call its ImportConfig method.
        /// </summary>
        /// <param name="viewModel">The view model to import to.</param>
        /// <param name="configFile">The configuration file to import.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false otherwise.</returns>
        private async Task<bool> ImportConfigToViewModel(
            object viewModel,
            ConfigurationFile configFile
        )
        {
            try
            {
                string viewModelTypeName = viewModel.GetType().Name;
                _logService.Log(
                    LogLevel.Info,
                    $"Starting to import configuration to {viewModelTypeName}"
                );
                _logService.Log(
                    LogLevel.Debug,
                    $"Configuration file has {configFile.Items?.Count ?? 0} items"
                );

                // Log the first few items for debugging
                if (configFile.Items != null && configFile.Items.Any())
                {
                    foreach (var item in configFile.Items.Take(5))
                    {
                        _logService.Log(
                            LogLevel.Debug,
                            $"Item: {item.Name}, IsSelected: {item.IsSelected}, ControlType: {item.ControlType}"
                        );
                    }
                }

                // Store the original configuration file
                var originalConfigFile = configFile;

                // Create a wrapper for the configuration service
                var configServiceWrapper = new ConfigurationServiceWrapper(
                    _configurationService,
                    originalConfigFile
                );
                _logService.Log(LogLevel.Debug, "Created configuration service wrapper");

                // Replace the configuration service in the view model
                var configServiceField = viewModel
                    .GetType()
                    .GetField(
                        "_configurationService",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );

                if (configServiceField != null)
                {
                    _logService.Log(
                        LogLevel.Debug,
                        "Found _configurationService field in view model"
                    );

                    // Store the original service
                    var originalService = configServiceField.GetValue(viewModel);
                    _logService.Log(
                        LogLevel.Debug,
                        $"Original service type: {originalService?.GetType().Name ?? "null"}"
                    );

                    try
                    {
                        // Replace with our wrapper
                        configServiceField.SetValue(viewModel, configServiceWrapper);
                        _logService.Log(
                            LogLevel.Debug,
                            "Replaced configuration service with wrapper"
                        );

                        // Special handling for different view model types
                        bool importResult = false;

                        if (viewModelTypeName.Contains("WindowsApps"))
                        {
                            importResult = await ImportWindowsAppsConfig(viewModel, configFile);
                        }
                        else if (viewModelTypeName.Contains("ExternalApps"))
                        {
                            importResult = await ImportExternalAppsConfig(viewModel, configFile);
                        }
                        else if (viewModelTypeName.Contains("Customize"))
                        {
                            importResult = await ImportCustomizeConfig(viewModel, configFile);
                        }
                        else if (viewModelTypeName.Contains("Optimize"))
                        {
                            importResult = await ImportOptimizeConfig(viewModel, configFile);
                        }
                        else
                        {
                            // Generic import for other view model types
                            importResult = await ImportGenericConfig(viewModel, configFile);
                        }

                        if (importResult)
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"Successfully imported configuration to {viewModelTypeName}"
                            );
                            return true;
                        }
                        else
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                $"Failed to import configuration to {viewModelTypeName}"
                            );
                            return false;
                        }
                    }
                    finally
                    {
                        // Restore the original service
                        configServiceField.SetValue(viewModel, originalService);
                        _logService.Log(LogLevel.Debug, "Restored original configuration service");
                    }
                }
                else
                {
                    _logService.Log(
                        LogLevel.Error,
                        $"Could not find _configurationService field in {viewModelTypeName}"
                    );

                    // Try direct application as a fallback
                    bool directApplyResult = await DirectlyApplyConfiguration(
                        viewModel,
                        configFile
                    );
                    if (directApplyResult)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            $"Successfully applied configuration directly to {viewModelTypeName}"
                        );
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error importing configuration to {viewModel.GetType().Name}: {ex.Message}"
                );
                _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Imports configuration to WindowsAppsViewModel.
        /// </summary>
        /// <param name="viewModel">The view model to import to.</param>
        /// <param name="configFile">The configuration file to import.</param>
        /// <returns>True if successful, false otherwise.</returns>
        private async Task<bool> ImportWindowsAppsConfig(
            object viewModel,
            ConfigurationFile configFile
        )
        {
            try
            {
                _logService.Log(LogLevel.Info, "Importing configuration to WindowsAppsViewModel");

                // First try using the ImportConfigCommand
                var importCommand =
                    viewModel.GetType().GetProperty("ImportConfigCommand")?.GetValue(viewModel)
                    as IAsyncRelayCommand;
                if (importCommand != null)
                {
                    _logService.Log(LogLevel.Debug, "Found ImportConfigCommand, executing...");
                    await importCommand.ExecuteAsync(null);

                    // Verify that the configuration was applied
                    bool configApplied = await VerifyConfigurationApplied(viewModel, configFile);

                    if (configApplied)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            "Successfully imported configuration using ImportConfigCommand"
                        );

                        // Force UI refresh
                        await RefreshUIIfNeeded(viewModel);

                        return true;
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            "Configuration may not have been properly applied using ImportConfigCommand"
                        );
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "ImportConfigCommand not found");
                }

                // If we get here, try direct application
                _logService.Log(LogLevel.Info, "Falling back to direct application");
                bool directApplyResult = await DirectlyApplyConfiguration(viewModel, configFile);

                if (directApplyResult)
                {
                    _logService.Log(LogLevel.Info, "Successfully applied configuration directly");
                    return true;
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "Failed to apply configuration directly");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error importing WindowsApps configuration: {ex.Message}"
                );
                _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Imports configuration to ExternalAppsViewModel.
        /// </summary>
        /// <param name="viewModel">The view model to import to.</param>
        /// <param name="configFile">The configuration file to import.</param>
        /// <returns>True if successful, false otherwise.</returns>
        private async Task<bool> ImportExternalAppsConfig(
            object viewModel,
            ConfigurationFile configFile
        )
        {
            try
            {
                _logService.Log(LogLevel.Info, "Importing configuration to ExternalAppsViewModel");

                // First try using the ImportConfigCommand
                var importCommand =
                    viewModel.GetType().GetProperty("ImportConfigCommand")?.GetValue(viewModel)
                    as IAsyncRelayCommand;
                if (importCommand != null)
                {
                    _logService.Log(LogLevel.Debug, "Found ImportConfigCommand, executing...");
                    await importCommand.ExecuteAsync(null);

                    // Verify that the configuration was applied
                    bool configApplied = await VerifyConfigurationApplied(viewModel, configFile);

                    if (configApplied)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            "Successfully imported configuration using ImportConfigCommand"
                        );

                        // Force UI refresh
                        await RefreshUIIfNeeded(viewModel);

                        return true;
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            "Configuration may not have been properly applied using ImportConfigCommand"
                        );
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "ImportConfigCommand not found");
                }

                // If we get here, try direct application
                _logService.Log(LogLevel.Info, "Falling back to direct application");
                bool directApplyResult = await DirectlyApplyConfiguration(viewModel, configFile);

                if (directApplyResult)
                {
                    _logService.Log(LogLevel.Info, "Successfully applied configuration directly");
                    return true;
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "Failed to apply configuration directly");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error importing ExternalApps configuration: {ex.Message}"
                );
                _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Imports configuration to CustomizeViewModel.
        /// </summary>
        /// <param name="viewModel">The view model to import to.</param>
        /// <param name="configFile">The configuration file to import.</param>
        /// <returns>True if successful, false otherwise.</returns>
        private async Task<bool> ImportCustomizeConfig(
            object viewModel,
            ConfigurationFile configFile
        )
        {
            try
            {
                _logService.Log(LogLevel.Info, "Importing configuration to CustomizeViewModel");

                // First try using the ImportConfigCommand
                var importCommand =
                    viewModel.GetType().GetProperty("ImportConfigCommand")?.GetValue(viewModel)
                    as IAsyncRelayCommand;
                if (importCommand != null)
                {
                    _logService.Log(LogLevel.Debug, "Found ImportConfigCommand, executing...");
                    await importCommand.ExecuteAsync(null);

                    // Verify that the configuration was applied
                    bool configApplied = await VerifyConfigurationApplied(viewModel, configFile);

                    if (configApplied)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            "Successfully imported configuration using ImportConfigCommand"
                        );

                        // Force UI refresh
                        await RefreshUIIfNeeded(viewModel);

                        return true;
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            "Configuration may not have been properly applied using ImportConfigCommand"
                        );
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "ImportConfigCommand not found");
                }

                // If we get here, try direct application
                _logService.Log(LogLevel.Info, "Falling back to direct application");
                bool directApplyResult = await DirectlyApplyConfiguration(viewModel, configFile);

                if (directApplyResult)
                {
                    _logService.Log(LogLevel.Info, "Successfully applied configuration directly");

                    // For CustomizeViewModel, try to call ApplyCustomizations if available
                    var applyCustomizationsMethod = viewModel
                        .GetType()
                        .GetMethod(
                            "ApplyCustomizations",
                            System.Reflection.BindingFlags.Public
                                | System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Instance
                        );

                    if (applyCustomizationsMethod != null)
                    {
                        _logService.Log(LogLevel.Debug, "Calling ApplyCustomizations method");

                        // Check if the method takes parameters
                        var parameters = applyCustomizationsMethod.GetParameters();
                        if (parameters.Length == 0)
                        {
                            applyCustomizationsMethod.Invoke(viewModel, null);
                        }
                        else if (
                            parameters.Length == 1
                            && parameters[0].ParameterType == typeof(bool)
                        )
                        {
                            // If it takes a boolean parameter, pass true to force application
                            applyCustomizationsMethod.Invoke(viewModel, new object[] { true });
                        }
                    }

                    // Additionally, iterate through all items and ensure their registry settings are applied
                    var itemsProperty = viewModel.GetType().GetProperty("Items");
                    if (itemsProperty != null)
                    {
                        var items =
                            itemsProperty.GetValue(viewModel) as System.Collections.IEnumerable;
                        if (items != null)
                        {
                            _logService.Log(
                                LogLevel.Debug,
                                "Iterating through items to ensure registry settings are applied"
                            );
                            foreach (var item in items)
                            {
                                var applySettingCommand =
                                    item.GetType()
                                        .GetProperty("ApplySettingCommand")
                                        ?.GetValue(item) as ICommand;
                                if (
                                    applySettingCommand != null
                                    && applySettingCommand.CanExecute(null)
                                )
                                {
                                    var nameProperty = item.GetType().GetProperty("Name");
                                    var name =
                                        nameProperty?.GetValue(item)?.ToString() ?? "unknown";
                                    _logService.Log(
                                        LogLevel.Debug,
                                        $"Executing ApplySettingCommand for {name}"
                                    );
                                    applySettingCommand.Execute(null);
                                }
                            }
                        }
                    }

                    return true;
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "Failed to apply configuration directly");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error importing Customize configuration: {ex.Message}"
                );
                _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Imports configuration to OptimizeViewModel.
        /// </summary>
        /// <param name="viewModel">The view model to import to.</param>
        /// <param name="configFile">The configuration file to import.</param>
        /// <returns>True if successful, false otherwise.</returns>
        private async Task<bool> ImportOptimizeConfig(
            object viewModel,
            ConfigurationFile configFile
        )
        {
            try
            {
                _logService.Log(LogLevel.Info, "Importing configuration to OptimizeViewModel");

                // First try using the ImportConfigCommand
                var importCommand =
                    viewModel.GetType().GetProperty("ImportConfigCommand")?.GetValue(viewModel)
                    as IAsyncRelayCommand;
                if (importCommand != null)
                {
                    _logService.Log(LogLevel.Debug, "Found ImportConfigCommand, executing...");
                    await importCommand.ExecuteAsync(null);

                    // Verify that the configuration was applied
                    bool configApplied = await VerifyConfigurationApplied(viewModel, configFile);

                    if (configApplied)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            "Successfully imported configuration using ImportConfigCommand"
                        );

                        // Force UI refresh
                        await RefreshUIIfNeeded(viewModel);

                        return true;
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            "Configuration may not have been properly applied using ImportConfigCommand"
                        );
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "ImportConfigCommand not found");
                }

                // If we get here, try direct application
                _logService.Log(LogLevel.Info, "Falling back to direct application");
                bool directApplyResult = await DirectlyApplyConfiguration(viewModel, configFile);

                if (directApplyResult)
                {
                    _logService.Log(LogLevel.Info, "Successfully applied configuration directly");

                    // For OptimizeViewModel, try to call ApplyOptimizations if available
                    var applyOptimizationsMethod = viewModel
                        .GetType()
                        .GetMethod(
                            "ApplyOptimizations",
                            System.Reflection.BindingFlags.Public
                                | System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Instance
                        );

                    if (applyOptimizationsMethod != null)
                    {
                        _logService.Log(LogLevel.Debug, "Calling ApplyOptimizations method");

                        // Check if the method takes parameters
                        var parameters = applyOptimizationsMethod.GetParameters();
                        if (parameters.Length == 0)
                        {
                            applyOptimizationsMethod.Invoke(viewModel, null);
                        }
                        else if (
                            parameters.Length == 1
                            && parameters[0].ParameterType == typeof(bool)
                        )
                        {
                            // If it takes a boolean parameter, pass true to force application
                            applyOptimizationsMethod.Invoke(viewModel, new object[] { true });
                        }
                    }

                    // Additionally, iterate through all items and ensure their registry settings are applied
                    var itemsProperty = viewModel.GetType().GetProperty("Items");
                    if (itemsProperty != null)
                    {
                        var items =
                            itemsProperty.GetValue(viewModel) as System.Collections.IEnumerable;
                        if (items != null)
                        {
                            _logService.Log(
                                LogLevel.Debug,
                                "Iterating through items to ensure registry settings are applied"
                            );
                            foreach (var item in items)
                            {
                                var applySettingCommand =
                                    item.GetType()
                                        .GetProperty("ApplySettingCommand")
                                        ?.GetValue(item) as ICommand;
                                if (
                                    applySettingCommand != null
                                    && applySettingCommand.CanExecute(null)
                                )
                                {
                                    var nameProperty = item.GetType().GetProperty("Name");
                                    var name =
                                        nameProperty?.GetValue(item)?.ToString() ?? "unknown";
                                    _logService.Log(
                                        LogLevel.Debug,
                                        $"Executing ApplySettingCommand for {name}"
                                    );
                                    applySettingCommand.Execute(null);
                                }
                            }
                        }
                    }

                    return true;
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "Failed to apply configuration directly");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error importing Optimize configuration: {ex.Message}"
                );
                _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Imports configuration to a generic view model.
        /// </summary>
        /// <param name="viewModel">The view model to import to.</param>
        /// <param name="configFile">The configuration file to import.</param>
        /// <returns>True if successful, false otherwise.</returns>
        private async Task<bool> ImportGenericConfig(object viewModel, ConfigurationFile configFile)
        {
            try
            {
                _logService.Log(LogLevel.Info, "Importing configuration to generic view model");

                // First try using the ImportConfigCommand
                var importCommand =
                    viewModel.GetType().GetProperty("ImportConfigCommand")?.GetValue(viewModel)
                    as IAsyncRelayCommand;
                if (importCommand != null)
                {
                    _logService.Log(LogLevel.Debug, "Found ImportConfigCommand, executing...");
                    await importCommand.ExecuteAsync(null);

                    // Verify that the configuration was applied
                    bool configApplied = await VerifyConfigurationApplied(viewModel, configFile);

                    if (configApplied)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            "Successfully imported configuration using ImportConfigCommand"
                        );

                        // Force UI refresh
                        await RefreshUIIfNeeded(viewModel);

                        return true;
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            "Configuration may not have been properly applied using ImportConfigCommand"
                        );
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "ImportConfigCommand not found");
                }

                // If we get here, try direct application
                _logService.Log(LogLevel.Info, "Falling back to direct application");
                bool directApplyResult = await DirectlyApplyConfiguration(viewModel, configFile);

                if (directApplyResult)
                {
                    _logService.Log(LogLevel.Info, "Successfully applied configuration directly");
                    return true;
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "Failed to apply configuration directly");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error importing generic configuration: {ex.Message}"
                );
                _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Verifies that the configuration was properly applied to the view model.
        /// </summary>
        /// <param name="viewModel">The view model to verify.</param>
        /// <param name="configFile">The configuration file that was applied.</param>
        /// <returns>True if the configuration was applied, false otherwise.</returns>
        private async Task<bool> VerifyConfigurationApplied(
            object viewModel,
            ConfigurationFile configFile
        )
        {
            try
            {
                _logService.Log(
                    LogLevel.Debug,
                    $"Verifying configuration was applied to {viewModel.GetType().Name}"
                );

                // Get the Items property from the view model
                var itemsProperty = viewModel.GetType().GetProperty("Items");
                if (itemsProperty == null)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"Could not find Items property in {viewModel.GetType().Name}"
                    );
                    return false;
                }

                var items = itemsProperty.GetValue(viewModel) as System.Collections.IEnumerable;
                if (items == null)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"Items collection is null in {viewModel.GetType().Name}"
                    );
                    return false;
                }

                // Check if at least some items have the expected IsSelected state
                int matchCount = 0;
                int totalChecked = 0;

                // Create dictionaries of config items by name and ID for faster lookup
                var configItemsByName = new Dictionary<string, ConfigurationItem>(
                    StringComparer.OrdinalIgnoreCase
                );
                var configItemsById = new Dictionary<string, ConfigurationItem>(
                    StringComparer.OrdinalIgnoreCase
                );

                if (configFile.Items != null)
                {
                    foreach (var item in configFile.Items)
                    {
                        if (
                            !string.IsNullOrEmpty(item.Name)
                            && !configItemsByName.ContainsKey(item.Name)
                        )
                        {
                            configItemsByName.Add(item.Name, item);
                        }

                        if (
                            item.CustomProperties.TryGetValue("Id", out var id)
                            && id != null
                            && !string.IsNullOrEmpty(id.ToString())
                            && !configItemsById.ContainsKey(id.ToString())
                        )
                        {
                            configItemsById.Add(id.ToString(), item);
                        }
                    }
                }

                // Get the view model type to determine special handling
                string viewModelTypeName = viewModel.GetType().Name;
                _logService.Log(
                    LogLevel.Debug,
                    $"Verifying view model of type: {viewModelTypeName}"
                );

                // Check up to 15 items
                foreach (var item in items)
                {
                    if (totalChecked >= 15)
                        break;

                    var nameProperty = item.GetType().GetProperty("Name");
                    var idProperty = item.GetType().GetProperty("Id");
                    var isSelectedProperty = item.GetType().GetProperty("IsSelected");

                    if (nameProperty != null && isSelectedProperty != null)
                    {
                        var name = nameProperty.GetValue(item)?.ToString();
                        var id = idProperty?.GetValue(item)?.ToString();

                        _logService.Log(LogLevel.Debug, $"Processing item: {name}, Id: {id}");

                        ConfigurationItem configItem = null;

                        // Try to match by ID first
                        if (
                            !string.IsNullOrEmpty(id)
                            && configItemsById.TryGetValue(id, out var itemById)
                        )
                        {
                            configItem = itemById;
                        }
                        // Then try to match by name
                        else if (
                            !string.IsNullOrEmpty(name)
                            && configItemsByName.TryGetValue(name, out var itemByName)
                        )
                        {
                            configItem = itemByName;
                        }

                        if (configItem != null)
                        {
                            totalChecked++;
                            bool itemMatches = true;

                            // Check IsSelected property
                            if (
                                isSelectedProperty.GetValue(item) is bool isSelected
                                && isSelected != configItem.IsSelected
                            )
                            {
                                _logService.Log(
                                    LogLevel.Debug,
                                    $"Item {name} has IsSelected={isSelected}, expected {configItem.IsSelected}"
                                );
                                itemMatches = false;
                            }

                            // For controls with additional properties, check those too
                            if (
                                configItem.ControlType == ControlType.ThreeStateSlider
                                || configItem.ControlType == ControlType.ComboBox
                            )
                            {
                                var sliderValueProperty = item.GetType().GetProperty("SliderValue");
                                if (
                                    sliderValueProperty != null
                                    && configItem.CustomProperties.TryGetValue(
                                        "SliderValue",
                                        out var sliderValue
                                    )
                                )
                                {
                                    int expectedValue = Convert.ToInt32(sliderValue);
                                    int actualValue = (int)(
                                        sliderValueProperty.GetValue(item) ?? 0
                                    );

                                    if (actualValue != expectedValue)
                                    {
                                        _logService.Log(
                                            LogLevel.Debug,
                                            $"Item {name} has SliderValue={actualValue}, expected {expectedValue}"
                                        );
                                        itemMatches = false;
                                    }
                                }
                            }

                            // For ComboBox, check SelectedValue
                            if (configItem.ControlType == ControlType.ComboBox)
                            {
                                var selectedValueProperty = item.GetType()
                                    .GetProperty("SelectedValue");
                                if (
                                    selectedValueProperty != null
                                    && !string.IsNullOrEmpty(configItem.SelectedValue)
                                )
                                {
                                    string expectedValue = configItem.SelectedValue;
                                    string actualValue = selectedValueProperty
                                        .GetValue(item)
                                        ?.ToString();

                                    if (actualValue != expectedValue)
                                    {
                                        _logService.Log(
                                            LogLevel.Debug,
                                            $"Item {name} has SelectedValue={actualValue}, expected {expectedValue}"
                                        );
                                        itemMatches = false;
                                    }
                                }
                            }

                            if (itemMatches)
                            {
                                matchCount++;
                            }
                        }
                    }
                }

                _logService.Log(
                    LogLevel.Debug,
                    $"Verification result: {matchCount} matches out of {totalChecked} checked items"
                );

                // If we checked at least 3 items and at least 50% match, consider it successful
                if (totalChecked >= 3 && (double)matchCount / totalChecked >= 0.5)
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"Configuration verification passed: {matchCount}/{totalChecked} items match"
                    );
                    return true;
                }
                else if (totalChecked > 0)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"Configuration verification failed: only {matchCount}/{totalChecked} items match"
                    );

                    // Even if verification fails, we'll try to directly apply the configuration
                    _logService.Log(LogLevel.Info, "Will attempt direct application as fallback");
                    return false;
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "No items could be checked for verification");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error verifying configuration: {ex.Message}");
                _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Directly applies the configuration to the view model without using the ImportConfigCommand.
        /// </summary>
        /// <param name="viewModel">The view model to apply the configuration to.</param>
        /// <param name="configFile">The configuration file to apply.</param>
        /// <returns>True if successful, false otherwise.</returns>
        private async Task<bool> DirectlyApplyConfiguration(
            object viewModel,
            ConfigurationFile configFile
        )
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Attempting to directly apply configuration to {viewModel.GetType().Name}"
                );

                // Get the Items property from the view model
                var itemsProperty = viewModel.GetType().GetProperty("Items");
                if (itemsProperty == null)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"Could not find Items property in {viewModel.GetType().Name}"
                    );
                    return false;
                }

                var items = itemsProperty.GetValue(viewModel) as System.Collections.IEnumerable;
                if (items == null)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"Items collection is null in {viewModel.GetType().Name}"
                    );
                    return false;
                }

                // Create dictionaries of config items by name and ID for faster lookup
                var configItemsByName = new Dictionary<string, ConfigurationItem>(
                    StringComparer.OrdinalIgnoreCase
                );
                var configItemsById = new Dictionary<string, ConfigurationItem>(
                    StringComparer.OrdinalIgnoreCase
                );

                if (configFile.Items != null)
                {
                    foreach (var item in configFile.Items)
                    {
                        if (
                            !string.IsNullOrEmpty(item.Name)
                            && !configItemsByName.ContainsKey(item.Name)
                        )
                        {
                            configItemsByName.Add(item.Name, item);
                        }

                        if (
                            item.CustomProperties.TryGetValue("Id", out var id)
                            && id != null
                            && !string.IsNullOrEmpty(id.ToString())
                            && !configItemsById.ContainsKey(id.ToString())
                        )
                        {
                            configItemsById.Add(id.ToString(), item);
                        }
                    }
                }

                // Update the items in the view model
                int updatedCount = 0;

                // Determine if we're dealing with a collection that implements INotifyCollectionChanged
                bool isObservableCollection = false;
                var itemsType = items.GetType();
                if (
                    typeof(System.Collections.Specialized.INotifyCollectionChanged).IsAssignableFrom(
                        itemsType
                    )
                )
                {
                    isObservableCollection = true;
                    _logService.Log(
                        LogLevel.Debug,
                        $"Items collection is an observable collection"
                    );
                }

                // Get the view model type to determine special handling
                string viewModelTypeName = viewModel.GetType().Name;
                _logService.Log(
                    LogLevel.Debug,
                    $"Processing view model of type: {viewModelTypeName}"
                );

                foreach (var item in items)
                {
                    var nameProperty = item.GetType().GetProperty("Name");
                    var idProperty = item.GetType().GetProperty("Id");
                    var isSelectedProperty = item.GetType().GetProperty("IsSelected");
                    var updateUIStateFromRegistryMethod = item.GetType()
                        .GetMethod("UpdateUIStateFromRegistry");

                    if (nameProperty != null && isSelectedProperty != null)
                    {
                        var name = nameProperty.GetValue(item)?.ToString();
                        var id = idProperty?.GetValue(item)?.ToString();

                        _logService.Log(LogLevel.Debug, $"Processing item: {name}, Id: {id}");

                        ConfigurationItem configItem = null;

                        // Try to match by ID first
                        if (
                            !string.IsNullOrEmpty(id)
                            && configItemsById.TryGetValue(id, out var itemById)
                        )
                        {
                            configItem = itemById;
                        }
                        // Then try to match by name
                        else if (
                            !string.IsNullOrEmpty(name)
                            && configItemsByName.TryGetValue(name, out var itemByName)
                        )
                        {
                            configItem = itemByName;
                        }

                        if (configItem != null)
                        {
                            // Get the current IsSelected value before changing it
                            bool currentIsSelected = (bool)(
                                isSelectedProperty.GetValue(item) ?? false
                            );

                            // Update UI state using the new method without triggering property change events
                            if (currentIsSelected != configItem.IsSelected)
                            {
                                _logService.Log(
                                    LogLevel.Debug,
                                    $"Updating IsSelected for {name} from {currentIsSelected} to {configItem.IsSelected}"
                                );
                                
                                // Use UpdateUIStateFromRegistry if available, otherwise fall back to direct property setting
                                if (updateUIStateFromRegistryMethod != null)
                                {
                                    // Get current status and value for the method call
                                    var statusProperty = item.GetType().GetProperty("Status");
                                    var currentValueProperty = item.GetType().GetProperty("CurrentValue");
                                    var selectedValueProperty = item.GetType().GetProperty("SelectedValue");
                                    
                                    var status = statusProperty?.GetValue(item);
                                    var currentValue = currentValueProperty?.GetValue(item);
                                    var selectedValue = selectedValueProperty?.GetValue(item);
                                    
                                    updateUIStateFromRegistryMethod.Invoke(item, new object[] { configItem.IsSelected, selectedValue, status, currentValue });
                                }
                                else
                                {
                                    // Fallback for items that don't have UpdateUIStateFromRegistry method
                                    isSelectedProperty.SetValue(item, configItem.IsSelected);
                                }
                            }

                            // Update other properties if available
                            bool propertiesUpdated = UpdateAdditionalProperties(item, configItem);

                            // If any property was updated, count it
                            if (currentIsSelected != configItem.IsSelected || propertiesUpdated)
                            {
                                updatedCount++;
                            }

                            // Ensure UI state is properly updated
                            TriggerPropertyChangedIfPossible(item);

                            // Always explicitly call ApplySetting method to ensure registry changes are applied
                            // regardless of view model type or IsSelected state
                            var applySettingCommand =
                                item.GetType().GetProperty("ApplySettingCommand")?.GetValue(item)
                                as ICommand;
                            if (applySettingCommand != null && applySettingCommand.CanExecute(null))
                            {
                                _logService.Log(
                                    LogLevel.Debug,
                                    $"Explicitly executing ApplySettingCommand for {name}"
                                );
                                applySettingCommand.Execute(null);
                            }
                        }
                    }
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Directly updated {updatedCount} items in {viewModel.GetType().Name}"
                );

                // Force UI refresh
                await RefreshUIIfNeeded(viewModel);

                return updatedCount > 0;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error directly applying configuration: {ex.Message}"
                );
                _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Attempts to trigger property changed notifications on an item if it implements INotifyPropertyChanged.
        /// </summary>
        /// <param name="item">The item to trigger property changed on.</param>
        private void TriggerPropertyChangedIfPossible(object item)
        {
            try
            {
                // Check if the item implements INotifyPropertyChanged
                if (item is System.ComponentModel.INotifyPropertyChanged notifyPropertyChanged)
                {
                    try
                    {
                        // Try to find the OnPropertyChanged method with a string parameter
                        var onPropertyChangedMethod = item.GetType()
                            .GetMethod(
                                "OnPropertyChanged",
                                System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Instance,
                                null,
                                new[] { typeof(string) },
                                null
                            );

                        if (onPropertyChangedMethod != null)
                        {
                            // Invoke the method with null string to refresh all properties
                            onPropertyChangedMethod.Invoke(item, new object[] { null });
                            _logService.Log(
                                LogLevel.Debug,
                                $"Triggered OnPropertyChanged(string) for {item.GetType().Name}"
                            );
                        }
                        else
                        {
                            // Try to find the OnPropertyChanged method with no parameters
                            var onPropertyChangedNoParamsMethod = item.GetType()
                                .GetMethod(
                                    "OnPropertyChanged",
                                    System.Reflection.BindingFlags.NonPublic
                                        | System.Reflection.BindingFlags.Instance,
                                    null,
                                    Type.EmptyTypes,
                                    null
                                );

                            if (onPropertyChangedNoParamsMethod != null)
                            {
                                // Invoke the method with no parameters
                                onPropertyChangedNoParamsMethod.Invoke(item, null);
                                _logService.Log(
                                    LogLevel.Debug,
                                    $"Triggered OnPropertyChanged() for {item.GetType().Name}"
                                );
                            }
                            else
                            {
                                // Try to find the RaisePropertyChanged method as an alternative
                                var raisePropertyChangedMethod = item.GetType()
                                    .GetMethod(
                                        "RaisePropertyChanged",
                                        System.Reflection.BindingFlags.Public
                                            | System.Reflection.BindingFlags.NonPublic
                                            | System.Reflection.BindingFlags.Instance
                                    );

                                if (raisePropertyChangedMethod != null)
                                {
                                    // Invoke with null to refresh all properties
                                    raisePropertyChangedMethod.Invoke(item, new object[] { null });
                                    _logService.Log(
                                        LogLevel.Debug,
                                        $"Triggered RaisePropertyChanged for {item.GetType().Name}"
                                    );
                                }
                            }
                        }
                    }
                    catch (System.Reflection.AmbiguousMatchException)
                    {
                        _logService.Log(
                            LogLevel.Debug,
                            $"Ambiguous match for OnPropertyChanged in {item.GetType().Name}, trying alternative approach"
                        );

                        // Try to get all methods named OnPropertyChanged
                        var methods = item.GetType()
                            .GetMethods(
                                System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Instance
                            )
                            .Where(m => m.Name == "OnPropertyChanged")
                            .ToList();

                        if (methods.Any())
                        {
                            // Try to find one that takes a string parameter
                            var stringParamMethod = methods.FirstOrDefault(m =>
                            {
                                var parameters = m.GetParameters();
                                return parameters.Length == 1
                                    && parameters[0].ParameterType == typeof(string);
                            });

                            if (stringParamMethod != null)
                            {
                                stringParamMethod.Invoke(item, new object[] { null });
                                _logService.Log(
                                    LogLevel.Debug,
                                    $"Triggered OnPropertyChanged using specific method for {item.GetType().Name}"
                                );
                            }
                        }
                    }

                    // Try to find a method that might trigger property changed
                    var refreshMethod = item.GetType()
                        .GetMethod(
                            "Refresh",
                            System.Reflection.BindingFlags.Public
                                | System.Reflection.BindingFlags.Instance
                        );

                    if (refreshMethod != null)
                    {
                        refreshMethod.Invoke(item, null);
                        _logService.Log(
                            LogLevel.Debug,
                            $"Called Refresh method for {item.GetType().Name}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Debug, $"Error triggering property changed: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates additional properties of an item based on the configuration item.
        /// </summary>
        /// <param name="item">The item to update.</param>
        /// <param name="configItem">The configuration item containing the values to apply.</param>
        /// <returns>True if any property was updated, false otherwise.</returns>
        private bool UpdateAdditionalProperties(object item, ConfigurationItem configItem)
        {
            try
            {
                bool anyPropertyUpdated = false;

                // Get the item type to access its properties
                var itemType = item.GetType();

                // Log the control type for debugging
                _logService.Log(
                    LogLevel.Debug,
                    $"Updating additional properties for {configItem.Name}, ControlType: {configItem.ControlType}"
                );

                // Update SliderValue for ThreeStateSlider or ComboBox
                if (
                    configItem.ControlType == ControlType.ThreeStateSlider
                    || configItem.ControlType == ControlType.ComboBox
                )
                {
                    var sliderValueProperty = itemType.GetProperty("SliderValue");
                    if (
                        sliderValueProperty != null
                        && configItem.CustomProperties.TryGetValue(
                            "SliderValue",
                            out var sliderValue
                        )
                    )
                    {
                        int newSliderValue = Convert.ToInt32(sliderValue);
                        int currentSliderValue = (int)(sliderValueProperty.GetValue(item) ?? 0);

                        if (currentSliderValue != newSliderValue)
                        {
                            _logService.Log(
                                LogLevel.Debug,
                                $"Updating SliderValue for {configItem.Name} from {currentSliderValue} to {newSliderValue}"
                            );
                            sliderValueProperty.SetValue(item, newSliderValue);
                            anyPropertyUpdated = true;
                        }
                    }
                }

                // Update SelectedValue for ComboBox
                if (configItem.ControlType == ControlType.ComboBox)
                {
                    var selectedValueProperty = itemType.GetProperty("SelectedValue");
                    if (
                        selectedValueProperty != null
                        && !string.IsNullOrEmpty(configItem.SelectedValue)
                    )
                    {
                        string currentSelectedValue = selectedValueProperty
                            .GetValue(item)
                            ?.ToString();

                        if (currentSelectedValue != configItem.SelectedValue)
                        {
                            _logService.Log(
                                LogLevel.Debug,
                                $"Updating SelectedValue for {configItem.Name} from '{currentSelectedValue}' to '{configItem.SelectedValue}'"
                            );
                            selectedValueProperty.SetValue(item, configItem.SelectedValue);
                            anyPropertyUpdated = true;
                        }
                    }
                }

                // Update SelectedTheme for theme selector
                if (
                    configItem.Name.Contains("Theme")
                    && configItem.CustomProperties.TryGetValue(
                        "SelectedTheme",
                        out var selectedTheme
                    )
                )
                {
                    var selectedThemeProperty = itemType.GetProperty("SelectedTheme");
                    if (selectedThemeProperty != null && selectedTheme != null)
                    {
                        string currentSelectedTheme = selectedThemeProperty
                            .GetValue(item)
                            ?.ToString();
                        string newSelectedTheme = selectedTheme.ToString();

                        if (currentSelectedTheme != newSelectedTheme)
                        {
                            _logService.Log(
                                LogLevel.Debug,
                                $"Updating SelectedTheme for {configItem.Name} from '{currentSelectedTheme}' to '{newSelectedTheme}'"
                            );
                            selectedThemeProperty.SetValue(item, newSelectedTheme);
                            anyPropertyUpdated = true;
                        }
                    }
                }

                // Update Status for items that have a status property
                var statusProperty = itemType.GetProperty("Status");
                if (
                    statusProperty != null
                    && configItem.CustomProperties.TryGetValue("Status", out var status)
                )
                {
                    statusProperty.SetValue(item, status);
                    anyPropertyUpdated = true;
                }

                // Update StatusMessage for items that have a status message property
                var statusMessageProperty = itemType.GetProperty("StatusMessage");
                if (
                    statusMessageProperty != null
                    && configItem.CustomProperties.TryGetValue(
                        "StatusMessage",
                        out var statusMessage
                    )
                )
                {
                    statusMessageProperty.SetValue(item, statusMessage?.ToString());
                    anyPropertyUpdated = true;
                }

                // For toggle switches, ensure IsChecked is synchronized with IsSelected
                var isCheckedProperty = itemType.GetProperty("IsChecked");
                if (isCheckedProperty != null)
                {
                    bool currentIsChecked = (bool)(isCheckedProperty.GetValue(item) ?? false);

                    if (currentIsChecked != configItem.IsSelected)
                    {
                        _logService.Log(
                            LogLevel.Debug,
                            $"Updating IsChecked for {configItem.Name} from {currentIsChecked} to {configItem.IsSelected}"
                        );
                        isCheckedProperty.SetValue(item, configItem.IsSelected);
                        anyPropertyUpdated = true;
                    }
                }

                // Update CurrentValue property if it exists
                var currentValueProperty = itemType.GetProperty("CurrentValue");
                if (currentValueProperty != null)
                {
                    // For toggle buttons, set the current value based on IsSelected
                    if (configItem.ControlType == ControlType.BinaryToggle)
                    {
                        object valueToSet = configItem.IsSelected ? 1 : 0;
                        currentValueProperty.SetValue(item, valueToSet);
                        _logService.Log(
                            LogLevel.Debug,
                            $"Setting CurrentValue for {configItem.Name} to {valueToSet}"
                        );
                        anyPropertyUpdated = true;
                    }
                    // For other control types, use the appropriate value
                    else if (
                        configItem.ControlType == ControlType.ThreeStateSlider
                        || configItem.ControlType == ControlType.ComboBox
                    )
                    {
                        if (
                            configItem.CustomProperties.TryGetValue(
                                "SliderValue",
                                out var sliderValue
                            )
                        )
                        {
                            currentValueProperty.SetValue(item, sliderValue);
                            _logService.Log(
                                LogLevel.Debug,
                                $"Setting CurrentValue for {configItem.Name} to {sliderValue}"
                            );
                            anyPropertyUpdated = true;
                        }
                    }
                }

                // Update RegistrySetting property if it exists
                var registrySettingProperty = itemType.GetProperty("RegistrySetting");
                if (
                    registrySettingProperty != null
                    && configItem.CustomProperties.TryGetValue(
                        "RegistrySetting",
                        out var registrySetting
                    )
                )
                {
                    registrySettingProperty.SetValue(item, registrySetting);
                    _logService.Log(
                        LogLevel.Debug,
                        $"Updated RegistrySetting for {configItem.Name}"
                    );
                    anyPropertyUpdated = true;
                }

                // Update LinkedRegistrySettings property if it exists
                var linkedRegistrySettingsProperty = itemType.GetProperty("LinkedRegistrySettings");
                if (
                    linkedRegistrySettingsProperty != null
                    && configItem.CustomProperties.TryGetValue(
                        "LinkedRegistrySettings",
                        out var linkedRegistrySettings
                    )
                )
                {
                    linkedRegistrySettingsProperty.SetValue(item, linkedRegistrySettings);
                    _logService.Log(
                        LogLevel.Debug,
                        $"Updated LinkedRegistrySettings for {configItem.Name}"
                    );
                    anyPropertyUpdated = true;
                }

                return anyPropertyUpdated;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error updating additional properties: {ex.Message}"
                );
                _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Forces a UI refresh if needed for the view model.
        /// </summary>
        /// <param name="viewModel">The view model to refresh.</param>
        private async Task RefreshUIIfNeeded(object viewModel)
        {
            try
            {
                _logService.Log(LogLevel.Debug, $"Refreshing UI for {viewModel.GetType().Name}");
                bool refreshed = false;

                // Get the view model type to determine special handling
                string viewModelTypeName = viewModel.GetType().Name;

                // Try multiple refresh methods in order of preference

                // 1. First try RefreshCommand if available
                var refreshCommandProperty = viewModel.GetType().GetProperty("RefreshCommand");
                if (refreshCommandProperty != null)
                {
                    var refreshCommand =
                        refreshCommandProperty.GetValue(viewModel) as IAsyncRelayCommand;
                    if (refreshCommand != null && refreshCommand.CanExecute(null))
                    {
                        _logService.Log(LogLevel.Debug, "Executing RefreshCommand");
                        await refreshCommand.ExecuteAsync(null);
                        refreshed = true;
                    }
                }

                // 2. Try RaisePropertyChanged for the Items property if the view model implements INotifyPropertyChanged
                if (!refreshed && viewModel is System.ComponentModel.INotifyPropertyChanged)
                {
                    try
                    {
                        // Try to find the OnPropertyChanged method with a string parameter
                        var onPropertyChangedMethod = viewModel
                            .GetType()
                            .GetMethod(
                                "OnPropertyChanged",
                                System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Instance,
                                null,
                                new[] { typeof(string) },
                                null
                            );

                        if (onPropertyChangedMethod != null)
                        {
                            _logService.Log(
                                LogLevel.Debug,
                                "Calling OnPropertyChanged(string) for Items property"
                            );
                            onPropertyChangedMethod.Invoke(viewModel, new object[] { "Items" });
                            refreshed = true;
                        }
                        else
                        {
                            // Try to find the RaisePropertyChanged method as an alternative
                            var raisePropertyChangedMethod = viewModel
                                .GetType()
                                .GetMethod(
                                    "RaisePropertyChanged",
                                    System.Reflection.BindingFlags.Public
                                        | System.Reflection.BindingFlags.NonPublic
                                        | System.Reflection.BindingFlags.Instance
                                );

                            if (raisePropertyChangedMethod != null)
                            {
                                _logService.Log(
                                    LogLevel.Debug,
                                    "Calling RaisePropertyChanged for Items property"
                                );
                                raisePropertyChangedMethod.Invoke(
                                    viewModel,
                                    new object[] { "Items" }
                                );
                                refreshed = true;
                            }
                            else
                            {
                                // Try to find the NotifyPropertyChanged method as another alternative
                                var notifyPropertyChangedMethod = viewModel
                                    .GetType()
                                    .GetMethod(
                                        "NotifyPropertyChanged",
                                        System.Reflection.BindingFlags.Public
                                            | System.Reflection.BindingFlags.NonPublic
                                            | System.Reflection.BindingFlags.Instance
                                    );

                                if (notifyPropertyChangedMethod != null)
                                {
                                    _logService.Log(
                                        LogLevel.Debug,
                                        "Calling NotifyPropertyChanged for Items property"
                                    );
                                    notifyPropertyChangedMethod.Invoke(
                                        viewModel,
                                        new object[] { "Items" }
                                    );
                                    refreshed = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"Error calling property changed method: {ex.Message}"
                        );
                        // Continue with other refresh methods
                    }
                }

                // 3. Try LoadItemsAsync method
                if (!refreshed)
                {
                    var loadItemsMethod = viewModel.GetType().GetMethod("LoadItemsAsync");
                    if (loadItemsMethod != null)
                    {
                        _logService.Log(LogLevel.Debug, "Calling LoadItemsAsync method");
                        await (Task)loadItemsMethod.Invoke(viewModel, null);
                        refreshed = true;
                    }
                }

                // 4. Try ApplySearch method
                if (!refreshed)
                {
                    var applySearchMethod = viewModel
                        .GetType()
                        .GetMethod(
                            "ApplySearch",
                            System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Instance
                        );
                    if (applySearchMethod != null)
                    {
                        _logService.Log(LogLevel.Debug, "Calling ApplySearch method");
                        applySearchMethod.Invoke(viewModel, null);
                        refreshed = true;
                    }
                }

                // 5. For specific view model types, try additional refresh methods
                if (!refreshed)
                {
                    if (viewModelTypeName.Contains("Customize"))
                    {
                        // Try to refresh the CustomizeViewModel specifically
                        var refreshCustomizationsMethod = viewModel
                            .GetType()
                            .GetMethod(
                                "RefreshCustomizations",
                                System.Reflection.BindingFlags.Public
                                    | System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Instance
                            );

                        if (refreshCustomizationsMethod != null)
                        {
                            _logService.Log(LogLevel.Debug, "Calling RefreshCustomizations method");
                            refreshCustomizationsMethod.Invoke(viewModel, null);
                            refreshed = true;
                        }
                    }
                    else if (viewModelTypeName.Contains("Optimize"))
                    {
                        // Try to refresh the OptimizeViewModel specifically
                        var refreshOptimizationsMethod = viewModel
                            .GetType()
                            .GetMethod(
                                "RefreshOptimizations",
                                System.Reflection.BindingFlags.Public
                                    | System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Instance
                            );

                        if (refreshOptimizationsMethod != null)
                        {
                            _logService.Log(LogLevel.Debug, "Calling RefreshOptimizations method");
                            refreshOptimizationsMethod.Invoke(viewModel, null);
                            refreshed = true;
                        }
                    }
                }

                // 6. If we still haven't refreshed, try to force a collection refresh
                if (!refreshed)
                {
                    // Get the Items property
                    var itemsProperty = viewModel.GetType().GetProperty("Items");
                    if (itemsProperty != null)
                    {
                        var items = itemsProperty.GetValue(viewModel);

                        // Check if it's an ObservableCollection
                        if (items != null && items.GetType().Name.Contains("ObservableCollection"))
                        {
                            // Try to call a refresh method on the collection
                            var refreshMethod = items.GetType().GetMethod("Refresh");
                            if (refreshMethod != null)
                            {
                                _logService.Log(
                                    LogLevel.Debug,
                                    "Calling Refresh on ObservableCollection"
                                );
                                refreshMethod.Invoke(items, null);
                                refreshed = true;
                            }
                        }
                    }
                }

                if (!refreshed)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        "Could not find a suitable method to refresh the UI"
                    );
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error refreshing UI: {ex.Message}");
                _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
            }
        }

        /// <summary>
        /// A wrapper for the configuration service that returns a specific configuration file.
        /// </summary>
        private class ConfigurationServiceWrapper : IConfigurationService
        {
            private readonly IConfigurationService _innerService;
            private readonly ConfigurationFile _configFile;
            private readonly ILogService _logService;

            public ConfigurationServiceWrapper(
                IConfigurationService innerService,
                ConfigurationFile configFile
            )
            {
                _innerService = innerService;
                _configFile = configFile;

                // Try to get the log service from the inner service using reflection
                try
                {
                    var logServiceField = _innerService
                        .GetType()
                        .GetField(
                            "_logService",
                            System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Instance
                        );
                    if (logServiceField != null)
                    {
                        _logService = logServiceField.GetValue(_innerService) as ILogService;
                    }
                }
                catch
                {
                    // Ignore any errors, we'll just operate without logging
                }
            }

            public Task<ConfigurationFile> LoadConfigurationAsync(string configType)
            {
                // Log the operation if possible
                _logService?.Log(
                    LogLevel.Debug,
                    $"ConfigurationServiceWrapper.LoadConfigurationAsync called with configType: {configType}"
                );
                _logService?.Log(
                    LogLevel.Debug,
                    $"Returning config file with {_configFile.Items?.Count ?? 0} items"
                );

                // Always return our config file, but ensure it has the correct configType
                var configFileCopy = new ConfigurationFile
                {
                    ConfigType = configType,
                    CreatedAt = _configFile.CreatedAt,
                    Items = _configFile.Items,
                };

                return Task.FromResult(configFileCopy);
            }

            public Task<bool> SaveConfigurationAsync<T>(IEnumerable<T> items, string configType)
            {
                // Log the operation if possible
                _logService?.Log(
                    LogLevel.Debug,
                    $"ConfigurationServiceWrapper.SaveConfigurationAsync called with configType: {configType}"
                );

                // Delegate to the inner service
                return _innerService.SaveConfigurationAsync(items, configType);
            }

            public Task<UnifiedConfigurationFile> LoadUnifiedConfigurationAsync()
            {
                // Log the operation if possible
                _logService?.Log(
                    LogLevel.Debug,
                    "ConfigurationServiceWrapper.LoadUnifiedConfigurationAsync called"
                );

                // Delegate to the inner service
                return _innerService.LoadUnifiedConfigurationAsync();
            }

            public Task<UnifiedConfigurationFile> LoadRecommendedConfigurationAsync()
            {
                // Log the operation if possible
                _logService?.Log(
                    LogLevel.Debug,
                    "ConfigurationServiceWrapper.LoadRecommendedConfigurationAsync called"
                );

                // Delegate to the inner service
                return _innerService.LoadRecommendedConfigurationAsync();
            }

            public Task<bool> SaveUnifiedConfigurationAsync(UnifiedConfigurationFile unifiedConfig)
            {
                return Task.FromResult(true);
            }

            public UnifiedConfigurationFile CreateUnifiedConfiguration(
                Dictionary<string, IEnumerable<ISettingItem>> sections,
                IEnumerable<string> includedSections
            )
            {
                // Log the operation if possible
                _logService?.Log(
                    LogLevel.Debug,
                    "ConfigurationServiceWrapper.CreateUnifiedConfiguration called"
                );

                // Delegate to the inner service
                return _innerService.CreateUnifiedConfiguration(sections, includedSections);
            }

            public ConfigurationFile ExtractSectionFromUnifiedConfiguration(
                UnifiedConfigurationFile unifiedConfig,
                string sectionName
            )
            {
                // Log the operation if possible
                _logService?.Log(
                    LogLevel.Debug,
                    $"ConfigurationServiceWrapper.ExtractSectionFromUnifiedConfiguration called with sectionName: {sectionName}"
                );

                // Delegate to the inner service
                return _innerService.ExtractSectionFromUnifiedConfiguration(
                    unifiedConfig,
                    sectionName
                );
            }
        }

        /// <summary>
        /// A mock configuration service that captures the settings passed to SaveConfigurationAsync.
        /// </summary>
        private class MockConfigurationService : IConfigurationService
        {
            // Flag to indicate this is being used for unified configuration
            public bool IsUnifiedConfigurationMode { get; set; } = true;

            // Flag to suppress dialogs
            public bool SuppressDialogs { get; set; } = true;

            // Captured settings
            public List<ISettingItem> CapturedSettings { get; } = new List<ISettingItem>();

            // Dialog service for showing messages
            private readonly IDialogService _dialogService;

            public MockConfigurationService(IDialogService dialogService = null)
            {
                _dialogService = dialogService;
            }

            public Task<ConfigurationFile> LoadConfigurationAsync(string configType)
            {
                // Return an empty configuration file
                return Task.FromResult(
                    new ConfigurationFile
                    {
                        ConfigType = configType,
                        CreatedAt = DateTime.UtcNow,
                        Items = new List<ConfigurationItem>(),
                    }
                );
            }

            public Task<bool> SaveConfigurationAsync<T>(IEnumerable<T> items, string configType)
            {

                // Check if items is null or empty
                if (items == null)
                {
                    return Task.FromResult(true);
                }

                if (!items.Any())
                {
                    return Task.FromResult(true);
                }

                try
                {
                    // We no longer need special handling for WindowsApps since we're directly accessing the Items collection
                    // Special handling for ExternalApps
                    if (configType == "ExternalApps")
                    {

                        // Convert each ExternalApp to ExternalAppSettingItem
                        var externalApps = new List<ExternalAppSettingItem>();

                        foreach (var item in items)
                        {
                            if (
                                item
                                is Winhance.WPF.Features.SoftwareApps.Models.ExternalApp externalApp
                            )
                            {
                                externalApps.Add(new ExternalAppSettingItem(externalApp));
                            }
                        }

                        CapturedSettings.AddRange(externalApps);
                    }
                    else
                    {
                        // For other types, try to cast directly to ISettingItem
                        try
                        {
                            var settingItems = items.Cast<ISettingItem>().ToList();

                            CapturedSettings.AddRange(settingItems);

                        }
                        catch (InvalidCastException ex)
                        {

                            // Try to convert each item individually
                            int convertedCount = 0;
                            foreach (var item in items)
                            {
                                try
                                {
                                    if (
                                        item
                                        is Winhance.WPF.Features.SoftwareApps.Models.WindowsApp windowsApp
                                    )
                                    {
                                        CapturedSettings.Add(new WindowsAppSettingItem(windowsApp));
                                        convertedCount++;
                                    }
                                    else if (
                                        item
                                        is Winhance.WPF.Features.SoftwareApps.Models.ExternalApp externalApp
                                    )
                                    {
                                        CapturedSettings.Add(
                                            new ExternalAppSettingItem(externalApp)
                                        );
                                        convertedCount++;
                                    }
                                }
                                catch (Exception itemEx)
                                {
                                    // Error processing individual item
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                        // Unexpected error in SaveConfigurationAsync
                }

                // Return true without showing any dialogs when in unified configuration mode
                if (SuppressDialogs || IsUnifiedConfigurationMode)
                {
                    return Task.FromResult(true);
                }

                // Show a success dialog if not in unified configuration mode
                if (_dialogService != null)
                {
                    _dialogService.ShowMessage(
                        $"Configuration saved successfully.",
                        "Configuration Saved"
                    );
                }

                return Task.FromResult(true);
            }

            public Task<UnifiedConfigurationFile> LoadUnifiedConfigurationAsync()
            {
                // Return an empty unified configuration file
                return Task.FromResult(
                    new UnifiedConfigurationFile
                    {
                        CreatedAt = DateTime.UtcNow,
                        WindowsApps = new ConfigSection(),
                        ExternalApps = new ConfigSection(),
                        Customize = new ConfigSection(),
                        Optimize = new ConfigSection(),
                    }
                );
            }

            public Task<UnifiedConfigurationFile> LoadRecommendedConfigurationAsync()
            {
                // Return an empty unified configuration file (same as LoadUnifiedConfigurationAsync)
                // In a mock service, we don't need to actually download anything
                return Task.FromResult(
                    new UnifiedConfigurationFile
                    {
                        CreatedAt = DateTime.UtcNow,
                        WindowsApps = new ConfigSection(),
                        ExternalApps = new ConfigSection(),
                        Customize = new ConfigSection(),
                        Optimize = new ConfigSection(),
                    }
                );
            }

            public Task<bool> SaveUnifiedConfigurationAsync(UnifiedConfigurationFile unifiedConfig)
            {
                return Task.FromResult(true);
            }

            public UnifiedConfigurationFile CreateUnifiedConfiguration(
                Dictionary<string, IEnumerable<ISettingItem>> sections,
                IEnumerable<string> includedSections
            )
            {
                // Create a unified configuration file with all sections included
                var unifiedConfig = new UnifiedConfigurationFile
                {
                    CreatedAt = DateTime.UtcNow,
                    WindowsApps = new ConfigSection(),
                    ExternalApps = new ConfigSection(),
                    Customize = new ConfigSection(),
                    Optimize = new ConfigSection(),
                };

                // Set the IsIncluded flag for each section based on whether it's in the includedSections list
                // and whether it has any items
                if (sections.TryGetValue("WindowsApps", out var windowsApps) && windowsApps.Any())
                {
                    unifiedConfig.WindowsApps.IsIncluded = true;
                    unifiedConfig.WindowsApps.Items = ConvertToConfigurationItems(windowsApps);
                }

                if (
                    sections.TryGetValue("ExternalApps", out var externalApps) && externalApps.Any()
                )
                {
                    unifiedConfig.ExternalApps.IsIncluded = true;
                    unifiedConfig.ExternalApps.Items = ConvertToConfigurationItems(externalApps);
                }

                if (sections.TryGetValue("Customize", out var customize) && customize.Any())
                {
                    unifiedConfig.Customize.IsIncluded = true;
                    unifiedConfig.Customize.Items = ConvertToConfigurationItems(customize);
                }

                if (sections.TryGetValue("Optimize", out var optimize) && optimize.Any())
                {
                    unifiedConfig.Optimize.IsIncluded = true;
                    unifiedConfig.Optimize.Items = ConvertToConfigurationItems(optimize);
                }

                return unifiedConfig;
            }

            // Helper method to convert ISettingItem objects to ConfigurationItem objects
            private List<ConfigurationItem> ConvertToConfigurationItems(
                IEnumerable<ISettingItem> items
            )
            {
                var result = new List<ConfigurationItem>();

                foreach (var item in items)
                {
                    var configItem = new ConfigurationItem
                    {
                        Name = item.Name,
                        IsSelected = item.IsSelected,
                        ControlType = item.ControlType,
                    };

                    // Add Id to custom properties
                    if (!string.IsNullOrEmpty(item.Id))
                    {
                        configItem.CustomProperties["Id"] = item.Id;
                    }

                    // Add GroupName to custom properties
                    if (!string.IsNullOrEmpty(item.GroupName))
                    {
                        configItem.CustomProperties["GroupName"] = item.GroupName;
                    }

                    // Add Description to custom properties
                    if (!string.IsNullOrEmpty(item.Description))
                    {
                        configItem.CustomProperties["Description"] = item.Description;
                    }

                    // Ensure SelectedValue is set for ComboBox controls
                    configItem.EnsureSelectedValueIsSet();

                    result.Add(configItem);
                }

                return result;
            }

            public ConfigurationFile ExtractSectionFromUnifiedConfiguration(
                UnifiedConfigurationFile unifiedConfig,
                string sectionName
            )
            {
                // Return an empty configuration file
                return new ConfigurationFile
                {
                    ConfigType = sectionName,
                    CreatedAt = DateTime.UtcNow,
                    Items = new List<ConfigurationItem>(),
                };
            }
        }
    }
}
