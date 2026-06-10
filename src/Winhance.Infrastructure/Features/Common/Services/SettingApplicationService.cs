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
using Winhance.Infrastructure.Features.Common.Helpers;


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
    ILocalizationService localizationService,
    IHardwareDetectionService hardwareDetectionService) : ISettingApplicationService
{
    // Battery presence doesn't change mid-session, so resolve it once and cache. The async
    // detection is awaited inside ApplySettingAsync (already async-adjacent to the receipt flow)
    // and stored here so the synchronous formatters can consult it. Fail OPEN: a detection failure
    // defaults to true (render BOTH AC and DC — more information, never a phantom suppression).
    private bool? _hasBatteryCache;

    private async Task<bool> GetHasBatteryAsync()
    {
        if (_hasBatteryCache.HasValue)
            return _hasBatteryCache.Value;

        try
        {
            _hasBatteryCache = await hardwareDetectionService.HasBatteryAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Debug, $"[SettingApplicationService] Battery detection failed, defaulting to true (render both AC/DC): {ex.Message}");
            _hasBatteryCache = true;
        }

        return _hasBatteryCache.Value;
    }

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
        // Resolve battery presence once here (cached, async-adjacent) so the synchronous formatters
        // can render AC-only on battery-less machines and before/after CANNOT disagree.
        string? beforeDisplay = null;
        if (setting.InputType != InputType.Action)
        {
            var hasBattery = await GetHasBatteryAsync().ConfigureAwait(false);
            try
            {
                var states = await discoveryService.GetSettingStatesAsync(new[] { setting }).ConfigureAwait(false);
                if (states.TryGetValue(settingId, out var state) && state.Success)
                    beforeDisplay = FormatBeforeDisplay(setting, state, hasBattery);
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

            // Battery flag was resolved in ApplySettingAsync's before-capture block for every
            // non-Action setting (and Action never hits the AC/DC formatting below). A null cache
            // means detection never ran for this path — fail open to rendering both components.
            var hasBattery = _hasBatteryCache ?? true;
            var after = FormatStateDisplay(setting, enable, value, hasBattery);
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

    /// <summary>
    /// Resolves a Selection option index to a human-readable label, mirroring the UI exactly
    /// (<c>SettingLocalizationService</c>): a per-option <c>DisplayName</c> that is itself a
    /// localization key (e.g. power settings' <c>Template_*</c> / <c>PowerPlan_*</c> keys) is
    /// localized verbatim; otherwise the per-setting <c>Setting_{id}_Option_{index}</c> key is
    /// used, with the raw <c>DisplayName</c> as the final fallback. Out-of-range indices resolve
    /// to the localized "Custom" state.
    /// </summary>
    private string GetOptionLabel(SettingDefinition setting, int index)
    {
        if (setting.ComboBox == null || index < 0 || index >= setting.ComboBox.Options.Count)
            return ResolveLocalized(SettingLocalizationKeys.CommonCustomState) ?? "Custom";

        var dn = setting.ComboBox.Options[index].DisplayName;
        var key = SettingLocalizationKeys.IsLocalizationKey(dn)
            ? dn
            : SettingLocalizationKeys.OptionDisplay(setting, index);
        return ResolveLocalized(key) ?? dn;
    }

    /// <summary>
    /// Best-effort conversion of a JSON-sourced numeric (may be <see cref="long"/>/<see cref="double"/>)
    /// to an int. Returns null when the value isn't numeric.
    /// </summary>
    private static int? TryToInt(object? value)
    {
        if (value == null) return null;
        try { return Convert.ToInt32(value); }
        catch { return null; }
    }

    private string FormatStateDisplay(SettingDefinition setting, bool enable, object? value, bool hasBattery)
    {
        switch (setting.InputType)
        {
            case InputType.Selection:
                // UI / recommended path: a single selected option index.
                if (value is int index)
                    return GetOptionLabel(setting, index);

                // Config-import path: AC/DC option indices arrive as a (acIndex, dcIndex) tuple.
                if (value is ValueTuple<int, int> acdcTuple)
                    return ComposeAcDc(GetOptionLabel(setting, acdcTuple.Item1), GetOptionLabel(setting, acdcTuple.Item2), hasBattery);

                if (value is Dictionary<string, object?> dict)
                {
                    // Power-plan shape: { "Guid": ..., "Name": "..." } — render just the friendly name.
                    if (dict.TryGetValue("Name", out var nameVal))
                        return nameVal?.ToString() ?? ResolveLocalized(SettingLocalizationKeys.CommonCustomState) ?? "Custom";

                    // Separate AC/DC option indices (UI quick-set path). JSON sources may box these
                    // as long/double, so coerce defensively.
                    if (dict.ContainsKey("ACValue") && dict.ContainsKey("DCValue"))
                    {
                        var acInt = TryToInt(dict["ACValue"]);
                        var dcInt = TryToInt(dict["DCValue"]);
                        if (acInt.HasValue && dcInt.HasValue)
                            return ComposeAcDc(GetOptionLabel(setting, acInt.Value), GetOptionLabel(setting, dcInt.Value), hasBattery);
                    }

                    return string.Join(", ", dict.Select(kv => $"{kv.Key}={kv.Value}"));
                }
                return value?.ToString() ?? ResolveLocalized(SettingLocalizationKeys.CommonCustomState) ?? "?";

            case InputType.NumericRange:
                // After-values are display units (the bridge fix converts on import; UI/recommended
                // paths already supply display units) — render as-is, with unit suffix when available.
                if (value is Dictionary<string, object?> acdcNum
                    && acdcNum.TryGetValue("ACValue", out var acNum)
                    && acdcNum.TryGetValue("DCValue", out var dcNum)
                    && setting.PowerCfgSettings?.Any() == true)
                {
                    var units = RecommendedSettingsResolver.GetPowerCfgDisplayUnits(setting);
                    return FormatPowerNumeric(units, acNum, dcNum, hasBattery);
                }
                if (value is Dictionary<string, object?> acdcNumPlain
                    && acdcNumPlain.TryGetValue("ACValue", out var acNumPlain)
                    && acdcNumPlain.TryGetValue("DCValue", out var dcNumPlain))
                    return ComposeAcDc(acNumPlain?.ToString() ?? "", dcNumPlain?.ToString() ?? "", hasBattery);
                return value?.ToString() ?? ResolveLocalized(SettingLocalizationKeys.CommonCustomState) ?? "?";

            default: // Toggle, CheckBox
                return localizationService.GetString(
                    enable ? "Template_EnabledDisabled_Option_1" : "Template_EnabledDisabled_Option_0");
        }
    }

    /// <summary>
    /// Formats the pre-apply state for the change-history receipt. For PowerCfg Separate
    /// NumericRange settings, <see cref="SettingStateResult.CurrentValue"/> isn't a usable AC/DC
    /// pair and the raw <c>ACValue</c>/<c>DCValue</c> in <see cref="SettingStateResult.RawValues"/>
    /// are SYSTEM units (e.g. seconds) — convert them to display units so the "before" matches the
    /// "after" rendering exactly (same <c>AC: x, DC: y</c> shape), keeping no-op detection working.
    /// PowerCfg Separate Selection settings get the same treatment: <c>CurrentValue</c> is a single
    /// AC-only option index, so the raw AC/DC system values are each mapped to an option index and
    /// rendered to match the config-import after-format byte-for-byte. On battery-less machines the
    /// DC component is omitted entirely (see <see cref="ComposeAcDc"/>) so before and after agree.
    /// All other settings defer to <see cref="FormatStateDisplay"/>.
    /// </summary>
    private string FormatBeforeDisplay(SettingDefinition setting, SettingStateResult state, bool hasBattery)
    {
        if (setting.InputType == InputType.NumericRange
            && setting.PowerCfgSettings?.Any() == true
            && state.RawValues is { } raw
            && raw.TryGetValue("ACValue", out var acRaw)
            && raw.TryGetValue("DCValue", out var dcRaw))
        {
            var acInt = TryToInt(acRaw);
            var dcInt = TryToInt(dcRaw);
            if (acInt.HasValue && dcInt.HasValue)
            {
                var units = RecommendedSettingsResolver.GetPowerCfgDisplayUnits(setting);
                var ac = RecommendedSettingsResolver.ConvertSystemToDisplayUnits(acInt.Value, units);
                var dc = RecommendedSettingsResolver.ConvertSystemToDisplayUnits(dcInt.Value, units);
                return FormatPowerNumeric(units, ac, dc, hasBattery);
            }
        }

        // PowerCfg Separate SELECTION settings: state.CurrentValue is a single (AC-only) option index,
        // so FormatStateDisplay would render one label while the config-import after-value renders
        // "AC: x, DC: y". Render the before in the same AC/DC shape so no-op detection works. The raw
        // ACValue/DCValue here are SYSTEM PowerCfg values (e.g. an enum/code), not option indices —
        // map each to its option index via the ValueMappings["PowerCfgValue"] lookup.
        if (setting.InputType == InputType.Selection
            && setting.PowerCfgSettings?.Any() == true
            && state.RawValues is { } selRaw
            && selRaw.TryGetValue("ACValue", out var acSel)
            && selRaw.TryGetValue("DCValue", out var dcSel))
        {
            var acVal = TryToInt(acSel);
            var dcVal = TryToInt(dcSel);
            if (acVal.HasValue && dcVal.HasValue)
            {
                // No match for a raw PowerCfg value must render as the localized "Custom" label
                // (-1 → GetOptionLabel out-of-range → Custom). NEVER use the raw value as an option
                // index — raw 1 must not silently become Options[1].
                var acIdx = RecommendedSettingsResolver.FindOptionIndexForPowerCfgValue(setting, acVal.Value) ?? -1;
                var dcIdx = RecommendedSettingsResolver.FindOptionIndexForPowerCfgValue(setting, dcVal.Value) ?? -1;
                return ComposeAcDc(GetOptionLabel(setting, acIdx), GetOptionLabel(setting, dcIdx), hasBattery);
            }
        }

        return FormatStateDisplay(setting, state.IsEnabled, state.CurrentValue, hasBattery);
    }

    /// <summary>
    /// Composes an AC/DC receipt fragment. On battery-less machines (<paramref name="hasBattery"/> is
    /// false) only the AC component is shown — the DC half is never written by PowerCfgApplier there,
    /// so rendering it would be a phantom. With a battery present, both halves render as before.
    /// </summary>
    private static string ComposeAcDc(string ac, string dc, bool hasBattery) =>
        hasBattery ? $"AC: {ac}, DC: {dc}" : $"AC: {ac}";

    /// <summary>
    /// Formats a PowerCfg NumericRange AC/DC value pair with a localized unit suffix per value.
    /// Mirrors <c>SettingLocalizationService.LocalizeUnits</c> so the receipt matches what the UI
    /// displays on the slider.  When the unit string is null/empty the pair renders without a suffix.
    /// On battery-less machines only the AC value renders (no phantom DC component).
    /// </summary>
    private string FormatPowerNumeric(string? units, object? ac, object? dc, bool hasBattery)
    {
        var localizedUnit = LocalizeUnit(units);
        if (string.IsNullOrEmpty(localizedUnit))
            return ComposeAcDc(ac?.ToString() ?? "", dc?.ToString() ?? "", hasBattery);
        return ComposeAcDc($"{ac} {localizedUnit}", $"{dc} {localizedUnit}", hasBattery);
    }

    /// <summary>
    /// Localizes a raw unit string via the same key mapping used by
    /// <c>SettingLocalizationService.LocalizeUnits</c>.  Returns the raw string
    /// (or empty) when no localization key exists so the caller can suppress the suffix.
    /// </summary>
    private string? LocalizeUnit(string? units)
    {
        if (string.IsNullOrEmpty(units)) return null;
        var key = units switch
        {
            "Minutes"      => "Common_Unit_Minutes",
            "Milliseconds" => "Common_Unit_Milliseconds",
            _              => null,
        };
        return key != null ? (ResolveLocalized(key) ?? units) : units;
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
