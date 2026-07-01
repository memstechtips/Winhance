using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
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
    ILocalizationService localizationService,
    IHardwareDetectionService hardwareDetectionService,
    IStateWriter stateWriter,
    IWindowsVersionService windowsVersionService,
    ICatalogDetectionService catalogDetection,
    ICatalogSettingStateProvider settingStateProvider,
    IConfigImportState configImportState) : ISettingApplicationService
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

    // The live Windows build, used to (a) gate ApplyPlanBuilder's per-target AppliesTo and (b) decide whether a
    // catalog setting whose OLD def is OS-filtered-out is still build-compatible enough to resolve via the bypassed
    // registry. Same source the config build-gating uses.
    private WinBuild CurrentBuild()
        => new(windowsVersionService.GetWindowsBuildNumber(), windowsVersionService.GetWindowsBuildRevision());

    /// <summary>Phase 6.4 cutover seam: apply a setting's operations through the NEW catalog engine when the setting
    /// is paired and the request is representable (plain toggle / check-box / selection / Action, numeric powercfg
    /// slider, and reset-to-default - Phase 6.4b), else fall back to the proven old apply.
    /// <see cref="ApplyRequestResolver"/> decides; <see cref="ApplyExecutor"/> runs the plan against the live
    /// <see cref="IStateWriter"/>. Unpaired / custom-detector / dynamic-option requests resolve to null and keep the
    /// old <see cref="ISettingOperationExecutor"/> path, so nothing regresses.</summary>
    private async Task<OperationResult> ApplyOperationsAsync(SettingDefinition setting, bool enable, object? value, bool resetToDefault)
    {
        // Phase 6.5: pass the LIVE Windows build so ApplyPlanBuilder emits only the targets gated to this OS
        // (Target.AppliesTo). Without it the build gate is skipped and a build-gated/merged setting (e.g. the
        // This PC folder settings - a Windows-11 HiddenByDefault write AND a Windows-10 key-delete on the SAME
        // key) would apply BOTH per-OS mechanisms. Settings with no build-gated targets (AppliesTo empty) are
        // unaffected: their targets are emitted regardless of build.
        var plan = ApplyRequestResolver.Resolve(setting, enable, value, resetToDefault, CurrentBuild());
        if (plan is null)
        {
            // The old executor runs HandleProcessAndServiceRestartsAsync internally as its final step, so the
            // fallback path needs nothing extra here.
            return await operationExecutor
                .ApplySettingOperationsAsync(setting, enable, value, resetToDefault).ConfigureAwait(false);
        }

        var result = ApplyExecutor.Execute(plan, stateWriter);

        // The new apply engine performs no process/service restarts, but the old executor did as its final,
        // unconditional step (SettingOperationExecutor's HandleProcessAndServiceRestartsAsync). Mirror it so a paired
        // setting that restarts Explorer/a service on apply still takes visual effect. This respects an active
        // SuppressRestarts scope (the applyRecommended-Action branch), so it does not double-restart - identical to
        // how the old executor's call behaved under suppression.
        await processRestartManager.HandleProcessAndServiceRestartsAsync(setting).ConfigureAwait(false);

        if (result.AllSucceeded)
            return OperationResult.Succeeded();

        var message = $"{result.Failed}/{result.Total} apply operation(s) failed for '{setting.Id}': {string.Join("; ", result.Failures)}";
        logService.Log(LogLevel.Warning, $"[SettingApplicationService] {message}");
        return OperationResult.Failed(message);
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
        {
            // Phase 6.5 cross-OS resolution: a merged catalog setting whose OLD def is OS-filtered-out on this
            // machine (e.g. a This PC folder setting imported from a "-win10" config, normalized to its canonical
            // id, running on the OS whose old split-def is filtered) misses the OS-filtered registry. The NEW engine
            // applies it OS-portably via build-gated targets, so resolve from the BYPASSED index when the catalog
            // has a peer that is build-compatible with this machine. A genuinely-incompatible / non-catalog id is
            // not resolved and still falls through to the throw below.
            if (SettingCatalog.All.Any(s => s.Id == settingId && s.Availability.Allows(CurrentBuild())))
                setting = settingsRegistry.GetByIdBypassed(settingId);
        }
        if (setting == null)
            throw new ArgumentException($"Setting '{settingId}' not found in registry");

        var featureId = settingsRegistry.GetFeatureIdForSetting(settingId)
            ?? settingsRegistry.GetFeatureIdForSettingBypassed(settingId)
            ?? throw new InvalidOperationException($"Setting '{settingId}' has no feature mapping");

        globalSettingsRegistry.RegisterSetting(featureId, setting);

        // Phase 6.6 Slice 2: a setting is "paired" when the new catalog holds a peer for it. Paired settings run
        // their relationships through the NEW RelationshipResolver engine (ApplyCatalogRelationshipsAsync, AFTER the
        // main apply); unpaired settings keep the OLD dependency-resolver / inline-preset paths below as the
        // fallback, so nothing regresses while the catalog is still being filled in.
        bool paired = SettingCatalog.All.Any(s => s.Id == settingId);

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
                // The full-state provider (new engine) reads the complete before-state incl. the typed AC/DC,
                // consistent with the after-state and the live UI. Old discovery has been retired: the provider covers
                // every setting (completeness-proven, 0 unpaired), so the old paired?provider:discovery fallback is gone.
                // A genuinely unpaired setting returns Success=false here, leaving beforeDisplay null - a cosmetic
                // change-history gap that cannot arise for a real setting.
                var states = await settingStateProvider.GetStatesAsync(new[] { setting }).ConfigureAwait(false);
                if (states.TryGetValue(settingId, out var state) && state.Success)
                {
                    beforeDisplay = FormatBeforeDisplay(setting, state, hasBattery);
                }
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

        // Paired settings run forward/reverse relationships through the new engine AFTER the main apply
        // (ApplyCatalogRelationshipsAsync). Only unpaired settings still use the OLD value-prereq + dependency paths.
        if (!skipValuePrerequisites && !paired)
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
                operationResult = await ApplyOperationsAsync(setting, enable, value, resetToDefault).ConfigureAwait(false);
                toRestart.Add(setting);

                var recApplied = await recommendedSettingsApplier
                    .ApplyRecommendedForFeatureAsync(settingId, this).ConfigureAwait(false);
                toRestart.AddRange(recApplied);
            }
            await processRestartManager.FlushCoalescedRestartsAsync(toRestart).ConfigureAwait(false);
        }
        else
        {
            operationResult = await ApplyOperationsAsync(setting, enable, value, resetToDefault).ConfigureAwait(false);
        }

        // Unpaired only: the OLD inline-preset path (preset -> apply children). Paired selections drive their
        // children through the new engine's State.Controls in ApplyCatalogRelationshipsAsync instead.
        if (!paired && setting.SettingPresets != null &&
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

        // Unpaired only: the OLD reverse parent-sync. Paired settings get reverse-sync from the new engine
        // (RelationshipResolver.ResolveReverseSync) inside ApplyCatalogRelationshipsAsync below.
        if (!skipValuePrerequisites && !paired)
        {
            await dependencyResolver.SyncParentToMatchingPresetAsync(setting, settingId, allSettings, this).ConfigureAwait(false);
        }

        // Phase 6.6 Slice 2: paired settings run ALL their relationships (forward Requires/Enables/Controls, reverse
        // parent-sync, reverse cascade-disable) through the new engine here, AFTER the main apply. A paired setting
        // with no relationships makes this a harmless no-op.
        if (!skipValuePrerequisites && paired)
        {
            var catalogSetting = SettingCatalog.All.First(s => s.Id == settingId);
            var targetLabel = ResolveTargetLabel(catalogSetting, enable, value, resetToDefault);
            await ApplyCatalogRelationshipsAsync(catalogSetting, targetLabel).ConfigureAwait(false);
        }

        // Always publish the event — even on partial failure, some operations may
        // have succeeded and listeners need to re-read actual system state.
        eventBus.Publish(new SettingAppliedEvent(settingId, enable, value));

        // Phase 6.7 Slice 8b-2b (D1): re-home the Winhance-plan recommended-power cascade the PowerService special
        // handler used to run in its tail. With the special-handler registration removed, power-plan apply now flows
        // through the new engine (resolver -> PowerPlanActivateOp -> writer); after a SUCCESSFUL switch TO the Winhance
        // plan, re-apply the recommended power settings for the feature - the same machinery the Action-recommended
        // branch uses. ApplyRecommendedForFeatureAsync excludes the trigger setting, so it cannot loop on power-plan.
        // Skipped during a config import that supplies its own individual power values (the import is the source of
        // truth), mirroring the old PowerService gate.
        if (settingId == SettingIds.PowerPlanSelection
            && operationResult.Success
            && IsWinhancePowerPlanValue(value)
            && !(configImportState.IsActive && configImportState.ImportSuppliesPowerValues))
        {
            await recommendedSettingsApplier
                .ApplyRecommendedSettingsForFeatureAsync(SettingIds.PowerPlanSelection, this).ConfigureAwait(false);
        }

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

    /// <summary>Phase 6.7 Slice 8b-2b (D1): true when a power-plan apply value identifies the Winhance Power Plan.
    /// The live UI passes the scheme GUID as a string; config import passes a {Guid,Name} dictionary
    /// (ConfigurationApplicationBridgeService). Both forms route through the shared
    /// <see cref="PowerPlanDefinitions.IsWinhancePowerPlan"/> check.</summary>
    private static bool IsWinhancePowerPlanValue(object? value) => value switch
    {
        string guid => PowerPlanDefinitions.IsWinhancePowerPlan(guid),
        Dictionary<string, object> dict => PowerPlanDefinitions.IsWinhancePowerPlan(
            dict.TryGetValue("Guid", out var g) ? g?.ToString() : null,
            dict.TryGetValue("Name", out var n) ? n?.ToString() : null),
        _ => false,
    };

    /// <summary>
    /// Phase 6.6 Slice 2: the state label the catalog setting was just moved into, derived the same way
    /// <see cref="ApplyRequestResolver"/> derives the apply label. Toggle/CheckBox -> "Enabled"/"Disabled";
    /// Selection -> the catalog state label at the applied option index; resetToDefault -> the WindowsDefault
    /// state's label. Returns null when the label cannot be derived (non-index selection value, or no
    /// WindowsDefault state) so the caller skips relationship resolution rather than guessing.
    /// </summary>
    private static string? ResolveTargetLabel(Setting setting, bool enable, object? value, bool resetToDefault)
    {
        if (resetToDefault)
            return setting.States.FirstOrDefault(s => s.HasRole(RoleKind.WindowsDefault))?.Label;

        // A two-state Enabled/Disabled target is a toggle/check-box; map enable straight to its label.
        bool isToggle = setting.States.Any(s => s.Label == "Enabled") && setting.States.Any(s => s.Label == "Disabled");
        if (isToggle)
            return enable ? "Enabled" : "Disabled";

        // Otherwise a selection: it moves to the state at the applied option index (States are authored
        // one-per-option, in option order, so the index IS the state index). A non-index selection value is not
        // representable, so return null and skip relationship resolution rather than guessing.
        if (value is int idx && idx >= 0 && idx < setting.States.Count)
            return setting.States[idx].Label;

        return null;
    }

    /// <summary>
    /// Phase 6.6 Slice 2: runs a paired setting's relationships through the new <see cref="RelationshipResolver"/>
    /// engine AFTER the main apply. Resolves forward (Requires/Enables + the target state's Controls), reverse
    /// parent-sync, and reverse cascade-disable, then applies each follow-on as a LEAF
    /// (SkipValuePrerequisites = true) so it triggers no further cascade. A shared visited set + self-skip
    /// prevents loops. Current state is read once from the new detection engine and served synchronously to the
    /// pure resolvers.
    /// </summary>
    private async Task ApplyCatalogRelationshipsAsync(Setting setting, string? targetLabel)
    {
        if (targetLabel is null)
            return;

        // Pre-fetch every catalog setting's current state ONCE. currentStateOf is sync (the resolvers are pure),
        // but DetectAsync is async, so resolve up front and serve from the cache. Detecting the whole catalog in
        // one batch is acceptable for a one-shot apply and is simplest/correct: it covers every related id the
        // resolvers may scan (forward Links, reverse Controls, reverse cascade dependents). DetectAsync isolates
        // each setting's failure, so a partial machine read degrades gracefully (unknown ids resolve to null).
        Dictionary<string, CatalogDetectionResult> detected;
        try
        {
            detected = await catalogDetection.DetectAsync(SettingCatalog.All).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[SettingApplicationService] Catalog relationship detection failed for '{setting.Id}': {ex.Message}");
            return;
        }

        string? currentStateOf(string id) =>
            detected != null && detected.TryGetValue(id, out var r) ? r.StateLabel : null;

        var fwd = RelationshipResolver.ResolveForward(setting, targetLabel, currentStateOf);
        var sync = RelationshipResolver.ResolveReverseSync(setting.Id, SettingCatalog.All, currentStateOf);
        var cascade = RelationshipResolver.ResolveReverseCascade(setting.Id, targetLabel, SettingCatalog.All, currentStateOf);

        // Self-skip + visited loop guard. Seed with the setting being applied so a relationship pointing back at
        // it is never re-applied.
        var visited = new HashSet<string> { setting.Id };

        foreach (var action in fwd.Concat(sync).Concat(cascade))
        {
            if (!visited.Add(action.SettingId))
                continue;

            var request = ToRequest(action.SettingId, action.StateLabel, action.IsReset);
            if (request is null)
                continue; // label not representable on the target setting - logged inside ToRequest

            try
            {
                await ApplySettingAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[SettingApplicationService] Relationship apply of '{action.SettingId}' (-> {action.StateLabel}) for '{setting.Id}' failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Phase 6.6 Slice 2: maps a relationship <c>ApplyAction</c> (target id + desired state label) into an
    /// <see cref="ApplySettingRequest"/>. The follow-on is always a LEAF (SkipValuePrerequisites = true) so it
    /// performs no further cascade - matching the old auto-enable / preset-child behaviour. A two-state
    /// Enabled/Disabled target maps the label to Enable; a selection maps the label to the option index (the
    /// state index, which equals the ComboBox option index by construction). Returns null (logged) when the
    /// target setting is missing or has no state with that label, so a bad relationship is skipped, never thrown.
    /// A reverse-cascade action passes <paramref name="isReset"/> = true, so the follow-on applies with
    /// ResetToDefault = true (deleting a [1,null] target via its ResetSet, matching the old DependencyManager cascade).
    /// </summary>
    private ApplySettingRequest? ToRequest(string targetId, string label, bool isReset)
    {
        var target = SettingCatalog.All.FirstOrDefault(s => s.Id == targetId);
        if (target is null)
        {
            logService.Log(LogLevel.Warning, $"[SettingApplicationService] Relationship target '{targetId}' is not in the catalog - skipping");
            return null;
        }

        bool isToggle = target.States.Any(s => s.Label == "Enabled") && target.States.Any(s => s.Label == "Disabled");
        if (isToggle)
        {
            return new ApplySettingRequest { SettingId = targetId, Enable = label == "Enabled", Value = null, SkipValuePrerequisites = true, ResetToDefault = isReset };
        }

        // Selection: the option index is the index of the state whose Label matches (states are authored
        // one-per-option in option order).
        int index = -1;
        for (int i = 0; i < target.States.Count; i++)
        {
            if (target.States[i].Label == label)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            logService.Log(LogLevel.Warning, $"[SettingApplicationService] Relationship target '{targetId}' has no state labelled '{label}' - skipping");
            return null;
        }

        return new ApplySettingRequest { SettingId = targetId, Enable = true, Value = index, SkipValuePrerequisites = true, ResetToDefault = isReset };
    }

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
    /// pair and the typed <see cref="SettingStateResult.AcValue"/>/<see cref="SettingStateResult.DcValue"/>
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
        // Read AC/DC from the new engine's typed fields (threaded onto the before-state at the provider read).
        // These are SYSTEM PowerCfg values (an enum/code), not option indices.
        int? acInt = state.AcValue;
        int? dcInt = state.DcValue;

        if (setting.InputType == InputType.NumericRange
            && setting.PowerCfgSettings?.Any() == true
            && acInt.HasValue && dcInt.HasValue)
        {
            var units = RecommendedSettingsResolver.GetPowerCfgDisplayUnits(setting);
            var ac = RecommendedSettingsResolver.ConvertSystemToDisplayUnits(acInt.Value, units);
            var dc = RecommendedSettingsResolver.ConvertSystemToDisplayUnits(dcInt.Value, units);
            return FormatPowerNumeric(units, ac, dc, hasBattery);
        }

        // PowerCfg Separate SELECTION settings: state.CurrentValue is a single (AC-only) option index,
        // so FormatStateDisplay would render one label while the config-import after-value renders
        // "AC: x, DC: y". Render the before in the same AC/DC shape so no-op detection works. The raw
        // ACValue/DCValue here are SYSTEM PowerCfg values (e.g. an enum/code), not option indices —
        // map each to its option index via the ValueMappings["PowerCfgValue"] lookup.
        if (setting.InputType == InputType.Selection
            && setting.PowerCfgSettings?.Any() == true
            && acInt.HasValue && dcInt.HasValue)
        {
            // No match for a raw PowerCfg value must render as the localized "Custom" label
            // (-1 -> GetOptionLabel out-of-range -> Custom). NEVER use the raw value as an option
            // index - raw 1 must not silently become Options[1].
            var acIdx = RecommendedSettingsResolver.FindOptionIndexForPowerCfgValue(setting, acInt.Value) ?? -1;
            var dcIdx = RecommendedSettingsResolver.FindOptionIndexForPowerCfgValue(setting, dcInt.Value) ?? -1;
            return ComposeAcDc(GetOptionLabel(setting, acIdx), GetOptionLabel(setting, dcIdx), hasBattery);
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
