using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.Core.Features.Common.Constants;
using Winhance.Infrastructure.Features.Common.Utilities;
using System.ServiceProcess;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class SettingApplicationService(
        IDomainServiceRouter domainServiceRouter,
        IWindowsRegistryService registryService,
        IComboBoxResolver comboBoxResolver,
        ILogService logService,
        IDependencyManager dependencyManager,
        IGlobalSettingsRegistry globalSettingsRegistry,
        IEventBus eventBus,
        ISystemSettingsDiscoveryService discoveryService,
        IRecommendedSettingsService recommendedSettingsService,
        IWindowsUIManagementService uiManagementService,
        IPowerSettingsQueryService powerSettingsQueryService,
        IHardwareDetectionService hardwareDetectionService,
        IWindowsCompatibilityFilter compatibilityFilter,
        IScheduledTaskService scheduledTaskService,
        IInteractiveUserService interactiveUserService) : ISettingApplicationService
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
                await HandleProcessAndServiceRestarts(setting);

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

        public async Task ApplyRecommendedSettingsForDomainAsync(string settingId)
        {
            try
            {
                var domainService = domainServiceRouter.GetDomainService(settingId);
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Starting to apply recommended settings for domain '{domainService.DomainName}'");

                var recommendedSettings = await recommendedSettingsService.GetRecommendedSettingsAsync(settingId);
                var settingsList = recommendedSettings.ToList();

                logService.Log(LogLevel.Info, $"[SettingApplicationService] Found {settingsList.Count} recommended settings for domain '{domainService.DomainName}'");

                if (settingsList.Count == 0)
                {
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] No recommended settings found for domain '{domainService.DomainName}'");
                    return;
                }

                foreach (var setting in settingsList)
                {
                    try
                    {
                        var recommendedValue = RecommendedSettingsService.GetRecommendedValueForSetting(setting);
                        logService.Log(LogLevel.Debug, $"[SettingApplicationService] Applying recommended setting '{setting.Id}' with value '{recommendedValue}'");

                        if (setting.InputType == InputType.Toggle)
                        {
                            var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.RecommendedValue != null);
                            bool enableValue = false;

                            if (registrySetting != null && recommendedValue != null)
                            {
                                enableValue = recommendedValue.Equals(registrySetting.EnabledValue);
                            }

                            await ApplySettingAsync(setting.Id, enableValue, recommendedValue, skipValuePrerequisites: true);
                        }
                        else if (setting.InputType == InputType.Selection)
                        {
                            var recommendedOption = RecommendedSettingsService.GetRecommendedOptionFromSetting(setting);

                            if (recommendedOption != null)
                            {
                                var registryValue = RecommendedSettingsService.GetRegistryValueFromOptionName(setting, recommendedOption);
                                var comboBoxIndex = RecommendedSettingsService.GetCorrectSelectionIndex(setting, recommendedOption, registryValue);
                                await ApplySettingAsync(setting.Id, true, comboBoxIndex, skipValuePrerequisites: true);
                            }
                            else
                            {
                                await ApplySettingAsync(setting.Id, true, recommendedValue, skipValuePrerequisites: true);
                            }
                        }
                        else
                        {
                            await ApplySettingAsync(setting.Id, true, recommendedValue, skipValuePrerequisites: true);
                        }

                        logService.Log(LogLevel.Debug, $"[SettingApplicationService] Successfully applied recommended setting '{setting.Id}'");
                    }
                    catch (Exception ex)
                    {
                        logService.Log(LogLevel.Warning, $"[SettingApplicationService] Failed to apply recommended setting '{setting.Id}': {ex.Message}");
                    }
                }

                logService.Log(LogLevel.Info, $"[SettingApplicationService] Completed applying recommended settings for domain '{domainService.DomainName}'");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[SettingApplicationService] Error applying recommended settings: {ex.Message}");
                throw;
            }
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

                // Auto-enable associated settings when this setting is enabled
                if (setting?.AutoEnableSettingIds?.Count > 0)
                {
                    foreach (var autoEnableId in setting.AutoEnableSettingIds)
                    {
                        try
                        {
                            var autoEnableDef = allSettings.FirstOrDefault(s => s.Id == autoEnableId)
                                ?? globalSettingsRegistry.GetSetting(autoEnableId) as SettingDefinition;
                            if (autoEnableDef != null)
                            {
                                var states = await discoveryService.GetSettingStatesAsync(new[] { autoEnableDef });
                                if (states.TryGetValue(autoEnableId, out var st) && st.Success && !st.IsEnabled)
                                {
                                    logService.Log(LogLevel.Info,
                                        $"[SettingApplicationService] Auto-enabling '{autoEnableId}' because '{settingId}' was enabled");
                                    await ApplySettingAsync(autoEnableId, true, skipValuePrerequisites: true);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logService.Log(LogLevel.Warning,
                                $"[SettingApplicationService] Failed to auto-enable '{autoEnableId}': {ex.Message}");
                        }
                    }
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

            var allSettings = await domainService.GetSettingsAsync();
            var setting = allSettings.FirstOrDefault(s => s.Id == settingId);

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
                    await ApplyRecommendedSettingsForDomainAsync(settingId);
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied recommended settings for '{settingId}'");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[SettingApplicationService] Failed to apply recommended settings for '{settingId}': {ex.Message}");
                }
            }

            if (setting != null)
            {
                await HandleProcessAndServiceRestarts(setting);
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

            if (setting.ScheduledTaskSettings?.Count > 0)
            {
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying {setting.ScheduledTaskSettings.Count} scheduled task settings for '{setting.Id}'");

                foreach (var taskSetting in setting.ScheduledTaskSettings)
                {
                    if (enable)
                        await scheduledTaskService.EnableTaskAsync(taskSetting.TaskPath);
                    else
                        await scheduledTaskService.DisableTaskAsync(taskSetting.TaskPath);
                }
            }

            if (setting.Id == "power-hibernation-enable")
            {
                byte hibValue = enable ? (byte)1 : (byte)0;
                var result = PowerProf.CallNtPowerInformation(
                    PowerProf.SystemReserveHiberFile,
                    ref hibValue, 1, IntPtr.Zero, 0);

                if (result == 0)
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Hibernation {(enable ? "enabled" : "disabled")} via CallNtPowerInformation");
                else
                    logService.Log(LogLevel.Warning, $"[SettingApplicationService] CallNtPowerInformation failed with status {result}");
            }

            if (setting.PowerShellScripts?.Count > 0)
            {
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Executing {setting.PowerShellScripts.Count} PowerShell scripts for '{setting.Id}'");

                foreach (var scriptSetting in setting.PowerShellScripts)
                {
                    var script = enable ? scriptSetting.EnabledScript : scriptSetting.DisabledScript;

                    if (!string.IsNullOrEmpty(script))
                    {
                        await PowerShellRunner.RunScriptAsync(script);
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
                        // OTS: write temp file to the interactive user's temp folder
                        // so reg.exe running as that user can access it.
                        string tempDir;
                        if (interactiveUserService.IsOtsElevation)
                        {
                            var userLocalAppData = interactiveUserService.GetInteractiveUserFolderPath(
                                Environment.SpecialFolder.LocalApplicationData);
                            tempDir = Path.Combine(userLocalAppData, "Temp");
                            Directory.CreateDirectory(tempDir);
                        }
                        else
                        {
                            tempDir = Path.GetTempPath();
                        }

                        var tempFile = Path.Combine(tempDir, $"winhance_{Guid.NewGuid()}.reg");
                        try
                        {
                            await File.WriteAllTextAsync(tempFile, regContent);
                            logService.Log(LogLevel.Debug, $"[SettingApplicationService] Wrote registry content to temp file: {tempFile}");

                            // OTS: run reg import as the interactive user so HKCU
                            // entries land in the standard user's hive, not the admin's.
                            if (interactiveUserService.IsOtsElevation
                                && interactiveUserService.HasInteractiveUserToken)
                            {
                                logService.Log(LogLevel.Debug, "[SettingApplicationService] OTS mode — running reg import as interactive user");
                                var result = await interactiveUserService.RunProcessAsInteractiveUserAsync(
                                    "reg.exe", $"import \"{tempFile}\"");

                                if (result.ExitCode != 0)
                                {
                                    logService.Log(LogLevel.Warning, $"[SettingApplicationService] reg import as interactive user failed (exit {result.ExitCode}): {result.StandardError}");
                                }
                            }
                            else
                            {
                                await RunCommandAsync($"reg import \"{tempFile}\"");
                            }

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
                        InputType.NumericRange when value != null => ConvertToSystemUnits(ConvertNumericValue(value), GetDisplayUnits(setting)),
                        _ => throw new NotSupportedException($"Input type '{setting.InputType}' not supported for PowerCfg operations")
                    };

                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying {setting.PowerCfgSettings.Count} PowerCfg settings for '{setting.Id}' with value: {valueToApply}");
                    await ExecutePowerCfgSettings(setting.PowerCfgSettings, valueToApply, await hardwareDetectionService.HasBatteryAsync());
                }
            }

            await HandleProcessAndServiceRestarts(setting);
        }

        private async Task HandleProcessAndServiceRestarts(SettingDefinition setting)
        {
            if (!string.IsNullOrEmpty(setting.RestartProcess))
            {
                if (uiManagementService.IsConfigImportMode)
                {
                    logService.Log(LogLevel.Debug, $"[SettingApplicationService] Skipping process restart for '{setting.RestartProcess}' (config import mode - will restart at end)");
                }
                else
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
            }

            if (!string.IsNullOrEmpty(setting.RestartService))
            {
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Restarting service '{setting.RestartService}' for setting '{setting.Id}'");
                try
                {
                    if (setting.RestartService.Contains("*"))
                    {
                        // Wildcard service names require enumeration
                        var pattern = setting.RestartService.Replace("*", "");
                        var allServices = ServiceController.GetServices();
                        var matchingServices = allServices.Where(s =>
                            s.ServiceName.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();

                        foreach (var svc in matchingServices)
                        {
                            using (svc)
                            {
                                try
                                {
                                    if (svc.Status == ServiceControllerStatus.Running)
                                    {
                                        svc.Stop();
                                        svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                                        svc.Start();
                                    }
                                }
                                catch (Exception svcEx)
                                {
                                    logService.Log(LogLevel.Warning, $"[SettingApplicationService] Failed to restart service '{svc.ServiceName}': {svcEx.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        using var sc = new ServiceController(setting.RestartService);
                        if (sc.Status == ServiceControllerStatus.Running)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                            sc.Start();
                        }
                    }
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
                System.Text.Json.JsonElement je when je.TryGetInt32(out int jsonInt) => jsonInt,
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

        private string GetDisplayUnits(SettingDefinition setting)
        {
            if (setting.CustomProperties?.TryGetValue("Units", out var units) == true && units is string unitsStr)
                return unitsStr;
            return setting.PowerCfgSettings?[0]?.Units ?? string.Empty;
        }

        private async Task ExecutePowerCfgSettings(List<PowerCfgSetting> powerCfgSettings, object valueToApply, bool hasBattery = true)
        {
            // Get active scheme GUID
            var activeSchemeResult = PowerProf.PowerGetActiveScheme(IntPtr.Zero, out var activeSchemePtr);
            if (activeSchemeResult != PowerProf.ERROR_SUCCESS)
            {
                logService.Log(LogLevel.Error, "[SettingApplicationService] Failed to get active power scheme");
                return;
            }

            var activeSchemeGuid = System.Runtime.InteropServices.Marshal.PtrToStructure<Guid>(activeSchemePtr);
            PowerProf.LocalFree(activeSchemePtr);

            int changeCount = 0;

            foreach (var powerCfgSetting in powerCfgSettings)
            {
                var (currentAc, currentDc) = await powerSettingsQueryService.GetPowerSettingACDCValuesAsync(powerCfgSetting);
                var subgroupGuid = Guid.Parse(powerCfgSetting.SubgroupGuid);
                var settingGuid = Guid.Parse(powerCfgSetting.SettingGuid);

                switch (powerCfgSetting.PowerModeSupport)
                {
                    case PowerModeSupport.Both:
                        var singleValue = ExtractSingleValue(valueToApply);

                        if (currentAc != singleValue)
                        {
                            PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)singleValue);
                            changeCount++;
                        }

                        if (hasBattery && currentDc != singleValue)
                        {
                            PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)singleValue);
                            changeCount++;
                        }
                        break;

                    case PowerModeSupport.Separate:
                        var (acValue, dcValue) = ExtractACDCValues(valueToApply);

                        if (currentAc != acValue)
                        {
                            PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)acValue);
                            changeCount++;
                        }

                        if (hasBattery && currentDc != dcValue)
                        {
                            PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)dcValue);
                            changeCount++;
                        }
                        break;

                    case PowerModeSupport.ACOnly:
                        var acOnlyValue = ExtractSingleValue(valueToApply);
                        if (currentAc != acOnlyValue)
                        {
                            PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)acOnlyValue);
                            changeCount++;
                        }
                        break;

                    case PowerModeSupport.DCOnly:
                        if (hasBattery)
                        {
                            var dcOnlyValue = ExtractSingleValue(valueToApply);
                            if (currentDc != dcOnlyValue)
                            {
                                PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref activeSchemeGuid, ref subgroupGuid, ref settingGuid, (uint)dcOnlyValue);
                                changeCount++;
                            }
                        }
                        break;
                }
            }

            if (changeCount == 0)
            {
                logService.Log(LogLevel.Info, "[SettingApplicationService] No powercfg changes needed (values already match)");
                return;
            }

            // Activate the scheme to apply changes
            PowerProf.PowerSetActiveScheme(IntPtr.Zero, ref activeSchemeGuid);

            logService.Log(LogLevel.Info, $"[SettingApplicationService] Applied {changeCount} powercfg changes via P/Invoke");
        }

        private async Task RunCommandAsync(string command)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(startInfo);
                if (process == null) return;

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    logService.Log(LogLevel.Warning, $"[SettingApplicationService] Command failed: {command} - {error}");
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[SettingApplicationService] Command execution failed: {command} - {ex.Message}");
            }
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
                System.Text.Json.JsonElement je when je.TryGetInt32(out int jsonInt) => jsonInt,
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

            if (value is System.Text.Json.JsonElement je)
                return je.TryGetInt32(out int jsonInt) ? jsonInt : 0;

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