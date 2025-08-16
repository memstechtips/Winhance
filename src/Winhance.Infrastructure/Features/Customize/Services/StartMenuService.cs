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
    /// Service implementation for managing Start Menu customization settings.
    /// Handles Start Menu layout, search, and behavior customizations.
    /// Maintains exact same method signatures and behavior for compatibility.
    /// </summary>
    public class StartMenuService : IStartMenuService
    {
        private readonly IScheduledTaskService _scheduledTaskService;
        private readonly SystemSettingOrchestrator _orchestrator;
        private readonly ILogService _logService;
        private readonly ISystemServices _systemServices;

        public string DomainName => "StartMenu";

        public StartMenuService(
            IScheduledTaskService scheduledTaskService,
            SystemSettingOrchestrator orchestrator,
            ILogService logService,
            ISystemServices systemServices
        )
        {
            _scheduledTaskService =
                scheduledTaskService
                ?? throw new ArgumentNullException(nameof(scheduledTaskService));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _systemServices =
                systemServices ?? throw new ArgumentNullException(nameof(systemServices));
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Start Menu settings");

                var group = StartMenuCustomizations.GetStartMenuCustomizations();
                return await _orchestrator.GetSettingsWithSystemStateAsync(
                    group.Settings,
                    DomainName
                );
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Start Menu settings: {ex.Message}");
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
            var group = StartMenuCustomizations.GetStartMenuCustomizations();
            return await Task.FromResult(group.Settings);
        }

        public async Task ApplyMultipleSettingsAsync(
            IEnumerable<ApplicationSetting> settings,
            bool isEnabled
        )
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Applying multiple Start Menu settings: enabled={isEnabled}"
                );

                foreach (var setting in settings)
                {
                    await ApplySettingAsync(setting.Id, isEnabled);
                }

                _logService.Log(LogLevel.Info, "Successfully applied multiple Start Menu settings");
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying multiple Start Menu settings: {ex.Message}"
                );
                throw;
            }
        }

        public async Task CleanStartMenuAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Starting Start Menu cleaning process");

                // Clean the Start Menu using the static method from the domain model
                await Task.Run(() =>
                    StartMenuCustomizations.CleanStartMenu(
                        _systemServices,
                        _logService,
                        _scheduledTaskService
                    )
                );

                _logService.Log(LogLevel.Info, "Start Menu cleaned successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error cleaning Start Menu: {ex.Message}");
                throw;
            }
        }
    }
}
