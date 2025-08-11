using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Application service for coordinating setting operations across domains.
    /// Follows Clean Architecture by handling use cases and orchestrating domain services.
    /// Supports all control types: BinaryToggle, ComboBox, NumericUpDown, Slider.
    /// </summary>
    public class SettingApplicationService : ISettingApplicationService
    {
        private readonly IDomainServiceLocator _domainServiceLocator;
        private readonly IEnumerable<IDomainService> _domainServices;
        private readonly ILogService _logService;

        public SettingApplicationService(
            IDomainServiceLocator domainServiceLocator,
            IEnumerable<IDomainService> domainServices,
            ILogService logService)
        {
            _domainServiceLocator = domainServiceLocator ?? throw new ArgumentNullException(nameof(domainServiceLocator));
            _domainServices = domainServices ?? throw new ArgumentNullException(nameof(domainServices));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"[SettingApplication] Applying setting '{settingId}', enable: {enable}, value: {value}");

                // Find the domain service that handles this setting
                var domainService = await _domainServiceLocator.FindServiceForSettingAsync(settingId);
                if (domainService == null)
                {
                    throw new InvalidOperationException($"No domain service found for setting '{settingId}'");
                }

                // Get the setting to determine control type
                var setting = await _domainServiceLocator.GetSettingAsync(settingId);
                if (setting == null)
                {
                    throw new InvalidOperationException($"Setting '{settingId}' not found");
                }

                _logService.Log(LogLevel.Debug, $"[SettingApplication] Setting '{settingId}' has control type: {setting.ControlType}");

                // Apply the setting based on control type
                switch (setting.ControlType)
                {
                    case ControlType.BinaryToggle:
                        // Binary toggles use the enable parameter
                        await domainService.ApplySettingAsync(settingId, enable);
                        break;

                    case ControlType.ComboBox:
                    case ControlType.NumericUpDown:
                    case ControlType.Slider:
                        // Value-based controls use the value parameter
                        if (value != null)
                        {
                            await domainService.ApplySettingAsync(settingId, enable, value);
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, $"[SettingApplication] No value provided for {setting.ControlType} setting '{settingId}'");
                            throw new ArgumentException($"Value is required for {setting.ControlType} control type");
                        }
                        break;

                    default:
                        throw new NotSupportedException($"Control type {setting.ControlType} is not supported");
                }

                _logService.Log(LogLevel.Info, $"[SettingApplication] Successfully applied setting '{settingId}'");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"[SettingApplication] Error applying setting '{settingId}': {ex.Message}");
                throw;
            }
        }

        public async Task<SettingApplicationResult> GetSettingStateAsync(string settingId)
        {
            try
            {
                _logService.Log(LogLevel.Debug, $"[SettingApplication] Getting state for setting '{settingId}'");

                var domainService = await _domainServiceLocator.FindServiceForSettingAsync(settingId);
                if (domainService == null)
                {
                    return new SettingApplicationResult
                    {
                        Success = false,
                        ErrorMessage = $"No domain service found for setting '{settingId}'"
                    };
                }

                var isEnabled = await domainService.IsSettingEnabledAsync(settingId);
                var currentValue = await domainService.GetSettingValueAsync(settingId);

                // Determine status based on whether the setting is applied
                var status = isEnabled ? RegistrySettingStatus.Applied : RegistrySettingStatus.NotApplied;

                return new SettingApplicationResult
                {
                    Success = true,
                    IsEnabled = isEnabled,
                    CurrentValue = currentValue,
                    Status = status
                };
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"[SettingApplication] Error getting state for setting '{settingId}': {ex.Message}");
                return new SettingApplicationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<IEnumerable<ApplicationSetting>> GetAllSettingsAsync()
        {
            try
            {
                var allSettings = new List<ApplicationSetting>();

                foreach (var service in _domainServices)
                {
                    var settings = await service.GetSettingsAsync();
                    allSettings.AddRange(settings);
                }

                return allSettings;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"[SettingApplication] Error getting all settings: {ex.Message}");
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsByDomainAsync(string domainName)
        {
            try
            {
                var service = _domainServices.FirstOrDefault(s => s.DomainName.Equals(domainName, StringComparison.OrdinalIgnoreCase));
                if (service == null)
                {
                    _logService.Log(LogLevel.Warning, $"[SettingApplication] Domain service '{domainName}' not found");
                    return Enumerable.Empty<ApplicationSetting>();
                }

                return await service.GetSettingsAsync();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"[SettingApplication] Error getting settings for domain '{domainName}': {ex.Message}");
                return Enumerable.Empty<ApplicationSetting>();
            }
        }
    }
}