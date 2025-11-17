using System;
using System.Collections.Generic;
using System.IO;
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
        IRecommendedSettingsService recommendedSettingsService,
        IWindowsUIManagementService uiManagementService,
        IPowerCfgQueryService powerCfgQueryService,
        IHardwareDetectionService hardwareDetectionService,
        IPowerShellExecutionService powerShellService,
        IWindowsCompatibilityFilter compatibilityFilter) : ISettingApplicationService
    {

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null, bool checkboxResult = false, string? commandString = null, bool applyRecommended = false, bool skipValuePrerequisites = false)
        {
            var valueDisplay = value is Dictionary<string, object?> dict
                ? $"Dictionary[AC:{dict.GetValueOrDefault("ACValue")}, DC:{dict.GetValueOrDefault("DCValue")}]"
                : value?.ToString() ?? "null";

            logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying setting '{settingId}' - Enable: {enable}, Value: {valueDisplay}");

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

            if (!skipValuePrerequisites)
            {
                await HandleValuePrerequisitesAsync(setting, settingId, allSettings);
                await HandleDependencies(settingId, allSettings, enable, value);
            }

            if (await domainService.TryApplySpecialSettingAsync(setting, value, checkboxResult))
            {
                eventBus.Publish(new SettingAppliedEvent(settingId, enable, value));
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied setting '{settingId}' via domain service");

                if (!skipValuePrerequisites)
                {
                    await SyncParentToMatchingPresetAsync(setting, settingId, allSettings);
                }

                return;
            }

            await ApplySettingOperations(setting, enable, value);

            if (setting.CustomProperties?.ContainsKey(CustomPropertyKeys.SettingPresets) == true &&
                setting.InputType == InputType.Selection &&
                value is int selectedIndex)
            {
                var presets = setting.CustomProperties[CustomPropertyKeys.SettingPresets]
                    as Dictionary<int, Dictionary<string, bool>>;

                if (presets?.ContainsKey(selectedIndex) == true)
                {
                    logService.Log(LogLevel.Info,
                        $"[SettingApplicationService] Applying preset for '{settingId}' at index {selectedIndex}");

                    var preset = presets[selectedIndex];
                    foreach (var (childSettingId, childValue) in preset)
                    {
                        try
                        {
                            var childSetting = globalSettingsRegistry.GetSetting(childSettingId);
                            if (childSetting == null)
                            {
                                logService.Log(LogLevel.Debug,
                                    $"[SettingApplicationService] Skipping preset child '{childSettingId}' - not registered (likely OS-filtered)");
                                continue;
                            }

                            if (childSetting is SettingDefinition childSettingDef)
                            {
                                var compatibleSettings = compatibilityFilter.FilterSettingsByWindowsVersion(new[] { childSettingDef });
                                if (!compatibleSettings.Any())
                                {
                                    logService.Log(LogLevel.Info,
                                        $"[SettingApplicationService] Skipping preset child '{childSettingId}' - not compatible with current OS version");
                                    continue;
                                }
                            }

                            await ApplySettingAsync(childSettingId, childValue, skipValuePrerequisites: true);
                            logService.Log(LogLevel.Info,
                                $"[SettingApplicationService] Applied preset setting '{childSettingId}' = {childValue}");
                        }
                        catch (Exception ex)
                        {
                            logService.Log(LogLevel.Warning,
                                $"[SettingApplicationService] Failed to apply preset setting '{childSettingId}': {ex.Message}");
                        }
                    }
                }
            }

            if (!skipValuePrerequisites)
            {
                await SyncParentToMatchingPresetAsync(setting, settingId, allSettings);
            }

            eventBus.Publish(new SettingAppliedEvent(settingId, enable, value));
            logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied setting '{settingId}'");
        }

        private async Task HandleDependencies(string settingId, IEnumerable<SettingDefinition> allSettings, bool enable, object? value)
        {
            if (enable)
            {
                var setting = allSettings.FirstOrDefault(s => s.Id == settingId);
                var directionalDependencies = setting?.Dependencies?
                    .Where(d => d.DependencyType != SettingDependencyType.RequiresValueBeforeAnyChange)
                    .ToList();

                if (directionalDependencies?.Any() == true)
                {
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Handling dependencies for '{settingId}'");
                    var dependencyResult = await dependencyManager.HandleSettingEnabledAsync(settingId, allSettings.Cast<ISettingItem>(), this, discoveryService);
                    if (!dependencyResult)
                        throw new InvalidOperationException($"Cannot enable '{settingId}' due to unsatisfied dependencies");
                }
            }
            else
            {
                var allRegisteredSettings = globalSettingsRegistry.GetAllSettings();
                var hasDependentSettings = allRegisteredSettings.Any(s => s.Dependencies?.Any(d =>
                    d.RequiredSettingId == settingId &&
                    d.DependencyType != SettingDependencyType.RequiresValueBeforeAnyChange) == true);
                if (hasDependentSettings)
                {
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Handling dependent settings for disabled '{settingId}'");
                    await dependencyManager.HandleSettingDisabledAsync(settingId, allRegisteredSettings, this, discoveryService);
                }
            }

            if (enable && value != null)
            {
                var allRegisteredSettings = globalSettingsRegistry.GetAllSettings();
                await dependencyManager.HandleSettingValueChangedAsync(settingId, allRegisteredSettings, this, discoveryService);
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

            if (setting.RegistrySettings?.Count > 0 && setting.RegContents?.Count == 0)
            {
                if (setting.InputType == InputType.Selection && value is Dictionary<string, object> customValues)
                {
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}' with custom state values");

                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        var valueName = registrySetting.ValueName ?? "KeyExists";

                        if (customValues.TryGetValue(valueName, out var specificValue))
                        {
                            if (specificValue == null)
                            {
                                registryService.ApplySetting(registrySetting, false);
                            }
                            else
                            {
                                registryService.ApplySetting(registrySetting, true, specificValue);
                            }
                        }
                    }
                }
                else if (setting.InputType == InputType.Selection && (value is int || (value is string stringValue && !string.IsNullOrEmpty(stringValue))))
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
                        var valueName = registrySetting.ValueName ?? "KeyExists";

                        if (specificValues.TryGetValue(valueName, out var specificValue))
                        {
                            if (specificValue == null)
                            {
                                registryService.ApplySetting(registrySetting, false);
                            }
                            else
                            {
                                registryService.ApplySetting(registrySetting, true, specificValue);
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

            if (setting.PowerShellScripts?.Count > 0)
            {
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Executing {setting.PowerShellScripts.Count} PowerShell scripts for '{setting.Id}'");

                foreach (var scriptSetting in setting.PowerShellScripts)
                {
                    var script = enable ? scriptSetting.EnabledScript : scriptSetting.DisabledScript;

                    if (!string.IsNullOrEmpty(script))
                    {
                        await powerShellService.ExecuteScriptAsync(script);
                    }
                }
            }

            if (setting.RegContents?.Count > 0)
            {
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Importing {setting.RegContents.Count} registry contents for '{setting.Id}'");

                foreach (var regContentSetting in setting.RegContents)
                {
                    var regContent = enable ? regContentSetting.EnabledContent : regContentSetting.DisabledContent;

                    if (!string.IsNullOrEmpty(regContent))
                    {
                        var tempFile = Path.Combine(Path.GetTempPath(), $"winhance_{Guid.NewGuid()}.reg");
                        try
                        {
                            await File.WriteAllTextAsync(tempFile, regContent);
                            logService.Log(LogLevel.Debug, $"[SettingApplicationService] Wrote registry content to temp file: {tempFile}");

                            var result = await commandService.ExecuteCommandAsync($"reg import \"{tempFile}\"");

                            logService.Log(LogLevel.Info, $"[SettingApplicationService] Registry import completed for '{setting.Id}'");
                        }
                        catch (Exception ex)
                        {
                            logService.Log(LogLevel.Error, $"[SettingApplicationService] Failed to import registry content for '{setting.Id}': {ex.Message}");
                            throw;
                        }
                        finally
                        {
                            if (File.Exists(tempFile))
                            {
                                File.Delete(tempFile);
                            }
                        }
                    }
                }
            }

            if (setting.PowerCfgSettings?.Count > 0)
            {
                if (setting.InputType == InputType.Selection &&
                    setting.PowerCfgSettings[0].PowerModeSupport == PowerModeSupport.Separate &&
                    value is ValueTuple<int, int> tupleSeparate)
                {
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying PowerCfg settings for '{setting.Id}' with separate AC/DC Selection tuple");

                    var acPowerCfgValue = comboBoxResolver.GetValueFromIndex(setting, tupleSeparate.Item1);
                    var dcPowerCfgValue = comboBoxResolver.GetValueFromIndex(setting, tupleSeparate.Item2);

                    var convertedDict = new Dictionary<string, object?>
                    {
                        ["ACValue"] = acPowerCfgValue,
                        ["DCValue"] = dcPowerCfgValue
                    };

                    await ExecutePowerCfgSettings(setting.PowerCfgSettings, convertedDict, await hardwareDetectionService.HasBatteryAsync());
                }
                else if (setting.InputType == InputType.Selection &&
                    setting.PowerCfgSettings[0].PowerModeSupport == PowerModeSupport.Separate &&
                    value is Dictionary<string, object?> dict)
                {
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying PowerCfg settings for '{setting.Id}' with separate AC/DC Selection values");

                    var acIndex = ExtractIndexFromValue(dict.TryGetValue("ACValue", out var acVal) ? acVal : 0);
                    var dcIndex = ExtractIndexFromValue(dict.TryGetValue("DCValue", out var dcVal) ? dcVal : 0);

                    var acPowerCfgValue = comboBoxResolver.GetValueFromIndex(setting, acIndex);
                    var dcPowerCfgValue = comboBoxResolver.GetValueFromIndex(setting, dcIndex);

                    var convertedDict = new Dictionary<string, object?>
                    {
                        ["ACValue"] = acPowerCfgValue,
                        ["DCValue"] = dcPowerCfgValue
                    };

                    await ExecutePowerCfgSettings(setting.PowerCfgSettings, convertedDict, await hardwareDetectionService.HasBatteryAsync());
                }
                else if (setting.InputType == InputType.NumericRange &&
                         setting.PowerCfgSettings[0].PowerModeSupport == PowerModeSupport.Separate &&
                         value is Dictionary<string, object?> numericDict)
                {
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying PowerCfg settings for '{setting.Id}' with separate AC/DC NumericRange values");

                    var acValue = numericDict.TryGetValue("ACValue", out var ac) ? ExtractSingleValue(ac) : 0;
                    var dcValue = numericDict.TryGetValue("DCValue", out var dc) ? ExtractSingleValue(dc) : 0;

                    var acSystemValue = ConvertToSystemUnits(acValue, setting.PowerCfgSettings[0].Units);
                    var dcSystemValue = ConvertToSystemUnits(dcValue, setting.PowerCfgSettings[0].Units);

                    var convertedDict = new Dictionary<string, object?>
                    {
                        ["ACValue"] = acSystemValue,
                        ["DCValue"] = dcSystemValue
                    };

                    await ExecutePowerCfgSettings(setting.PowerCfgSettings, convertedDict, await hardwareDetectionService.HasBatteryAsync());
                }
                else
                {
                    if (setting.InputType == InputType.NumericRange && value == null)
                    {
                        logService.Log(LogLevel.Debug, $"[SettingApplicationService] Skipping PowerCfg setting '{setting.Id}' - no value provided (old config format)");
                        return;
                    }

                    int valueToApply = setting.InputType switch
                    {
                        InputType.Toggle => enable ? 1 : 0,
                        InputType.Selection when value is int index => comboBoxResolver.GetValueFromIndex(setting, index),
                        InputType.NumericRange when value != null => ConvertToSystemUnits(ConvertNumericValue(value), setting.PowerCfgSettings[0].Units),
                        _ => throw new NotSupportedException($"Input type '{setting.InputType}' not supported for PowerCfg operations")
                    };

                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying {setting.PowerCfgSettings.Count} PowerCfg settings for '{setting.Id}' with value: {valueToApply}");
                    await ExecutePowerCfgSettings(setting.PowerCfgSettings, valueToApply, await hardwareDetectionService.HasBatteryAsync());
                }
            }

            if (!string.IsNullOrEmpty(setting.RestartProcess))
            {
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Restarting process '{setting.RestartProcess}' for setting '{setting.Id}'");
                try
                {
                    uiManagementService.KillProcess(setting.RestartProcess);
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[SettingApplicationService] Failed to restart process '{setting.RestartProcess}': {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(setting.RestartService))
            {
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Restarting service '{setting.RestartService}' for setting '{setting.Id}'");
                try
                {
                    var script = setting.RestartService.Contains("*")
                        ? $"Get-Service -Name '{setting.RestartService}' | Restart-Service -Force -ErrorAction SilentlyContinue"
                        : $"Restart-Service -Name '{setting.RestartService}' -Force -ErrorAction SilentlyContinue";

                    await powerShellService.ExecuteScriptAsync(script);
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[SettingApplicationService] Failed to restart service '{setting.RestartService}': {ex.Message}");
                }
            }
        }

        private int ConvertNumericValue(object value)
        {
            return value switch
            {
                int intVal => intVal,
                long longVal => (int)longVal,
                double doubleVal => (int)doubleVal,
                float floatVal => (int)floatVal,
                string stringVal when int.TryParse(stringVal, out int parsed) => parsed,
                _ => throw new ArgumentException($"Cannot convert '{value}' (type: {value?.GetType().Name ?? "null"}) to numeric value")
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

        private async Task ExecutePowerCfgSettings(List<PowerCfgSetting> powerCfgSettings, object valueToApply, bool hasBattery = true)
        {
            var commands = new List<string>();

            foreach (var powerCfgSetting in powerCfgSettings)
            {
                switch (powerCfgSetting.PowerModeSupport)
                {
                    case PowerModeSupport.Both:
                        var singleValue = ExtractSingleValue(valueToApply);
                        commands.Add($"powercfg /setacvalueindex SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {singleValue}");

                        if (hasBattery)
                        {
                            commands.Add($"powercfg /setdcvalueindex SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {singleValue}");
                        }
                        break;

                    case PowerModeSupport.Separate:
                        var (acValue, dcValue) = ExtractACDCValues(valueToApply);
                        commands.Add($"powercfg /setacvalueindex SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {acValue}");

                        if (hasBattery)
                        {
                            commands.Add($"powercfg /setdcvalueindex SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {dcValue}");
                        }
                        break;

                    case PowerModeSupport.ACOnly:
                        var acOnlyValue = ExtractSingleValue(valueToApply);
                        commands.Add($"powercfg /setacvalueindex SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {acOnlyValue}");
                        break;

                    case PowerModeSupport.DCOnly:
                        if (hasBattery)
                        {
                            var dcOnlyValue = ExtractSingleValue(valueToApply);
                            commands.Add($"powercfg /setdcvalueindex SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {dcOnlyValue}");
                        }
                        break;
                }
            }

            commands.Add("powercfg /setactive SCHEME_CURRENT");

            var batchScript = string.Join(" && ", commands);
            await commandService.ExecuteCommandAsync(batchScript);

            logService.Log(LogLevel.Info, $"[SettingApplicationService] Executed {commands.Count} powercfg commands in batch");
        }

        private int ExtractSingleValue(object value)
        {
            return value switch
            {
                int intVal => intVal,
                long longVal => (int)longVal,
                double doubleVal => (int)doubleVal,
                float floatVal => (int)floatVal,
                string stringVal when int.TryParse(stringVal, out int parsed) => parsed,
                ValueTuple<int, int> tuple => tuple.Item1,
                _ => throw new ArgumentException($"Cannot convert '{value}' (type: {value?.GetType().Name ?? "null"}) to single numeric value")
            };
        }

        private (int acValue, int dcValue) ExtractACDCValues(object value)
        {
            if (value is ValueTuple<object, object> tuple)
            {
                return (ExtractSingleValue(tuple.Item1), ExtractSingleValue(tuple.Item2));
            }

            if (value is Dictionary<string, object?> dict)
            {
                var acValue = dict.TryGetValue("ACValue", out var ac) ? ExtractSingleValue(ac) : 0;
                var dcValue = dict.TryGetValue("DCValue", out var dc) ? ExtractSingleValue(dc) : 0;
                return (acValue, dcValue);
            }

            var singleValue = ExtractSingleValue(value);
            return (singleValue, singleValue);
        }

        private int ExtractIndexFromValue(object? value)
        {
            if (value == null) return 0;

            if (value.GetType().Name == "ComboBoxOption")
            {
                var valueProp = value.GetType().GetProperty("Value");
                if (valueProp != null)
                {
                    var innerValue = valueProp.GetValue(value);
                    if (innerValue is int intVal)
                        return intVal;
                }
            }

            if (value is int directInt)
                return directInt;

            if (int.TryParse(value.ToString(), out int parsed))
                return parsed;

            return 0;
        }

        private async Task HandleValuePrerequisitesAsync(
            SettingDefinition setting,
            string settingId,
            IEnumerable<SettingDefinition> allSettings)
        {
            if (setting.Dependencies?.Any() != true)
            {
                return;
            }

            var valuePrerequisites = setting.Dependencies
                .Where(d => d.DependencyType == SettingDependencyType.RequiresValueBeforeAnyChange)
                .ToList();

            if (!valuePrerequisites.Any())
            {
                return;
            }

            foreach (var dependency in valuePrerequisites)
            {
                logService.Log(LogLevel.Info,
                    $"[ValuePrereq] Processing: '{settingId}' requires '{dependency.RequiredSettingId}' = '{dependency.RequiredValue}'");

                var requiredSetting = allSettings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);

                if (requiredSetting == null)
                {
                    requiredSetting = globalSettingsRegistry.GetSetting(dependency.RequiredSettingId) as SettingDefinition;
                }

                if (requiredSetting == null)
                {
                    logService.Log(LogLevel.Warning,
                        $"[ValuePrereq] Required setting '{dependency.RequiredSettingId}' not found in current module or global registry");
                    continue;
                }

                var states = await discoveryService.GetSettingStatesAsync(new[] { requiredSetting });
                if (!states.TryGetValue(dependency.RequiredSettingId, out var currentState) || !currentState.Success)
                {
                    logService.Log(LogLevel.Warning,
                        $"[ValuePrereq] Could not get current state of '{dependency.RequiredSettingId}'");
                    continue;
                }

                bool requirementMet = DoesCurrentValueMatchRequirement(
                    requiredSetting,
                    currentState,
                    dependency.RequiredValue);

                if (!requirementMet)
                {
                    logService.Log(LogLevel.Info,
                        $"[ValuePrereq] Auto-fixing '{dependency.RequiredSettingId}' to '{dependency.RequiredValue}' before applying '{settingId}'");

                    var valueToApply = GetValueToApplyForRequirement(requiredSetting, dependency.RequiredValue);

                    await ApplySettingAsync(
                        dependency.RequiredSettingId,
                        enable: true,
                        value: valueToApply,
                        skipValuePrerequisites: true);

                    logService.Log(LogLevel.Info,
                        $"[ValuePrereq] Successfully auto-fixed '{dependency.RequiredSettingId}', proceeding with '{settingId}'");
                }
            }
        }

        private bool DoesCurrentValueMatchRequirement(
            SettingDefinition setting,
            SettingStateResult currentState,
            string? requiredValue)
        {
            if (string.IsNullOrEmpty(requiredValue))
            {
                return true;
            }

            if (setting.InputType == InputType.Selection &&
                setting.CustomProperties?.TryGetValue(CustomPropertyKeys.ComboBoxDisplayNames, out var namesObj) == true &&
                namesObj is string[] displayNames)
            {
                int requiredIndex = -1;
                for (int i = 0; i < displayNames.Length; i++)
                {
                    if (displayNames[i].Equals(requiredValue, StringComparison.OrdinalIgnoreCase))
                    {
                        requiredIndex = i;
                        break;
                    }
                }

                if (requiredIndex >= 0 && currentState.CurrentValue is int currentIndex)
                {
                    return currentIndex == requiredIndex;
                }
            }

            if (setting.InputType == InputType.Toggle)
            {
                bool requiredBool = requiredValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                   requiredValue.Equals("enabled", StringComparison.OrdinalIgnoreCase);
                bool currentBool = currentState.IsEnabled;
                return currentBool == requiredBool;
            }

            return false;
        }

        private object? GetValueToApplyForRequirement(SettingDefinition setting, string? requiredValue)
        {
            if (string.IsNullOrEmpty(requiredValue))
            {
                return null;
            }

            if (setting.InputType == InputType.Selection &&
                setting.CustomProperties?.TryGetValue(CustomPropertyKeys.ComboBoxDisplayNames, out var namesObj) == true &&
                namesObj is string[] displayNames)
            {
                for (int i = 0; i < displayNames.Length; i++)
                {
                    if (displayNames[i].Equals(requiredValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }

                logService.Log(LogLevel.Warning,
                    $"[ValuePrereq] Could not find ComboBox option matching '{requiredValue}'");
                return null;
            }

            if (setting.InputType == InputType.Toggle)
            {
                return requiredValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                       requiredValue.Equals("enabled", StringComparison.OrdinalIgnoreCase);
            }

            return null;
        }

        private async Task SyncParentToMatchingPresetAsync(
            SettingDefinition setting,
            string settingId,
            IEnumerable<SettingDefinition> allSettings)
        {
            var prerequisite = setting.Dependencies?
                .FirstOrDefault(d => d.DependencyType == SettingDependencyType.RequiresValueBeforeAnyChange);

            if (prerequisite == null)
            {
                return;
            }

            var parentSetting = allSettings.FirstOrDefault(s => s.Id == prerequisite.RequiredSettingId);
            if (parentSetting?.CustomProperties?.ContainsKey(CustomPropertyKeys.SettingPresets) != true)
            {
                return;
            }

            var presets = parentSetting.CustomProperties[CustomPropertyKeys.SettingPresets]
                as Dictionary<int, Dictionary<string, bool>>;

            if (presets == null || presets.Count == 0)
            {
                return;
            }

            logService.Log(LogLevel.Info,
                $"[PostChange] Checking if child settings now match a preset for parent '{prerequisite.RequiredSettingId}'");

            foreach (var (presetIndex, presetChildren) in presets)
            {
                var allMatch = await DoAllChildrenMatchPreset(presetChildren, allSettings);

                if (allMatch)
                {
                    logService.Log(LogLevel.Info,
                        $"[PostChange] All children match preset at index {presetIndex}, syncing parent '{prerequisite.RequiredSettingId}'");

                    await ApplySettingAsync(
                        prerequisite.RequiredSettingId,
                        enable: true,
                        value: presetIndex,
                        skipValuePrerequisites: true);

                    return;
                }
            }

            logService.Log(LogLevel.Debug,
                $"[PostChange] No preset match found for parent '{prerequisite.RequiredSettingId}', leaving at current value");
        }

        private async Task<bool> DoAllChildrenMatchPreset(
            Dictionary<string, bool> preset,
            IEnumerable<SettingDefinition> allSettings)
        {
            var compatiblePresetEntries = new Dictionary<string, bool>();

            foreach (var (childId, expectedValue) in preset)
            {
                var childSetting = globalSettingsRegistry.GetSetting(childId);
                if (childSetting == null)
                {
                    logService.Log(LogLevel.Debug,
                        $"[PostChange] Skipping preset child '{childId}' from matching - not registered (likely OS-filtered)");
                    continue;
                }

                if (childSetting is SettingDefinition childSettingDef)
                {
                    var compatibleSettings = compatibilityFilter.FilterSettingsByWindowsVersion(new[] { childSettingDef });
                    if (!compatibleSettings.Any())
                    {
                        logService.Log(LogLevel.Debug,
                            $"[PostChange] Skipping preset child '{childId}' from matching - not compatible with current OS version");
                        continue;
                    }
                }

                compatiblePresetEntries[childId] = expectedValue;
            }

            var childSettingDefinitions = allSettings
                .Where(s => compatiblePresetEntries.ContainsKey(s.Id))
                .ToList();

            if (childSettingDefinitions.Count != compatiblePresetEntries.Count)
            {
                logService.Log(LogLevel.Info,
                    $"[PostChange] Child count mismatch - Expected: {compatiblePresetEntries.Count}, Found in allSettings: {childSettingDefinitions.Count}");
                logService.Log(LogLevel.Info,
                    $"[PostChange] This is likely because child settings span multiple domains. Fetching from global registry instead.");

                childSettingDefinitions.Clear();
                foreach (var childId in compatiblePresetEntries.Keys)
                {
                    var childSetting = globalSettingsRegistry.GetSetting(childId) as SettingDefinition;
                    if (childSetting != null)
                    {
                        childSettingDefinitions.Add(childSetting);
                    }
                }

                if (childSettingDefinitions.Count != compatiblePresetEntries.Count)
                {
                    logService.Log(LogLevel.Warning,
                        $"[PostChange] Still mismatched after global registry lookup - Expected: {compatiblePresetEntries.Count}, Found: {childSettingDefinitions.Count}");
                    return false;
                }
            }

            var states = await discoveryService.GetSettingStatesAsync(childSettingDefinitions);

            foreach (var (childId, expectedValue) in compatiblePresetEntries)
            {
                if (!states.TryGetValue(childId, out var state) || !state.Success)
                {
                    logService.Log(LogLevel.Debug,
                        $"[PostChange] Could not get state for child '{childId}'");
                    return false;
                }

                if (state.IsEnabled != expectedValue)
                {
                    logService.Log(LogLevel.Info,
                        $"[PostChange] Child '{childId}' mismatch - Expected: {expectedValue}, Actual: {state.IsEnabled}");
                    return false;
                }

                logService.Log(LogLevel.Debug,
                    $"[PostChange] Child '{childId}' matches - Value: {state.IsEnabled}");
            }

            return true;
        }
    }
}