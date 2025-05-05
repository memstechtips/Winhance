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
        private readonly IThemeService _themeService;
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
        /// <param name="themeService">The theme service.</param>
        /// <param name="dialogService">The dialog service.</param>
        public CustomizeConfigurationApplier(
            IServiceProvider serviceProvider,
            ILogService logService,
            IViewModelRefresher viewModelRefresher,
            IConfigurationPropertyUpdater propertyUpdater,
            IThemeService themeService,
            IDialogService dialogService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _viewModelRefresher = viewModelRefresher ?? throw new ArgumentNullException(nameof(viewModelRefresher));
            _propertyUpdater = propertyUpdater ?? throw new ArgumentNullException(nameof(propertyUpdater));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
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
                if (viewModel.TaskbarSettings != null)
                {
                    // Use Application.Current.Dispatcher to ensure we're on the UI thread
                    bool? cleanTaskbarResult = await Application.Current.Dispatcher.InvokeAsync(() => {
                        return CustomDialog.ShowConfirmation(
                            "Clean Taskbar",
                            "Do you want to clean the taskbar?",
                            new List<string> { "Cleaning the taskbar will remove pinned items and reset it to default settings." }, // Put message in the middle section
                            "" // Empty footer
                        );
                    });
                    
                    bool cleanTaskbar = cleanTaskbarResult == true;
                    
                    if (cleanTaskbar)
                    {
                        _logService.Log(LogLevel.Info, "User chose to clean the taskbar");
                        
                        // Execute the clean taskbar command
                        if (viewModel.TaskbarSettings.CleanTaskbarCommand != null && 
                            viewModel.TaskbarSettings.CleanTaskbarCommand.CanExecute(null))
                        {
                            await viewModel.TaskbarSettings.CleanTaskbarCommand.ExecuteAsync(null);
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, "CleanTaskbarCommand not available");
                        }
                    }
                    else
                    {
                        _logService.Log(LogLevel.Info, "User chose not to clean the taskbar");
                    }
                }
                
                // Prompt for cleaning Start Menu
                if (viewModel.StartMenuSettings != null)
                {
                    // Use Application.Current.Dispatcher to ensure we're on the UI thread
                    bool? cleanStartMenuResult = await Application.Current.Dispatcher.InvokeAsync(() => {
                        return CustomDialog.ShowConfirmation(
                            "Clean Start Menu",
                            "Do you want to clean the Start Menu?",
                            new List<string> { "Cleaning the Start Menu will remove pinned items and reset it to default settings." }, // Put message in the middle section
                            "" // Empty footer
                        );
                    });
                    
                    bool cleanStartMenu = cleanStartMenuResult == true;
                    
                    if (cleanStartMenu)
                    {
                        _logService.Log(LogLevel.Info, "User chose to clean the Start Menu");
                        
                        // Execute the clean Start Menu command
                        if (viewModel.StartMenuSettings.CleanStartMenuCommand != null && 
                            viewModel.StartMenuSettings.CleanStartMenuCommand.CanExecute(null))
                        {
                            await viewModel.StartMenuSettings.CleanStartMenuCommand.ExecuteAsync(null);
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, "CleanStartMenuCommand not available");
                        }
                    }
                    else
                    {
                        _logService.Log(LogLevel.Info, "User chose not to clean the Start Menu");
                    }
                }
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