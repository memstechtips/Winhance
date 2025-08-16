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
    /// Service implementation for managing Windows security optimization settings.
    /// Handles UAC, Windows Defender, and security-related optimizations.
    /// </summary>
    public class SecurityService : ISecurityService
    {
        private readonly SystemSettingOrchestrator _orchestrator;
        private readonly ILogService _logService;
        private readonly IComboBoxValueResolver _comboBoxResolver;

        /// <summary>
        /// Gets the domain name for Security optimizations.
        /// </summary>
        public string DomainName => "Security";

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityService"/> class.
        /// Uses composition with SystemSettingOrchestrator and maintains ComboBox resolver capability.
        /// </summary>
        public SecurityService(
            SystemSettingOrchestrator orchestrator,
            ILogService logService,
            IComboBoxValueResolver comboBoxValueResolver
        )
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _comboBoxResolver =
                comboBoxValueResolver
                ?? throw new ArgumentNullException(nameof(comboBoxValueResolver));
        }

        /// <summary>
        /// Gets all Security optimization settings with their current system state.
        /// </summary>
        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Security optimization settings");

                var optimizations = WindowsSecurityOptimizations.GetWindowsSecurityOptimizations();
                return await _orchestrator.GetSettingsWithSystemStateAsync(
                    optimizations.Settings,
                    DomainName
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading Security optimization settings: {ex.Message}"
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
            var optimizations = WindowsSecurityOptimizations.GetWindowsSecurityOptimizations();
            return await Task.FromResult(optimizations.Settings);
        }
    }
}
