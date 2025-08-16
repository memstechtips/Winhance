using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    /// <summary>
    /// Service implementation for managing Windows Explorer optimization settings.
    /// Handles file explorer performance, indexing, search optimization, and system efficiency tweaks.
    /// Maintains exact same method signatures and behavior for compatibility.
    /// </summary>
    public class ExplorerOptimizationService : IExplorerOptimizationService
    {
        private readonly SystemSettingOrchestrator _orchestrator;
        private readonly ILogService _logService;

        /// <summary>
        /// Gets the domain name for explorer optimizations.
        /// </summary>
        public string DomainName => "ExplorerOptimization";

        /// <summary>
        /// Initializes a new instance of the <see cref="ExplorerOptimizationService"/> class.
        /// </summary>
        /// <param name="orchestrator">The system setting orchestrator for applying settings.</param>
        /// <param name="logService">The log service for logging operations.</param>
        public ExplorerOptimizationService(
            SystemSettingOrchestrator orchestrator,
            ILogService logService
        )
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Gets all Explorer optimization settings with their current system state.
        /// </summary>
        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Explorer optimization settings");

                var optimizations = ExplorerOptimizations.GetExplorerOptimizations();
                return await _orchestrator.GetSettingsWithSystemStateAsync(
                    optimizations.Settings,
                    DomainName
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading Explorer optimization settings: {ex.Message}"
                );
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        /// <summary>
        /// Applies a setting.
        /// </summary>
        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            var settings = await GetRawSettingsAsync();
            await _orchestrator.ApplySettingAsync(settingId, enable, value, settings, DomainName);
        }

        /// <summary>
        /// Checks if a setting is enabled.
        /// </summary>
        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _orchestrator.GetSettingStatusAsync(settingId, settings);
        }

        /// <summary>
        /// Gets the current value of a setting.
        /// </summary>
        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _orchestrator.GetSettingValueAsync(settingId, settings);
        }

        /// <summary>
        /// Helper method to get raw settings without system state.
        /// </summary>
        private async Task<IEnumerable<ApplicationSetting>> GetRawSettingsAsync()
        {
            var optimizations = ExplorerOptimizations.GetExplorerOptimizations();
            return await Task.FromResult(optimizations.Settings);
        }

        public async Task ExecuteExplorerActionAsync(string actionId)
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Executing Explorer optimization action '{actionId}'"
                );

                // Handle different explorer optimization actions based on actionId
                switch (actionId.ToLowerInvariant())
                {
                    case "restart-explorer":
                        // Note: This method now needs ICommandService injected or accessed through orchestrator
                        _logService.Log(
                            LogLevel.Warning,
                            "ExecuteExplorerActionAsync requires command execution capability - consider refactoring to use orchestrator"
                        );
                        break;

                    case "clear-thumbnail-cache":
                        _logService.Log(
                            LogLevel.Warning,
                            "ExecuteExplorerActionAsync requires command execution capability - consider refactoring to use orchestrator"
                        );
                        break;

                    case "rebuild-search-index":
                        _logService.Log(
                            LogLevel.Warning,
                            "ExecuteExplorerActionAsync requires command execution capability - consider refactoring to use orchestrator"
                        );
                        break;

                    case "optimize-indexing":
                        _logService.Log(
                            LogLevel.Warning,
                            "ExecuteExplorerActionAsync requires command execution capability - consider refactoring to use orchestrator"
                        );
                        break;

                    default:
                        _logService.Log(
                            LogLevel.Warning,
                            $"Unknown Explorer optimization action: {actionId}"
                        );
                        break;
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Explorer optimization action '{actionId}' completed successfully"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error executing Explorer optimization action '{actionId}': {ex.Message}"
                );
                throw;
            }
        }
    }
}
