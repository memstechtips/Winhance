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
    /// Service implementation for managing Windows Explorer customization settings.
    /// Handles file explorer appearance, layout, visual preferences, and user interface customizations.
    /// </summary>
    public class ExplorerCustomizationService : IExplorerCustomizationService
    {
        private readonly IRegistryService _registryService;
        private readonly ICommandService _commandService;
        private readonly ILogService _logService;

        public string DomainName => "ExplorerCustomization";

        public ExplorerCustomizationService(
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
                _logService.Log(LogLevel.Info, "Loading Explorer customization settings");
                
                var group = ExplorerCustomizations.GetExplorerCustomizations();
                return group.Settings;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Explorer customization settings: {ex.Message}");
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying Explorer customization setting '{settingId}': enable={enable}");

                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null)
                {
                    throw new ArgumentException($"Setting '{settingId}' not found in Explorer customization domain");
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

                _logService.Log(LogLevel.Info, $"Successfully applied Explorer customization setting '{settingId}'");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying Explorer customization setting '{settingId}': {ex.Message}");
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
                _logService.Log(LogLevel.Error, $"Error checking Explorer customization setting '{settingId}': {ex.Message}");
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
                _logService.Log(LogLevel.Error, $"Error getting Explorer customization setting value '{settingId}': {ex.Message}");
                return null;
            }
        }

        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Checking if customization setting '{settingId}' is enabled");
                
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
                    $"Error checking if customization setting '{settingId}' is enabled: {ex.Message}"
                );
                return false;
            }
        }

        public async Task ExecuteExplorerActionAsync(string actionId)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Executing Explorer customization action '{actionId}'");

                // Handle different explorer customization actions based on actionId
                switch (actionId.ToLowerInvariant())
                {
                    case "restart-explorer":
                        await _commandService.ExecuteCommandAsync("taskkill /f /im explorer.exe");
                        await Task.Delay(1000); // Wait a moment
                        await _commandService.ExecuteCommandAsync("start explorer.exe");
                        break;
                    
                    case "refresh-desktop":
                        await _commandService.ExecuteCommandAsync("rundll32.exe user32.dll,UpdatePerUserSystemParameters");
                        break;
                    
                    case "apply-theme":
                        // Apply visual theme changes
                        await _commandService.ExecuteCommandAsync("rundll32.exe user32.dll,UpdatePerUserSystemParameters");
                        break;
                    
                    default:
                        _logService.Log(LogLevel.Warning, $"Unknown Explorer customization action: {actionId}");
                        break;
                }

                _logService.Log(LogLevel.Info, $"Explorer customization action '{actionId}' completed successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error executing Explorer customization action '{actionId}': {ex.Message}");
                throw;
            }
        }
    }
}
