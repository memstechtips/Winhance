using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services.Strategies
{
    /// <summary>
    /// Strategy for applying settings that use command operations.
    /// </summary>
    public class CommandSettingApplicationStrategy : ISettingApplicationStrategy
    {
        private readonly ICommandService _commandService;
        private readonly ILogService _logService;
        private readonly ISystemSettingsDiscoveryService _systemSettingsDiscoveryService;

        public CommandSettingApplicationStrategy(
            ICommandService commandService,
            ILogService logService,
            ISystemSettingsDiscoveryService systemSettingsDiscoveryService)
        {
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _systemSettingsDiscoveryService = systemSettingsDiscoveryService ?? throw new ArgumentNullException(nameof(systemSettingsDiscoveryService));
        }

        public bool CanHandle(ApplicationSetting setting)
        {
            return setting.CommandSettings?.Count > 0;
        }

        /// <summary>
        /// Applies a binary toggle setting.
        /// </summary>
        public virtual async Task ApplyBinaryToggleAsync(ApplicationSetting setting, bool enable)
        {
            // Apply command settings (primarily Optimize, but Customize can safely ignore)
            if (setting.CommandSettings?.Count > 0)
            {
                foreach (var commandSetting in setting.CommandSettings)
                {
                    await _commandService.ApplyCommandSettingsAsync(new[] { commandSetting }, enable);
                }
            }
        }

        /// <summary>
        /// Applies a ComboBox setting using the centralized resolver pattern.
        /// Commands don't typically use ComboBox, so this is a no-op.
        /// </summary>
        public virtual async Task ApplyComboBoxIndexAsync(ApplicationSetting setting, int comboBoxIndex)
        {
            // Command-based settings don't typically use ComboBox patterns
            // This is mainly for registry-based settings
            await Task.CompletedTask;
        }

        /// <summary>
        /// Applies a numeric up/down setting with a specific value.
        /// </summary>
        public virtual async Task ApplyNumericUpDownAsync(ApplicationSetting setting, object value)
        {
            try
            {
                // Apply command settings if present (for power settings)
                if (setting.CommandSettings?.Count > 0)
                {
                    foreach (var commandSetting in setting.CommandSettings)
                    {
                        // For numeric settings, we typically enable the command with the numeric value
                        // The actual command should contain the value or be constructed appropriately
                        await _commandService.ApplyCommandSettingsAsync(new[] { commandSetting }, true);
                    }
                }

                _logService.Log(LogLevel.Info, $"Applied command-based numeric setting '{setting.Id}' with value: {value}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying command-based numeric setting '{setting.Id}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the current status of a setting.
        /// </summary>
        public virtual async Task<bool> GetSettingStatusAsync(string settingId, IEnumerable<ApplicationSetting> settings)
        {
            try
            {
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting != null)
                {
                    // Use system settings discovery for accurate state detection
                    var state = await _systemSettingsDiscoveryService.GetCurrentSettingsStateAsync(new[] { setting });
                    return state.TryGetValue(settingId, out var isEnabled) ? isEnabled : false;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error checking command-based setting '{settingId}': {ex.Message}"
                );
                return false;
            }
        }

        /// <summary>
        /// Gets the current value of a setting.
        /// Command-based settings typically don't have retrievable values.
        /// </summary>
        public virtual async Task<object?> GetSettingValueAsync(string settingId, IEnumerable<ApplicationSetting> settings)
        {
            try
            {
                // Command-based settings typically don't have retrievable values
                // They're usually just executed and we can only check their state
                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error getting command-based setting value '{settingId}': {ex.Message}"
                );
                return null;
            }
        }
    }
}
