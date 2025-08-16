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
    /// Service implementation for managing sound optimization settings.
    /// Handles audio enhancements, sound schemes, and audio-related optimizations.
    /// </summary>
    public class SoundService : ISoundService
    {
        private readonly SystemSettingOrchestrator _orchestrator;
        private readonly ILogService _logService;

        /// <summary>
        /// Gets the domain name for Sound optimizations.
        /// </summary>
        public string DomainName => "Sound";

        /// <summary>
        /// Initializes a new instance of the <see cref="SoundService"/> class.
        /// </summary>
        public SoundService(SystemSettingOrchestrator orchestrator, ILogService logService)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Gets all Sound optimization settings with their current system state.
        /// </summary>
        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Sound optimization settings");

                var optimizations = SoundOptimizations.GetSoundOptimizations();
                return await _orchestrator.GetSettingsWithSystemStateAsync(
                    optimizations.Settings,
                    DomainName
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading Sound optimization settings: {ex.Message}"
                );
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            var settings = await GetRawSettingsAsync();
            await _orchestrator.ApplySettingAsync(settingId, enable, value, settings, DomainName);
        }

        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _orchestrator.GetSettingStatusAsync(settingId, settings);
        }

        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _orchestrator.GetSettingValueAsync(settingId, settings);
        }

        private async Task<IEnumerable<ApplicationSetting>> GetRawSettingsAsync()
        {
            var optimizations = SoundOptimizations.GetSoundOptimizations();
            return await Task.FromResult(optimizations.Settings);
        }
    }
}
