using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    /// <summary>
    /// Service implementation for managing Windows Explorer optimization settings.
    /// Handles file explorer performance, indexing, search optimization, and system efficiency tweaks.
    /// </summary>
    public class ExplorerOptimizationService : IExplorerOptimizationService
    {
        private readonly IRegistryService _registryService;
        private readonly ICommandService _commandService;
        private readonly ILogService _logService;
        private readonly ISystemSettingsDiscoveryService _systemSettingsDiscoveryService;

        public string DomainName => "ExplorerOptimization";

        public ExplorerOptimizationService(
            IRegistryService registryService,
            ICommandService commandService,
            ILogService logService,
            ISystemSettingsDiscoveryService systemSettingsDiscoveryService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _systemSettingsDiscoveryService = systemSettingsDiscoveryService ?? throw new ArgumentNullException(nameof(systemSettingsDiscoveryService));
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Explorer optimization settings with system state");
                
                var optimizations = ExplorerOptimizations.GetExplorerOptimizations();
                var settings = optimizations.Settings.ToList();

                // Initialize settings with their actual system state
                var systemStates = await _systemSettingsDiscoveryService.GetCurrentSettingsStateAndValuesAsync(settings);

                // Create new settings with updated IsInitiallyEnabled values
                var updatedSettings = new List<ApplicationSetting>();
                foreach (var originalSetting in settings)
                {
                    if (systemStates.TryGetValue(originalSetting.Id, out var state))
                    {
                        var updatedSetting = originalSetting with
                        {
                            IsInitiallyEnabled = state.IsEnabled,
                            CurrentValue = state.CurrentValue,
                            IsEnabled = true // Settings are always enabled for interaction
                        };
                        updatedSettings.Add(updatedSetting);
                    }
                    else
                    {
                        _logService.Log(LogLevel.Warning, 
                            $"No system state found for setting '{originalSetting.Id}', using defaults");
                        updatedSettings.Add(originalSetting);
                    }
                }

                return updatedSettings;
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

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying Explorer optimization setting '{settingId}': enable={enable}");

                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null)
                {
                    throw new ArgumentException($"Setting '{settingId}' not found in Explorer optimization domain");
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

                _logService.Log(LogLevel.Info, $"Successfully applied Explorer optimization setting '{settingId}'");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying Explorer optimization setting '{settingId}': {ex.Message}");
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
                _logService.Log(LogLevel.Error, $"Error checking Explorer optimization setting '{settingId}': {ex.Message}");
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
                _logService.Log(LogLevel.Error, $"Error getting Explorer optimization setting value '{settingId}': {ex.Message}");
                return null;
            }
        }

        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Checking if optimization setting '{settingId}' is enabled");
                
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
                    $"Error checking if optimization setting '{settingId}' is enabled: {ex.Message}"
                );
                return false;
            }
        }

        public async Task ExecuteExplorerActionAsync(string actionId)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Executing Explorer optimization action '{actionId}'");

                // Handle different explorer optimization actions based on actionId
                switch (actionId.ToLowerInvariant())
                {
                    case "restart-explorer":
                        await _commandService.ExecuteCommandAsync("taskkill /f /im explorer.exe");
                        await Task.Delay(1000); // Wait a moment
                        await _commandService.ExecuteCommandAsync("start explorer.exe");
                        break;
                    
                    case "clear-thumbnail-cache":
                        await _commandService.ExecuteCommandAsync("del /f /s /q %localappdata%\\Microsoft\\Windows\\Explorer\\thumbcache_*.db");
                        break;
                    
                    case "rebuild-search-index":
                        await _commandService.ExecuteCommandAsync("sc stop wsearch");
                        await Task.Delay(2000);
                        await _commandService.ExecuteCommandAsync("sc start wsearch");
                        break;
                    
                    case "optimize-indexing":
                        // Disable indexing on non-essential locations for performance
                        await _commandService.ExecuteCommandAsync("powershell -Command \"Get-WmiObject -Class Win32_Volume | Where-Object {$_.IndexingEnabled -eq $true -and $_.DriveLetter -ne 'C:'} | Set-WmiInstance -Arguments @{IndexingEnabled=$false}\"");
                        break;
                    
                    default:
                        _logService.Log(LogLevel.Warning, $"Unknown Explorer optimization action: {actionId}");
                        break;
                }

                _logService.Log(LogLevel.Info, $"Explorer optimization action '{actionId}' completed successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error executing Explorer optimization action '{actionId}': {ex.Message}");
                throw;
            }
        }
    }
}
