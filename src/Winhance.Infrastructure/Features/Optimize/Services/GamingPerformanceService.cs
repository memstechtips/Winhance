using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    /// <summary>
    /// Service implementation for managing gaming and performance optimization settings.
    /// Handles game mode, performance tweaks, and gaming-related optimizations.
    /// Maintains exact same method signatures and behavior for compatibility.
    /// </summary>
    public class GamingPerformanceService : IDomainService
    {
        private readonly SettingControlHandler _controlHandler;
        private readonly ISystemSettingsDiscoveryService _discoveryService;
        private readonly ILogService _logService;

        /// <summary>
        /// Gets the domain name for gaming and performance optimizations.
        /// </summary>
        public string DomainName => FeatureIds.GamingPerformance;

        /// <summary>
        /// Initializes a new instance of the <see cref="GamingPerformanceService"/> class.
        /// </summary>
        /// <param name="controlHandler">The system setting controlHandler for applying settings.</param>
        /// <param name="logService">The log service for logging operations.</param>
        public GamingPerformanceService(
             SettingControlHandler controlHandler,
            ISystemSettingsDiscoveryService discoveryService,
            ILogService logService
        )
        {
            _controlHandler = controlHandler ?? throw new ArgumentNullException(nameof(controlHandler));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Gets all gaming and performance optimization settings with their current system state.
        /// </summary>
        /// <returns>Collection of application settings for gaming and performance.</returns>
        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            try
            {
                var optimizations = GamingandPerformanceOptimizations.GetGamingandPerformanceOptimizations();
                return await _discoveryService.GetSettingsWithSystemStateAsync(optimizations.Settings, DomainName);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Gaming Performance settings: {ex.Message}");
                return Enumerable.Empty<SettingDefinition>();
            }
        }

        /// <summary>
        /// Applies a setting.
        /// </summary>
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

        /// <summary>
        /// Checks if a setting is enabled.
        /// </summary>
        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _controlHandler.GetSettingStatusAsync(settingId, settings);
        }

        /// <summary>
        /// Gets the current value of a setting.
        /// </summary>
        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _controlHandler.GetSettingValueAsync(settingId, settings);
        }

        /// <summary>
        /// Helper method to get raw settings without system state.
        /// </summary>
        public async Task<IEnumerable<SettingDefinition>> GetRawSettingsAsync()
        {
            var optimizations =
                GamingandPerformanceOptimizations.GetGamingandPerformanceOptimizations();
            return await Task.FromResult(optimizations.Settings);
        }
    }
}
