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
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// Base class for view models that handle configuration saving and loading.
    /// </summary>
    /// <typeparam name="T">The type of setting item.</typeparam>
    public abstract class ConfigurationViewModelBase<T> : ObservableObject where T : class, ISettingItem
    {
        private readonly IConfigurationService _configurationService;
        private readonly ILogService _logService;
        private readonly IDialogService _dialogService;

        /// <summary>
        /// Gets the configuration type for this view model.
        /// </summary>
        public abstract string ConfigType { get; }

        /// <summary>
        /// Gets the settings collection.
        /// </summary>
        public abstract ObservableCollection<T> Settings { get; }

        // SaveConfigCommand and ImportConfigCommand removed as part of unified configuration cleanup

        /// <summary>
        /// Gets the command to save the unified configuration.
        /// </summary>
        public IAsyncRelayCommand SaveUnifiedConfigCommand { get; }

        /// <summary>
        /// Gets the command to import the unified configuration.
        /// </summary>
        public IAsyncRelayCommand ImportUnifiedConfigCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationViewModelBase{T}"/> class.
        /// </summary>
        /// <param name="configurationService">The configuration service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        protected ConfigurationViewModelBase(
            IConfigurationService configurationService,
            ILogService logService,
            IDialogService dialogService)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            // Create commands
            // SaveConfigCommand and ImportConfigCommand removed as part of unified configuration cleanup
            SaveUnifiedConfigCommand = new AsyncRelayCommand(SaveUnifiedConfig);
            ImportUnifiedConfigCommand = new AsyncRelayCommand(ImportUnifiedConfig);
        }

        // SaveConfig and ImportConfig methods removed as part of unified configuration cleanup

        /// <summary>
        /// Saves a unified configuration file containing settings for multiple parts of the application.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task<bool> SaveUnifiedConfig()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Starting to save unified configuration");
                
                // Create a dictionary of sections with their availability and item counts
                var sections = new Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)>
                {
                    { "WindowsApps", (false, false, 0) },
                    { "ExternalApps", (false, false, 0) },
                    { "Customize", (false, false, 0) },
                    { "Optimize", (false, false, 0) }
                };
                
                // Always include the current section
                sections[ConfigType] = (true, true, Settings.Count);
                
                // Show the unified configuration save dialog
                var result = await _dialogService.ShowUnifiedConfigurationSaveDialogAsync(
                    "Save Unified Configuration",
                    "Select which sections to include in the unified configuration:",
                    sections);
                
                if (result == null)
                {
                    _logService.Log(LogLevel.Info, "Save unified configuration canceled by user");
                    return false;
                }
                
                // Get the selected sections
                var selectedSections = result.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
                
                if (!selectedSections.Any())
                {
                    _logService.Log(LogLevel.Info, "No sections selected for unified configuration");
                    _dialogService.ShowMessage("No sections selected", "Please select at least one section to include in the unified configuration.");
                    return false;
                }
                
                // Create a dictionary of sections and their settings
                var sectionSettings = new Dictionary<string, IEnumerable<ISettingItem>>();
                
                // Add the current section's settings
                sectionSettings[ConfigType] = Settings;
                
                // Create a unified configuration
                var unifiedConfig = _configurationService.CreateUnifiedConfiguration(sectionSettings, selectedSections);
                
                // Save the unified configuration
                bool saveResult = await _configurationService.SaveUnifiedConfigurationAsync(unifiedConfig);
                
                if (saveResult)
                {
                    _logService.Log(LogLevel.Info, "Unified configuration saved successfully");
                    
                    // Show a success message
                    _dialogService.ShowMessage(
                        "The unified configuration has been saved successfully.",
                        "Unified Configuration Saved");
                }
                else
                {
                    _logService.Log(LogLevel.Info, "Save unified configuration canceled by user");
                }

                return saveResult;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error saving unified configuration: {ex.Message}");
                await _dialogService.ShowErrorAsync("Error", $"Error saving unified configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Imports a unified configuration file.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task<bool> ImportUnifiedConfig()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Starting to import unified configuration");
                
                // Load the unified configuration
                var unifiedConfig = await _configurationService.LoadUnifiedConfigurationAsync();
                
                if (unifiedConfig == null)
                {
                    _logService.Log(LogLevel.Info, "Import unified configuration canceled by user");
                    return false;
                }
                
                // Check which sections are available in the unified configuration
                var sections = new Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)>();
                
                // Check WindowsApps section
                if (unifiedConfig.WindowsApps != null && unifiedConfig.WindowsApps.Items != null)
                {
                    sections["WindowsApps"] = (true, true, unifiedConfig.WindowsApps.Items.Count);
                }
                else
                {
                    sections["WindowsApps"] = (false, false, 0);
                }
                
                // Check ExternalApps section
                if (unifiedConfig.ExternalApps != null && unifiedConfig.ExternalApps.Items != null)
                {
                    sections["ExternalApps"] = (true, true, unifiedConfig.ExternalApps.Items.Count);
                }
                else
                {
                    sections["ExternalApps"] = (false, false, 0);
                }
                
                // Check Customize section
                if (unifiedConfig.Customize != null && unifiedConfig.Customize.Items != null)
                {
                    sections["Customize"] = (true, true, unifiedConfig.Customize.Items.Count);
                }
                else
                {
                    sections["Customize"] = (false, false, 0);
                }
                
                // Check Optimize section
                if (unifiedConfig.Optimize != null && unifiedConfig.Optimize.Items != null)
                {
                    sections["Optimize"] = (true, true, unifiedConfig.Optimize.Items.Count);
                }
                else
                {
                    sections["Optimize"] = (false, false, 0);
                }
                
                // Show the unified configuration import dialog
                var result = await _dialogService.ShowUnifiedConfigurationImportDialogAsync(
                    "Import Unified Configuration",
                    "Select which sections to import from the unified configuration:",
                    sections);
                
                if (result == null)
                {
                    _logService.Log(LogLevel.Info, "Import unified configuration canceled by user");
                    return false;
                }
                
                // Get the selected sections
                var selectedSections = result.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
                
                if (!selectedSections.Any())
                {
                    _logService.Log(LogLevel.Info, "No sections selected for import");
                    _dialogService.ShowMessage("Please select at least one section to import from the unified configuration.", "No sections selected");
                    return false;
                }
                
                // Check if the current section is selected
                if (!selectedSections.Contains(ConfigType))
                {
                    _logService.Log(LogLevel.Info, $"Section {ConfigType} is not selected for import");
                    _dialogService.ShowMessage(
                        $"The {ConfigType} section is not selected for import.",
                        "Section Not Selected");
                    return false;
                }
                
                // Extract the current section from the unified configuration
                var configFile = _configurationService.ExtractSectionFromUnifiedConfiguration(unifiedConfig, ConfigType);
                
                if (configFile != null && configFile.Items != null && configFile.Items.Any())
                {
                    _logService.Log(LogLevel.Info, $"Successfully extracted {ConfigType} section with {configFile.Items.Count} items");
                    
                    // Update the settings based on the loaded configuration
                    int updatedCount = await ApplyConfigurationToSettings(configFile);
                    
                    _logService.Log(LogLevel.Info, $"{ConfigType} configuration imported successfully. Updated {updatedCount} settings.");
                    
                    // Get the names of all items that were set to IsSelected = true
                    var selectedItems = Settings.Where(item => item.IsSelected).Select(item => item.Name).ToList();
                    
                    // Show dialog with the list of imported items
                    CustomDialog.ShowInformation(
                        "Configuration Imported",
                        "Configuration imported successfully.",
                        selectedItems,
                        $"The imported settings have been applied."
                    );

                    return true;
                }
                else
                {
                    _logService.Log(LogLevel.Info, $"No {ConfigType} configuration imported");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error importing unified configuration: {ex.Message}");
                await _dialogService.ShowErrorAsync("Error", $"Error importing unified configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies a loaded configuration to the settings.
        /// </summary>
        /// <param name="configFile">The configuration file to apply.</param>
        /// <returns>The number of settings that were updated.</returns>
        protected virtual async Task<int> ApplyConfigurationToSettings(ConfigurationFile configFile)
        {
            // This method should be overridden by derived classes to apply the configuration to their specific settings
            await Task.CompletedTask; // To keep the async signature
            return 0;
        }
    }
}