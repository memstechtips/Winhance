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
    /// Follows SOLID principles by delegating to focused strategy implementations.
    /// </summary>
    public class SystemSettingOrchestrator
    {
        private readonly IEnumerable<ISettingApplicationStrategy> _strategies;
        private readonly IWindowsCompatibilityFilter _compatibilityFilter;
        private readonly ILogService _logService;
        private readonly ISystemSettingsDiscoveryService _systemSettingsDiscoveryService;
        private readonly IComboBoxValueResolver? _comboBoxResolver;

        public SystemSettingOrchestrator(
            IEnumerable<ISettingApplicationStrategy> strategies,
            IWindowsCompatibilityFilter compatibilityFilter,
            ILogService logService,
            ISystemSettingsDiscoveryService systemSettingsDiscoveryService,
            IComboBoxValueResolver? comboBoxResolver = null
        )
        {
            _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
            _compatibilityFilter =
                compatibilityFilter ?? throw new ArgumentNullException(nameof(compatibilityFilter));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _systemSettingsDiscoveryService =
                systemSettingsDiscoveryService
                ?? throw new ArgumentNullException(nameof(systemSettingsDiscoveryService));
            _comboBoxResolver = comboBoxResolver;
        }

        /// <summary>
        /// Applies a setting with the specified value. Handles all control types uniformly.
        /// </summary>
        public virtual async Task ApplySettingAsync(
            string settingId,
            bool enable,
            object? value,
            IEnumerable<ApplicationSetting> availableSettings,
            string domainName
        )
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Applying {domainName} setting '{settingId}': enable={enable}, value={value}"
                );

                var settings = availableSettings.ToList();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);

                if (setting == null)
                {
                    throw new ArgumentException(
                        $"Setting '{settingId}' not found in {domainName} domain"
                    );
                }

                // Apply setting based on control type using strategies
                switch (setting.ControlType)
                {
                    case ControlType.BinaryToggle:
                        await ApplyBinaryToggleAsync(setting, enable);
                        break;
                    case ControlType.ComboBox:
                        if (value is int comboBoxIndex)
                        {
                            await ApplyComboBoxIndexAsync(setting, comboBoxIndex);
                        }
                        else if (value != null)
                        {
                            // Legacy fallback for non-index values
                            await ApplyComboBoxAsync(setting, value);
                        }
                        else
                        {
                            throw new ArgumentException(
                                $"ComboBox setting '{settingId}' requires a ComboBox index value"
                            );
                        }
                        break;
                    case ControlType.NumericUpDown:
                        if (value != null)
                        {
                            await ApplyNumericUpDownAsync(setting, value);
                        }
                        else
                        {
                            throw new ArgumentException(
                                $"NumericUpDown setting '{settingId}' requires a value"
                            );
                        }
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Control type '{setting.ControlType}' is not supported"
                        );
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Successfully applied {domainName} setting '{settingId}'"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying {domainName} setting '{settingId}': {ex.Message}"
                );
                throw;
            }
        }

        /// <summary>
        /// Applies a binary toggle setting using strategies.
        /// </summary>
        public virtual async Task ApplyBinaryToggleAsync(ApplicationSetting setting, bool enable)
        {
            var applicableStrategies = _strategies.Where(s => s.CanHandle(setting));
            foreach (var strategy in applicableStrategies)
            {
                await strategy.ApplyBinaryToggleAsync(setting, enable);
            }
        }

        /// <summary>
        /// Applies a ComboBox setting using strategies.
        /// </summary>
        public virtual async Task ApplyComboBoxIndexAsync(
            ApplicationSetting setting,
            int comboBoxIndex
        )
        {
            var applicableStrategies = _strategies.Where(s => s.CanHandle(setting));
            foreach (var strategy in applicableStrategies)
            {
                await strategy.ApplyComboBoxIndexAsync(setting, comboBoxIndex);
            }
        }

        /// <summary>
        /// Legacy ComboBox application method.
        /// </summary>
        public virtual async Task ApplyComboBoxAsync(ApplicationSetting setting, object value)
        {
            try
            {
                int intValue = Convert.ToInt32(value);

                // Check if this is a complex ComboBox with value mappings
                if (
                    setting.CustomProperties?.TryGetValue("ValueMappings", out var mappingsObj)
                        == true
                    && mappingsObj is Dictionary<int, Dictionary<string, int>> valueMappings
                )
                {
                    await ApplyComplexComboBoxAsync(setting, intValue, valueMappings);
                }
                else
                {
                    // Standard ComboBox - delegate to strategies
                    await ApplyComboBoxIndexAsync(setting, intValue);
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Applied combobox setting '{setting.Id}' with value: {intValue}"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying combobox setting '{setting.Id}': {ex.Message}"
                );
                throw;
            }
        }

        /// <summary>
        /// Applies a complex combobox setting with value mappings.
        /// </summary>
        public virtual async Task ApplyComplexComboBoxAsync(
            ApplicationSetting setting,
            int selectedValue,
            Dictionary<int, Dictionary<string, int>> valueMappings
        )
        {
            var applicableStrategies = _strategies.Where(s => s.CanHandle(setting));
            foreach (var strategy in applicableStrategies)
            {
                await strategy.ApplyComboBoxIndexAsync(setting, selectedValue);
            }
        }

        /// <summary>
        /// Applies a numeric up/down setting using strategies.
        /// </summary>
        public virtual async Task ApplyNumericUpDownAsync(ApplicationSetting setting, object value)
        {
            var applicableStrategies = _strategies.Where(s => s.CanHandle(setting));
            foreach (var strategy in applicableStrategies)
            {
                await strategy.ApplyNumericUpDownAsync(setting, value);
            }
        }

        /// <summary>
        /// Gets the current status of a setting using strategies.
        /// </summary>
        public virtual async Task<bool> GetSettingStatusAsync(
            string settingId,
            IEnumerable<ApplicationSetting> availableSettings
        )
        {
            var settings = availableSettings.ToList();
            var setting = settings.FirstOrDefault(s => s.Id == settingId);

            if (setting != null)
            {
                var applicableStrategies = _strategies.Where(s => s.CanHandle(setting));
                foreach (var strategy in applicableStrategies)
                {
                    var result = await strategy.GetSettingStatusAsync(settingId, settings);
                    if (result)
                        return true; // Return true if any strategy reports enabled
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the current value of a setting using strategies.
        /// </summary>
        public virtual async Task<object?> GetSettingValueAsync(
            string settingId,
            IEnumerable<ApplicationSetting> availableSettings
        )
        {
            var settings = availableSettings.ToList();
            var setting = settings.FirstOrDefault(s => s.Id == settingId);

            if (setting != null)
            {
                var applicableStrategies = _strategies.Where(s => s.CanHandle(setting));
                foreach (var strategy in applicableStrategies)
                {
                    var result = await strategy.GetSettingValueAsync(settingId, settings);
                    if (result != null)
                        return result; // Return first non-null value
                }
            }

            return null;
        }

        /// <summary>
        /// Helper method to load settings with their current system state.
        /// </summary>
        public virtual async Task<IEnumerable<ApplicationSetting>> GetSettingsWithSystemStateAsync(
            IEnumerable<ApplicationSetting> originalSettings,
            string domainName
        )
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Loading {domainName} settings with system state and centralized ComboBox resolution"
                );

                // Apply Windows version-based filtering first
                var filteredSettings = _compatibilityFilter.FilterSettingsByWindowsVersion(
                    originalSettings
                );
                var settings = filteredSettings.ToList();

                // First, get basic system state (without ComboBox-specific resolution)
                var systemStates = await GetBasicSystemStateAsync(settings);

                // Create new settings with updated state and ComboBox resolution
                var updatedSettings = new List<ApplicationSetting>();

                foreach (var originalSetting in settings)
                {
                    ApplicationSetting updatedSetting;

                    if (systemStates.TryGetValue(originalSetting.Id, out var state))
                    {
                        // Handle ComboBox settings with centralized resolution
                        if (
                            originalSetting.ControlType == ControlType.ComboBox
                            && _comboBoxResolver?.CanResolve(originalSetting) == true
                        )
                        {
                            try
                            {
                                // Resolve current ComboBox index using the centralized resolver
                                var currentComboBoxIndex =
                                    await _comboBoxResolver.ResolveCurrentIndexAsync(
                                        originalSetting
                                    );

                                updatedSetting = originalSetting with
                                {
                                    IsInitiallyEnabled = state.IsEnabled,
                                    CurrentValue = currentComboBoxIndex, // ComboBox index, not raw registry value
                                    IsEnabled = state.IsEnabled,
                                };

                                _logService.Log(
                                    LogLevel.Info,
                                    $"Resolved ComboBox '{originalSetting.Id}' to index: {currentComboBoxIndex}"
                                );
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(
                                    LogLevel.Warning,
                                    $"Failed to resolve ComboBox '{originalSetting.Id}': {ex.Message}. Using raw registry value."
                                );

                                // Fallback to basic state without ComboBox resolution
                                updatedSetting = originalSetting with
                                {
                                    IsInitiallyEnabled = state.IsEnabled,
                                    CurrentValue = state.CurrentValue,
                                    IsEnabled = state.IsEnabled,
                                };
                            }
                        }
                        else
                        {
                            // Non-ComboBox settings use standard state resolution
                            updatedSetting = originalSetting with
                            {
                                IsInitiallyEnabled = state.IsEnabled,
                                CurrentValue = state.CurrentValue,
                                IsEnabled = state.IsEnabled,
                            };
                        }

                        updatedSettings.Add(updatedSetting);
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"No system state found for setting '{originalSetting.Id}', using defaults"
                        );
                        updatedSettings.Add(originalSetting);
                    }
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Completed {domainName} settings loading with centralized ComboBox resolution. Processed {updatedSettings.Count} settings."
                );

                return updatedSettings;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading {domainName} settings with centralized resolution: {ex.Message}"
                );
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        /// <summary>
        /// Gets basic system state without ComboBox-specific resolution.
        /// </summary>
        protected virtual async Task<
            Dictionary<string, (bool IsEnabled, object? CurrentValue)>
        > GetBasicSystemStateAsync(IEnumerable<ApplicationSetting> settings)
        {
            return await _systemSettingsDiscoveryService.GetCurrentSettingsStateAndValuesAsync(
                settings
            );
        }
    }
}
