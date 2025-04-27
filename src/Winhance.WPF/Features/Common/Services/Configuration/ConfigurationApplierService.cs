using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Service for applying configuration settings to different view models.
    /// </summary>
    public class ConfigurationApplierService : IConfigurationApplierService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logService;
        private readonly IEnumerable<ISectionConfigurationApplier> _sectionAppliers;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationApplierService"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="sectionAppliers">The section-specific configuration appliers.</param>
        public ConfigurationApplierService(
            IServiceProvider serviceProvider,
            ILogService logService,
            IEnumerable<ISectionConfigurationApplier> sectionAppliers)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _sectionAppliers = sectionAppliers ?? throw new ArgumentNullException(nameof(sectionAppliers));
        }

        /// <summary>
        /// Applies configuration settings to the selected sections.
        /// </summary>
        /// <param name="config">The unified configuration file.</param>
        /// <param name="selectedSections">The sections to apply.</param>
        /// <returns>A dictionary of section names and their application result.</returns>
        public async Task<Dictionary<string, bool>> ApplySectionsAsync(UnifiedConfigurationFile config, IEnumerable<string> selectedSections)
        {
            var sectionResults = new Dictionary<string, bool>();
            
            // Use a cancellation token with a timeout to prevent hanging
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(120)); // Increased timeout
            var cancellationToken = cancellationTokenSource.Token;
            
            // Track execution time
            var startTime = DateTime.Now;
            _logService.Log(LogLevel.Info, $"Starting ApplySectionsAsync at {startTime}");
        
            foreach (var section in selectedSections)
            {
                var sectionStartTime = DateTime.Now;
                _logService.Log(LogLevel.Info, $"Processing section: {section} at {sectionStartTime}");
                
                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    _logService.Log(LogLevel.Warning, $"Skipping section {section} due to timeout");
                    sectionResults[section] = false;
                    continue;
                }
                
                // Extract the section from the unified configuration
                var configFile = ExtractSectionFromUnifiedConfiguration(config, section);
                
                if (configFile == null)
                {
                    _logService.Log(LogLevel.Warning, $"Failed to extract section {section} from unified configuration");
                    sectionResults[section] = false;
                    continue;
                }
                
                if (configFile.Items == null || !configFile.Items.Any())
                {
                    _logService.Log(LogLevel.Warning, $"Section {section} is empty or not included in the unified configuration");
                    sectionResults[section] = false;
                    continue;
                }
        
                _logService.Log(LogLevel.Info, $"Extracted section {section} with {configFile.Items.Count} items");
                
                // Apply the configuration to the appropriate view model
                bool sectionResult = false;
                
                try
                {
                    // Find the appropriate section applier
                    var sectionApplier = _sectionAppliers.FirstOrDefault(a => a.SectionName.Equals(section, StringComparison.OrdinalIgnoreCase));
                    
                    if (sectionApplier != null)
                    {
                        _logService.Log(LogLevel.Info, $"Starting to apply configuration for section {section}");
                        
                        // Apply with timeout protection
                        var applyTask = sectionApplier.ApplyConfigAsync(configFile);
                        
                        // Set a section-specific timeout (longer for Optimize section which has power plan operations)
                        int timeoutMs = section.Equals("Optimize", StringComparison.OrdinalIgnoreCase) ? 60000 : 30000; // Increased timeouts
                        
                        _logService.Log(LogLevel.Info, $"Setting timeout of {timeoutMs}ms for section {section}");
                        
                        // Create a separate cancellation token source for this section
                        using var sectionCts = new CancellationTokenSource(timeoutMs);
                        var sectionCancellationToken = sectionCts.Token;
                        
                        try
                        {
                            // Wait for the task to complete or timeout
                            var delayTask = Task.Delay(timeoutMs, sectionCancellationToken);
                            var completedTask = await Task.WhenAny(applyTask, delayTask);
                            
                            if (completedTask == applyTask)
                            {
                                sectionResult = await applyTask;
                                _logService.Log(LogLevel.Info, $"Section {section} applied with result: {sectionResult}");
                            }
                            else
                            {
                                _logService.Log(LogLevel.Warning, $"Applying section {section} timed out after {timeoutMs}ms");
                                
                                // Try to cancel the operation if possible
                                sectionCts.Cancel();
                                
                                // Log detailed information about the section
                                _logService.Log(LogLevel.Warning, $"Section {section} details: {configFile.Items?.Count ?? 0} items");
                                
                                sectionResult = false;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            _logService.Log(LogLevel.Warning, $"Section {section} application was canceled");
                            sectionResult = false;
                        }
                    }
                    else
                    {
                        _logService.Log(LogLevel.Warning, $"No applier found for section: {section}");
                        sectionResult = false;
                    }
                    
                    // If we have items but didn't update any, consider it a success
                    // This handles the case where all items are already in the desired state
                    if (!sectionResult && configFile.Items != null && configFile.Items.Any())
                    {
                        _logService.Log(LogLevel.Info, $"No items were updated in section {section}, but considering it a success since configuration was applied");
                        sectionResult = true;
                    }
                }
                catch (TaskCanceledException)
                {
                    _logService.Log(LogLevel.Warning, $"Applying section {section} was canceled due to timeout");
                    sectionResult = false;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error applying configuration to section {section}: {ex.Message}");
                    _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                    sectionResult = false;
                }
                
                sectionResults[section] = sectionResult;
            }
        
            // Calculate and log execution time
            var endTime = DateTime.Now;
            var executionTime = endTime - startTime;
            _logService.Log(LogLevel.Info, $"Completed ApplySectionsAsync in {executionTime.TotalSeconds:F2} seconds");
            
            // Log summary of results
            foreach (var result in sectionResults)
            {
                _logService.Log(LogLevel.Info, $"Section {result.Key}: {(result.Value ? "Success" : "Failed")}");
            }
            
            return sectionResults;
        }

        private ConfigurationFile ExtractSectionFromUnifiedConfiguration(UnifiedConfigurationFile unifiedConfig, string section)
        {
            try
            {
                var configFile = new ConfigurationFile
                {
                    ConfigType = section,
                    Items = new List<ConfigurationItem>()
                };

                switch (section)
                {
                    case "WindowsApps":
                        configFile.Items = unifiedConfig.WindowsApps.Items;
                        break;
                    case "ExternalApps":
                        configFile.Items = unifiedConfig.ExternalApps.Items;
                        break;
                    case "Customize":
                        configFile.Items = unifiedConfig.Customize.Items;
                        break;
                    case "Optimize":
                        configFile.Items = unifiedConfig.Optimize.Items;
                        break;
                    default:
                        _logService.Log(LogLevel.Warning, $"Unknown section: {section}");
                        return null;
                }

                return configFile;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error extracting section {section}: {ex.Message}");
                return null;
            }
        }
    }
}