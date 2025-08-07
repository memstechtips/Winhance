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
    /// Service implementation for managing Start Menu customization settings.
    /// Handles Start Menu layout, search, and behavior customizations.
    /// </summary>
    public class StartMenuService : IStartMenuService
    {
        private readonly IRegistryService _registryService;
        private readonly ILogService _logService;

        public string DomainName => "StartMenu";

        public StartMenuService(
            IRegistryService registryService,
            ILogService logService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Start Menu settings");
                
                var group = StartMenuCustomizations.GetStartMenuCustomizations();
                return group.Settings;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Start Menu settings: {ex.Message}");
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying Start Menu setting '{settingId}': enable={enable}");

                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null)
                {
                    throw new ArgumentException($"Setting '{settingId}' not found in Start Menu domain");
                }

                // Apply registry settings
                if (setting.RegistrySettings?.Count > 0)
                {
                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        await _registryService.ApplySettingAsync(registrySetting, enable);
                    }
                }

                _logService.Log(LogLevel.Info, $"Successfully applied Start Menu setting '{settingId}'");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying Start Menu setting '{settingId}': {ex.Message}");
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
                _logService.Log(LogLevel.Error, $"Error checking Start Menu setting '{settingId}': {ex.Message}");
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
                _logService.Log(LogLevel.Error, $"Error getting Start Menu setting value '{settingId}': {ex.Message}");
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

        public async Task ApplyMultipleSettingsAsync(IEnumerable<ApplicationSetting> settings, bool isEnabled)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying multiple Start Menu settings: enabled={isEnabled}");
                
                foreach (var setting in settings)
                {
                    await ApplySettingAsync(setting.Id, isEnabled);
                }
                
                _logService.Log(LogLevel.Info, "Successfully applied multiple Start Menu settings");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying multiple Start Menu settings: {ex.Message}");
                throw;
            }
        }
    }
}
