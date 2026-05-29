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
    ICompatibleSettingsRegistry settingsRegistry,
    ISpecialSettingHandlerRegistry specialHandlerRegistry,
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
        var applyRecommended = request.ApplyRecommended;
        var skipValuePrerequisites = request.SkipValuePrerequisites;
        var resetToDefault = request.ResetToDefault;

        var valueDisplay = value is Dictionary<string, object?> dict
            ? $"Dictionary[AC:{dict.GetValueOrDefault("ACValue")}, DC:{dict.GetValueOrDefault("DCValue")}]"
            : value?.ToString() ?? "null";

        logService.Log(LogLevel.Info, $"[SettingApplicationService] Applying setting '{settingId}' - Enable: {enable}, Value: {valueDisplay}");

        var setting = settingsRegistry.GetById(settingId);
        if (setting == null)
            throw new ArgumentException($"Setting '{settingId}' not found in registry");

        var featureId = settingsRegistry.GetFeatureIdForSetting(settingId)
            ?? throw new InvalidOperationException($"Setting '{settingId}' has no feature mapping");

        globalSettingsRegistry.RegisterSetting(featureId, setting);

        // allSettings is needed by dependency resolver and preset sync — fetch once,
        // pass through. Only needed when prerequisites aren't being skipped.
        IEnumerable<SettingDefinition> allSettings = skipValuePrerequisites
            ? Enumerable.Empty<SettingDefinition>()
            : settingsRegistry.GetFilteredSettings(featureId);

        if (!skipValuePrerequisites)
        {
            await dependencyResolver.HandleValuePrerequisitesAsync(setting, settingId, allSettings, this).ConfigureAwait(false);
            await dependencyResolver.HandleDependenciesAsync(settingId, allSettings, enable, value, this).ConfigureAwait(false);
        }

        var specialHandler = specialHandlerRegistry.TryGet(settingId);
        if (specialHandler != null
            && await specialHandler.TryApplySpecialSettingAsync(setting, value!, checkboxResult, this).ConfigureAwait(false))
        {
            await processRestartManager.HandleProcessAndServiceRestartsAsync(setting).ConfigureAwait(false);

            eventBus.Publish(new SettingAppliedEvent(settingId, enable, value));
            logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied setting '{settingId}' via special handler");

            if (!skipValuePrerequisites)
            {
                await dependencyResolver.SyncParentToMatchingPresetAsync(setting, settingId, allSettings, this).ConfigureAwait(false);
            }

            return OperationResult.Succeeded();
        }

        OperationResult operationResult;
        if (applyRecommended && setting.InputType == InputType.Action)
        {
            // One coalesced restart for the whole click: suppress the primary action's restart AND the
            // recommended batch, then flush once for primary + recommended combined.
            var toRestart = new List<SettingDefinition>();
            using (processRestartManager.SuppressRestarts())
            {
                operationResult = await operationExecutor
                    .ApplySettingOperationsAsync(setting, enable, value, resetToDefault).ConfigureAwait(false);
                toRestart.Add(setting);

                var recApplied = await recommendedSettingsApplier
                    .ApplyRecommendedForFeatureAsync(settingId, this).ConfigureAwait(false);
                toRestart.AddRange(recApplied);
            }
            await processRestartManager.FlushCoalescedRestartsAsync(toRestart).ConfigureAwait(false);
        }
        else
        {
            operationResult = await operationExecutor
                .ApplySettingOperationsAsync(setting, enable, value, resetToDefault).ConfigureAwait(false);
        }

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

    public Task ApplyRecommendedSettingsForFeatureAsync(string settingId) =>
        recommendedSettingsApplier.ApplyRecommendedSettingsForFeatureAsync(settingId, this);

}
