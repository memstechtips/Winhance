using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Generic ComboBox value resolver that handles standard registry-based ComboBox scenarios.
    /// Follows DRY principle by providing common ComboBox resolution logic for all domains.
    /// Uses Convention over Configuration pattern with ApplicationSetting metadata.
    /// Adheres to SRP by handling only generic registry-to-ComboBox value mapping.
    /// </summary>
    public class GenericComboBoxValueResolver : IComboBoxValueResolver
    {
        private readonly IRegistryService _registryService;
        private readonly ILogService _logService;

        public string DomainName => "Generic";

        public GenericComboBoxValueResolver(
            IRegistryService registryService,
            ILogService logService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public bool CanResolve(ApplicationSetting setting)
        {
            // Can resolve any ComboBox setting that has proper metadata structure
            // Excludes complex domain-specific settings that need specialized resolvers
            var isComboBox = setting.ControlType == ControlType.ComboBox;
            var hasValidMetadata = HasValidComboBoxMetadata(setting);
            var isNotComplex = !IsComplexDomainSetting(setting);
            
            _logService.Log(LogLevel.Info, $"[GenericResolver] CanResolve '{setting.Id}': ComboBox={isComboBox}, ValidMetadata={hasValidMetadata}, NotComplex={isNotComplex}");
            
            return isComboBox && hasValidMetadata && isNotComplex;
        }

        /// <summary>
        /// Determines if the setting has valid metadata for generic resolution.
        /// Follows Convention over Configuration by checking for expected metadata structure.
        /// Supports both ComboBoxOptions and ValueMappings patterns.
        /// </summary>
        private bool HasValidComboBoxMetadata(ApplicationSetting setting)
        {
            // Check if setting has registry settings
            if (setting.RegistrySettings?.Any() != true)
            {
                _logService.Log(LogLevel.Warning, $"[GenericResolver] '{setting.Id}' has no registry settings");
                return false;
            }

            // Check for ComboBoxOptions in primary registry setting
            var primarySetting = setting.RegistrySettings.FirstOrDefault(r => r.IsPrimary);
            var hasComboBoxOptionsInPrimary = primarySetting?.CustomProperties?.ContainsKey("ComboBoxOptions") == true;

            // Check for ComboBoxOptions or ValueMappings in setting's CustomProperties
            var hasComboBoxOptionsInSetting = setting.CustomProperties?.ContainsKey("ComboBoxOptions") == true;
            var hasValueMappings = setting.CustomProperties?.ContainsKey("ValueMappings") == true;

            var isValid = hasComboBoxOptionsInPrimary || hasComboBoxOptionsInSetting || hasValueMappings;
            
            _logService.Log(LogLevel.Info, $"[GenericResolver] '{setting.Id}' metadata: ComboBoxOptions(Primary)={hasComboBoxOptionsInPrimary}, ComboBoxOptions(Setting)={hasComboBoxOptionsInSetting}, ValueMappings={hasValueMappings}, Valid={isValid}");
            
            return isValid;
        }

        /// <summary>
        /// Identifies settings that require complex domain-specific logic.
        /// Ensures domain-specific resolvers take precedence following SRP.
        /// </summary>
        private bool IsComplexDomainSetting(ApplicationSetting setting)
        {
            // Currently no settings require specialized domain resolvers
            // All ComboBox settings can be handled generically using ComboBoxOptions or ValueMappings
            // This follows DRY principle by consolidating all ComboBox logic
            var complexSettingIds = new string[0];

            return complexSettingIds.Contains(setting.Id);
        }

        public async Task<int?> ResolveCurrentIndexAsync(ApplicationSetting setting)
        {
            try
            {
                _logService.Log(LogLevel.Debug, $"[GenericResolver] Resolving ComboBox value for '{setting.Id}'");

                // Check if setting uses ValueMappings (multi-registry pattern)
                if (setting.CustomProperties?.ContainsKey("ValueMappings") == true)
                {
                    return await ResolveUsingValueMappingsAsync(setting);
                }

                // Fall back to ComboBoxOptions pattern (single registry)
                return await ResolveUsingComboBoxOptionsAsync(setting);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error resolving ComboBox value for '{setting.Id}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resolves ComboBox index using ValueMappings pattern (multi-registry values).
        /// Follows SRP by handling complex multi-registry ComboBox logic.
        /// </summary>
        private async Task<int?> ResolveUsingValueMappingsAsync(ApplicationSetting setting)
        {
            // Get ValueMappings metadata
            if (!setting.CustomProperties.TryGetValue("ValueMappings", out var mappingsObj) ||
                mappingsObj is not Dictionary<int, Dictionary<string, int>> valueMappings)
            {
                _logService.Log(LogLevel.Warning, $"Invalid ValueMappings metadata for '{setting.Id}'");
                return null;
            }

            // Read all registry values for comparison
            var currentValues = new Dictionary<string, int>();
            foreach (var registrySetting in setting.RegistrySettings)
            {
                var value = await GetRegistryValueAsync(registrySetting);
                if (value.HasValue)
                {
                    currentValues[registrySetting.Name] = value.Value;
                }
            }

            _logService.Log(LogLevel.Info, $"[GenericResolver] Current registry values for '{setting.Id}': {string.Join(", ", currentValues.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

            // Find matching ValueMapping
            foreach (var mapping in valueMappings)
            {
                var index = mapping.Key;
                var expectedValues = mapping.Value;
                
                var matches = expectedValues.All(expected => 
                    currentValues.TryGetValue(expected.Key, out var currentValue) && 
                    currentValue == expected.Value);

                if (matches)
                {
                    _logService.Log(LogLevel.Info, $"[GenericResolver] Matched ValueMapping index {index} for '{setting.Id}'");
                    return index;
                }
            }

            _logService.Log(LogLevel.Warning, $"No ValueMapping matches current registry values for '{setting.Id}'");
            return null;
        }

        /// <summary>
        /// Resolves ComboBox index using ComboBoxOptions pattern (single registry value).
        /// Follows SRP by handling simple single-registry ComboBox logic.
        /// </summary>
        private async Task<int?> ResolveUsingComboBoxOptionsAsync(ApplicationSetting setting)
        {
            // First try to get ComboBoxOptions from setting's CustomProperties
            Dictionary<string, int> comboBoxOptions = null;
            
            if (setting.CustomProperties?.TryGetValue("ComboBoxOptions", out var settingOptionsObj) == true &&
                settingOptionsObj is Dictionary<string, int> settingOptions)
            {
                comboBoxOptions = settingOptions;
                _logService.Log(LogLevel.Info, $"[GenericResolver] Using ComboBoxOptions from setting for '{setting.Id}'");
            }
            else
            {
                // Fall back to primary registry setting's CustomProperties
                var primarySetting = setting.RegistrySettings.FirstOrDefault(r => r.IsPrimary) 
                                   ?? setting.RegistrySettings.FirstOrDefault();

                if (primarySetting?.CustomProperties?.TryGetValue("ComboBoxOptions", out var primaryOptionsObj) == true &&
                    primaryOptionsObj is Dictionary<string, int> primaryOptions)
                {
                    comboBoxOptions = primaryOptions;
                    _logService.Log(LogLevel.Info, $"[GenericResolver] Using ComboBoxOptions from primary registry setting for '{setting.Id}'");
                }
            }

            if (comboBoxOptions == null)
            {
                _logService.Log(LogLevel.Warning, $"No valid ComboBoxOptions found for '{setting.Id}'");
                return null;
            }

            // Get current registry value from primary setting
            var primarySetting2 = setting.RegistrySettings.FirstOrDefault(r => r.IsPrimary) 
                                ?? setting.RegistrySettings.FirstOrDefault();

            var currentValue = await GetRegistryValueAsync(primarySetting2);
            if (currentValue == null)
            {
                _logService.Log(LogLevel.Warning, $"Could not read registry value for '{setting.Id}'");
                return null;
            }

            _logService.Log(LogLevel.Info, $"[GenericResolver] Current registry value for '{setting.Id}': {currentValue}");

            // Find the ComboBox option that matches the current registry value
            var matchingOption = comboBoxOptions.FirstOrDefault(kvp => kvp.Value.Equals(currentValue));
            if (matchingOption.Key == null)
            {
                _logService.Log(LogLevel.Warning, $"No ComboBox option matches registry value {currentValue} for '{setting.Id}'");
                return null;
            }

            // Get the index of this option in the ComboBox (ensure consistent ordering)
            var optionsList = comboBoxOptions.Keys.OrderBy(k => k).ToList();
            var comboBoxIndex = optionsList.IndexOf(matchingOption.Key);

            _logService.Log(LogLevel.Info, $"[GenericResolver] Mapped '{matchingOption.Key}' (value={currentValue}) to ComboBox index {comboBoxIndex}");
            return comboBoxIndex;
        }

        public async Task ApplyIndexAsync(ApplicationSetting setting, int index)
        {
            try
            {
                _logService.Log(LogLevel.Debug, $"[GenericResolver] Applying ComboBox index {index} for '{setting.Id}'");

                // Check if setting uses ValueMappings (multi-registry pattern)
                if (setting.CustomProperties?.ContainsKey("ValueMappings") == true)
                {
                    await ApplyUsingValueMappingsAsync(setting, index);
                    return;
                }

                // Fall back to ComboBoxOptions pattern (single registry)
                await ApplyUsingComboBoxOptionsAsync(setting, index);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying ComboBox value for '{setting.Id}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Applies ComboBox index using ValueMappings pattern (multi-registry values).
        /// Follows SRP by handling complex multi-registry ComboBox logic.
        /// </summary>
        private async Task ApplyUsingValueMappingsAsync(ApplicationSetting setting, int index)
        {
            // Get ValueMappings metadata
            if (!setting.CustomProperties.TryGetValue("ValueMappings", out var mappingsObj) ||
                mappingsObj is not Dictionary<int, Dictionary<string, int>> valueMappings)
            {
                throw new ArgumentException($"Invalid ValueMappings metadata for '{setting.Id}'");
            }

            // Get the registry values for the specified index
            if (!valueMappings.TryGetValue(index, out var registryValues))
            {
                throw new ArgumentException($"ComboBox index {index} not found in ValueMappings for '{setting.Id}'");
            }

            _logService.Log(LogLevel.Info, $"[GenericResolver] Applying ValueMapping index {index} for '{setting.Id}': {string.Join(", ", registryValues.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

            // Apply each registry value
            foreach (var registryValue in registryValues)
            {
                var registryName = registryValue.Key;
                var value = registryValue.Value;

                // Find the corresponding RegistrySetting
                var registrySetting = setting.RegistrySettings.FirstOrDefault(rs => rs.Name == registryName);
                if (registrySetting == null)
                {
                    _logService.Log(LogLevel.Warning, $"Registry setting '{registryName}' not found in setting '{setting.Id}', skipping");
                    continue;
                }

                await SetRegistryValueAsync(registrySetting, value);
                _logService.Log(LogLevel.Info, $"[GenericResolver] Applied {registryName} = {value}");
            }

            _logService.Log(LogLevel.Info, $"Successfully applied ValueMapping index {index} for '{setting.Id}'");
        }

        /// <summary>
        /// Applies ComboBox index using ComboBoxOptions pattern (single registry value).
        /// Follows SRP by handling simple single-registry ComboBox logic.
        /// </summary>
        private async Task ApplyUsingComboBoxOptionsAsync(ApplicationSetting setting, int index)
        {
            // First try to get ComboBoxOptions from setting's CustomProperties
            Dictionary<string, int> comboBoxOptions = null;
            
            if (setting.CustomProperties?.TryGetValue("ComboBoxOptions", out var settingOptionsObj) == true &&
                settingOptionsObj is Dictionary<string, int> settingOptions)
            {
                comboBoxOptions = settingOptions;
                _logService.Log(LogLevel.Info, $"[GenericResolver] Using ComboBoxOptions from setting for '{setting.Id}'");
            }
            else
            {
                // Fall back to primary registry setting's CustomProperties
                var primarySetting = setting.RegistrySettings.FirstOrDefault(r => r.IsPrimary) 
                                   ?? setting.RegistrySettings.FirstOrDefault();

                if (primarySetting?.CustomProperties?.TryGetValue("ComboBoxOptions", out var primaryOptionsObj) == true &&
                    primaryOptionsObj is Dictionary<string, int> primaryOptions)
                {
                    comboBoxOptions = primaryOptions;
                    _logService.Log(LogLevel.Info, $"[GenericResolver] Using ComboBoxOptions from primary registry setting for '{setting.Id}'");
                }
            }

            if (comboBoxOptions == null)
            {
                throw new ArgumentException($"No valid ComboBoxOptions found for '{setting.Id}'");
            }

            // Get the option at the specified index
            var optionsList = comboBoxOptions.Keys.OrderBy(k => k).ToList();
            if (index < 0 || index >= optionsList.Count)
            {
                throw new ArgumentException($"ComboBox index {index} is out of range for '{setting.Id}' (max: {optionsList.Count - 1})");
            }

            var selectedOption = optionsList[index];
            var registryValue = comboBoxOptions[selectedOption];

            _logService.Log(LogLevel.Info, $"[GenericResolver] Applying '{selectedOption}' (index={index}, value={registryValue}) for '{setting.Id}'");

            // Apply to all registry settings in the setting
            // This handles both single and multi-registry scenarios (like WindowsTheme Apps+System)
            foreach (var registrySetting in setting.RegistrySettings)
            {
                await SetRegistryValueAsync(registrySetting, registryValue);
            }

            _logService.Log(LogLevel.Info, $"Successfully applied ComboBox index {index} for '{setting.Id}'");
        }

        /// <summary>
        /// Helper method to read registry value.
        /// Follows DRY principle by centralizing registry read logic.
        /// </summary>
        private async Task<int?> GetRegistryValueAsync(RegistrySetting registrySetting)
        {
            try
            {
                var value = await _registryService.GetCurrentValueAsync(registrySetting);
                return value as int? ?? registrySetting.DefaultValue as int?;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Error reading registry value '{registrySetting.Name}': {ex.Message}");
                return registrySetting.DefaultValue as int?;
            }
        }

        /// <summary>
        /// Helper method to set registry value.
        /// Follows DRY principle by centralizing registry write logic.
        /// </summary>
        private async Task SetRegistryValueAsync(RegistrySetting registrySetting, int value)
        {
            try
            {
                // Direct string usage - no conversion needed
                _registryService.SetValue(
                    $"{registrySetting.Hive}\\{registrySetting.SubKey}",
                    registrySetting.Name,
                    value,
                    registrySetting.ValueType
                );

                _logService.Log(LogLevel.Info, $"[GenericResolver] Set {registrySetting.Name} = {value}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error setting registry value '{registrySetting.Name}': {ex.Message}");
                throw;
            }
        }
    }
}
