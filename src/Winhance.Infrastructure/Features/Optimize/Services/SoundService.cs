using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    /// Service implementation for managing sound optimization settings.
    /// Handles audio enhancements, sound schemes, and audio-related optimizations.
    /// </summary>
    public class SoundService : IDomainService
    {
        private readonly SettingControlHandler _controlHandler;
        private readonly ISystemSettingsDiscoveryService _discoveryService;
        private readonly ILogService _logService;

        /// <summary>
        /// Gets the domain name for Sound optimizations.
        /// </summary>
        public string DomainName => FeatureIds.Sound;

        /// <summary>
        /// Initializes a new instance of the <see cref="SoundService"/> class.
        /// </summary>
        public SoundService(
             SettingControlHandler controlHandler,
            ISystemSettingsDiscoveryService discoveryService,
            ILogService logService)
        {
            _controlHandler = controlHandler ?? throw new ArgumentNullException(nameof(controlHandler));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Gets all Sound optimization settings with their current system state.
        /// </summary>
        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            try
            {
                var optimizations = SoundOptimizations.GetSoundOptimizations();
                return await _discoveryService.GetSettingsWithSystemStateAsync(optimizations.Settings, DomainName);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Sound settings: {ex.Message}");
                return Enumerable.Empty<SettingDefinition>();
            }
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

        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _controlHandler.GetSettingStatusAsync(settingId, settings);
        }

        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _controlHandler.GetSettingValueAsync(settingId, settings);
        }

        public async Task<IEnumerable<SettingDefinition>> GetRawSettingsAsync()
        {
            var optimizations = SoundOptimizations.GetSoundOptimizations();
            return await Task.FromResult(optimizations.Settings);
        }
    }
}
