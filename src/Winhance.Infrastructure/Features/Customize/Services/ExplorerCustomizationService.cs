using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Customize.Services
{
    /// <summary>
    /// Service implementation for managing Windows Explorer customization settings.
    /// Handles file explorer appearance, layout, visual preferences, and user interface customizations.
    /// Maintains exact same method signatures and behavior for compatibility.
    /// </summary>
    public class ExplorerCustomizationService : IExplorerCustomizationService
    {
        private readonly SystemSettingOrchestrator _orchestrator;
        private readonly ILogService _logService;
        private readonly ICommandService _commandService;
        private readonly IRegistryService _registryService;

        public string DomainName => "ExplorerCustomization";

        public ExplorerCustomizationService(
            SystemSettingOrchestrator orchestrator,
            ILogService logService,
            ICommandService commandService,
            IRegistryService registryService
        )
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _commandService =
                commandService ?? throw new ArgumentNullException(nameof(commandService));
            _registryService =
                registryService ?? throw new ArgumentNullException(nameof(registryService));
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Explorer customization settings");

                var group = ExplorerCustomizations.GetExplorerCustomizations();
                return await _orchestrator.GetSettingsWithSystemStateAsync(
                    group.Settings,
                    DomainName
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading Explorer customization settings: {ex.Message}"
                );
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        /// <summary>
        /// Helper method to get raw settings without system state.
        /// </summary>
        private async Task<IEnumerable<ApplicationSetting>> GetRawSettingsAsync()
        {
            var group = ExplorerCustomizations.GetExplorerCustomizations();
            return await Task.FromResult(group.Settings);
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            // Use orchestrator for consistent behavior with other domain services
            var settings = await GetRawSettingsAsync();
            await _orchestrator.ApplySettingAsync(settingId, enable, value, settings, DomainName);
        }

        public async Task<bool> GetSettingStatusAsync(string settingId)
        {
            // Use orchestrator for consistent behavior
            var settings = await GetRawSettingsAsync();
            return await _orchestrator.GetSettingStatusAsync(settingId, settings);
        }

        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            // Use orchestrator for consistent behavior
            var settings = await GetRawSettingsAsync();
            return await _orchestrator.GetSettingValueAsync(settingId, settings);
        }

        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            // Use orchestrator for consistent behavior
            var settings = await GetRawSettingsAsync();
            return await _orchestrator.GetSettingStatusAsync(settingId, settings);
        }

        public async Task ExecuteExplorerActionAsync(string actionId)
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Executing Explorer customization action '{actionId}'"
                );

                // Handle different explorer customization actions based on actionId
                switch (actionId.ToLowerInvariant())
                {
                    case "restart-explorer":
                        await _commandService.ExecuteCommandAsync("taskkill /f /im explorer.exe");
                        await Task.Delay(1000); // Wait a moment
                        await _commandService.ExecuteCommandAsync("start explorer.exe");
                        break;

                    case "refresh-desktop":
                        await _commandService.ExecuteCommandAsync(
                            "rundll32.exe user32.dll,UpdatePerUserSystemParameters"
                        );
                        break;

                    case "apply-theme":
                        // Apply visual theme changes
                        await _commandService.ExecuteCommandAsync(
                            "rundll32.exe user32.dll,UpdatePerUserSystemParameters"
                        );
                        break;

                    default:
                        _logService.Log(
                            LogLevel.Warning,
                            $"Unknown Explorer customization action: {actionId}"
                        );
                        break;
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Explorer customization action '{actionId}' completed successfully"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error executing Explorer customization action '{actionId}': {ex.Message}"
                );
                throw;
            }
        }
    }
}
