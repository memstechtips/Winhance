using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces.WindowsRegistry;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Models.WindowsRegistry;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class SystemSettingsDiscoveryService : ISystemSettingsDiscoveryService
    {
        private readonly IWindowsRegistryService _registryService;
        private readonly ICommandService _commandService;
        private readonly ILogService _logService;
        private readonly IWindowsCompatibilityFilter _compatibilityFilter;
        private readonly IComboBoxResolver _comboBoxResolver;

        public SystemSettingsDiscoveryService(
            IWindowsRegistryService windowsRegistryService,
            ICommandService commandService,
            ILogService logService,
            IWindowsCompatibilityFilter compatibilityFilter,
            IComboBoxResolver comboBoxResolver)
        {
            _registryService = windowsRegistryService ?? throw new ArgumentNullException(nameof(windowsRegistryService));
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _compatibilityFilter = compatibilityFilter ?? throw new ArgumentNullException(nameof(compatibilityFilter));
            _comboBoxResolver = comboBoxResolver ?? throw new ArgumentNullException(nameof(comboBoxResolver));
        }

        public async Task<Dictionary<string, bool>> GetCurrentSettingsStateAsync(IEnumerable<SettingDefinition> settings)
        {
            var results = new Dictionary<string, bool>();
            if (settings == null) return results;

            foreach (var setting in settings)
            {
                try
                {
                    bool isEnabled = false;

                    if (setting.RegistrySettings?.Count > 0)
                    {
                        foreach (var registrySetting in setting.RegistrySettings)
                        {
                            if (_registryService.IsSettingApplied(registrySetting))
                            {
                                isEnabled = true;
                                break;
                            }
                        }
                    }
                    else if (setting.CommandSettings?.Any() == true)
                    {
                        isEnabled = false;
                    }

                    results[setting.Id] = isEnabled;
                }
                catch (Exception)
                {
                    results[setting.Id] = false;
                }
            }

            return results;
        }

        public async Task<Dictionary<string, object?>> GetCurrentSettingsValuesAsync(IEnumerable<SettingDefinition> settings)
        {
            var results = new Dictionary<string, object?>();
            if (settings == null) return results;

            foreach (var setting in settings)
            {
                try
                {
                    object? currentValue = null;
                    if (setting.RegistrySettings?.Count > 0)
                    {
                        currentValue = _registryService.GetValue(setting.RegistrySettings[0].KeyPath, setting.RegistrySettings[0].ValueName);
                    }
                    results[setting.Id] = currentValue;
                }
                catch (Exception)
                {
                    results[setting.Id] = null;
                }
            }

            return results;
        }

        public async Task<Dictionary<string, (bool IsEnabled, object? CurrentValue)>> GetCurrentSettingsStateAndValuesAsync(IEnumerable<SettingDefinition> settings)
        {
            var results = new Dictionary<string, (bool IsEnabled, object? CurrentValue)>();
            if (settings == null) return results;

            var stateTask = GetCurrentSettingsStateAsync(settings);
            var valuesTask = GetCurrentSettingsValuesAsync(settings);
            await Task.WhenAll(stateTask, valuesTask);

            var states = await stateTask;
            var values = await valuesTask;

            foreach (var setting in settings)
            {
                var isEnabled = states.TryGetValue(setting.Id, out var enabled) ? enabled : false;
                var currentValue = values.TryGetValue(setting.Id, out var value) ? value : null;
                results[setting.Id] = (isEnabled, currentValue);
            }

            return results;
        }

        public async Task<Dictionary<string, Dictionary<RegistrySetting, object?>>> GetIndividualRegistryValuesAsync(IEnumerable<SettingDefinition> settings)
        {
            var results = new Dictionary<string, Dictionary<RegistrySetting, object?>>();
            if (settings == null) return results;

            foreach (var setting in settings)
            {
                try
                {
                    var individualValues = new Dictionary<RegistrySetting, object?>();
                    if (setting.RegistrySettings?.Count > 0)
                    {
                        foreach (var registrySetting in setting.RegistrySettings)
                        {
                            var currentValue = GetRegistryValueAsync(registrySetting);
                            individualValues[registrySetting] = currentValue;
                        }
                    }
                    results[setting.Id] = individualValues;
                }
                catch (Exception)
                {
                    results[setting.Id] = new Dictionary<RegistrySetting, object?>();
                }
            }

            return results;
        }

        public async Task<IEnumerable<SettingDefinition>> GetSettingsWithSystemStateAsync(
            IEnumerable<SettingDefinition> originalSettings,
            string domainName
        )
        {
            try
            {
                var filteredSettings = _compatibilityFilter.FilterSettingsByWindowsVersion(originalSettings);
                var settings = filteredSettings.ToList();

                var systemStates = await GetCurrentSettingsStateAndValuesAsync(settings);
                var updatedSettings = new List<SettingDefinition>();

                foreach (var originalSetting in settings)
                {
                    if (systemStates.TryGetValue(originalSetting.Id, out var state))
                    {
                        SettingDefinition updatedSetting;

                        updatedSetting = originalSetting;

                        updatedSettings.Add(updatedSetting);
                    }
                    else
                    {
                        updatedSettings.Add(originalSetting);
                    }
                }

                return updatedSettings;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading {domainName} settings: {ex.Message}");
                return Enumerable.Empty<SettingDefinition>();
            }
        }

        private object? GetRegistryValueAsync(RegistrySetting registrySetting)
        {
            try
            {
                return _registryService.GetValue(registrySetting.KeyPath, registrySetting.ValueName);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
