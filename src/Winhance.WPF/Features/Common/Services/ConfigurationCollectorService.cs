using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    /// Service for collecting configuration settings from different view models.
    /// </summary>
    public class ConfigurationCollectorService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logService;

        /// <summary>
        /// A wrapper class that implements ISettingItem for Power Plan settings
        /// </summary>
        private class PowerPlanSettingItem : ISettingItem
        {
            private readonly ConfigurationItem _configItem;
            private readonly object _originalItem;
            private string _id;
            private string _name;
            private string _description;
            private bool _isSelected;
            private string _groupName;
            private bool _isVisible;
            private ControlType _controlType;
            private int _sliderValue;

            public PowerPlanSettingItem(ConfigurationItem configItem, object originalItem)
            {
                _configItem = configItem;
                _originalItem = originalItem;
                
                // Initialize properties from the ConfigurationItem
                _id = _configItem.CustomProperties.TryGetValue("Id", out var id) ? id?.ToString() : "PowerPlanComboBox";
                _name = _configItem.Name;
                _description = _configItem.CustomProperties.TryGetValue("Description", out var desc) ? desc?.ToString() : "Power Plan setting";
                _isSelected = _configItem.IsSelected;
                _groupName = _configItem.CustomProperties.TryGetValue("GroupName", out var group) ? group?.ToString() : "Power Management";
                _isVisible = true;
                _controlType = ControlType.ComboBox;
                _sliderValue = _configItem.CustomProperties.TryGetValue("SliderValue", out var value) ? Convert.ToInt32(value) : 0;
                
                // Ensure the ConfigurationItem has the correct format
                // Make sure SelectedValue is set properly
                if (_configItem.SelectedValue == null)
                {
                    // Try to get it from PowerPlanOptions if available
                    if (_configItem.CustomProperties.TryGetValue("PowerPlanOptions", out var options) &&
                        options is List<string> powerPlanOptions &&
                        powerPlanOptions.Count > 0 &&
                        _configItem.CustomProperties.TryGetValue("SliderValue", out var sliderValue))
                    {
                        int index = Convert.ToInt32(sliderValue);
                        if (index >= 0 && index < powerPlanOptions.Count)
                        {
                            _configItem.SelectedValue = powerPlanOptions[index];
                        }
                    }
                }
                
                // Always ensure SelectedValue is set based on SliderValue if it's still null
                if (_configItem.SelectedValue == null && _configItem.CustomProperties.TryGetValue("SliderValue", out var sv))
                {
                    int index = Convert.ToInt32(sv);
                    if (_configItem.CustomProperties.TryGetValue("PowerPlanOptions", out var opt) &&
                        opt is List<string> planOptions &&
                        planOptions.Count > index && index >= 0)
                    {
                        _configItem.SelectedValue = planOptions[index];
                    }
                    else
                    {
                        // Fallback to default power plan names if PowerPlanOptions is not available
                        string[] defaultOptions = { "Balanced", "High Performance", "Ultimate Performance" };
                        if (index >= 0 && index < defaultOptions.Length)
                        {
                            _configItem.SelectedValue = defaultOptions[index];
                        }
                    }
                }
                
                // Initialize other required properties
                Dependencies = new List<SettingDependency>();
                IsUpdatingFromCode = false;
                ApplySettingCommand = null;
            }

            // Properties with getters and setters
            public string Id { get => _id; set => _id = value; }
            public string Name { get => _name; set => _name = value; }
            public string Description { get => _description; set => _description = value; }
            public bool IsSelected { get => _isSelected; set => _isSelected = value; }
            public string GroupName { get => _groupName; set => _groupName = value; }
            public bool IsVisible { get => _isVisible; set => _isVisible = value; }
            public ControlType ControlType { get => _controlType; set => _controlType = value; }
            public int SliderValue { get => _sliderValue; set => _sliderValue = value; }
            
            // Additional required properties
            public List<SettingDependency> Dependencies { get; set; }
            public bool IsUpdatingFromCode { get; set; }
            public System.Windows.Input.ICommand ApplySettingCommand { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationCollectorService"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="logService">The log service.</param>
        public ConfigurationCollectorService(
            IServiceProvider serviceProvider,
            ILogService logService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Collects settings from all view models.
        /// </summary>
        /// <returns>A dictionary of section names and their settings.</returns>
        public async Task<Dictionary<string, IEnumerable<ISettingItem>>> CollectAllSettingsAsync()
        {
            var sectionSettings = new Dictionary<string, IEnumerable<ISettingItem>>();

            // Get all view models from the service provider
            var windowsAppsViewModel = _serviceProvider.GetService<WindowsAppsViewModel>();
            var externalAppsViewModel = _serviceProvider.GetService<ExternalAppsViewModel>();
            var customizeViewModel = _serviceProvider.GetService<CustomizeViewModel>();
            var optimizeViewModel = _serviceProvider.GetService<OptimizeViewModel>();

            // Add settings from WindowsAppsViewModel
            if (windowsAppsViewModel != null)
            {
                await CollectWindowsAppsSettings(windowsAppsViewModel, sectionSettings);
            }

            // Add settings from ExternalAppsViewModel
            if (externalAppsViewModel != null)
            {
                await CollectExternalAppsSettings(externalAppsViewModel, sectionSettings);
            }

            // Add settings from CustomizeViewModel
            if (customizeViewModel != null)
            {
                await CollectCustomizeSettings(customizeViewModel, sectionSettings);
            }

            // Add settings from OptimizeViewModel
            if (optimizeViewModel != null)
            {
                await CollectOptimizeSettings(optimizeViewModel, sectionSettings);
            }

            return sectionSettings;
        }

        private async Task CollectWindowsAppsSettings(WindowsAppsViewModel viewModel, Dictionary<string, IEnumerable<ISettingItem>> sectionSettings)
        {
            try
            {
                
                // Ensure the view model is initialized
                if (!viewModel.IsInitialized)
                {
                    await viewModel.LoadItemsAsync();
                }
                
                // Convert each WindowsApp to WindowsAppSettingItem
                var windowsAppSettingItems = new List<WindowsAppSettingItem>();
                
                foreach (var item in viewModel.Items)
                {
                    if (item is WindowsApp windowsApp)
                    {
                        windowsAppSettingItems.Add(new WindowsAppSettingItem(windowsApp));
                        _logService.Log(LogLevel.Debug, $"Added WindowsAppSettingItem for {windowsApp.Name}");
                    }
                }
                
                _logService.Log(LogLevel.Info, $"Created {windowsAppSettingItems.Count} WindowsAppSettingItems");
                
                // Always add WindowsApps to sectionSettings, even if empty
                sectionSettings["WindowsApps"] = windowsAppSettingItems;
                _logService.Log(LogLevel.Info, $"Added WindowsApps section with {windowsAppSettingItems.Count} items");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error collecting WindowsApps settings: {ex.Message}");
                
                // Create some default WindowsApps as fallback
                var defaultApps = new[]
                {
                    new WindowsApp
                    {
                        Name = "Microsoft Edge",
                        PackageName = "Microsoft.MicrosoftEdge",
                        IsSelected = true,
                        Description = "Microsoft Edge browser"
                    },
                    new WindowsApp
                    {
                        Name = "Calculator",
                        PackageName = "Microsoft.WindowsCalculator",
                        IsSelected = true,
                        Description = "Windows Calculator app"
                    },
                    new WindowsApp
                    {
                        Name = "Photos",
                        PackageName = "Microsoft.Windows.Photos",
                        IsSelected = true,
                        Description = "Windows Photos app"
                    }
                };
                
                var defaultWindowsAppSettingItems = new List<WindowsAppSettingItem>();
                foreach (var app in defaultApps)
                {
                    defaultWindowsAppSettingItems.Add(new WindowsAppSettingItem(app));
                }
                
                // Always add WindowsApps to sectionSettings, even if using defaults
                sectionSettings["WindowsApps"] = defaultWindowsAppSettingItems;
                _logService.Log(LogLevel.Info, $"Added WindowsApps section with {defaultWindowsAppSettingItems.Count} default items");
            }
        }

        private async Task CollectExternalAppsSettings(ExternalAppsViewModel viewModel, Dictionary<string, IEnumerable<ISettingItem>> sectionSettings)
        {
            try
            {
                _logService.Log(LogLevel.Debug, "Collecting settings from ExternalAppsViewModel");
                
                // Ensure the view model is initialized
                if (!viewModel.IsInitialized)
                {
                    _logService.Log(LogLevel.Debug, "ExternalAppsViewModel not initialized, loading items");
                    await viewModel.LoadItemsAsync();
                }
                
                // Convert each ExternalApp to ExternalAppSettingItem
                var externalAppSettingItems = new List<ExternalAppSettingItem>();
                
                foreach (var item in viewModel.Items)
                {
                    if (item is ExternalApp externalApp)
                    {
                        externalAppSettingItems.Add(new ExternalAppSettingItem(externalApp));
                        _logService.Log(LogLevel.Debug, $"Added ExternalAppSettingItem for {externalApp.Name}");
                    }
                }
                
                _logService.Log(LogLevel.Info, $"Created {externalAppSettingItems.Count} ExternalAppSettingItems");
                
                // Add the settings to the dictionary
                sectionSettings["ExternalApps"] = externalAppSettingItems;
                _logService.Log(LogLevel.Info, $"Added ExternalApps section with {externalAppSettingItems.Count} items");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error collecting ExternalApps settings: {ex.Message}");
                
                // Add an empty list as fallback
                sectionSettings["ExternalApps"] = new List<ISettingItem>();
                _logService.Log(LogLevel.Info, "Added empty ExternalApps section due to error");
            }
        }

        private async Task CollectCustomizeSettings(CustomizeViewModel viewModel, Dictionary<string, IEnumerable<ISettingItem>> sectionSettings)
        {
            try
            {
                _logService.Log(LogLevel.Debug, "Collecting settings from CustomizeViewModel");
                
                // Ensure the view model is initialized
                if (!viewModel.IsInitialized)
                {
                    _logService.Log(LogLevel.Debug, "CustomizeViewModel not initialized, loading items");
                    await viewModel.LoadItemsAsync();
                }
                
                // Collect settings directly
                var customizeItems = new List<ISettingItem>();
                
                foreach (var item in viewModel.Items)
                {
                    if (item is ISettingItem settingItem)
                    {
                        // Skip the DarkModeToggle item to avoid conflicts with ThemeSelector
                        if (settingItem.Id == "DarkModeToggle")
                        {
                            _logService.Log(LogLevel.Debug, $"Skipping DarkModeToggle item to avoid conflicts with ThemeSelector");
                            continue;
                        }
                        
                        // Special handling for Windows Theme / Choose Your Mode
                        if (settingItem.Id == "ThemeSelector" ||
                            settingItem.Name.Contains("Windows Theme") ||
                            settingItem.Name.Contains("Theme Selector") ||
                            settingItem.Name.Contains("Choose Your Mode"))
                        {
                            // Ensure it has the correct ControlType and properties for ComboBox
                            if (settingItem is ApplicationSettingItem applicationSetting)
                            {
                                applicationSetting.ControlType = ControlType.ComboBox;
                                
                                // Get the SelectedTheme from the RegistrySetting if available
                                if (applicationSetting.RegistrySetting?.CustomProperties != null &&
                                    applicationSetting.RegistrySetting.CustomProperties.ContainsKey("SelectedTheme"))
                                {
                                    var selectedTheme = applicationSetting.RegistrySetting.CustomProperties["SelectedTheme"]?.ToString();
                                    _logService.Log(LogLevel.Debug, $"Found SelectedTheme in RegistrySetting: {selectedTheme}");
                                }
                                
                                _logService.Log(LogLevel.Debug, $"Forced ControlType to ComboBox for Theme Selector");
                            }
                            else if (settingItem is ApplicationSettingViewModel applicationViewModel)
                            {
                                applicationViewModel.ControlType = ControlType.ComboBox;
                                
                                // Ensure SelectedValue is set based on SelectedTheme if available
                                var selectedThemeProperty = applicationViewModel.GetType().GetProperty("SelectedTheme");
                                var selectedValueProperty = applicationViewModel.GetType().GetProperty("SelectedValue");
                                
                                if (selectedThemeProperty != null && selectedValueProperty != null)
                                {
                                    var selectedTheme = selectedThemeProperty.GetValue(applicationViewModel)?.ToString();
                                    if (!string.IsNullOrEmpty(selectedTheme))
                                    {
                                        selectedValueProperty.SetValue(applicationViewModel, selectedTheme);
                                    }
                                }
                                
                                _logService.Log(LogLevel.Debug, $"Forced ControlType to ComboBox for Theme Selector (ViewModel) and ensured SelectedValue is set");
                            }
                            else if (settingItem is ApplicationSettingItem customizationSetting)
                            {
                                customizationSetting.ControlType = ControlType.ComboBox;
                                
                                // Get the SelectedTheme from the RegistrySetting if available
                                if (customizationSetting.RegistrySetting?.CustomProperties != null &&
                                    customizationSetting.RegistrySetting.CustomProperties.ContainsKey("SelectedTheme"))
                                {
                                    var selectedTheme = customizationSetting.RegistrySetting.CustomProperties["SelectedTheme"]?.ToString();
                                    _logService.Log(LogLevel.Debug, $"Found SelectedTheme in RegistrySetting: {selectedTheme}");
                                }
                                
                                _logService.Log(LogLevel.Debug, $"Forced ControlType to ComboBox for Theme Selector (CustomizationSetting)");
                            }
                        }
                        
                        customizeItems.Add(settingItem);
                        _logService.Log(LogLevel.Debug, $"Added setting item for {settingItem.Name}");
                    }
                }
                
                _logService.Log(LogLevel.Info, $"Collected {customizeItems.Count} customize items");
                
                // Add the settings to the dictionary
                sectionSettings["Customize"] = customizeItems;
                _logService.Log(LogLevel.Info, $"Added Customize section with {customizeItems.Count} items");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error collecting Customize settings: {ex.Message}");
                
                // Add an empty list as fallback
                sectionSettings["Customize"] = new List<ISettingItem>();
                _logService.Log(LogLevel.Info, "Added empty Customize section due to error");
            }
        }

        private async Task CollectOptimizeSettings(OptimizeViewModel viewModel, Dictionary<string, IEnumerable<ISettingItem>> sectionSettings)
        {
            try
            {
                _logService.Log(LogLevel.Debug, "Collecting settings from OptimizeViewModel");
                
                // Ensure the view model is initialized
                if (!viewModel.IsInitialized)
                {
                    _logService.Log(LogLevel.Debug, "OptimizeViewModel not initialized, initializing now");
                    await viewModel.InitializeCommand.ExecuteAsync(null);
                    _logService.Log(LogLevel.Debug, "OptimizeViewModel initialized");
                }
                
                // Force load items even if already initialized to ensure we have the latest data
                _logService.Log(LogLevel.Debug, "Loading OptimizeViewModel items");
                await viewModel.LoadItemsAsync();
                _logService.Log(LogLevel.Debug, $"OptimizeViewModel items loaded, count: {viewModel.Items?.Count ?? 0}");
                
                // Collect settings directly
                var optimizeItems = new List<ISettingItem>();
                
                // First check if Items collection has any settings
                if (viewModel.Items != null && viewModel.Items.Count > 0)
                {
                    foreach (var item in viewModel.Items)
                    {
                        if (item is ISettingItem settingItem)
                        {
                            optimizeItems.Add(settingItem);
                            _logService.Log(LogLevel.Debug, $"Added setting item from Items collection: {settingItem.Name}");
                        }
                    }
                }
                
                // If we didn't get any items from the Items collection, try to collect from child view models directly
                if (optimizeItems.Count == 0)
                {
                    _logService.Log(LogLevel.Debug, "No items found in Items collection, collecting from child view models");
                    
                    // Collect from GamingandPerformanceOptimizationsViewModel
                    if (viewModel.GamingandPerformanceOptimizationsViewModel?.Settings != null)
                    {
                        foreach (var setting in viewModel.GamingandPerformanceOptimizationsViewModel.Settings)
                        {
                            if (setting is ISettingItem settingItem)
                            {
                                optimizeItems.Add(settingItem);
                                _logService.Log(LogLevel.Debug, $"Added setting item from Gaming: {settingItem.Name}");
                            }
                        }
                    }
                    
                    // Collect from PrivacyOptimizationsViewModel
                    if (viewModel.PrivacyOptimizationsViewModel?.Settings != null)
                    {
                        foreach (var setting in viewModel.PrivacyOptimizationsViewModel.Settings)
                        {
                            if (setting is ISettingItem settingItem)
                            {
                                optimizeItems.Add(settingItem);
                                _logService.Log(LogLevel.Debug, $"Added setting item from Privacy: {settingItem.Name}");
                            }
                        }
                    }
                    
                    // Collect from UpdateOptimizationsViewModel
                    if (viewModel.UpdateOptimizationsViewModel?.Settings != null)
                    {
                        foreach (var setting in viewModel.UpdateOptimizationsViewModel.Settings)
                        {
                            if (setting is ISettingItem settingItem)
                            {
                                optimizeItems.Add(settingItem);
                                _logService.Log(LogLevel.Debug, $"Added setting item from Updates: {settingItem.Name}");
                            }
                        }
                    }
                    
                    // Collect from PowerSettingsViewModel
                    if (viewModel.PowerSettingsViewModel?.Settings != null)
                    {
                        foreach (var setting in viewModel.PowerSettingsViewModel.Settings)
                        {
                            if (setting is ISettingItem settingItem)
                            {
                                // Special handling for Power Plan
                                if (settingItem.Id == "PowerPlanComboBox" || settingItem.Name.Contains("Power Plan"))
                                {
                                    // Ensure it has the correct ControlType
                                    if (settingItem is ApplicationSettingItem applicationSetting)
                                    {
                                        applicationSetting.ControlType = ControlType.ComboBox;
                                        _logService.Log(LogLevel.Debug, $"Forced ControlType to ComboBox for Power Plan");
                                        
                                        // Get the current power plan value from the view model
                                        if (viewModel.PowerSettingsViewModel != null)
                                        {
                                            // Set the SliderValue to the current power plan index
                                            int powerPlanIndex = viewModel.PowerSettingsViewModel.PowerPlanValue;
                                            applicationSetting.SliderValue = powerPlanIndex;
                                            _logService.Log(LogLevel.Debug, $"Set SliderValue to {powerPlanIndex} for Power Plan");
                                            
                                            // Instead of replacing the item, update its properties
                                            applicationSetting.ControlType = ControlType.ComboBox;
                                            applicationSetting.SliderValue = powerPlanIndex;
                                            
                                            // Create a separate ConfigurationItem for the config file
                                            var configItem = new ConfigurationItem
                                            {
                                                Name = "Power Plan",  // Use a consistent name
                                                IsSelected = true,    // Always enable the Power Plan
                                                ControlType = ControlType.ComboBox,
                                                CustomProperties = new Dictionary<string, object>
                                                {
                                                    { "Id", "PowerPlanComboBox" },  // Use a consistent ID
                                                    { "GroupName", "Power Management" },
                                                    { "Description", "Select power plan for your system" },
                                                    { "SliderValue", powerPlanIndex }
                                                }
                                            };
                                            
                                            // Set the SelectedValue to the current power plan name
                                            if (powerPlanIndex >= 0 && powerPlanIndex < viewModel.PowerSettingsViewModel.PowerPlanLabels.Count)
                                            {
                                                string powerPlanName = viewModel.PowerSettingsViewModel.PowerPlanLabels[powerPlanIndex];
                                                
                                                // Set SelectedValue at the top level (this is what appears in the config file)
                                                configItem.SelectedValue = powerPlanName;
                                                
                                                // Add PowerPlanOptions to CustomProperties (similar to ThemeOptions in Windows Theme)
                                                configItem.CustomProperties["PowerPlanOptions"] = viewModel.PowerSettingsViewModel.PowerPlanLabels.ToList();
                                                _logService.Log(LogLevel.Debug, $"Set SelectedValue to {powerPlanName} for Power Plan ConfigItem");
                                            }
                                            
                                            // Add this ConfigurationItem directly to the optimizeItems collection
                                            // We'll create a wrapper that implements ISettingItem
                                            var powerPlanSettingItem = new PowerPlanSettingItem(configItem, applicationSetting);
                                            
                                            // Add it to the collection if it doesn't already exist
                                            bool exists = false;
                                            foreach (var item in optimizeItems)
                                            {
                                                if (item is PowerPlanSettingItem)
                                                {
                                                    exists = true;
                                                    break;
                                                }
                                            }
                                            
                                            if (!exists)
                                            {
                                                optimizeItems.Add(powerPlanSettingItem);
                                                _logService.Log(LogLevel.Debug, $"Added PowerPlanSettingItem to optimizeItems");
                                            }
                                        }
                                    }
                                    else if (settingItem is ApplicationSettingViewModel applicationViewModel)
                                    {
                                        applicationViewModel.ControlType = ControlType.ComboBox;
                                        _logService.Log(LogLevel.Debug, $"Forced ControlType to ComboBox for Power Plan (ViewModel)");
                                        
                                        // Get the current power plan value from the view model
                                        if (viewModel.PowerSettingsViewModel != null)
                                        {
                                            // Set the SliderValue to the current power plan index
                                            int powerPlanIndex = viewModel.PowerSettingsViewModel.PowerPlanValue;
                                            applicationViewModel.SliderValue = powerPlanIndex;
                                            _logService.Log(LogLevel.Debug, $"Set SliderValue to {powerPlanIndex} for Power Plan (ViewModel)");
                                            
                                            // Instead of replacing the item, update its properties
                                            applicationViewModel.ControlType = ControlType.ComboBox;
                                            applicationViewModel.SliderValue = powerPlanIndex;
                                            
                                            // Create a separate ConfigurationItem for the config file
                                            var configItem = new ConfigurationItem
                                            {
                                                Name = "Power Plan",  // Use a consistent name
                                                IsSelected = true,    // Always enable the Power Plan
                                                ControlType = ControlType.ComboBox,
                                                CustomProperties = new Dictionary<string, object>
                                                {
                                                    { "Id", "PowerPlanComboBox" },  // Use a consistent ID
                                                    { "GroupName", "Power Management" },
                                                    { "Description", "Select power plan for your system" },
                                                    { "SliderValue", powerPlanIndex }
                                                }
                                            };
                                            
                                            // Set the SelectedValue to the current power plan name
                                            if (powerPlanIndex >= 0 && powerPlanIndex < viewModel.PowerSettingsViewModel.PowerPlanLabels.Count)
                                            {
                                                string powerPlanName = viewModel.PowerSettingsViewModel.PowerPlanLabels[powerPlanIndex];
                                                
                                                // Set SelectedValue at the top level (this is what appears in the config file)
                                                configItem.SelectedValue = powerPlanName;
                                                
                                                // Add PowerPlanOptions to CustomProperties (similar to ThemeOptions in Windows Theme)
                                                configItem.CustomProperties["PowerPlanOptions"] = viewModel.PowerSettingsViewModel.PowerPlanLabels.ToList();
                                                _logService.Log(LogLevel.Debug, $"Set SelectedValue to {powerPlanName} for Power Plan ConfigItem (ViewModel)");
                                            }
                                            
                                            // Add this ConfigurationItem directly to the optimizeItems collection
                                            // We'll create a wrapper that implements ISettingItem
                                            var powerPlanSettingItem = new PowerPlanSettingItem(configItem, applicationViewModel);
                                            
                                            // Add it to the collection if it doesn't already exist
                                            bool exists = false;
                                            foreach (var item in optimizeItems)
                                            {
                                                if (item is PowerPlanSettingItem)
                                                {
                                                    exists = true;
                                                    break;
                                                }
                                            }
                                            
                                            if (!exists)
                                            {
                                                optimizeItems.Add(powerPlanSettingItem);
                                                _logService.Log(LogLevel.Debug, $"Added PowerPlanSettingItem to optimizeItems (ViewModel)");
                                            }
                                        }
                                    }
                                }
                                
                                // Skip adding the original Power Plan item since we've already added our custom PowerPlanSettingItem
                                if (!(settingItem.Id == "PowerPlanComboBox" || settingItem.Name.Contains("Power Plan")))
                                {
                                    optimizeItems.Add(settingItem);
                                    _logService.Log(LogLevel.Debug, $"Added setting item from Power: {settingItem.Name}");
                                }
                                else
                                {
                                    _logService.Log(LogLevel.Debug, $"Skipped adding original Power Plan item: {settingItem.Name}");
                                }
                            }
                        }
                    }
                    
                    // Collect from ExplorerOptimizationsViewModel
                    if (viewModel.ExplorerOptimizationsViewModel?.Settings != null)
                    {
                        foreach (var setting in viewModel.ExplorerOptimizationsViewModel.Settings)
                        {
                            if (setting is ISettingItem settingItem)
                            {
                                optimizeItems.Add(settingItem);
                                _logService.Log(LogLevel.Debug, $"Added setting item from Explorer: {settingItem.Name}");
                            }
                        }
                    }
                    
                    // Collect from NotificationOptimizationsViewModel
                    if (viewModel.NotificationOptimizationsViewModel?.Settings != null)
                    {
                        foreach (var setting in viewModel.NotificationOptimizationsViewModel.Settings)
                        {
                            if (setting is ISettingItem settingItem)
                            {
                                optimizeItems.Add(settingItem);
                                _logService.Log(LogLevel.Debug, $"Added setting item from Notifications: {settingItem.Name}");
                            }
                        }
                    }
                    
                    // Collect from SoundOptimizationsViewModel
                    if (viewModel.SoundOptimizationsViewModel?.Settings != null)
                    {
                        foreach (var setting in viewModel.SoundOptimizationsViewModel.Settings)
                        {
                            if (setting is ISettingItem settingItem)
                            {
                                optimizeItems.Add(settingItem);
                                _logService.Log(LogLevel.Debug, $"Added setting item from Sound: {settingItem.Name}");
                            }
                        }
                    }
                    
                    // Collect from WindowsSecuritySettingsViewModel
                    if (viewModel.WindowsSecuritySettingsViewModel?.Settings != null)
                    {
                        foreach (var setting in viewModel.WindowsSecuritySettingsViewModel.Settings)
                        {
                            if (setting is ISettingItem settingItem)
                            {
                                // Special handling for UAC Slider
                                if (settingItem.Id == "UACSlider" || settingItem.Name.Contains("User Account Control"))
                                {
                                    // Ensure it has the correct ControlType
                                    if (settingItem is ApplicationSettingItem applicationSetting)
                                    {
                                        applicationSetting.ControlType = ControlType.ThreeStateSlider;
                                        _logService.Log(LogLevel.Debug, $"Forced ControlType to ThreeStateSlider for UAC Slider");
                                    }
                                    else if (settingItem is ApplicationSettingViewModel applicationViewModel)
                                    {
                                        applicationViewModel.ControlType = ControlType.ThreeStateSlider;
                                        _logService.Log(LogLevel.Debug, $"Forced ControlType to ThreeStateSlider for UAC Slider (ViewModel)");
                                    }
                                }
                                
                                optimizeItems.Add(settingItem);
                                _logService.Log(LogLevel.Debug, $"Added setting item from Security: {settingItem.Name}");
                            }
                        }
                    }
                }
                
                _logService.Log(LogLevel.Info, $"Collected {optimizeItems.Count} optimize items");
                
                // Always create a standalone Power Plan item and add it directly to the configuration file
                if (viewModel.PowerSettingsViewModel != null)
                {
                    try
                    {
                        // Get the current power plan index and name
                        int powerPlanIndex = viewModel.PowerSettingsViewModel.PowerPlanValue;
                        string powerPlanName = null;
                        
                        if (powerPlanIndex >= 0 && powerPlanIndex < viewModel.PowerSettingsViewModel.PowerPlanLabels.Count)
                        {
                            powerPlanName = viewModel.PowerSettingsViewModel.PowerPlanLabels[powerPlanIndex];
                        }
                        
                        // Create a ConfigurationItem for the Power Plan
                        var configItem = new ConfigurationItem
                        {
                            Name = "Power Plan",
                            PackageName = null,
                            IsSelected = true,
                            ControlType = ControlType.ComboBox,
                            SelectedValue = powerPlanName,  // Set SelectedValue directly
                            CustomProperties = new Dictionary<string, object>
                            {
                                { "Id", "PowerPlanComboBox" },
                                { "GroupName", "Power Management" },
                                { "Description", "Select power plan for your system" },
                                { "SliderValue", powerPlanIndex },
                                { "PowerPlanOptions", viewModel.PowerSettingsViewModel.PowerPlanLabels.ToList() }
                            }
                        };
                        
                        // Log the configuration item for debugging
                        _logService.Log(LogLevel.Info, $"Created Power Plan configuration item with SelectedValue: {powerPlanName}, SliderValue: {powerPlanIndex}");
                        _logService.Log(LogLevel.Info, $"PowerPlanOptions: {string.Join(", ", viewModel.PowerSettingsViewModel.PowerPlanLabels)}");
                        
                        // Ensure SelectedValue is not null
                        if (string.IsNullOrEmpty(configItem.SelectedValue) &&
                            configItem.CustomProperties.ContainsKey("PowerPlanOptions") &&
                            configItem.CustomProperties["PowerPlanOptions"] is List<string> options &&
                            options.Count > powerPlanIndex && powerPlanIndex >= 0)
                        {
                            configItem.SelectedValue = options[powerPlanIndex];
                            _logService.Log(LogLevel.Info, $"Set SelectedValue to {configItem.SelectedValue} from PowerPlanOptions");
                        }
                        
                        // Add the item directly to the configuration file
                        var configService = _serviceProvider.GetService<IConfigurationService>();
                        if (configService != null)
                        {
                            // Use reflection to access the ConfigurationFile
                            var configFileProperty = configService.GetType().GetProperty("CurrentConfiguration");
                            if (configFileProperty != null)
                            {
                                var configFile = configFileProperty.GetValue(configService) as ConfigurationFile;
                                if (configFile != null && configFile.Items != null)
                                {
                                    // Remove any existing Power Plan items
                                    configFile.Items.RemoveAll(item =>
                                        item.Name == "Power Plan" ||
                                        (item.CustomProperties != null && item.CustomProperties.TryGetValue("Id", out var id) && id?.ToString() == "PowerPlanComboBox"));
                                    
                                    // Add the new Power Plan item
                                    configFile.Items.Add(configItem);
                                    _logService.Log(LogLevel.Info, $"Added Power Plan item directly to the configuration file with SelectedValue: {powerPlanName}");
                                }
                                else
                                {
                                    _logService.Log(LogLevel.Warning, "Could not access ConfigurationFile.Items");
                                }
                            }
                            else
                            {
                                _logService.Log(LogLevel.Warning, "Could not access CurrentConfiguration property");
                            }
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, "Could not get IConfigurationService from service provider");
                        }
                        
                        // Also create a wrapper that implements ISettingItem for the optimizeItems collection
                        var powerPlanSettingItem = new PowerPlanSettingItem(configItem, null);
                        
                        // Remove any existing Power Plan items from optimizeItems
                        optimizeItems.RemoveAll(item =>
                            (item is PowerPlanSettingItem) ||
                            (item is ApplicationSettingItem settingItem &&
                             (settingItem.Id == "PowerPlanComboBox" || settingItem.Name.Contains("Power Plan"))));
                        
                        // Add the new Power Plan item to optimizeItems
                        optimizeItems.Add(powerPlanSettingItem);
                        _logService.Log(LogLevel.Info, "Added Power Plan item to optimizeItems");
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Error, $"Error creating Power Plan item: {ex.Message}");
                    }
                }
                
                // Add the settings to the dictionary
                sectionSettings["Optimize"] = optimizeItems;
                _logService.Log(LogLevel.Info, $"Added Optimize section with {optimizeItems.Count} items");
                
                // If we still have no items, log a warning
                if (optimizeItems.Count == 0)
                {
                    _logService.Log(LogLevel.Warning, "No optimize items were collected. This may indicate an initialization issue with the OptimizeViewModel or its child view models.");
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error collecting Optimize settings: {ex.Message}");
                
                // Add an empty list as fallback
                sectionSettings["Optimize"] = new List<ISettingItem>();
                _logService.Log(LogLevel.Info, "Added empty Optimize section due to error");
            }
        }
    }
}