using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Application service for coordinating setting operations across domains.
    /// Follows Clean Architecture by handling use cases and orchestrating domain services.
    /// Supports all control types: BinaryToggle, ComboBox, NumericUpDown, Slider.
    /// </summary>
    public class SettingApplicationService : ISettingApplicationService
    {
        private readonly IDomainServiceRegistry _domainServiceRegistry;
        private readonly ILogService _logService;

        public SettingApplicationService(
            IDomainServiceRegistry domainServiceRegistry,
            ILogService logService
        )
        {
            _domainServiceRegistry =
                domainServiceRegistry
                ?? throw new ArgumentNullException(nameof(domainServiceRegistry));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"[SettingApplication] Applying setting '{settingId}', enable: {enable}, value: {value}"
                );

                // Use SOLID registry pattern for O(1) domain service lookup and delegate completely
                var domainService = _domainServiceRegistry.GetDomainService(settingId);

                // Pure delegation: let domain service handle all control type logic internally
                await domainService.ApplySettingAsync(settingId, enable, value);

                _logService.Log(
                    LogLevel.Info,
                    $"[SettingApplication] Successfully applied setting '{settingId}'"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"[SettingApplication] Error applying setting '{settingId}': {ex.Message}"
                );
                throw;
            }
        }

        public async Task<SettingApplicationResult> GetSettingStateAsync(string settingId)
        {
            try
            {
                _logService.Log(
                    LogLevel.Debug,
                    $"[SettingApplication] Getting state for setting '{settingId}'"
                );

                var domainService = _domainServiceRegistry.GetDomainService(settingId);

                var isEnabled = await domainService.IsSettingEnabledAsync(settingId);
                var currentValue = await domainService.GetSettingValueAsync(settingId);

                // Determine status based on whether the setting is applied
                var status = isEnabled
                    ? RegistrySettingStatus.Applied
                    : RegistrySettingStatus.NotApplied;

                return new SettingApplicationResult
                {
                    Success = true,
                    IsEnabled = isEnabled,
                    CurrentValue = currentValue,
                    Status = status,
                };
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"[SettingApplication] Error getting state for setting '{settingId}': {ex.Message}"
                );
                return new SettingApplicationResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<IEnumerable<ApplicationSetting>> GetAllSettingsAsync()
        {
            try
            {
                var allSettings = new List<ApplicationSetting>();

                // This method should be refactored to use registry approach for better performance
                // For now, we'll throw as this violates the new SOLID architecture
                throw new NotSupportedException(
                    "GetAllSettingsAsync should be refactored to use domain-specific queries instead of loading all domains"
                );

                return allSettings;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"[SettingApplication] Error getting all settings: {ex.Message}"
                );
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsByDomainAsync(
            string domainName
        )
        {
            try
            {
                // This method should be refactored to use registry approach
                throw new NotSupportedException(
                    "GetSettingsByDomainAsync should be refactored to use registry-based domain lookup"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"[SettingApplication] Error getting settings for domain '{domainName}': {ex.Message}"
                );
                return Enumerable.Empty<ApplicationSetting>();
            }
        }
    }
}
