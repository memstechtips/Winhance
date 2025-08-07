using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Customize.ViewModels;
using Winhance.WPF.Features.Optimize.ViewModels;
using Winhance.WPF.Features.SoftwareApps.Models;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// SOLID-compliant service for collecting configuration settings from various ViewModels.
    /// Follows Single Responsibility Principle by focusing only on configuration collection.
    /// Uses Dependency Inversion Principle by depending on abstractions (IFeatureViewModel).
    /// </summary>
    public class ConfigurationCollectorService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logService;

        public ConfigurationCollectorService(IServiceProvider serviceProvider, ILogService logService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Collects all configuration settings from registered ViewModels.
        /// Uses SOLID principles by working with individual feature ViewModels.
        /// </summary>
        /// <returns>Dictionary of section settings organized by category.</returns>
        public async Task<Dictionary<string, IEnumerable<ISettingItem>>> CollectAllSettingsAsync()
        {
            var sectionSettings = new Dictionary<string, IEnumerable<ISettingItem>>();

            try
            {
                _logService.Log(LogLevel.Info, "Starting configuration collection using SOLID architecture");

                // Collect from Software Apps ViewModels
                await CollectSoftwareAppsSettingsAsync(sectionSettings);

                // Collect from Customize ViewModels  
                await CollectCustomizeSettingsAsync(sectionSettings);

                // Collect from individual Optimization feature ViewModels (SOLID approach)
                await CollectOptimizationSettingsAsync(sectionSettings);

                _logService.Log(LogLevel.Info, $"Configuration collection completed. Total sections: {sectionSettings.Count}");
                
                return sectionSettings;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error during configuration collection: {ex.Message}");
                return sectionSettings;
            }
        }

        /// <summary>
        /// Collects settings from Software Apps ViewModels.
        /// </summary>
        private async Task CollectSoftwareAppsSettingsAsync(Dictionary<string, IEnumerable<ISettingItem>> sectionSettings)
        {
            // Collect Windows Apps
            var windowsAppsViewModel = _serviceProvider.GetService<WindowsAppsViewModel>();
            if (windowsAppsViewModel != null)
            {
                await CollectWindowsAppsSettings(windowsAppsViewModel, sectionSettings);
            }

            // Collect External Apps
            var externalAppsViewModel = _serviceProvider.GetService<ExternalAppsViewModel>();
            if (externalAppsViewModel != null)
            {
                await CollectExternalAppsSettings(externalAppsViewModel, sectionSettings);
            }
        }

        /// <summary>
        /// Collects settings from Customize ViewModels.
        /// </summary>
        private async Task CollectCustomizeSettingsAsync(Dictionary<string, IEnumerable<ISettingItem>> sectionSettings)
        {
            // Get individual customization feature ViewModels (SOLID approach)
            var windowsThemeViewModel = _serviceProvider.GetService<WindowsThemeCustomizationsViewModel>();
            var startMenuViewModel = _serviceProvider.GetService<StartMenuCustomizationsViewModel>();
            var taskbarViewModel = _serviceProvider.GetService<TaskbarCustomizationsViewModel>();
            var explorerCustomViewModel = _serviceProvider.GetService<ExplorerCustomizationsViewModel>();

            // Collect from individual customization feature ViewModels
            if (windowsThemeViewModel != null)
            {
                await CollectFeatureSettings(windowsThemeViewModel, "Windows Theme", sectionSettings);
            }

            if (startMenuViewModel != null)
            {
                await CollectFeatureSettings(startMenuViewModel, "Start Menu", sectionSettings);
            }

            if (taskbarViewModel != null)
            {
                await CollectFeatureSettings(taskbarViewModel, "Taskbar", sectionSettings);
            }

            if (explorerCustomViewModel != null)
            {
                await CollectFeatureSettings(explorerCustomViewModel, "Explorer Customization", sectionSettings);
            }
        }

        /// <summary>
        /// Collects settings from individual Optimization feature ViewModels.
        /// This follows SOLID principles by working with each feature independently.
        /// </summary>
        private async Task CollectOptimizationSettingsAsync(Dictionary<string, IEnumerable<ISettingItem>> sectionSettings)
        {
            // Get individual optimization feature ViewModels (SOLID approach)
            var gamingViewModel = _serviceProvider.GetService<GamingandPerformanceOptimizationsViewModel>();
            var powerViewModel = _serviceProvider.GetService<PowerOptimizationsViewModel>();
            var privacyViewModel = _serviceProvider.GetService<PrivacyOptimizationsViewModel>();
            var updateViewModel = _serviceProvider.GetService<UpdateOptimizationsViewModel>();
            var securityViewModel = _serviceProvider.GetService<WindowsSecurityOptimizationsViewModel>();
            var explorerOptViewModel = _serviceProvider.GetService<ExplorerOptimizationsViewModel>();
            var notificationViewModel = _serviceProvider.GetService<NotificationOptimizationsViewModel>();
            var soundViewModel = _serviceProvider.GetService<SoundOptimizationsViewModel>();

            // Collect from individual optimization feature ViewModels
            if (gamingViewModel != null)
            {
                await CollectFeatureSettings(gamingViewModel, "Gaming and Performance", sectionSettings);
            }

            if (powerViewModel != null)
            {
                await CollectFeatureSettings(powerViewModel, "Power", sectionSettings);
            }

            if (privacyViewModel != null)
            {
                await CollectFeatureSettings(privacyViewModel, "Privacy", sectionSettings);
            }

            if (updateViewModel != null)
            {
                await CollectFeatureSettings(updateViewModel, "Update", sectionSettings);
            }

            if (securityViewModel != null)
            {
                await CollectFeatureSettings(securityViewModel, "Security", sectionSettings);
            }

            if (explorerOptViewModel != null)
            {
                await CollectFeatureSettings(explorerOptViewModel, "Explorer", sectionSettings);
            }

            if (notificationViewModel != null)
            {
                await CollectFeatureSettings(notificationViewModel, "Notification", sectionSettings);
            }

            if (soundViewModel != null)
            {
                await CollectFeatureSettings(soundViewModel, "Sound", sectionSettings);
            }
        }

        /// <summary>
        /// Generic method to collect settings from any feature ViewModel that implements IFeatureViewModel.
        /// This method follows SOLID principles by working with the interface abstraction.
        /// </summary>
        /// <param name="featureViewModel">The feature ViewModel to collect settings from.</param>
        /// <param name="sectionName">The name of the section for logging and organization.</param>
        /// <param name="sectionSettings">The dictionary to add settings to.</param>
        private async Task CollectFeatureSettings(IFeatureViewModel featureViewModel, string sectionName, Dictionary<string, IEnumerable<ISettingItem>> sectionSettings)
        {
            try
            {
                _logService.Log(LogLevel.Debug, $"Collecting settings from {sectionName} feature ViewModel");

                // Load settings to ensure we have the latest data
                await featureViewModel.LoadSettingsAsync();
                _logService.Log(LogLevel.Debug, $"{sectionName} settings loaded, count: {featureViewModel.Settings?.Count ?? 0}");

                // Collect settings from the feature
                var featureItems = new List<ISettingItem>();

                if (featureViewModel.Settings != null && featureViewModel.Settings.Count > 0)
                {
                    foreach (var setting in featureViewModel.Settings)
                    {
                        if (setting is ISettingItem settingItem)
                        {
                            featureItems.Add(settingItem);
                            _logService.Log(LogLevel.Debug, $"Added setting item from {sectionName}: {settingItem.Name}");
                        }
                    }
                }

                // Add the settings to the dictionary using the section name
                if (!sectionSettings.ContainsKey(sectionName))
                {
                    sectionSettings[sectionName] = featureItems;
                }
                else
                {
                    // If section already exists, merge the items
                    var existingItems = sectionSettings[sectionName].ToList();
                    existingItems.AddRange(featureItems);
                    sectionSettings[sectionName] = existingItems;
                }

                _logService.Log(LogLevel.Info, $"Added {sectionName} section with {featureItems.Count} items");

                // Log warning if no items were collected
                if (featureItems.Count == 0)
                {
                    _logService.Log(LogLevel.Warning, $"No {sectionName} items were collected. This may indicate an initialization issue with the {sectionName} ViewModel.");
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error collecting {sectionName} settings: {ex.Message}");

                // Add an empty list as fallback
                sectionSettings[sectionName] = new List<ISettingItem>();
                _logService.Log(LogLevel.Info, $"Added empty {sectionName} section due to error");
            }
        }

        /// <summary>
        /// Collects settings from WindowsAppsViewModel.
        /// </summary>
        private async Task CollectWindowsAppsSettings(WindowsAppsViewModel viewModel, Dictionary<string, IEnumerable<ISettingItem>> sectionSettings)
        {
            try
            {
                _logService.Log(LogLevel.Debug, "Collecting settings from WindowsAppsViewModel");

                // Ensure the view model is initialized
                if (!viewModel.IsInitialized)
                {
                    _logService.Log(LogLevel.Debug, "WindowsAppsViewModel not initialized, loading items");
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

                // Add the settings to the dictionary
                sectionSettings["WindowsApps"] = windowsAppSettingItems;
                _logService.Log(LogLevel.Info, $"Added WindowsApps section with {windowsAppSettingItems.Count} items");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error collecting WindowsApps settings: {ex.Message}");

                // Add an empty list as fallback
                sectionSettings["WindowsApps"] = new List<ISettingItem>();
                _logService.Log(LogLevel.Info, "Added empty WindowsApps section due to error");
            }
        }

        /// <summary>
        /// Collects settings from ExternalAppsViewModel.
        /// </summary>
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
    }
}
