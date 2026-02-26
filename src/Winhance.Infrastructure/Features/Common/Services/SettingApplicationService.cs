using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;


namespace Winhance.Infrastructure.Features.Common.Services;

public class SettingApplicationService(
    IDomainServiceRouter domainServiceRouter,
    ILogService logService,
    IGlobalSettingsRegistry globalSettingsRegistry,
    IEventBus eventBus,
    IRecommendedSettingsApplier recommendedSettingsApplier,
    IProcessRestartManager processRestartManager,
    ISettingDependencyResolver dependencyResolver,
    IWindowsCompatibilityFilter compatibilityFilter,
    ISettingOperationExecutor operationExecutor) : ISettingApplicationService
{

    public async Task<OperationResult> ApplySettingAsync(ApplySettingRequest request)
    {
        var settingId = request.SettingId;
        var enable = request.Enable;
        var value = request.Value;
        var checkboxResult = request.CheckboxResult;
        var commandString = request.CommandString;
        var applyRecommended = request.ApplyRecommended;
        var skipValuePrerequisites = request.SkipValuePrerequisites;

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
            return OperationResult.Succeeded();
        }

        if (!skipValuePrerequisites)
        {
            await dependencyResolver.HandleValuePrerequisitesAsync(setting, settingId, allSettings, this).ConfigureAwait(false);
            await dependencyResolver.HandleDependenciesAsync(settingId, allSettings, enable, value, this).ConfigureAwait(false);
        }

        if (domainService is ISpecialSettingHandler specialHandler
            && await specialHandler.TryApplySpecialSettingAsync(setting, value!, checkboxResult, this).ConfigureAwait(false))
        {
            await processRestartManager.HandleProcessAndServiceRestartsAsync(setting).ConfigureAwait(false);

            eventBus.Publish(new SettingAppliedEvent(settingId, enable, value));
            logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied setting '{settingId}' via domain service");

            if (!skipValuePrerequisites)
            {
                await dependencyResolver.SyncParentToMatchingPresetAsync(setting, settingId, allSettings, this).ConfigureAwait(false);
            }

            return OperationResult.Succeeded();
        }

        var operationResult = await operationExecutor.ApplySettingOperationsAsync(setting, enable, value).ConfigureAwait(false);

        if (setting.SettingPresets != null &&
            setting.InputType == InputType.Selection &&
            value is int selectedIndex)
        {
            var presets = setting.SettingPresets;

            if (presets.ContainsKey(selectedIndex))
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

                        await ApplySettingAsync(new ApplySettingRequest { SettingId = childSettingId, Enable = childValue, SkipValuePrerequisites = true }).ConfigureAwait(false);
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

        // Always publish the event — even on partial failure, some operations may
        // have succeeded and listeners need to re-read actual system state.
        eventBus.Publish(new SettingAppliedEvent(settingId, enable, value));

        if (!operationResult.Success)
        {
            logService.Log(LogLevel.Warning, $"[SettingApplicationService] Setting '{settingId}' partially failed: {operationResult.ErrorMessage}");
            return operationResult;
        }

        logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied setting '{settingId}'");
        return OperationResult.Succeeded();
    }

    public Task ApplyRecommendedSettingsForDomainAsync(string settingId) =>
        recommendedSettingsApplier.ApplyRecommendedSettingsForDomainAsync(settingId, this);

    private async Task ExecuteActionCommand(IDomainService domainService, string commandString, bool applyRecommended, string settingId)
    {
        logService.Log(LogLevel.Info, $"[SettingApplicationService] Executing ActionCommand '{commandString}' for setting '{settingId}'");

        var allSettings = await domainService.GetSettingsAsync().ConfigureAwait(false);
        var setting = allSettings.FirstOrDefault(s => s.Id == settingId);

        if (domainService is IActionCommandProvider commandProvider)
        {
            if (!commandProvider.SupportedCommands.Contains(commandString))
                throw new NotSupportedException($"Command '{commandString}' not supported by '{domainService.GetType().Name}'");
            await commandProvider.ExecuteCommandAsync(commandString).ConfigureAwait(false);
        }
        else
        {
            throw new NotSupportedException($"Service '{domainService.GetType().Name}' does not support action commands");
        }

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

}
