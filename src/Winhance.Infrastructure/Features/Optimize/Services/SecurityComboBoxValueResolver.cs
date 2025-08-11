using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    /// <summary>
    /// Security domain resolver for ComboBox values.
    /// Follows SRP by handling only Security ComboBox resolution logic.
    /// Follows DIP by depending on IRegistryService abstraction.
    /// </summary>
    public class SecurityComboBoxValueResolver : ISecurityComboBoxValueResolver
    {
        private readonly IRegistryService _registryService;
        private readonly ILogService _logService;

        public string DomainName => "Security";

        public SecurityComboBoxValueResolver(
            IRegistryService registryService,
            ILogService logService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public bool CanResolve(ApplicationSetting setting)
        {
            // Check if this is a security-related ComboBox setting by examining the setting ID
            return setting.ControlType == ControlType.ComboBox && 
                   IsSecuritySetting(setting);
        }

        /// <summary>
        /// Determines if the setting belongs to the Security domain.
        /// </summary>
        /// <param name="setting">The application setting to check</param>
        /// <returns>True if it's a security setting</returns>
        private bool IsSecuritySetting(ApplicationSetting setting)
        {
            // Check for known security setting IDs or patterns
            return setting.Id == "windows-security-uac-level" || 
                   setting.GroupName?.Contains("Security", StringComparison.OrdinalIgnoreCase) == true ||
                   setting.Name?.Contains("UAC", StringComparison.OrdinalIgnoreCase) == true;
        }

        public async Task<int?> ResolveCurrentIndexAsync(ApplicationSetting setting)
        {
            try
            {
                _logService.Log(LogLevel.Debug, $"[SecurityResolver] Resolving ComboBox value for '{setting.Id}'");

                // Handle UAC Level setting specifically
                if (setting.Id == "windows-security-uac-level")
                {
                    return await ResolveUacLevelAsync(setting);
                }

                // Handle other Security ComboBox settings using generic LinkedSettings approach
                return await ResolveGenericComboBoxAsync(setting);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error resolving Security ComboBox value for '{setting.Id}': {ex.Message}");
                return null;
            }
        }

        public async Task ApplyIndexAsync(ApplicationSetting setting, int index)
        {
            try
            {
                _logService.Log(LogLevel.Debug, $"[SecurityResolver] Applying ComboBox index {index} for '{setting.Id}'");

                // Handle UAC Level setting specifically
                if (setting.Id == "windows-security-uac-level")
                {
                    await ApplyUacLevelAsync(setting, index);
                    return;
                }

                // Handle other Security ComboBox settings using generic approach
                await ApplyGenericComboBoxAsync(setting, index);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying Security ComboBox value for '{setting.Id}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Resolves UAC level from multiple registry values and maps to ComboBox index.
        /// This encapsulates UAC-specific domain logic in the Security domain.
        /// </summary>
        private async Task<int?> ResolveUacLevelAsync(ApplicationSetting setting)
        {
            if (setting.RegistrySettings == null || setting.RegistrySettings.Count < 2)
                return null;

            // Read UAC registry values
            var consentPromptValue = await GetRegistryValueAsync(setting.RegistrySettings[0]) ?? 5;
            var secureDesktopValue = await GetRegistryValueAsync(setting.RegistrySettings[1]) ?? 1;

            _logService.Log(LogLevel.Debug, 
                $"UAC registry values: ConsentPrompt={consentPromptValue}, SecureDesktop={secureDesktopValue}");

            // UAC domain logic: Map registry values to ComboBox index
            var mappings = new Dictionary<(int ConsentPrompt, int SecureDesktop), int>
            {
                [(2, 1)] = 0, // Always notify
                [(5, 1)] = 1, // Notify changes only
                [(5, 0)] = 2, // Notify changes no dim
                [(0, 0)] = 3, // Never notify
            };

            var key = (consentPromptValue, secureDesktopValue);
            var level = mappings.TryGetValue(key, out var mappedLevel) ? (int?)mappedLevel : null;

            _logService.Log(LogLevel.Info, 
                $"UAC level resolved to ComboBox index: {level}");

            return level;
        }

        /// <summary>
        /// Applies UAC level by mapping ComboBox index to multiple registry values.
        /// This encapsulates UAC-specific domain logic in the Security domain.
        /// </summary>
        private async Task ApplyUacLevelAsync(ApplicationSetting setting, int index)
        {
            if (setting.RegistrySettings == null || setting.RegistrySettings.Count < 2)
                throw new ArgumentException($"UAC setting '{setting.Id}' requires at least 2 registry settings");

            // UAC domain logic: Map ComboBox index to registry values
            var valueMappings = new Dictionary<int, (int ConsentPrompt, int SecureDesktop)>
            {
                [0] = (2, 1), // Always notify
                [1] = (5, 1), // Notify changes only
                [2] = (5, 0), // Notify changes no dim
                [3] = (0, 0), // Never notify
            };

            if (!valueMappings.TryGetValue(index, out var values))
                throw new ArgumentException($"Invalid ComboBox index {index} for UAC setting");

            _logService.Log(LogLevel.Debug, 
                $"Applying UAC values: ConsentPrompt={values.ConsentPrompt}, SecureDesktop={values.SecureDesktop}");

            // Apply registry values
            await SetRegistryValueAsync(setting.RegistrySettings[0], values.ConsentPrompt);
            await SetRegistryValueAsync(setting.RegistrySettings[1], values.SecureDesktop);

            _logService.Log(LogLevel.Info, $"Successfully applied UAC level {index}");
        }

        /// <summary>
        /// Generic ComboBox resolver for other Security settings using LinkedSettings pattern.
        /// </summary>
        private async Task<int?> ResolveGenericComboBoxAsync(ApplicationSetting setting)
        {
            // This would handle other Security ComboBox settings in the future
            // For now, return null as no generic Security ComboBox settings exist
            return null;
        }

        /// <summary>
        /// Generic ComboBox applier for other Security settings using LinkedSettings pattern.
        /// </summary>
        private async Task ApplyGenericComboBoxAsync(ApplicationSetting setting, int index)
        {
            // This would handle other Security ComboBox settings in the future
            // For now, no-op as no generic Security ComboBox settings exist
            await Task.CompletedTask;
        }

        /// <summary>
        /// Helper method to read registry value.
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
                _logService.Log(LogLevel.Error, $"Error reading registry value '{registrySetting.Name}': {ex.Message}");
                return registrySetting.DefaultValue as int?;
            }
        }

        /// <summary>
        /// Helper method to set registry value.
        /// </summary>
        private async Task SetRegistryValueAsync(RegistrySetting registrySetting, int value)
        {
            string hiveName = registrySetting.Hive switch
            {
                RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
                RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
                RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
                RegistryHive.Users => "HKEY_USERS",
                RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
                _ => registrySetting.Hive.ToString()
            };

            _registryService.SetValue(
                $"{hiveName}\\{registrySetting.SubKey}",
                registrySetting.Name,
                value,
                RegistryValueKind.DWord
            );

            _logService.Log(LogLevel.Info, $"Set {registrySetting.Name} = {value}");
        }
    }
}
