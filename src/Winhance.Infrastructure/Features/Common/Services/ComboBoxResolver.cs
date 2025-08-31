using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces.WindowsRegistry;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Models.WindowsRegistry;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class ComboBoxResolver : IComboBoxResolver
    {
        private readonly IWindowsRegistryService _registryService;
        private readonly ILogService _logService;
        public const int CUSTOM_STATE_INDEX = -1;

        public string DomainName => "Generic";

        public ComboBoxResolver(
            IWindowsRegistryService windowsRegistryService,
            ILogService logService)
        {
            _registryService = windowsRegistryService ?? throw new ArgumentNullException(nameof(windowsRegistryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public bool CanResolve(SettingDefinition setting)
        {
            var isComboBox = setting.InputType == SettingInputType.Selection;
            var hasValidMetadata = HasValidComboBoxMetadata(setting);

            _logService.Log(LogLevel.Info, $"[ComboBoxResolver] CanResolve '{setting.Id}': ComboBox={isComboBox}, ValidMetadata={hasValidMetadata}");

            return isComboBox && hasValidMetadata;
        }

        private bool HasValidComboBoxMetadata(SettingDefinition setting)
        {
            if (setting.RegistrySettings?.Any() != true)
            {
                _logService.Log(LogLevel.Warning, $"[ComboBoxResolver] '{setting.Id}' has no registry settings");
                return false;
            }

            var hasValueMappings = setting.CustomProperties?.ContainsKey(CustomPropertyKeys.ValueMappings) == true;
            var hasDisplayNames = setting.CustomProperties?.ContainsKey(CustomPropertyKeys.ComboBoxDisplayNames) == true;

            var isValid = hasValueMappings && hasDisplayNames;

            _logService.Log(LogLevel.Info, $"[ComboBoxResolver] '{setting.Id}' metadata: ValueMappings={hasValueMappings}, ComboBoxDisplayNames={hasDisplayNames}, Valid={isValid}");

            return isValid;
        }

        public async Task<int?> ResolveCurrentIndexAsync(SettingDefinition setting)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"[ComboBoxResolver] Resolving ComboBox value for '{setting.Id}'");

                // Get ValueMappings metadata
                if (!setting.CustomProperties.TryGetValue(CustomPropertyKeys.ValueMappings, out var mappingsObj) ||
                    mappingsObj is not Dictionary<int, Dictionary<string, int>> valueMappings)
                {
                    _logService.Log(LogLevel.Warning, $"Invalid ValueMappings metadata for '{setting.Id}'");
                    return null;
                }

                // Read all registry values for comparison, handling multiple settings with same name
                var currentValues = new Dictionary<string, List<int>>();
                foreach (var registrySetting in setting.RegistrySettings)
                {
                    var value = GetRegistryValueAsync(registrySetting);
                    if (value.HasValue)
                    {
                        if (!currentValues.ContainsKey(registrySetting.ValueName))
                        {
                            currentValues[registrySetting.ValueName] = new List<int>();
                        }
                        currentValues[registrySetting.ValueName].Add(value.Value);
                    }
                }

                _logService.Log(LogLevel.Info, $"[ComboBoxResolver] Current registry values for '{setting.Id}': {string.Join(", ", currentValues.Select(kvp => $"{kvp.Key}=[{string.Join(",", kvp.Value)}]"))}");

                // Find matching ValueMapping
                foreach (var mapping in valueMappings)
                {
                    var index = mapping.Key;
                    var expectedValues = mapping.Value;

                    var matches = expectedValues.All(expected =>
                        currentValues.TryGetValue(expected.Key, out var currentValuesList) &&
                        currentValuesList.All(currentValue => currentValue == expected.Value));

                    if (matches)
                    {
                        _logService.Log(LogLevel.Info, $"[ComboBoxResolver] Matched ValueMapping index {index} for '{setting.Id}'");
                        return index;
                    }
                }

                var supportsCustomState = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.SupportsCustomState, out var supports) == true && (bool)supports;
                
                if (supportsCustomState)
                {
                    _logService.Log(LogLevel.Info, $"[ComboBoxResolver] Custom registry state detected for '{setting.Id}'");
                    return CUSTOM_STATE_INDEX;
                }
                
                _logService.Log(LogLevel.Warning, $"No ValueMapping matches current registry values for '{setting.Id}'");
                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error resolving ComboBox value for '{setting.Id}': {ex.Message}");
                return null;
            }
        }

        public async Task ApplyIndexAsync(SettingDefinition setting, int index)
        {
            try
            {
                if (index == CUSTOM_STATE_INDEX)
                {
                    _logService.Log(LogLevel.Info, $"[ComboBoxResolver] Cannot apply custom state index for '{setting.Id}'");
                    return;
                }

                _logService.Log(LogLevel.Info, $"[ComboBoxResolver] Applying ComboBox index {index} for '{setting.Id}'");

                if (!setting.CustomProperties.TryGetValue(CustomPropertyKeys.ValueMappings, out var mappingsObj) ||
                    mappingsObj is not Dictionary<int, Dictionary<string, int>> valueMappings)
                {
                    throw new ArgumentException($"Invalid ValueMappings metadata for '{setting.Id}'");
                }

                if (!valueMappings.TryGetValue(index, out var registryValues))
                {
                    throw new ArgumentException($"ComboBox index {index} not found in ValueMappings for '{setting.Id}'");
                }

                _logService.Log(LogLevel.Info, $"[ComboBoxResolver] Applying ValueMapping index {index} for '{setting.Id}': {string.Join(", ", registryValues.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

                // Apply each registry value
                foreach (var registryValue in registryValues)
                {
                    var registryName = registryValue.Key;
                    var value = registryValue.Value;

                    // Find ALL registry settings with matching name (handles duplicate names)
                    var matchingRegistrySettings = setting.RegistrySettings.Where(rs => rs.ValueName == registryName);
                    if (!matchingRegistrySettings.Any())
                    {
                        _logService.Log(LogLevel.Warning, $"Registry setting '{registryName}' not found in setting '{setting.Id}', skipping");
                        continue;
                    }

                    // Apply the same value to ALL registry settings with this name
                    foreach (var registrySetting in matchingRegistrySettings)
                    {
                        await SetRegistryValueAsync(registrySetting, value);
                        _logService.Log(LogLevel.Info, $"[ComboBoxResolver] Applied {registryName} = {value} at {registrySetting.KeyPath}");
                    }
                }

                _logService.Log(LogLevel.Info, $"Successfully applied ValueMapping index {index} for '{setting.Id}'");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying ComboBox value for '{setting.Id}': {ex.Message}");
                throw;
            }
        }

        public int GetIndexForDisplayValue(SettingDefinition setting, string displayValue)
        {
            if (string.IsNullOrEmpty(displayValue))
            {
                _logService.Log(LogLevel.Warning, $"Display value is null or empty for setting '{setting.Id}'");
                return -1;
            }

            try
            {
                // Get ComboBoxDisplayNames from setting
                if (setting.CustomProperties?.TryGetValue(CustomPropertyKeys.ComboBoxDisplayNames, out var displayNamesObj) != true ||
                    displayNamesObj is not string[] displayNames)
                {
                    _logService.Log(LogLevel.Warning, $"No ComboBoxDisplayNames found for setting '{setting.Id}'");
                    return -1;
                }

                // Find the display value in the array (case-insensitive)
                for (int i = 0; i < displayNames.Length; i++)
                {
                    if (string.Equals(displayNames[i], displayValue, StringComparison.OrdinalIgnoreCase))
                    {
                        _logService.Log(LogLevel.Debug, $"Mapped display value '{displayValue}' to index {i} for setting '{setting.Id}'");
                        return i;
                    }
                }

                _logService.Log(LogLevel.Warning, $"Display value '{displayValue}' not found in ComboBoxDisplayNames for setting '{setting.Id}'");
                return -1;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting index for display value '{displayValue}' in setting '{setting.Id}': {ex.Message}");
                return -1;
            }
        }

        private int? GetRegistryValueAsync(RegistrySetting registrySetting)
        {
            try
            {
                var value = _registryService.GetValue(registrySetting.KeyPath, registrySetting.ValueName);
                return value as int? ?? registrySetting.DefaultValue as int?;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Error reading registry value '{registrySetting.ValueName}': {ex.Message}");
                return registrySetting.DefaultValue as int?;
            }
        }

        private async Task SetRegistryValueAsync(RegistrySetting registrySetting, int value)
        {
            try
            {
                _registryService.SetValue(
                    registrySetting.KeyPath,
                    registrySetting.ValueName,
                    value,
                    registrySetting.ValueType
                );

                _logService.Log(LogLevel.Info, $"[ComboBoxResolver] Set {registrySetting.ValueName} = {value}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error setting registry value '{registrySetting.ValueName}': {ex.Message}");
                throw;
            }
        }
    }
}