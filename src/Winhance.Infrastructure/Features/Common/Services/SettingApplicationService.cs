using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
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
    ISettingOperationExecutor operationExecutor,
    IChangeHistoryService changeHistory,
    ISystemSettingsDiscoveryService discoveryService,
    ILocalizationService localizationService) : ISettingApplicationService
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

        // Change-history receipt: capture the pre-apply state so the entry can say "before → after".
        // Captured BEFORE the dependency resolver runs so nested applies don't mutate the read.
        string? beforeDisplay = null;
        if (setting.InputType != InputType.Action)
        {
            try
            {
                var states = await discoveryService.GetSettingStatesAsync(new[] { setting }).ConfigureAwait(false);
                if (states.TryGetValue(settingId, out var state) && state.Success)
                    beforeDisplay = FormatStateDisplay(setting, state.IsEnabled, state.CurrentValue);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Debug, $"[SettingApplicationService] Change-history before-state read failed for '{settingId}': {ex.Message}");
            }
        }

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

            LogChangeHistory(setting, settingId, enable, value, beforeDisplay);
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
        LogChangeHistory(setting, settingId, enable, value, beforeDisplay);
        return OperationResult.Succeeded();
    }

    public Task ApplyRecommendedSettingsForFeatureAsync(string settingId) =>
        recommendedSettingsApplier.ApplyRecommendedSettingsForFeatureAsync(settingId, this);

    private void LogChangeHistory(SettingDefinition setting, string settingId, bool enable, object? value, string? beforeDisplay)
    {
        try
        {
            var name = ResolveLocalized(SettingLocalizationKeys.Name(setting)) ?? setting.Name;
            var group = ResolveLocalizedGroup(setting.GroupName);

            if (setting.InputType == InputType.Action)
            {
                changeHistory.LogSettingAction(name, group);
                return;
            }

            var after = FormatStateDisplay(setting, enable, value);
            var before = beforeDisplay ?? ResolveLocalized(SettingLocalizationKeys.CommonCustomState) ?? "?";
            if (before == after)
                return; // not a change — no receipt entry

            changeHistory.LogSettingChange(name, group, before, after);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[SettingApplicationService] Change-history logging failed for '{settingId}': {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the localized string, or null when the key is missing. The real LocalizationService
    /// returns the literal "[{key}]" miss-marker for an unknown key (never null/empty), so we mirror
    /// SettingLocalizationService's StartsWith("[") &amp;&amp; EndsWith("]") detection exactly.
    /// </summary>
    private string? ResolveLocalized(string key)
    {
        var result = localizationService.GetString(key);
        return result.StartsWith("[") && result.EndsWith("]") ? null : result;
    }

    private string FormatStateDisplay(SettingDefinition setting, bool enable, object? value)
    {
        switch (setting.InputType)
        {
            case InputType.Selection:
                if (value is int index && setting.ComboBox != null
                    && index >= 0 && index < setting.ComboBox.Options.Count)
                {
                    return ResolveLocalized(SettingLocalizationKeys.OptionDisplay(setting, index))
                        ?? setting.ComboBox.Options[index].DisplayName;
                }
                if (value is Dictionary<string, object?> acdc)
                    return string.Join(", ", acdc.Select(kv => $"{kv.Key}={kv.Value}"));
                return value?.ToString() ?? ResolveLocalized(SettingLocalizationKeys.CommonCustomState) ?? "?";

            case InputType.NumericRange:
                if (value is Dictionary<string, object?> acdcNum)
                    return string.Join(", ", acdcNum.Select(kv => $"{kv.Key}={kv.Value}"));
                return value?.ToString() ?? ResolveLocalized(SettingLocalizationKeys.CommonCustomState) ?? "?";

            default: // Toggle, CheckBox
                return localizationService.GetString(
                    enable ? "Template_EnabledDisabled_Option_1" : "Template_EnabledDisabled_Option_0");
        }
    }

    private string? ResolveLocalizedGroup(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName))
            return null;
        // Mirror SettingLocalizationService's group resolution: compact key first, snake fallback, raw name last.
        return ResolveLocalized(SettingLocalizationKeys.GroupCompact(groupName))
            ?? ResolveLocalized(SettingLocalizationKeys.GroupSnake(groupName))
            ?? groupName;
    }

}
