using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
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
    public class GamingPerformanceService : IGamingPerformanceService
    {
        private readonly SystemSettingOrchestrator _orchestrator;
        private readonly ILogService _logService;

        /// <summary>
        /// Gets the domain name for gaming and performance optimizations.
        /// </summary>
        public string DomainName => "GamingPerformance";

        /// <summary>
        /// Initializes a new instance of the <see cref="GamingPerformanceService"/> class.
        /// </summary>
        /// <param name="orchestrator">The system setting orchestrator for applying settings.</param>
        /// <param name="logService">The log service for logging operations.</param>
        public GamingPerformanceService(
            SystemSettingOrchestrator orchestrator,
            ILogService logService
        )
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Gets all gaming and performance optimization settings with their current system state.
        /// </summary>
        /// <returns>Collection of application settings for gaming and performance.</returns>
        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Gaming Performance optimization settings");

                var optimizations =
                    GamingandPerformanceOptimizations.GetGamingandPerformanceOptimizations();
                return await _orchestrator.GetSettingsWithSystemStateAsync(
                    optimizations.Settings,
                    DomainName
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading Gaming Performance optimization settings: {ex.Message}"
                );
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
            var optimizations =
                GamingandPerformanceOptimizations.GetGamingandPerformanceOptimizations();
            return await Task.FromResult(optimizations.Settings);
        }
    }
}
