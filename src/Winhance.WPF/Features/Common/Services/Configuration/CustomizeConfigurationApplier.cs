using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.WPF.Features.Customize.ViewModels;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Service for applying configuration to the Customize section.
    /// </summary>
    public class CustomizeConfigurationApplier : ISectionConfigurationApplier
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logService;
        private readonly IViewModelRefresher _viewModelRefresher;
        private readonly IConfigurationPropertyUpdater _propertyUpdater;
        private readonly IThemeService _themeService;

        /// <summary>
        /// Gets the section name that this applier handles.
        /// </summary>
        public string SectionName => "Customize";

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomizeConfigurationApplier"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="viewModelRefresher">The view model refresher.</param>
        /// <param name="propertyUpdater">The property updater.</param>
        /// <param name="themeService">The theme service.</param>
        public CustomizeConfigurationApplier(
            IServiceProvider serviceProvider,
            ILogService logService,
            IViewModelRefresher viewModelRefresher,
            IConfigurationPropertyUpdater propertyUpdater,
            IThemeService themeService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _viewModelRefresher = viewModelRefresher ?? throw new ArgumentNullException(nameof(viewModelRefresher));
            _propertyUpdater = propertyUpdater ?? throw new ArgumentNullException(nameof(propertyUpdater));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        }

        /// <summary>
        /// Applies the configuration to the Customize section.
        /// </summary>
        /// <param name="configFile">The configuration file to apply.</param>
        /// <returns>True if any items were updated, false otherwise.</returns>
        public async Task<bool> ApplyConfigAsync(ConfigurationFile configFile)
        {
            try
            {
                _logService.Log(LogLevel.Info, "Applying configuration to CustomizeViewModel");
                
                var viewModel = _serviceProvider.GetService<CustomizeViewModel>();
                if (viewModel == null)
                {
                    _logService.Log(LogLevel.Warning, "CustomizeViewModel not available");
                    return false;
                }
                
                // Ensure the view model is initialized
                if (!viewModel.IsInitialized)
                {
                    _logService.Log(LogLevel.Info, "CustomizeViewModel not initialized, initializing now");
                    await viewModel.InitializeCommand.ExecuteAsync(null);
                }
                
                int totalUpdatedCount = 0;
                
                // Handle Windows Theme customizations
                totalUpdatedCount += await ApplyWindowsThemeCustomizations(viewModel, configFile);
                
                // Apply the configuration directly to the view model's items
                int itemsUpdatedCount = await _propertyUpdater.UpdateItemsAsync(viewModel.Items, configFile);
                totalUpdatedCount += itemsUpdatedCount;
                
                _logService.Log(LogLevel.Info, $"Updated {totalUpdatedCount} items in CustomizeViewModel");
                
                // Refresh the UI
                await _viewModelRefresher.RefreshViewModelAsync(viewModel);
                
                return totalUpdatedCount > 0;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying Customize configuration: {ex.Message}");
                return false;
            }
        }

        private async Task<int> ApplyWindowsThemeCustomizations(CustomizeViewModel viewModel, ConfigurationFile configFile)
        {
            int updatedCount = 0;
            
            try
            {
                // Get the WindowsThemeSettings property from the view model
                var windowsThemeViewModel = viewModel.WindowsThemeSettings;
                if (windowsThemeViewModel == null)
                {
                    _logService.Log(LogLevel.Warning, "WindowsThemeSettings not found in CustomizeViewModel");
                    return 0;
                }
                
                _logService.Log(LogLevel.Info, "Found WindowsThemeSettings, checking for Theme Selector item");
                
                // Check if there's a Theme Selector item in the config file
                var themeItem = configFile.Items?.FirstOrDefault(item =>
                    (item.Name?.Contains("Windows Theme") == true ||
                     item.Name?.Contains("Theme Selector") == true ||
                     item.Name?.Contains("Choose Your Mode") == true ||
                     (item.CustomProperties.TryGetValue("Id", out var id) && id?.ToString() == "ThemeSelector")));
                
                if (themeItem != null)
                {
                    _logService.Log(LogLevel.Info, $"Found Theme Selector item: {themeItem.Name}");
                    
                    string newSelectedTheme = null;
                    
                    // Try to get SelectedTheme from CustomProperties first (preferred method)
                    if (themeItem.CustomProperties.TryGetValue("SelectedTheme", out var selectedTheme) && selectedTheme != null)
                    {
                        newSelectedTheme = selectedTheme.ToString();
                        _logService.Log(LogLevel.Info, $"Found SelectedTheme in CustomProperties: {newSelectedTheme}");
                    }
                    // If not available, try to use SelectedValue directly
                    else if (!string.IsNullOrEmpty(themeItem.SelectedValue))
                    {
                        newSelectedTheme = themeItem.SelectedValue;
                        _logService.Log(LogLevel.Info, $"Using SelectedValue directly: {newSelectedTheme}");
                    }
                    // As a last resort, try to derive it from SliderValue (for backward compatibility)
                    else if (themeItem.CustomProperties.TryGetValue("SliderValue", out var sliderValue))
                    {
                        int sliderValueInt = Convert.ToInt32(sliderValue);
                        newSelectedTheme = sliderValueInt == 1 ? "Dark Mode" : "Light Mode";
                        _logService.Log(LogLevel.Info, $"Derived SelectedTheme from SliderValue {sliderValueInt}: {newSelectedTheme}");
                    }
                    
                    if (!string.IsNullOrEmpty(newSelectedTheme))
                    {
                        _logService.Log(LogLevel.Info, $"Updating theme settings in view model to: {newSelectedTheme}");
                        
                        // Store the current state of the view model
                        bool currentIsDarkMode = windowsThemeViewModel.IsDarkModeEnabled;
                        string currentTheme = windowsThemeViewModel.SelectedTheme;
                        
                        // Update the view model properties to trigger the property change handlers
                        // This will show the wallpaper dialog through the normal UI flow
                        try
                        {
                            // Update the IsDarkModeEnabled property first
                            bool isDarkMode = newSelectedTheme == "Dark Mode";
                            
                            _logService.Log(LogLevel.Info, $"Setting IsDarkModeEnabled to {isDarkMode}");
                            windowsThemeViewModel.IsDarkModeEnabled = isDarkMode;
                            
                            // Then update the SelectedTheme property
                            _logService.Log(LogLevel.Info, $"Setting SelectedTheme to {newSelectedTheme}");
                            windowsThemeViewModel.SelectedTheme = newSelectedTheme;
                            
                            // The property change handlers in WindowsThemeCustomizationsViewModel will
                            // show the wallpaper dialog and apply the theme
                            
                            _logService.Log(LogLevel.Success, $"Successfully triggered theme change UI flow for: {newSelectedTheme}");
                            updatedCount++;
                        }
                        catch (Exception ex)
                        {
                            _logService.Log(LogLevel.Error, $"Error updating theme settings in view model: {ex.Message}");
                        }
                    }
                    else
                    {
                        _logService.Log(LogLevel.Warning, "Could not determine SelectedTheme from config item");
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "Theme Selector item not found in config file");
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying Windows theme customizations: {ex.Message}");
            }
            
            return updatedCount;
        }
    }
}