using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces.WindowsRegistry;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Services.Configuration;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Service for managing unified configuration operations across the application.
    /// </summary>
    public class UnifiedConfigurationService : IUnifiedConfigurationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfigurationService _configurationService;
        private readonly ILogService _logService;
        private readonly IDialogService _dialogService;
        private readonly IWindowsRegistryService _registryService;
        private readonly ConfigurationCollectorService _collectorService;
        private readonly IConfigurationApplierService _applierService;
        private readonly ConfigurationUIService _uiService;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnifiedConfigurationService"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="configurationService">The configuration service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="registryService">The registry service.</param>
        public UnifiedConfigurationService(
            IServiceProvider serviceProvider,
            IConfigurationService configurationService,
            ILogService logService,
            IDialogService dialogService,
            IWindowsRegistryService windowsRegistryService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _registryService = windowsRegistryService ?? throw new ArgumentNullException(nameof(windowsRegistryService));
            
            // Initialize helper services
            _collectorService = new ConfigurationCollectorService(serviceProvider, logService);
            _applierService = serviceProvider.GetRequiredService<IConfigurationApplierService>();
            _uiService = new ConfigurationUIService(logService);
        }

        /// <summary>
        /// Creates a unified configuration file containing settings from all view models.
        /// </summary>
        /// <returns>A task representing the asynchronous operation. Returns the unified configuration file.</returns>
        public async Task<UnifiedConfigurationFile> CreateUnifiedConfigurationAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Creating unified configuration from all view models");
                
                // Collect settings from all view models
                var sectionSettings = await _collectorService.CollectAllSettingsAsync();
                
                // Create a list of all available sections - include all sections by default
                var availableSections = new List<string> { "WindowsApps", "ExternalApps", "Customize", "Optimize" };

                // Create and return the unified configuration
                var unifiedConfig = _configurationService.CreateUnifiedConfiguration(sectionSettings, availableSections);
                
                _logService.Log(LogLevel.Info, "Successfully created unified configuration from all view models");
                
                return unifiedConfig;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error creating unified configuration: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Saves a unified configuration file.
        /// </summary>
        /// <param name="config">The unified configuration to save.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false otherwise.</returns>
        public async Task<bool> SaveUnifiedConfigurationAsync(UnifiedConfigurationFile config)
        {
            try
            {
                _logService.Log(LogLevel.Info, "Saving unified configuration");
                
                bool saveResult = await _configurationService.SaveUnifiedConfigurationAsync(config);
                
                if (saveResult)
                {
                    _logService.Log(LogLevel.Info, "Unified configuration saved successfully");
                    // Success dialog is now shown only in MainViewModel to avoid duplicate dialogs
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
                _dialogService.ShowMessage($"Error saving unified configuration: {ex.Message}", "Error");
                return false;
            }
        }

        /// <summary>
        /// Loads a unified configuration file.
        /// </summary>
        /// <returns>A task representing the asynchronous operation. Returns the unified configuration file if successful, null otherwise.</returns>
        public async Task<UnifiedConfigurationFile> LoadUnifiedConfigurationAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading unified configuration");
                
                var unifiedConfig = await _configurationService.LoadUnifiedConfigurationAsync();
                
                if (unifiedConfig == null)
                {
                    _logService.Log(LogLevel.Info, "Load unified configuration canceled by user");
                    return null;
                }
                
                _logService.Log(LogLevel.Info, $"Configuration loaded with sections: WindowsApps ({unifiedConfig.WindowsApps.Items.Count} items), " +
                                  $"ExternalApps ({unifiedConfig.ExternalApps.Items.Count} items), " +
                                  $"Customize ({unifiedConfig.Customize.Items.Count} items), " +
                                  $"Optimize ({unifiedConfig.Optimize.Items.Count} items)");
                
                return unifiedConfig;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading unified configuration: {ex.Message}");
                _dialogService.ShowMessage($"Error loading unified configuration: {ex.Message}", "Error");
                return null;
            }
        }

        /// <summary>
        /// Shows the unified configuration dialog to let the user select which sections to include.
        /// </summary>
        /// <param name="config">The unified configuration file.</param>
        /// <param name="isSaveDialog">Whether this is a save dialog (true) or an import dialog (false).</param>
        /// <returns>A dictionary of section names and their selection state.</returns>
        public async Task<Dictionary<string, bool>> ShowUnifiedConfigurationDialogAsync(UnifiedConfigurationFile config, bool isSaveDialog)
        {
            return await _uiService.ShowUnifiedConfigurationDialogAsync(config, isSaveDialog);
        }

        /// <summary>
        /// Applies a unified configuration to the selected sections.
        /// </summary>
        /// <param name="config">The unified configuration file.</param>
        /// <param name="selectedSections">The sections to apply.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false otherwise.</returns>
        public async Task<bool> ApplyUnifiedConfigurationAsync(UnifiedConfigurationFile config, IEnumerable<string> selectedSections)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying unified configuration to selected sections: {string.Join(", ", selectedSections)}");

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
                
                // Apply the configuration to the selected sections
                var sectionResults = await _applierService.ApplySectionsAsync(config, selectedSections);
                
                // Log the results for each section
                _logService.Log(LogLevel.Info, "Import results by section:");
                foreach (var sectionResult in sectionResults)
                {
                    _logService.Log(LogLevel.Info, $"  {sectionResult.Key}: {(sectionResult.Value ? "Success" : "Failed")}");
                }
                
                bool overallResult = sectionResults.All(r => r.Value);
                _logService.Log(LogLevel.Info, $"Finished applying unified configuration to selected sections. Overall result: {(overallResult ? "Success" : "Partial failure")}");
                
                return overallResult;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying unified configuration: {ex.Message}");
                return false;
            }
        }
    }
}
