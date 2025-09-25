using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class SettingApplicationService(
        IDomainServiceRouter domainServiceRouter,
        IWindowsRegistryService registryService,
        IComboBoxResolver comboBoxResolver,
        ICommandService commandService,
        ILogService logService,
        IDependencyManager dependencyManager,
        IGlobalSettingsRegistry globalSettingsRegistry,
        IEventBus eventBus,
        ISystemSettingsDiscoveryService discoveryService,
        IRecommendedSettingsService recommendedSettingsService) : ISettingApplicationService
    {

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null, bool checkboxResult = false, string? commandString = null, bool applyRecommended = false)
        {
            logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying setting '{settingId}' - Enable: {enable}, Value: {value}");

            var domainService = domainServiceRouter.GetDomainService(settingId);
            var allSettings = await domainService.GetSettingsAsync();
            var setting = allSettings.FirstOrDefault(s => s.Id == settingId);

            if (setting == null)
                throw new ArgumentException($"Setting '{settingId}' not found in {domainService.DomainName} settings");

            globalSettingsRegistry.RegisterSetting(domainService.DomainName, setting);

            if (!string.IsNullOrEmpty(commandString))
            {
                await ExecuteActionCommand(domainService, commandString, applyRecommended, settingId);
                return;
            }

            await HandleDependencies(settingId, allSettings, enable, value);

            await ApplySettingOperations(setting, enable, value);

            eventBus.Publish(new SettingAppliedEvent(settingId, enable, value));
            logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied setting '{settingId}'");
        }

        private async Task HandleDependencies(string settingId, IEnumerable<SettingDefinition> allSettings, bool enable, object? value)
        {
            if (enable)
            {
                var setting = allSettings.FirstOrDefault(s => s.Id == settingId);
                if (setting?.Dependencies?.Any() == true)
                {
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Handling dependencies for '{settingId}'");
                    var dependencyResult = await dependencyManager.HandleSettingEnabledAsync(settingId, allSettings.Cast<ISettingItem>(), this, discoveryService);
                    if (!dependencyResult)
                        throw new InvalidOperationException($"Cannot enable '{settingId}' due to unsatisfied dependencies");
                }
            }
            else
            {
                var hasDependentSettings = allSettings.Any(s => s.Dependencies?.Any(d => d.RequiredSettingId == settingId) == true);
                if (hasDependentSettings)
                {
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Handling dependent settings for disabled '{settingId}'");
                    await dependencyManager.HandleSettingDisabledAsync(settingId, allSettings, this, discoveryService);
                }
            }

            if (enable && value != null)
            {
                await dependencyManager.HandleSettingValueChangedAsync(settingId, allSettings, this, discoveryService);
            }
        }

        private async Task ExecuteActionCommand(IDomainService domainService, string commandString, bool applyRecommended, string settingId)
        {
            logService.Log(LogLevel.Info, $"[SettingApplicationService] Executing ActionCommand '{commandString}' for setting '{settingId}'");

            var method = domainService.GetType().GetMethod(commandString);
            if (method == null)
                throw new NotSupportedException($"Method '{commandString}' not found on service '{domainService.GetType().Name}'");

            if (!typeof(Task).IsAssignableFrom(method.ReturnType))
                throw new NotSupportedException($"Method '{commandString}' must return Task for async execution");

            var result = method.Invoke(domainService, null);
            if (result is Task task)
                await task;

            if (applyRecommended)
            {
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying recommended settings for domain containing '{settingId}'");
                try
                {
                    await recommendedSettingsService.ApplyRecommendedSettingsAsync(settingId);
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied recommended settings for '{settingId}'");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[SettingApplicationService] Failed to apply recommended settings for '{settingId}': {ex.Message}");
                }
            }

            logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully executed ActionCommand '{commandString}' for setting '{settingId}'");
        }


        private async Task ApplySettingOperations(SettingDefinition setting, bool enable, object? value)
        {
            logService.Log(LogLevel.Info, $"[SettingApplicationService] Processing operations for '{setting.Id}' - Type: {setting.InputType}");

            if (setting.RegistrySettings?.Count > 0)
            {
                if (setting.InputType == InputType.Selection && (value is int || (value is string stringValue && !string.IsNullOrEmpty(stringValue))))
                {
                    int index = value switch
                    {
                        int intValue => intValue,
                        string strValue => comboBoxResolver.GetIndexFromDisplayName(setting, strValue),
                        _ => 0
                    };
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}' with unified mapping for index: {index}");

                    var specificValues = comboBoxResolver.ResolveIndexToRawValues(setting, index);

                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        if (specificValues.TryGetValue(registrySetting.ValueName, out var specificValue))
                        {
                            if (specificValue == null)
                            {
                                registryService.ApplySetting(registrySetting, false);
                            }
                            else
                            {
                                registryService.ApplySetting(registrySetting, true, Convert.ToInt32(specificValue));
                            }
                        }
                        else
                        {
                            bool applyValue = comboBoxResolver.GetValueFromIndex(setting, index) != 0;
                            registryService.ApplySetting(registrySetting, applyValue);
                        }
                    }
                }
                else
                {
                    bool applyValue = setting.InputType switch
                    {
                        InputType.Toggle => enable,
                        InputType.NumericRange when value != null => ConvertNumericValue(value) != 0,
                        InputType.Selection => enable,
                        _ => throw new NotSupportedException($"Input type '{setting.InputType}' not supported for registry operations")
                    };

                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}' with value: {applyValue}");

                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        registryService.ApplySetting(registrySetting, applyValue);
                    }
                }
            }

            if (setting.CommandSettings?.Count > 0)
            {
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Executing {setting.CommandSettings.Count} commands for '{setting.Id}'");

                foreach (var commandSetting in setting.CommandSettings)
                {
                    if (setting.InputType == InputType.Toggle)
                    {
                        var command = enable ? commandSetting.EnabledCommand : commandSetting.DisabledCommand;
                        await commandService.ExecuteCommandAsync(command);
                    }
                    else if (setting.InputType == InputType.Selection && value is int index)
                    {
                        var valueToApply = comboBoxResolver.GetValueFromIndex(setting, index);
                        var command = valueToApply != 0 ? commandSetting.EnabledCommand : commandSetting.DisabledCommand;
                        
                        if (!string.IsNullOrEmpty(command) && command.Contains("{value}"))
                        {
                            command = command.Replace("{value}", valueToApply.ToString());
                        }
                        
                        await commandService.ExecuteCommandAsync(command);
                    }
                    else if (setting.InputType == InputType.NumericRange && value != null)
                    {
                        var numericValue = ConvertNumericValue(value);
                        var command = commandSetting.EnabledCommand.Replace("{value}", numericValue.ToString());
                        await commandService.ExecuteCommandAsync(command);
                    }
                }
            }

            if (setting.PowerCfgSettings?.Count > 0)
            {
                int valueToApply = setting.InputType switch
                {
                    InputType.Toggle => enable ? 1 : 0,
                    InputType.Selection when value is int index => comboBoxResolver.GetValueFromIndex(setting, index),
                    InputType.NumericRange when value != null => ConvertToSystemUnits(ConvertNumericValue(value), setting.PowerCfgSettings[0].Units),
                    _ => throw new NotSupportedException($"Input type '{setting.InputType}' not supported for PowerCfg operations")
                };

                logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying {setting.PowerCfgSettings.Count} PowerCfg settings for '{setting.Id}' with value: {valueToApply}");
                await ExecutePowerCfgSettings(setting.PowerCfgSettings, valueToApply);
            }
        }

        private int ConvertNumericValue(object value)
        {
            return value switch
            {
                int intVal => intVal,
                double doubleVal => (int)doubleVal,
                float floatVal => (int)floatVal,
                string stringVal when int.TryParse(stringVal, out int parsed) => parsed,
                _ => throw new ArgumentException($"Cannot convert '{value}' to numeric value")
            };
        }

        private int ConvertToSystemUnits(int displayValue, string units)
        {
            return units?.ToLowerInvariant() switch
            {
                "minutes" => displayValue * 60,
                "hours" => displayValue * 3600,
                "milliseconds" => displayValue / 1000,
                _ => displayValue
            };
        }

        private async Task ExecutePowerCfgSettings(List<PowerCfgSetting> powerCfgSettings, int valueToApply)
        {
            foreach (var powerCfgSetting in powerCfgSettings)
            {
                if (powerCfgSetting.ApplyToACDC)
                {
                    string targetScheme = powerCfgSetting.TargetPowerPlanGuid ?? "SCHEME_CURRENT";
                    await commandService.ExecuteCommandAsync($"powercfg /setacvalueindex {targetScheme} {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {valueToApply}");
                    await commandService.ExecuteCommandAsync($"powercfg /setdcvalueindex {targetScheme} {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {valueToApply}");
                }
            }
        }


    }
}