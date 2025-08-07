using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;

namespace Winhance.Infrastructure.Features.Customize.Services
{
    /// <summary>
    /// Service implementation for managing Taskbar customization settings.
    /// Handles taskbar appearance, behavior, and cleanup operations.
    /// </summary>
    public class TaskbarService : ITaskbarService
    {
        private readonly IRegistryService _registryService;
        private readonly ICommandService _commandService;
        private readonly ILogService _logService;

        public string DomainName => "Taskbar";

        public TaskbarService(
            IRegistryService registryService,
            ICommandService commandService,
            ILogService logService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Taskbar settings");
                
                var group = TaskbarCustomizations.GetTaskbarCustomizations();
                return group.Settings;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Taskbar settings: {ex.Message}");
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying Taskbar setting '{settingId}': enable={enable}");

                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null)
                {
                    throw new ArgumentException($"Setting '{settingId}' not found in Taskbar domain");
                }

                // Apply registry settings
                if (setting.RegistrySettings?.Count > 0)
                {
                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        await _registryService.ApplySettingAsync(registrySetting, enable);
                    }
                }

                // Apply command settings
                if (setting.CommandSettings?.Count > 0)
                {
                    foreach (var commandSetting in setting.CommandSettings)
                    {
                        await _commandService.ApplyCommandSettingsAsync(new[] { commandSetting }, enable);
                    }
                }

                _logService.Log(LogLevel.Info, $"Successfully applied Taskbar setting '{settingId}'");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying Taskbar setting '{settingId}': {ex.Message}");
                throw;
            }
        }

        public async Task<bool> GetSettingStatusAsync(string settingId)
        {
            try
            {
                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting?.RegistrySettings?.Count > 0)
                {
                    var status = await _registryService.GetSettingStatusAsync(setting.RegistrySettings[0]);
                    return status == RegistrySettingStatus.Applied;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking Taskbar setting '{settingId}': {ex.Message}");
                return false;
            }
        }

        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            try
            {
                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting?.RegistrySettings?.Count > 0)
                {
                    return await _registryService.GetCurrentValueAsync(setting.RegistrySettings[0]);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting Taskbar setting value '{settingId}': {ex.Message}");
                return null;
            }
        }
        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Checking if setting '{settingId}' is enabled");
                
                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                if (setting?.RegistrySettings?.Count > 0)
                {
                    var status = await _registryService.GetSettingStatusAsync(setting.RegistrySettings[0]);
                    return status == RegistrySettingStatus.Applied;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error checking if setting '{settingId}' is enabled: {ex.Message}"
                );
                return false;
            }
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
