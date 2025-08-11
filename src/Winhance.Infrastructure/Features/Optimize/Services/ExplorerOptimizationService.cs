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
    /// Extends BaseSystemSettingsService to inherit common setting application logic.
    /// </summary>
    public class ExplorerOptimizationService : BaseSystemSettingsService, IExplorerOptimizationService
    {
        /// <summary>
        /// Gets the domain name for explorer optimizations.
        /// </summary>
        public override string DomainName => "ExplorerOptimization";

        /// <summary>
        /// Initializes a new instance of the <see cref="ExplorerOptimizationService"/> class.
        /// </summary>
        /// <param name="registryService">The registry service for registry manipulations.</param>
        /// <param name="commandService">The command service for command-based settings.</param>
        /// <param name="logService">The log service for logging operations.</param>
        /// <param name="systemSettingsDiscoveryService">The system settings discovery service.</param>
        public ExplorerOptimizationService(
            IRegistryService registryService,
            ICommandService commandService,
            ILogService logService,
            ISystemSettingsDiscoveryService systemSettingsDiscoveryService)
            : base(registryService, commandService, logService, systemSettingsDiscoveryService)
        {
        }

        /// <summary>
        /// Gets all Explorer optimization settings with their current system state.
        /// </summary>
        public override async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            var optimizations = ExplorerOptimizations.GetExplorerOptimizations();
            return await GetSettingsWithSystemStateAsync(optimizations.Settings);
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
