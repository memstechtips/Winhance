using System.Diagnostics;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Helpers;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class SettingApplicationService(
    ICatalogSettingsRegistry settingsRegistry,
    ISpecialSettingHandlerRegistry specialHandlerRegistry,
    ILogService logService,
    IEventBus eventBus,
    IRecommendedSettingsApplier recommendedSettingsApplier,
    IProcessRestartManager processRestartManager,
    IChangeHistoryService changeHistory,
    ILocalizationService localizationService,
    IHardwareDetectionService hardwareDetectionService,
    IStateWriter stateWriter,
    IAsyncEffectRunner asyncEffectRunner,
    IWindowsVersionService windowsVersionService,
    ICatalogDetectionService catalogDetection,
    ICatalogSettingStateProvider settingStateProvider,
    IPowerSettingsQueryService powerSettingsQueryService,
    IConfigImportState configImportState) : ISettingApplicationService
{
    private WinBuild CurrentBuild()
        => new(windowsVersionService.GetWindowsBuildNumber(), windowsVersionService.GetWindowsBuildRevision());

    private async Task<OperationResult> ApplyOperationsAsync(Setting setting, bool enable, object? value, bool resetToDefault)
    {
        // Pass the LIVE Windows build so ApplyPlanBuilder emits only the targets gated to this OS
        // (Target.AppliesTo). Without it the build gate is skipped and a build-gated/merged setting (e.g. the
        // This PC folder settings - a Windows-11 HiddenByDefault write AND a Windows-10 key-delete on the SAME
        // key) would apply BOTH per-OS mechanisms. Settings with no build-gated targets (AppliesTo empty) are
        // unaffected: their targets are emitted regardless of build.
        var plan = ApplyRequestResolver.Resolve(setting.Id, enable, value, resetToDefault, CurrentBuild());
        if (plan is null)
        {
            // Resolve is total for every reachable request shape (ResolveTotalityAuditTests), so a null here is an
            // un-audited/unreachable shape. Fail loudly with a logged result rather than dereferencing a null plan.
            var nullPlanMessage = $"No apply plan resolved for '{setting.Id}' (enable={enable}, resetToDefault={resetToDefault}) - unaudited request shape";
            logService.Log(LogLevel.Warning, $"[SettingApplicationService] {nullPlanMessage}");
            return OperationResult.Failed(nullPlanMessage);
        }

        var applyPlan = ApplyPlan.From(plan);
        var result = ApplyExecutor.Execute(applyPlan, stateWriter);

        // Awaited before the restarts below, so a script's writes are in place when Explorer restarts.
        var deferredFailures = await asyncEffectRunner.RunAllAsync(applyPlan.AsyncEffects).ConfigureAwait(false);

        // The apply engine performs no process/service restarts, so run them here explicitly - so a setting
        // that restarts Explorer/a service on apply still takes visual effect. This respects an active
        // SuppressRestarts scope (the applyRecommended-Action branch), so it does not double-restart.
        await processRestartManager.HandleProcessAndServiceRestartsAsync(setting).ConfigureAwait(false);

        if (result.AllSucceeded && deferredFailures.Count == 0)
            return OperationResult.Succeeded();

        var allFailures = deferredFailures.Count == 0
            ? result.Failures
            : result.Failures.Concat(deferredFailures).ToList();
        var message = $"{result.Failed + deferredFailures.Count}/{applyPlan.Total} apply operation(s) failed for '{setting.Id}': {string.Join("; ", allFailures)}";
        logService.Log(LogLevel.Warning, $"[SettingApplicationService] {message}");
        return OperationResult.Failed(message);
    }

    public async Task<OperationResult> ApplySettingAsync(ApplySettingRequest request)
    {
        // Times the WHOLE apply. That single number on the success line is what tells a user at a glance
        // whether a toggle was fast; the per-phase Debug lines (broadcast, relationship detection) say
        // where the time went when it was not.
        var applyStopwatch = Stopwatch.StartNew();

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

        // The catalog registry's GetById alias-normalizes the id (a retired "-win10" This PC alias resolves to its
        // canonical merged Setting) and OS-scopes membership, but a merged setting is OS-portable (Availability
        // Everywhere + build-gated targets), so it resolves DIRECTLY here on either OS. A genuinely
        // OS-incompatible or non-catalog id returns null and falls through to the throw.
        var setting = settingsRegistry.GetById(settingId);
        if (setting == null)
            throw new ArgumentException($"Setting '{settingId}' not found in registry");

        // A setting is "paired" when the catalog holds a peer for it. Paired settings run their relationships
        // through the RelationshipResolver engine (ApplyCatalogRelationshipsAsync, AFTER the main apply); an
        // unpaired setting runs no relationship pass. The only exact-match-unpaired settings are the 6
        // dependency-free -win10 aliases.
        bool paired = SettingCatalog.All.Any(s => s.Id == settingId);

        // Pair the id to its catalog Setting for the change-history rendering methods (LogChangeHistory /
        // FormatBeforeDisplay / FormatStateDisplay / GetOptionLabel). Find is alias-normalized (a -win10 This PC id
        // resolves to its canonical merged Setting) and catalog-wide (NOT OS-scoped), so it degrades to null for a
        // genuinely-unpaired id and the receipt is simply skipped rather than throwing. In production this is the
        // same Setting as 'setting' above (the catalog registry's GetById resolves the identical canonical Setting);
        // it is read separately so an unpaired id (unreachable past the throw, but exercised by the tests) skips the
        // receipt via a null pairing instead of rendering a non-catalog object.
        var renderSetting = SettingCatalog.Find(settingId);

        // Change-history receipt: capture the pre-apply state so the entry can say "before → after".
        // Captured BEFORE any relationship follow-on / nested applies run so they don't mutate the read.
        // One read for both halves of the receipt, so before/after CANNOT disagree. Unknown battery state
        // renders BOTH AC and DC - more information beats silently hiding the DC half.
        string? beforeDisplay = null;
        if (setting.Control != ControlKind.Action)
        {
            var hasBattery = hardwareDetectionService.HasBattery() ?? true;
            try
            {
                // The full-state provider reads the complete before-state incl. the typed AC/DC, consistent with
                // the after-state and the live UI. A genuinely unpaired setting returns Success=false here, leaving
                // beforeDisplay null - a cosmetic change-history gap that cannot arise for a real setting.
                var state = request.BeforeState;
                if (state is null)
                {
                    var states = await settingStateProvider.GetStatesAsync(new[] { setting }).ConfigureAwait(false);
                    states.TryGetValue(settingId, out state);
                }

                if (renderSetting != null && state is { Success: true })
                {
                    beforeDisplay = FormatBeforeDisplay(renderSetting, state, hasBattery);
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Debug, $"[SettingApplicationService] Change-history before-state read failed for '{settingId}': {ex.Message}");
            }
        }

        // The recommended stamp belongs to CREATING the Winhance plan, not to selecting it. Read before the
        // apply, because the apply is what creates it - afterwards the plan always exists. Switching between
        // existing plans must leave each plan's own stored values alone, the way Windows plans behave.
        bool winhancePlanIsNew = false;
        if (settingId == SettingIds.PowerPlanSelection && IsWinhancePowerPlanValue(value))
        {
            var existingPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            winhancePlanIsNew = !existingPlans.Any(p => PowerPlanCatalog.IsWinhancePowerPlan(p.Guid, p.Name));
        }

        var specialHandler = specialHandlerRegistry.TryGet(settingId);
        if (specialHandler != null
            && await specialHandler.TryApplySpecialSettingAsync(settingId, value!, checkboxResult, this).ConfigureAwait(false))
        {
            await processRestartManager.HandleProcessAndServiceRestartsAsync(setting).ConfigureAwait(false);

            eventBus.Publish(new SettingAppliedEvent(settingId, enable, value));
            logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied setting '{settingId}' via special handler in {applyStopwatch.ElapsedMilliseconds}ms");

            if (renderSetting != null)
                LogChangeHistory(renderSetting, settingId, enable, value, beforeDisplay);
            return OperationResult.Succeeded();
        }

        // The branch below applies the feature's recommended settings INLINE, inside the coalesced-restart
        // scope. Named so the confirmation-checkbox rule further down can see that they have already been
        // applied and must not apply them a second time.
        bool recommendedAppliedInline = applyRecommended && setting.Control == ControlKind.Action;

        OperationResult operationResult;
        if (recommendedAppliedInline)
        {
            // One coalesced restart for the whole click: suppress the primary action's restart AND the
            // recommended batch, then flush once for primary + recommended combined.
            var toRestart = new List<Setting>();
            using (processRestartManager.SuppressRestarts())
            {
                operationResult = await ApplyOperationsAsync(setting, enable, value, resetToDefault).ConfigureAwait(false);
                if (renderSetting != null) toRestart.Add(renderSetting);

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

        // Paired settings run ALL their relationships (forward Requires/Enables/Controls, reverse parent-sync,
        // reverse cascade-disable) here, AFTER the main apply. A paired setting with no relationships makes this
        // a harmless no-op.
        if (!skipValuePrerequisites && paired)
        {
            var catalogSetting = SettingCatalog.All.First(s => s.Id == settingId);
            var targetLabel = ResolveTargetLabel(catalogSetting, enable, value, resetToDefault, CurrentBuild());
            await ApplyCatalogRelationshipsAsync(catalogSetting, targetLabel).ConfigureAwait(false);
        }

        // Always publish the event — even on partial failure, some operations may
        // have succeeded and listeners need to re-read actual system state.
        eventBus.Publish(new SettingAppliedEvent(settingId, enable, value));

        // Stamp the recommended power settings into the Winhance plan the FIRST time it is created (resolver
        // -> PowerPlanActivateOp -> writer creates it), never on a later switch back to it - re-stamping would
        // silently revert any adjustment the user made while on that plan. Same machinery as the
        // Action-recommended branch; ApplyRecommendedForFeatureAsync excludes the trigger setting, so it cannot
        // loop on power-plan. Skipped during a config import that supplies its own individual power values (the
        // import is the source of truth).
        if (settingId == SettingIds.PowerPlanSelection
            && operationResult.Success
            && winhancePlanIsNew
            && IsWinhancePowerPlanValue(value)
            && !(configImportState.IsActive && configImportState.ImportSuppliesPowerValues))
        {
            await recommendedSettingsApplier
                .ApplyRecommendedSettingsForFeatureAsync(SettingIds.PowerPlanSelection, this).ConfigureAwait(false);
        }

        // The confirmation checkbox on a setting with NO special handler means one thing, and every
        // Setting_{id}_ConfirmCheckbox string that reaches here says it: also apply this feature's recommended
        // settings. Without this the box was inert on the config-import path - cleaning the taskbar removed the
        // pinned items and left Task View and Search showing, which is not what the prompt offered.
        //
        // The specialHandler-is-null guard is load-bearing. A setting WITH a special handler owns its own
        // checkbox semantics - theme-mode-windows' box means "also change the wallpaper", which
        // ThemeWallpaperApplier applies itself - so a generic rule without the guard would apply a whole
        // feature's recommended settings off a wallpaper opt-in. It holds on both special-handler paths: a
        // handler that ACCEPTS returns above, and one that DECLINES falls through to here with a non-null
        // handler, so neither reaches this.
        //
        // recommendedAppliedInline excludes the live UI button path, which sets ApplyRecommended AND
        // CheckboxResult from the same checkbox (SettingItemViewModel.RunActionAsync) and has already applied
        // them in the coalesced-restart branch above; applying again here would double-fire. What this reaches
        // is the config-import path (ConfigurationApplicationBridgeService), which sets CheckboxResult alone.
        //
        // A failure here cannot fail the main apply - the action itself already ran - so it is logged and
        // swallowed, exactly as UpdateService treats the same call.
        if (specialHandler is null && checkboxResult && !recommendedAppliedInline && operationResult.Success)
        {
            try
            {
                await ApplyRecommendedSettingsForFeatureAsync(settingId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[SettingApplicationService] Failed to apply some recommended settings for '{settingId}' after its confirmation checkbox: {ex.Message}");
            }
        }

        if (!operationResult.Success)
        {
            logService.Log(LogLevel.Warning, $"[SettingApplicationService] Setting '{settingId}' partially failed: {operationResult.ErrorMessage}");
            return operationResult;
        }

        logService.Log(LogLevel.Info, $"[SettingApplicationService] Successfully applied setting '{settingId}' in {applyStopwatch.ElapsedMilliseconds}ms");
        if (renderSetting != null)
            LogChangeHistory(renderSetting, settingId, enable, value, beforeDisplay);
        return OperationResult.Succeeded();
    }

    public Task ApplyRecommendedSettingsForFeatureAsync(string settingId) =>
        recommendedSettingsApplier.ApplyRecommendedSettingsForFeatureAsync(settingId, this);

    // The live UI passes the scheme GUID as a string; config import passes a {Guid,Name} dictionary.
    private static bool IsWinhancePowerPlanValue(object? value) => value switch
    {
        string guid => PowerPlanCatalog.IsWinhancePowerPlan(guid),
        Dictionary<string, object> dict => PowerPlanCatalog.IsWinhancePowerPlan(
            dict.TryGetValue("Guid", out var g) ? g?.ToString() : null,
            dict.TryGetValue("Name", out var n) ? n?.ToString() : null),
        _ => false,
    };

    // Null when the label cannot be derived (non-index selection value, no WindowsDefault state), so the caller
    // skips relationship resolution rather than guessing.
    private static string? ResolveTargetLabel(Setting setting, bool enable, object? value, bool resetToDefault, WinBuild build)
    {
        if (resetToDefault)
            // Build-aware so a merged setting's OS-divergent WindowsDefault resolves for the live OS (see ApplyRequestResolver).
            return setting.States.FirstOrDefault(s => s.HasRole(RoleKind.WindowsDefault, build))?.Label;

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

    // Each follow-on is applied as a LEAF (SkipValuePrerequisites = true) so it triggers no further cascade; a
    // shared visited set + self-skip prevents loops.
    private async Task ApplyCatalogRelationshipsAsync(Setting setting, string? targetLabel)
    {
        if (targetLabel is null)
            return;

        var targetState = setting.States.FirstOrDefault(st => st.Label == targetLabel);

        // WHICH OTHER SETTINGS CAN THIS APPLY REACH? Every gate the three resolvers use to decide
        // CANDIDACY is a PURE CATALOG PREDICATE - none of them reads machine state to decide whether a
        // setting is a candidate, only to decide what to do with one that already is - so evaluating those
        // same predicates here produces a provable SUPERSET of the ids that can yield an action:
        //
        //   ResolveForward        - reads currentStateOf(link.OtherId) for the Requires links on the TARGET
        //                           state only. Its Enables links and its Controls children produce actions
        //                           without reading any state at all.
        //   ResolveReverseSync    - a parent is a candidate only when one of its states Controls this id;
        //                           it then reads that parent and every child its states Control.
        //   ResolveReverseCascade - a dependent can only act when one of its states declares
        //                           Requires(this id, ReverseCascade: true).
        var syncParents = SettingCatalog.All
            .Where(p => p.States.Any(st => st.Controls?.ContainsKey(setting.Id) == true))
            .ToList();

        var cascadeDependents = SettingCatalog.All
            .Where(d => d.States.Any(st => st.Links.Any(l =>
                l.Kind == LinkKind.Requires && l.OtherId == setting.Id && l.ReverseCascade)))
            .ToList();

        // ResolveForward can only return an action when the TARGET STATE itself declares one - a Link of
        // either kind, or Controls. (Relationships are a property of the state, not of the setting.)
        bool forwardPossible = targetState is not null
            && (targetState.Links.Count > 0 || targetState.Controls is { Count: > 0 });

        // Nothing in the catalog relates to this setting in this state, so no resolver can return an action
        // however the machine happens to be configured. Detecting anything here would be pure cost, and this
        // is the common case: the whole relationship graph is a few dozen Links and a handful of Controls
        // across 414 settings, so most applies stop here having read nothing.
        if (!forwardPossible && syncParents.Count == 0 && cascadeDependents.Count == 0)
        {
            // SAY SO: a silent return here would make the scoping (skipping detection for unrelated applies)
            // invisible and unverifiable from a user's log.
            logService.Log(LogLevel.Debug,
                $"[SettingApplicationService] Relationship scope for '{setting.Id}': 0 related settings - detection skipped");
            return;
        }

        // The ids whose CURRENT STATE a resolver can read. Anything outside this set is never handed to
        // currentStateOf, so detecting it cannot change a single decision. The applied setting is in it
        // because ResolveReverseSync reads the changed child's own state when scoring a parent's presets.
        var scopeIds = new HashSet<string>(StringComparer.Ordinal) { setting.Id };
        if (targetState is not null)
        {
            foreach (var link in targetState.Links)
                if (link.Kind == LinkKind.Requires)
                    scopeIds.Add(link.OtherId);
        }
        foreach (var parent in syncParents)
        {
            scopeIds.Add(parent.Id);
            foreach (var state in parent.States)
                if (state.Controls is { } controls)
                    foreach (var childId in controls.Keys)
                        scopeIds.Add(childId);
        }
        foreach (var dependent in cascadeDependents)
            scopeIds.Add(dependent.Id);

        // Materialized FROM SettingCatalog.All, so each scoped setting is the same object a full-catalog
        // detect would have read. An id with no catalog Setting is simply not detectable - it was not
        // detectable under the full-catalog read either, and resolves to null there too.
        var scope = SettingCatalog.All.Where(st => scopeIds.Contains(st.Id)).ToList();

        // Resolved up front and served from the cache: currentStateOf is sync (the resolvers are pure) but
        // DetectAsync is async. DetectAsync isolates each setting's failure, so a partial machine read
        // degrades gracefully (unknown ids resolve to null). Detection is per-setting - the context
        // pre-fetches from the batch's own targets and every custom detector reads only its own setting - so
        // a scoped batch returns exactly the results the full-catalog batch returned for these ids.
        Dictionary<string, CatalogDetectionResult> detected;
        var detectStopwatch = Stopwatch.StartNew();
        try
        {
            detected = await catalogDetection.DetectAsync(scope).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[SettingApplicationService] Catalog relationship detection failed for '{setting.Id}' after {detectStopwatch.ElapsedMilliseconds}ms: {ex.Message}");
            return;
        }

        // The other half of the scope story: how many settings the resolvers can actually reach, and what
        // reading their current state cost.
        logService.Log(LogLevel.Debug,
            $"[SettingApplicationService] Relationship scope for '{setting.Id}': {scope.Count} related settings, detected in {detectStopwatch.ElapsedMilliseconds}ms");

        string? currentStateOf(string id) =>
            detected != null && detected.TryGetValue(id, out var r) ? r.StateLabel : null;

        // Each reverse resolver gets the candidate list that passes ITS OWN first gate rather than the whole
        // catalog. Every setting left out would have been dropped by that resolver's very next line, so the
        // actions returned are identical - this narrows the loop, never the behaviour.
        var fwd = RelationshipResolver.ResolveForward(setting, targetLabel, currentStateOf);
        var sync = RelationshipResolver.ResolveReverseSync(setting.Id, syncParents, currentStateOf);
        var cascade = RelationshipResolver.ResolveReverseCascade(setting.Id, targetLabel, cascadeDependents, currentStateOf, CurrentBuild());

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

    // A follow-on is always a LEAF. Null (logged) when the target is missing or has no state with that label - a bad
    // relationship is skipped, never thrown. State index == ComboBox option index by construction.
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

    private void LogChangeHistory(Setting setting, string settingId, bool enable, object? value, string? beforeDisplay)
    {
        try
        {
            var name = ResolveLocalized(SettingLocalizationKeys.Name(setting)) ?? setting.Display.Name;
            var group = ResolveLocalizedGroup(setting.Display.GroupName);

            if (setting.Control == ControlKind.Action)
            {
                changeHistory.LogSettingAction(name, group);
                return;
            }

            // Same read as the before-capture block above. The service caches, so before and after
            // cannot disagree; unknown renders both components.
            var hasBattery = hardwareDetectionService.HasBattery() ?? true;
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

    private string? ResolveLocalized(string key) =>
        localizationService.TryGetString(key, out var value) ? value : null;

    // Mirrors SettingLocalizationService: a DisplayName that is itself a key (Template_* / PowerPlan_*) is localized
    // verbatim; otherwise Setting_{id}_Option_{index}, with the raw DisplayName as the final fallback.
    private string GetOptionLabel(Setting setting, int index)
    {
        if (index < 0 || index >= setting.States.Count)
            return ResolveLocalized(SettingLocalizationKeys.CommonCustomState) ?? "Custom";

        var dn = setting.States[index].Label;
        var key = SettingLocalizationKeys.IsLocalizationKey(dn)
            ? dn
            : SettingLocalizationKeys.OptionDisplay(setting, index);
        return ResolveLocalized(key) ?? dn;
    }

    // A JSON-sourced numeric may box as long or double.
    private static int? TryToInt(object? value)
    {
        if (value == null) return null;
        try { return Convert.ToInt32(value); }
        catch { return null; }
    }

    private string FormatStateDisplay(Setting setting, bool enable, object? value, bool hasBattery)
    {
        switch (setting.Control)
        {
            // A power-plan setting's catalog Control (PowerPlan) routes here alongside Selection (same
            // dict/index shapes).
            case ControlKind.Selection:
            case ControlKind.PowerPlan:
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
                    if (dict.TryGetValue("ACValue", out var acRaw) && dict.TryGetValue("DCValue", out var dcRaw))
                    {
                        var acInt = TryToInt(acRaw);
                        var dcInt = TryToInt(dcRaw);
                        if (acInt.HasValue && dcInt.HasValue)
                            return ComposeAcDc(GetOptionLabel(setting, acInt.Value), GetOptionLabel(setting, dcInt.Value), hasBattery);
                    }

                    return string.Join(", ", dict.Select(kv => $"{kv.Key}={kv.Value}"));
                }
                return value?.ToString() ?? ResolveLocalized(SettingLocalizationKeys.CommonCustomState) ?? "?";

            case ControlKind.Slider:
                // After-values are display units (the config bridge converts on import; UI/recommended paths already
                // supply display units) — render as-is, with unit suffix when available.
                if (value is Dictionary<string, object?> acdcNum
                    && acdcNum.TryGetValue("ACValue", out var acNum)
                    && acdcNum.TryGetValue("DCValue", out var dcNum)
                    && setting.Targets.OfType<PowerCfgTarget>().Any())
                {
                    var units = RecommendedSettingsResolver.GetPowerCfgDisplayUnits(setting);
                    return FormatPowerNumeric(units, acNum, dcNum, hasBattery);
                }
                if (value is Dictionary<string, object?> acdcNumPlain
                    && acdcNumPlain.TryGetValue("ACValue", out var acNumPlain)
                    && acdcNumPlain.TryGetValue("DCValue", out var dcNumPlain))
                    return ComposeAcDc(acNumPlain?.ToString() ?? "", dcNumPlain?.ToString() ?? "", hasBattery);
                return value?.ToString() ?? ResolveLocalized(SettingLocalizationKeys.CommonCustomState) ?? "?";

            default: // Toggle (Action is handled in LogChangeHistory before this is reached)
                return localizationService.GetString(
                    enable ? "Template_EnabledDisabled_Option_1" : "Template_EnabledDisabled_Option_0");
        }
    }

    // For PowerCfg Separate settings CurrentValue isn't a usable AC/DC pair: the typed AcValue/DcValue are SYSTEM
    // units, converted here to display units so "before" matches the "after" rendering byte-for-byte (which keeps
    // no-op detection working). On battery-less machines the DC component is omitted so before and after agree.
    private string FormatBeforeDisplay(Setting setting, SettingStateResult state, bool hasBattery)
    {
        int? acInt = state.AcValue;
        int? dcInt = state.DcValue;

        if (setting.Control == ControlKind.Slider
            && setting.Targets.OfType<PowerCfgTarget>().Any()
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
        if (setting.Control == ControlKind.Selection
            && setting.Targets.OfType<PowerCfgTarget>().Any()
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

    // On battery-less machines only AC is shown - the DC half is never written by PowerCfgApplier there, so
    // rendering it would be a phantom.
    private static string ComposeAcDc(string ac, string dc, bool hasBattery) =>
        hasBattery ? $"AC: {ac}, DC: {dc}" : $"AC: {ac}";

    private string FormatPowerNumeric(string? units, object? ac, object? dc, bool hasBattery)
    {
        var localizedUnit = LocalizeUnit(units);
        if (string.IsNullOrEmpty(localizedUnit))
            return ComposeAcDc(ac?.ToString() ?? "", dc?.ToString() ?? "", hasBattery);
        return ComposeAcDc($"{ac} {localizedUnit}", $"{dc} {localizedUnit}", hasBattery);
    }

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
