using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class SettingOperationExecutor(
        IWindowsRegistryService registryService,
        IComboBoxResolver comboBoxResolver,
        IProcessRestartManager processRestartManager,
        IPowerCfgApplier powerCfgApplier,
        IScheduledTaskService scheduledTaskService,
        IInteractiveUserService interactiveUserService,
        IProcessExecutor processExecutor,
        IPowerShellRunner powerShellRunner,
        IFileSystemService fileSystemService,
        ILogService logService) : ISettingOperationExecutor
    {
        public async Task<OperationResult> ApplySettingOperationsAsync(SettingDefinition setting, bool enable, object? value)
        {
            logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Processing operations for '{setting.Id}' - Type: {setting.InputType}");

            if (setting.RegistrySettings?.Count > 0 && setting.RegContents?.Count == 0)
            {
                if (setting.InputType == InputType.Selection && value is Dictionary<string, object> customValues)
                {
                    logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}' with custom state values");

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
                    logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}' with unified mapping for index: {index}");

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
                        InputType.NumericRange when value != null => NumericConversionHelper.ConvertNumericValue(value) != 0,
                        InputType.Selection => enable,
                        _ => throw new NotSupportedException($"Input type '{setting.InputType}' not supported for registry operations")
                    };

                    logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}' with value: {applyValue}");

                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        registryService.ApplySetting(registrySetting, applyValue);
                    }
                }
            }

            if (setting.ScheduledTaskSettings?.Count > 0)
            {
                logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.ScheduledTaskSettings.Count} scheduled task settings for '{setting.Id}'");

                foreach (var taskSetting in setting.ScheduledTaskSettings)
                {
                    if (enable)
                        await scheduledTaskService.EnableTaskAsync(taskSetting.TaskPath).ConfigureAwait(false);
                    else
                        await scheduledTaskService.DisableTaskAsync(taskSetting.TaskPath).ConfigureAwait(false);
                }
            }

            if (setting.PowerShellScripts?.Count > 0)
            {
                logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Executing {setting.PowerShellScripts.Count} PowerShell scripts for '{setting.Id}'");

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
                logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Importing {setting.RegContents.Count} registry contents for '{setting.Id}'");

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
                            tempDir = fileSystemService.CombinePath(userLocalAppData, "Temp");
                            fileSystemService.CreateDirectory(tempDir);
                        }
                        else
                        {
                            tempDir = fileSystemService.GetTempPath();
                        }

                        var tempFile = fileSystemService.CombinePath(tempDir, $"winhance_{Guid.NewGuid()}.reg");
                        try
                        {
                            await fileSystemService.WriteAllTextAsync(tempFile, regContent).ConfigureAwait(false);
                            logService.Log(LogLevel.Debug, $"[SettingOperationExecutor] Wrote registry content to temp file: {tempFile}");

                            // OTS: run reg import as the interactive user so HKCU
                            // entries land in the standard user's hive, not the admin's.
                            if (interactiveUserService.IsOtsElevation
                                && interactiveUserService.HasInteractiveUserToken)
                            {
                                logService.Log(LogLevel.Debug, "[SettingOperationExecutor] OTS mode — running reg import as interactive user");
                                var result = await interactiveUserService.RunProcessAsInteractiveUserAsync(
                                    "reg.exe", $"import \"{tempFile}\"").ConfigureAwait(false);

                                if (result.ExitCode != 0)
                                {
                                    logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] reg import as interactive user failed (exit {result.ExitCode}): {result.StandardError}");
                                }
                            }
                            else
                            {
                                await RunCommandAsync($"reg import \"{tempFile}\"").ConfigureAwait(false);
                            }

                            logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Registry import completed for '{setting.Id}'");
                        }
                        catch (Exception ex)
                        {
                            logService.Log(LogLevel.Error, $"[SettingOperationExecutor] Failed to import registry content for '{setting.Id}': {ex.Message}");
                            throw;
                        }
                        finally
                        {
                            if (fileSystemService.FileExists(tempFile))
                            {
                                fileSystemService.DeleteFile(tempFile);
                            }
                        }
                    }
                }
            }

            if (setting.PowerCfgSettings?.Count > 0)
            {
                await powerCfgApplier.ApplyPowerCfgSettingsAsync(setting, enable, value).ConfigureAwait(false);
            }

            if (setting.NativePowerApiSettings?.Count > 0)
            {
                logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.NativePowerApiSettings.Count} native power API settings for '{setting.Id}'");

                foreach (var apiSetting in setting.NativePowerApiSettings)
                {
                    byte inputValue = enable ? apiSetting.EnabledValue : apiSetting.DisabledValue;
                    var result = PowerProf.CallNtPowerInformation(
                        apiSetting.InformationLevel,
                        ref inputValue,
                        1,
                        IntPtr.Zero,
                        0);

                    if (result == 0)
                        logService.Log(LogLevel.Info, $"[SettingOperationExecutor] CallNtPowerInformation(level={apiSetting.InformationLevel}) succeeded for '{setting.Id}'");
                    else
                        logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] CallNtPowerInformation(level={apiSetting.InformationLevel}) failed with status {result} for '{setting.Id}'");
                }
            }

            await processRestartManager.HandleProcessAndServiceRestartsAsync(setting).ConfigureAwait(false);

            return OperationResult.Succeeded();
        }

        private async Task RunCommandAsync(string command)
        {
            try
            {
                var result = await processExecutor.ExecuteAsync("cmd.exe", $"/c {command}").ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] Command failed: {command} - {result.StandardError}");
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[SettingOperationExecutor] Command execution failed: {command} - {ex.Message}");
            }
        }
    }
}
