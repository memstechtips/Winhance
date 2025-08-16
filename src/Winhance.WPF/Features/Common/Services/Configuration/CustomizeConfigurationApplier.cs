using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.WPF.Features.Common.Views;
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
        private readonly IWindowsThemeService _windowsThemeService;
        private readonly IDialogService _dialogService;

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
        /// <param name="windowsThemeService">The Windows theme service.</param>
        /// <param name="dialogService">The dialog service.</param>
        public CustomizeConfigurationApplier(
            IServiceProvider serviceProvider,
            ILogService logService,
            IViewModelRefresher viewModelRefresher,
            IConfigurationPropertyUpdater propertyUpdater,
            IWindowsThemeService windowsThemeService,
            IDialogService dialogService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _viewModelRefresher = viewModelRefresher ?? throw new ArgumentNullException(nameof(viewModelRefresher));
            _propertyUpdater = propertyUpdater ?? throw new ArgumentNullException(nameof(propertyUpdater));
            _windowsThemeService = windowsThemeService ?? throw new ArgumentNullException(nameof(windowsThemeService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
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
                    await viewModel.InitializeAsync();
                }
                
                int totalUpdatedCount = 0;
                
                // Handle Windows Theme customizations
                totalUpdatedCount += await ApplyWindowsThemeCustomizations(viewModel, configFile);
                
                // Apply the configuration directly to the view model's settings
                int itemsUpdatedCount = await _propertyUpdater.UpdateItemsAsync(viewModel.Settings, configFile);
                totalUpdatedCount += itemsUpdatedCount;
                
                _logService.Log(LogLevel.Info, $"Updated {totalUpdatedCount} items in CustomizeViewModel");
                
                // Refresh the UI
                await _viewModelRefresher.RefreshViewModelAsync(viewModel);
                
                // After applying all configuration settings, prompt for cleaning taskbar and Start Menu
                await PromptForCleaningTaskbarAndStartMenu(viewModel);
                
                return totalUpdatedCount > 0;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying Customize configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Prompts the user to clean the taskbar and Start Menu after configuration import.
        /// </summary>
        /// <param name="viewModel">The customize view model.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task PromptForCleaningTaskbarAndStartMenu(CustomizeViewModel viewModel)
        {
            try
            {
                _logService.Log(LogLevel.Info, "Prompting for cleaning taskbar and Start Menu");
                
                // Prompt for cleaning taskbar
                // Note: In clean architecture, we would need to access taskbar cleaning through the service layer
                // For now, we'll skip this functionality until the service layer provides these operations
                _logService.Log(LogLevel.Info, "Taskbar cleaning functionality needs to be implemented in the service layer");
                
                // TODO: Implement taskbar cleaning through IApplicationSettingsService
                // Example: await _settingsService.CleanTaskbarAsync();
                
                // Prompt for cleaning Start Menu
                // Note: In clean architecture, we would need to access Start Menu cleaning through the service layer
                // For now, we'll skip this functionality until the service layer provides these operations
                _logService.Log(LogLevel.Info, "Start Menu cleaning functionality needs to be implemented in the service layer");
                
                // TODO: Implement Start Menu cleaning through IApplicationSettingsService
                // Example: await _settingsService.CleanStartMenuAsync();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error prompting for cleaning taskbar and Start Menu: {ex.Message}");
            }
        }

        private async Task<int> ApplyWindowsThemeCustomizations(CustomizeViewModel viewModel, ConfigurationFile configFile)
        {
            int updatedCount = 0;
            
            try
            {
                // In clean architecture, theme settings are part of the unified Settings collection
                // Find theme-related settings in the Settings collection
                var themeSettings = viewModel.Settings?.Where(s => s.GroupName?.Contains("Theme") == true || s.GroupName?.Contains("Windows Theme") == true).ToList();
                if (themeSettings == null || !themeSettings.Any())
                {
                    _logService.Log(LogLevel.Warning, "No theme settings found in CustomizeViewModel.Settings");
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
                        
                        // Apply theme through the service layer (clean architecture)
                        try
                        {
                            // Determine if this is a dark mode theme
                            bool isDarkMode = newSelectedTheme.Contains("Dark", StringComparison.OrdinalIgnoreCase);
                            
                            _logService.Log(LogLevel.Info, $"Applying theme through service layer: {newSelectedTheme} (Dark Mode: {isDarkMode})");
                            
                            // Apply the theme through the Windows theme service using proper domain service pattern
                            await _windowsThemeService.ApplySettingAsync("theme-mode-windows", isDarkMode);
                            
                            // Update the corresponding setting in the viewModel to reflect the change
                            var themeSetting = themeSettings.FirstOrDefault(s => s.Name?.Contains("Theme") == true);
                            if (themeSetting != null)
                            {
                                if (themeSetting.ControlType == ControlType.ComboBox)
                                {
                                    themeSetting.SelectedValue = newSelectedTheme;
                                }
                                else if (themeSetting.ControlType == ControlType.BinaryToggle)
                                {
                                    themeSetting.IsSelected = isDarkMode;
                                }
                            }
                            
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