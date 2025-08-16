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
    /// Service implementation for managing Taskbar customization settings.
    /// Handles taskbar appearance, behavior, and cleanup operations.
    /// Maintains exact same method signatures and behavior for compatibility.
    /// </summary>
    public class TaskbarService : ITaskbarService
    {
        private readonly SystemSettingOrchestrator _orchestrator;
        private readonly ILogService _logService;
        private readonly ICommandService _commandService;

        public string DomainName => "Taskbar";

        public TaskbarService(
            SystemSettingOrchestrator orchestrator,
            ILogService logService,
            ICommandService commandService)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Taskbar settings");
                
                var group = TaskbarCustomizations.GetTaskbarCustomizations();
                return await _orchestrator.GetSettingsWithSystemStateAsync(group.Settings, DomainName);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Taskbar settings: {ex.Message}");
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
            var group = TaskbarCustomizations.GetTaskbarCustomizations();
            return await Task.FromResult(group.Settings);
        }

        public async Task ExecuteTaskbarCleanupAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Executing taskbar cleanup operation");

                // Execute taskbar cleanup commands
                var cleanupCommands = new[]
                {
                    "taskkill /f /im explorer.exe",
                    "start explorer.exe"
                };

                foreach (var command in cleanupCommands)
                {
                    await _commandService.ExecuteCommandAsync(command);
                }

                _logService.Log(LogLevel.Info, "Taskbar cleanup completed successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error during taskbar cleanup: {ex.Message}");
                throw;
            }
        }
    }
}
