using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces.WindowsRegistry;
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
    public class ExplorerCustomizationService : IDomainService
    {
        private readonly SettingControlHandler _controlHandler;
        private readonly ISystemSettingsDiscoveryService _discoveryService;
        private readonly ILogService _logService;
        private readonly ICommandService _commandService;
        private readonly IWindowsRegistryService _registryService;

        public string DomainName => FeatureIds.ExplorerCustomization;

        public ExplorerCustomizationService(
             SettingControlHandler controlHandler,
            ISystemSettingsDiscoveryService discoveryService,
            ILogService logService,
            ICommandService commandService,
            IWindowsRegistryService windowsRegistryService
        )
        {
            _controlHandler = controlHandler ?? throw new ArgumentNullException(nameof(controlHandler));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _commandService =
                commandService ?? throw new ArgumentNullException(nameof(commandService));
            _registryService =
                windowsRegistryService ?? throw new ArgumentNullException(nameof(windowsRegistryService));
        }

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Explorer customization settings");

                var group = ExplorerCustomizations.GetExplorerCustomizations();
                return await _discoveryService.GetSettingsWithSystemStateAsync(
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
                return Enumerable.Empty<SettingDefinition>();
            }
        }

        /// <summary>
        /// Helper method to get raw settings without system state.
        /// </summary>
        public async Task<IEnumerable<SettingDefinition>> GetRawSettingsAsync()
        {
            var group = ExplorerCustomizations.GetExplorerCustomizations();
            return await Task.FromResult(group.Settings);
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            var settings = await GetRawSettingsAsync();
            var setting = settings.FirstOrDefault(s => s.Id == settingId);
            if (setting == null)
                throw new ArgumentException($"Setting '{settingId}' not found");

            switch (setting.InputType)
            {
                case SettingInputType.Toggle:
                    await _controlHandler.ApplyBinaryToggleAsync(setting, enable);
                    break;
                case SettingInputType.Selection when value is int index:
                    await _controlHandler.ApplyComboBoxIndexAsync(setting, index);
                    break;
                case SettingInputType.NumericRange when value != null:
                    await _controlHandler.ApplyNumericUpDownAsync(setting, value);
                    break;
                default:
                    throw new NotSupportedException($"Input type '{setting.InputType}' not supported");
            }
        }

        public async Task<bool> GetSettingStatusAsync(string settingId)
        {
            // Use controlHandler for consistent behavior
            var settings = await GetRawSettingsAsync();
            return await _controlHandler.GetSettingStatusAsync(settingId, settings);
        }

        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            // Use controlHandler for consistent behavior
            var settings = await GetRawSettingsAsync();
            return await _controlHandler.GetSettingValueAsync(settingId, settings);
        }

        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            // Use controlHandler for consistent behavior
            var settings = await GetRawSettingsAsync();
            return await _controlHandler.GetSettingStatusAsync(settingId, settings);
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
