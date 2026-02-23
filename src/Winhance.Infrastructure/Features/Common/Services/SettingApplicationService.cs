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
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Common.Constants;
namespace Winhance.Infrastructure.Features.Common.Services
{
    public class SettingApplicationService(
        IDomainServiceRouter domainServiceRouter,
        IWindowsRegistryService registryService,
        IComboBoxResolver comboBoxResolver,
        ILogService logService,
        IGlobalSettingsRegistry globalSettingsRegistry,
        IEventBus eventBus,
        IRecommendedSettingsApplier recommendedSettingsApplier,
        IProcessRestartManager processRestartManager,
        IPowerCfgApplier powerCfgApplier,
        ISettingDependencyResolver dependencyResolver,
        IWindowsCompatibilityFilter compatibilityFilter,
        IScheduledTaskService scheduledTaskService,
        IInteractiveUserService interactiveUserService,
        IProcessExecutor processExecutor,
        IPowerShellRunner powerShellRunner) : ISettingApplicationService
    {

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null, bool checkboxResult = false, string? commandString = null, bool applyRecommended = false, bool skipValuePrerequisites = false)
        {
            var valueDisplay = value is Dictionary<string, object?> dict
                ? $"Dictionary[AC:{dict.GetValueOrDefault("ACValue")}, DC:{dict.GetValueOrDefault("DCValue")}]"
                : value?.ToString() ?? "null";

            logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying setting '{settingId}' - Enable: {enable}, Value: {valueDisplay}");

            var domainService = domainServiceRouter.GetDomainService(settingId);
            var allSettings = await domainService.GetSettingsAsync().ConfigureAwait(false);
            var setting = allSettings.FirstOrDefault(s => s.Id == settingId);

            if (setting == null)
                throw new ArgumentException($"Setting '{settingId}' not found in {domainService.DomainName} settings");

            globalSettingsRegistry.RegisterSetting(domainService.DomainName, setting);

            if (!string.IsNullOrEmpty(commandString))
            {
                await ExecuteActionCommand(domainService, commandString, applyRecommended, settingId).ConfigureAwait(false);
                return;
            }

            if (!skipValuePrerequisites)
            {
                await dependencyResolver.HandleValuePrerequisitesAsync(setting, settingId, allSettings, this).ConfigureAwait(false);
                await dependencyResolver.HandleDependenciesAsync(settingId, allSettings, enable, value, this).ConfigureAwait(false);
            }

            if (await domainService.TryApplySpecialSettingAsync(setting, value!, checkboxResult, this).ConfigureAwait(false))
            {
                await processRestartManager.HandleProcessAndServiceRestartsAsync(setting).ConfigureAwait(false);

                eventBus.Publish(new SettingAppliedEvent(settingId, enable, value));
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied setting '{settingId}' via domain service");

                if (!skipValuePrerequisites)
                {
                    await dependencyResolver.SyncParentToMatchingPresetAsync(setting, settingId, allSettings, this).ConfigureAwait(false);
                }

                return;
            }

            await ApplySettingOperations(setting, enable, value).ConfigureAwait(false);

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

                            await ApplySettingAsync(childSettingId, childValue, skipValuePrerequisites: true).ConfigureAwait(false);
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
                await dependencyResolver.SyncParentToMatchingPresetAsync(setting, settingId, allSettings, this).ConfigureAwait(false);
            }

            eventBus.Publish(new SettingAppliedEvent(settingId, enable, value));
            logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied setting '{settingId}'");
        }

        public Task ApplyRecommendedSettingsForDomainAsync(string settingId) =>
            recommendedSettingsApplier.ApplyRecommendedSettingsForDomainAsync(settingId, this);

        private async Task ExecuteActionCommand(IDomainService domainService, string commandString, bool applyRecommended, string settingId)
        {
            logService.Log(LogLevel.Info, $"[SettingApplicationService] Executing ActionCommand '{commandString}' for setting '{settingId}'");

            var allSettings = await domainService.GetSettingsAsync().ConfigureAwait(false);
            var setting = allSettings.FirstOrDefault(s => s.Id == settingId);

            var method = domainService.GetType().GetMethod(commandString);
            if (method == null)
                throw new NotSupportedException($"Method '{commandString}' not found on service '{domainService.GetType().Name}'");

            if (!typeof(Task).IsAssignableFrom(method.ReturnType))
                throw new NotSupportedException($"Method '{commandString}' must return Task for async execution");

            var result = method.Invoke(domainService, null);
            if (result is Task task)
                await task.ConfigureAwait(false);

            if (applyRecommended)
            {
                logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying recommended settings for domain containing '{settingId}'");
                try
                {
                    await ApplyRecommendedSettingsForDomainAsync(settingId).ConfigureAwait(false);
                    logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied recommended settings for '{settingId}'");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[SettingApplicationService] Failed to apply recommended settings for '{settingId}': {ex.Message}");
                }
            }

            if (setting != null)
            {
                await processRestartManager.HandleProcessAndServiceRestartsAsync(setting).ConfigureAwait(false);
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
                        await scheduledTaskService.EnableTaskAsync(taskSetting.TaskPath).ConfigureAwait(false);
                    else
                        await scheduledTaskService.DisableTaskAsync(taskSetting.TaskPath).ConfigureAwait(false);
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
                        await powerShellRunner.RunScriptAsync(script).ConfigureAwait(false);
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
                            await File.WriteAllTextAsync(tempFile, regContent).ConfigureAwait(false);
                            logService.Log(LogLevel.Debug, $"[SettingApplicationService] Wrote registry content to temp file: {tempFile}");

                            // OTS: run reg import as the interactive user so HKCU
                            // entries land in the standard user's hive, not the admin's.
                            if (interactiveUserService.IsOtsElevation
                                && interactiveUserService.HasInteractiveUserToken)
                            {
                                logService.Log(LogLevel.Debug, "[SettingApplicationService] OTS mode — running reg import as interactive user");
                                var result = await interactiveUserService.RunProcessAsInteractiveUserAsync(
                                    "reg.exe", $"import \"{tempFile}\"").ConfigureAwait(false);

                                if (result.ExitCode != 0)
                                {
                                    logService.Log(LogLevel.Warning, $"[SettingApplicationService] reg import as interactive user failed (exit {result.ExitCode}): {result.StandardError}");
                                }
                            }
                            else
                            {
                                await RunCommandAsync($"reg import \"{tempFile}\"").ConfigureAwait(false);
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
                await powerCfgApplier.ApplyPowerCfgSettingsAsync(setting, enable, value).ConfigureAwait(false);
            }

            await processRestartManager.HandleProcessAndServiceRestartsAsync(setting).ConfigureAwait(false);
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

        private async Task RunCommandAsync(string command)
        {
            try
            {
                var result = await processExecutor.ExecuteAsync("cmd.exe", $"/c {command}").ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    logService.Log(LogLevel.Warning, $"[SettingApplicationService] Command failed: {command} - {result.StandardError}");
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[SettingApplicationService] Command execution failed: {command} - {ex.Message}");
            }
        }

    }
}