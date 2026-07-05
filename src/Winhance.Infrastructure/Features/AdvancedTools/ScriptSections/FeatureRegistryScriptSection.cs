using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using static Winhance.Infrastructure.Features.AdvancedTools.Helpers.PowerShellScriptUtilities;

namespace Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;

/// <summary>
/// Emits registry entries for feature groups (Optimize/Customize), scheduled tasks,
/// wallpaper settings, and Windows Update disabled mode hardening.
/// </summary>
internal class FeatureRegistryScriptSection
{
    private readonly RegistryCommandEmitter _registryEmitter;
    private readonly ILogService _logService;

    public FeatureRegistryScriptSection(RegistryCommandEmitter registryEmitter, ILogService logService)
    {
        _registryEmitter = registryEmitter;
        _logService = logService;
    }

    public void AppendFeatureGroupRegistryEntries(
        StringBuilder sb,
        FeatureGroupSection featureGroup,
        IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> allSettings,
        string groupName,
        bool isHkcu,
        string indent,
        WinBuild? build = null)
    {
        foreach (var featureKvp in featureGroup.Features)
        {
            var featureId = featureKvp.Key;
            var configSection = featureKvp.Value;

            if (!allSettings.TryGetValue(featureId, out var settingDefinitions))
            {
                _logService.Log(LogLevel.Warning, $"Could not find SettingDefinitions for feature: {featureId}");
                continue;
            }

            bool hasEntriesForCurrentHive = false;
            foreach (var configItem in configSection.Items)
            {
                var settingDef = settingDefinitions.FirstOrDefault(s => s.Id == configItem.Id);
                if (settingDef == null) continue;

                if (settingDef.Id == SettingIds.PowerPlanSelection) continue;

                // Slice E1b: presence gating reads the catalog Setting (Targets/Effects) instead of the def's
                // mechanism lists, paired alias-safely via SettingCatalog.Find. It errs toward OVER-reporting so a
                // header is emitted whenever the emit could produce content (content is never dropped by a skipped
                // header): for a paired setting the catalog check is proven equal to the old def check over the whole
                // population (MechanismPresenceEquivalenceTests); an unpaired setting (Find null - none in production,
                // the equivalence test asserts zero unpaired) falls back to the def check, mirroring the registry/
                // script emit's own def fallback below (the scheduled-task emit is catalog-only since E1a, so a def
                // fallback there could only over-report a harmless empty header for a never-occurring unpaired task).
                var catalog = SettingCatalog.Find(settingDef.Id);
                bool hasRegistry = catalog != null
                    ? AutounattendMechanismPresence.HasRegistryInHive(catalog, isHkcu)
                    : AutounattendMechanismPresence.HasRegistryInHive(settingDef, isHkcu);
                bool hasTask = catalog != null
                    ? AutounattendMechanismPresence.HasScheduledTask(catalog)
                    : AutounattendMechanismPresence.HasScheduledTask(settingDef);
                bool hasScript = catalog != null
                    ? AutounattendMechanismPresence.HasScriptInHive(catalog, isHkcu)
                    : AutounattendMechanismPresence.HasScriptInHive(settingDef, isHkcu);

                if (hasRegistry || (!isHkcu && hasTask) || hasScript)
                    hasEntriesForCurrentHive = true;

                if (!isHkcu && settingDef.Id == "power-hibernation-enable")
                {
                    hasEntriesForCurrentHive = true;
                }

                if (hasEntriesForCurrentHive) break;
            }

            if (!hasEntriesForCurrentHive) continue;

            // Get the feature display name for the section header
            var featureDisplayName = GetFeatureDisplayName(featureId);

            sb.AppendLine();
            sb.AppendLine($"{indent}# ============================================================================");
            sb.AppendLine($"{indent}# {featureDisplayName.ToUpper()}");
            sb.AppendLine($"{indent}# ============================================================================");
            sb.AppendLine();

            // Process each setting in the feature
            foreach (var configItem in configSection.Items)
            {
                var settingDef = settingDefinitions.FirstOrDefault(s => s.Id == configItem.Id);
                if (settingDef == null)
                {
                    _logService.Log(LogLevel.Warning, $"Could not find SettingDefinition for: {configItem.Id}");
                    continue;
                }

                // Skip settings that have PowerCfgSettings but no RegistrySettings (already handled in Power Settings
                // section). Slice E1b: read presence off the catalog Setting (PowerCfgTarget vs RegTarget/
                // RegistryWriteEffect) when paired, else the old def (mirroring the emit routing); the catalog check
                // is proven equal to the old def-based check by MechanismPresenceEquivalenceTests.
                var catalogForSkip = SettingCatalog.Find(settingDef.Id);
                bool powerCfgOnly = catalogForSkip != null
                    ? AutounattendMechanismPresence.HasPowerCfg(catalogForSkip) && !AutounattendMechanismPresence.HasRegistry(catalogForSkip)
                    : AutounattendMechanismPresence.HasPowerCfg(settingDef) && !AutounattendMechanismPresence.HasRegistry(settingDef);
                if (powerCfgOnly)
                    continue;

                // Set when a paired Action setting was emitted (registry + scripts) through the new catalog Effects
                // emitter below, so the shared PowerShell-script block does not double-emit its scripts.
                bool actionHandledByCatalog = false;

                // Apply the setting, but only output registry entries that match the current hive
                if (configItem.InputType == InputType.Toggle)
                {
                    // Phase 6.8 F2b: route paired, NON-build-gated toggles through the new catalog emitter
                    // (AppendToggleCommandsFromCatalog - proven command-multiset-equivalent to the old emitter by
                    // ScriptGenToggleEquivalenceTests). Build-gated (OS-merged "This PC") toggles and unpaired settings
                    // fall back to the old emitter, which has the OS-filtered def (the new method has no build context to
                    // pick the per-OS target). The new method emits ONLY registry targets, so the RegContents tail is
                    // emitted explicitly here via AppendRegContentCommandsFromCatalog (F2c, off the active state's
                    // RegContentEffects) - a no-op for toggles without RegContent effects, so no guard is needed.
                    // Phase 6.8 tail: build-gated (OS-merged "This PC folder") toggles now route through the new
                    // catalog emitter too, with the live build threaded so AppendToggleCommandsFromCatalog picks the
                    // OS-appropriate target (Win11 HiddenByDefault vs Win10 KeyExists). They go to the new path ONLY
                    // when a build is available; without one (a unit test feeding no build) they fall back to the old
                    // OS-filtered-def emitter, preserving prior behaviour. Non-build-gated paired toggles always use
                    // the new path (no AppliesTo to filter). Proven per-OS by ScriptGenBuildGatedToggleEquivalenceTests.
                    var catalogToggle = SettingCatalog.All.FirstOrDefault(s => s.Id == settingDef.Id);
                    bool isBuildGated = catalogToggle != null && catalogToggle.Targets.Any(t => t.AppliesTo.Count > 0);
                    if (catalogToggle != null && (!isBuildGated || build is not null))
                    {
                        _registryEmitter.AppendToggleCommandsFromCatalog(sb, catalogToggle, settingDef, configItem, isHkcu, indent, build);
                        _registryEmitter.AppendRegContentCommandsFromCatalog(sb, catalogToggle, configItem.IsSelected, isHkcu, indent);
                    }
                    else
                    {
                        _registryEmitter.AppendToggleCommandsFiltered(sb, settingDef, configItem, isHkcu, indent);
                    }
                }
                else if (configItem.InputType == InputType.Selection)
                {
                    _registryEmitter.AppendSelectionCommandsFiltered(sb, settingDef, configItem, isHkcu, indent);
                }
                else if (configItem.InputType == InputType.Action)
                {
                    // Action settings are one-shot "apply" — only emit when the user actually
                    // selected them. Unlike Toggle, an unselected Action has no "disabled"
                    // semantic; we must not emit a DisabledValue write (which would delete
                    // the key the action would have set).
                    // Phase 6.8 tail: route paired Action settings (registry writes AND scripts) through the new
                    // catalog Effects emitter. AppendActionCommandsFromCatalog guards IsSelected internally and emits
                    // both passes byte-equivalently to the old AppendToggleCommandsFiltered + AppendPowerShellScripts
                    // (ScriptGenActionEquivalenceTests). The shared script block below is skipped for this item so its
                    // scripts are not double-emitted. All three Action settings are catalog-paired; an unpaired Action
                    // falls back to the old registry emit (when selected) + the shared script block.
                    var catalogAction = SettingCatalog.All.FirstOrDefault(s => s.Id == settingDef.Id);
                    if (catalogAction != null)
                    {
                        AppendActionCommandsFromCatalog(sb, catalogAction, configItem, isHkcu, indent);
                        actionHandledByCatalog = true;
                    }
                    else if (configItem.IsSelected == true)
                    {
                        _registryEmitter.AppendToggleCommandsFiltered(sb, settingDef, configItem, isHkcu, indent);
                    }
                }

                // Emit PowerShell scripts whose RunContext matches the current pass.
                // Phase 6.8 F3: route paired settings WITH states (toggle/selection) through the new catalog script
                // emitter (AppendPowerShellScriptsFromCatalog - proven byte-equivalent by
                // ScriptGenPowerShellEquivalenceTests). Action settings (Effects-modeled, no States) and unpaired
                // settings stay on the old emitter, which reads settingDef.PowerShellScripts. EXCEPTION: a Selection with
                // NO SelectedIndex (a "Custom" value matching no preset option) has no catalog state to resolve - the old
                // emitter's hasCustomState path emits the un-baked EnabledScript, which the new state-mirror cannot
                // reproduce - so route that case to the old emitter too (byte-faithful via AppendPowerShellScripts).
                // A paired Action already emitted its scripts via AppendActionCommandsFromCatalog above; don't re-emit.
                if (!actionHandledByCatalog)
                {
                    var catalogForScripts = SettingCatalog.All.FirstOrDefault(s => s.Id == settingDef.Id);
                    bool selectionWithoutIndex = configItem.InputType == InputType.Selection && !configItem.SelectedIndex.HasValue;
                    if (catalogForScripts != null && catalogForScripts.States.Count > 0 && !selectionWithoutIndex)
                        AppendPowerShellScriptsFromCatalog(sb, catalogForScripts, settingDef, configItem, isHkcu, indent);
                    else
                        AppendPowerShellScripts(sb, settingDef, configItem, isHkcu, indent);
                }

            }

            if (!isHkcu)
            {
                var scheduledTasksToApply = new List<(string TaskName, string Action, string Description)>();

                foreach (var configItem in configSection.Items)
                {
                    var settingDef = settingDefinitions.FirstOrDefault(s => s.Id == configItem.Id);

                    // Phase 6.8 Slice E1a: source the scheduled-task paths + description from the catalog Setting
                    // (TaskTarget + Display.Description) instead of settingDef.ScheduledTaskSettings + .Description.
                    // Pair alias-safely via SettingCatalog.Find. Proven command-equivalent to the old collection
                    // (CollectScheduledTasks) by ScriptGenScheduledTaskEquivalenceTests, which also asserts every
                    // scheduled-task setting is catalog-paired (zero unpaired).
                    var catalogForTasks = settingDef != null ? SettingCatalog.Find(settingDef.Id) : null;
                    if (catalogForTasks != null)
                    {
                        scheduledTasksToApply.AddRange(CollectScheduledTasksFromCatalog(catalogForTasks, configItem));
                    }

                    if (settingDef?.Id == "power-hibernation-enable")
                    {
                        var hibernateState = configItem.IsSelected == true ? "on" : "off";
                        sb.AppendLine();
                        sb.AppendLine($"{indent}Write-Log \"Setting hibernation to {hibernateState}...\" \"INFO\"");
                        sb.AppendLine($"{indent}powercfg /hibernate {hibernateState} 2>$null");
                        sb.AppendLine($"{indent}Write-Log \"Hibernation set to {hibernateState}\" \"SUCCESS\"");
                    }
                }

                if (scheduledTasksToApply.Any())
                {
                    AppendScheduledTaskBatch(sb, scheduledTasksToApply, indent);
                }
            }

            if (featureId == FeatureIds.WindowsTheme && isHkcu)
            {
                AppendWallpaperSetting(sb, indent);
            }

            if (featureId == FeatureIds.Update && !isHkcu)
            {
                var updatePolicySetting = configSection.Items.FirstOrDefault(i => i.Id == SettingIds.UpdatesPolicyMode);
                if (updatePolicySetting?.SelectedIndex == 3)
                {
                    AppendWindowsUpdateDisabledModeLogic(sb, indent);
                }
            }
        }
    }

    public string GetFeatureDisplayName(string featureId)
    {
        var definition = FeatureDefinitions.Get(featureId);
        return definition != null ? $"{definition.DefaultName} Settings" : $"{featureId} Settings";
    }

    /// <summary>Emits the PowerShell-script blocks for a setting whose RunContext matches the current hive pass.
    /// Behaviour-preserving extraction of the old in-loop block (Phase 6.8 F3): scripts are sourced from
    /// <see cref="SettingDefinition.PowerShellScripts"/> and the old ComboBox options. The new-catalog mirror is
    /// <see cref="AppendPowerShellScriptsFromCatalog"/>, which is proven byte-equivalent by
    /// ScriptGenPowerShellEquivalenceTests.</summary>
    internal void AppendPowerShellScripts(
        StringBuilder sb,
        SettingDefinition settingDef,
        ConfigurationItem configItem,
        bool isHkcu,
        string indent)
    {
        if (settingDef.PowerShellScripts?.Count > 0)
        {
            foreach (var scriptSetting in settingDef.PowerShellScripts)
            {
                bool scriptIsUser = scriptSetting.RunContext == RunContext.User;
                if (scriptIsUser != isHkcu)
                {
                    continue;
                }

                // Custom state (user-entered values) always counts as "enabled" - the user
                // picking Custom DNS is expressing intent to configure, not to reset.
                bool hasCustomState = configItem.CustomStateValues?.Any() == true;
                var useEnabled = hasCustomState || configItem.IsSelected == true;

                if (!hasCustomState
                    && settingDef.InputType == InputType.Selection
                    && settingDef.ComboBox?.Options is { } selScriptOptions
                    && configItem.SelectedIndex.HasValue
                    && configItem.SelectedIndex.Value >= 0
                    && configItem.SelectedIndex.Value < selScriptOptions.Count
                    && selScriptOptions[configItem.SelectedIndex.Value].Script is { } scriptOption)
                {
                    // A "None" option applies no script - emit nothing into the
                    // autounattend for this selection (e.g. Custom / leave-alone).
                    if (scriptOption == ScriptOption.None)
                    {
                        continue;
                    }

                    useEnabled = scriptOption == ScriptOption.Enabled;
                }

                var script = useEnabled ? scriptSetting.EnabledScript : scriptSetting.DisabledScript;

                // Placeholder substitution. Merge sources with CustomStateValues winning
                // so a user-entered "Custom" selection overrides any preset option.
                if (!string.IsNullOrEmpty(script))
                {
                    var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    if (settingDef.ComboBox?.Options is { } selVarOptions
                        && configItem.SelectedIndex.HasValue
                        && configItem.SelectedIndex.Value >= 0
                        && configItem.SelectedIndex.Value < selVarOptions.Count
                        && selVarOptions[configItem.SelectedIndex.Value].ScriptVariables is { } variables)
                    {
                        foreach (var kvp in variables)
                        {
                            placeholders[kvp.Key] = kvp.Value;
                        }
                    }

                    if (configItem.CustomStateValues is { } customValues)
                    {
                        foreach (var kvp in customValues)
                        {
                            if (kvp.Value != null)
                            {
                                placeholders[kvp.Key] = kvp.Value.ToString() ?? string.Empty;
                            }
                        }
                    }

                    foreach (var kvp in placeholders)
                    {
                        script = script.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
                    }
                }

                if (!string.IsNullOrEmpty(script))
                {
                    var escapedDescription = EscapePowerShellString(settingDef.Description);
                    sb.AppendLine();
                    sb.AppendLine($"{indent}# PowerShell script for: {settingDef.Name}");
                    sb.AppendLine($"{indent}try {{");
                    foreach (var line in script.Split('\n'))
                    {
                        var trimmedLine = line.Trim();
                        if (!string.IsNullOrEmpty(trimmedLine))
                        {
                            sb.AppendLine($"{indent}    {trimmedLine}");
                        }
                    }
                    sb.AppendLine($"{indent}    Write-Log \"{escapedDescription}\" \"SUCCESS\"");
                    sb.AppendLine($"{indent}}} catch {{");
                    sb.AppendLine($"{indent}    Write-Log \"Failed: {escapedDescription} - $($_.Exception.Message)\" \"ERROR\"");
                    sb.AppendLine($"{indent}}}");
                    sb.AppendLine();
                }
            }
        }
    }

    /// <summary>New-catalog mirror of <see cref="AppendPowerShellScripts"/> (Phase 6.8 F3). Emits byte-identical
    /// output, sourcing the script bodies from the catalog Setting's active <see cref="SettingState"/> ScriptEffects
    /// instead of <see cref="SettingDefinition.PowerShellScripts"/> / the old ComboBox. The converter has already
    /// baked each option's preset ScriptVariables into <see cref="ScriptEffect.Script"/> and placed the correct
    /// Enabled/Disabled/None script on the right state, so only the runtime CustomStateValues pass is re-applied here.
    /// LIMITATION: a Selection whose <c>SelectedIndex</c> is null (a "Custom" value matching no preset option) has no
    /// catalog state to resolve, so this emits nothing - whereas the old emitter's hasCustomState path emits the
    /// un-baked EnabledScript. The production loop therefore routes Selection-without-index back to
    /// <see cref="AppendPowerShellScripts"/> rather than here. Equivalence (for the routed-here cases) is pinned by
    /// ScriptGenPowerShellEquivalenceTests.</summary>
    internal void AppendPowerShellScriptsFromCatalog(
        StringBuilder sb,
        Winhance.Core.Features.Common.Catalog.Setting catalogSetting,
        SettingDefinition settingDef,
        ConfigurationItem configItem,
        bool isHkcu,
        string indent)
    {
        // Resolve the state whose ScriptEffects this pass should emit. A Selection keys off SelectedIndex; a
        // toggle/action keys off the Enabled/Disabled state (Custom values count as "enabled", as in the old loop).
        SettingState? activeState;
        if (settingDef.InputType == InputType.Selection
            && configItem.SelectedIndex.HasValue
            && configItem.SelectedIndex.Value >= 0
            && configItem.SelectedIndex.Value < catalogSetting.States.Count)
        {
            activeState = catalogSetting.States[configItem.SelectedIndex.Value];
        }
        else
        {
            var useEnabled = configItem.IsSelected == true || configItem.CustomStateValues?.Any() == true;
            var targetLabel = useEnabled ? "Enabled" : "Disabled";
            activeState = catalogSetting.States.FirstOrDefault(s => s.Label == targetLabel);
        }

        if (activeState is null)
        {
            return;
        }

        foreach (var scriptEffect in activeState.Effects.OfType<ScriptEffect>())
        {
            // Same User->HKCU / System->HKLM mapping as the old PowerShellScripts loop.
            if ((scriptEffect.Run == RunContext.User) != isHkcu)
            {
                continue;
            }

            var script = scriptEffect.Script;

            // Runtime CustomStateValues substitution only (the old code's SECOND placeholder pass). The option's
            // preset ScriptVariables are already baked into ScriptEffect.Script by the converter.
            if (!string.IsNullOrEmpty(script) && configItem.CustomStateValues is { } customValues)
            {
                foreach (var kvp in customValues)
                {
                    if (kvp.Value != null)
                    {
                        script = script.Replace($"{{{{{kvp.Key}}}}}", kvp.Value.ToString() ?? string.Empty);
                    }
                }
            }

            if (!string.IsNullOrEmpty(script))
            {
                var escapedDescription = EscapePowerShellString(catalogSetting.Display.Description);
                sb.AppendLine();
                sb.AppendLine($"{indent}# PowerShell script for: {catalogSetting.Display.Name}");
                sb.AppendLine($"{indent}try {{");
                foreach (var line in script.Split('\n'))
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine))
                    {
                        sb.AppendLine($"{indent}    {trimmedLine}");
                    }
                }
                sb.AppendLine($"{indent}    Write-Log \"{escapedDescription}\" \"SUCCESS\"");
                sb.AppendLine($"{indent}}} catch {{");
                sb.AppendLine($"{indent}    Write-Log \"Failed: {escapedDescription} - $($_.Exception.Message)\" \"ERROR\"");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }
        }
    }

    /// <summary>Phase 6.8 script-gen tail: new-catalog mirror of the OLD Action emission. An Action setting routes
    /// through AppendToggleCommandsFiltered (registry, only when IsSelected) + AppendPowerShellScripts (scripts) in
    /// the old loop; the catalog models it as SETTING-level Effects (no States/Targets). This emits the same bytes,
    /// sourcing the registry writes from RegistryWriteEffects and the scripts from ScriptEffects. ORDER matches the
    /// old loop: the registry pass (AppendToggleCommandsFiltered, which for these settings hits only the plain
    /// Set-RegistryValue branch) runs before the script pass (AppendPowerShellScripts). Emits nothing unless the
    /// Action is selected (matching the old Action branch's IsSelected guard). The Action population is
    /// RegistryWriteEffect/ScriptEffect-only (asserted by ScriptGenActionEquivalenceTests). Proven byte-equivalent by
    /// ScriptGenActionEquivalenceTests.</summary>
    internal void AppendActionCommandsFromCatalog(
        StringBuilder sb,
        Winhance.Core.Features.Common.Catalog.Setting catalogSetting,
        ConfigurationItem configItem,
        bool isHkcu,
        string indent)
    {
        // Action one-shot: emit only when the user selected it (matches the old Action branch's IsSelected == true guard).
        if (configItem.IsSelected != true)
            return;

        // Registry pass first (mirrors the old loop: AppendToggleCommandsFiltered runs before AppendPowerShellScripts).
        _registryEmitter.AppendActionRegistryCommandsFromCatalog(sb, catalogSetting, isHkcu, indent);

        // Script pass (mirrors AppendPowerShellScripts for an enabled Action). Setting-level ScriptEffects.
        AppendActionScriptsFromCatalog(sb, catalogSetting, configItem, isHkcu, indent);
    }

    /// <summary>Emits the PowerShell-script blocks for an Action setting's setting-level ScriptEffects whose RunContext
    /// matches the current hive pass. Byte-identical to <see cref="AppendPowerShellScripts"/> for an enabled Action:
    /// the converter copies each old PowerShellScript.EnabledScript verbatim into a ScriptEffect, and an Action has no
    /// ComboBox options, so the only placeholder pass that applies is the runtime CustomStateValues substitution
    /// (mirroring the old code's CustomStateValues merge; an Action's ScriptVariables source does not exist).</summary>
    internal void AppendActionScriptsFromCatalog(
        StringBuilder sb,
        Winhance.Core.Features.Common.Catalog.Setting catalogSetting,
        ConfigurationItem configItem,
        bool isHkcu,
        string indent)
    {
        foreach (var scriptEffect in catalogSetting.Effects.OfType<ScriptEffect>())
        {
            // Same User->HKCU / System->HKLM mapping as the old PowerShellScripts loop.
            if ((scriptEffect.Run == RunContext.User) != isHkcu)
                continue;

            var script = scriptEffect.Script;

            // Runtime CustomStateValues substitution only (the old code's placeholder pass; an Action has no
            // ComboBox-option ScriptVariables, so that source is absent).
            if (!string.IsNullOrEmpty(script) && configItem.CustomStateValues is { } customValues)
            {
                foreach (var kvp in customValues)
                {
                    if (kvp.Value != null)
                    {
                        script = script.Replace($"{{{{{kvp.Key}}}}}", kvp.Value.ToString() ?? string.Empty);
                    }
                }
            }

            if (!string.IsNullOrEmpty(script))
            {
                var escapedDescription = EscapePowerShellString(catalogSetting.Display.Description);
                sb.AppendLine();
                sb.AppendLine($"{indent}# PowerShell script for: {catalogSetting.Display.Name}");
                sb.AppendLine($"{indent}try {{");
                foreach (var line in script.Split('\n'))
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine))
                    {
                        sb.AppendLine($"{indent}    {trimmedLine}");
                    }
                }
                sb.AppendLine($"{indent}    Write-Log \"{escapedDescription}\" \"SUCCESS\"");
                sb.AppendLine($"{indent}}} catch {{");
                sb.AppendLine($"{indent}    Write-Log \"Failed: {escapedDescription} - $($_.Exception.Message)\" \"ERROR\"");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }
        }
    }

    /// <summary>Collects a setting's scheduled-task apply tuples (TaskPath, /Enable|/Disable, Description) from the
    /// OLD <see cref="SettingDefinition.ScheduledTaskSettings"/> + Description. Behaviour-preserving extraction of the
    /// old in-loop block (Phase 6.8 Slice E1a). The new-catalog mirror is
    /// <see cref="CollectScheduledTasksFromCatalog"/>, proven command-equivalent by
    /// ScriptGenScheduledTaskEquivalenceTests.</summary>
    internal IEnumerable<(string TaskName, string Action, string Description)> CollectScheduledTasks(
        SettingDefinition settingDef, ConfigurationItem configItem)
    {
        if (settingDef.ScheduledTaskSettings?.Count > 0)
        {
            foreach (var taskSetting in settingDef.ScheduledTaskSettings)
            {
                var action = configItem.IsSelected == true ? "/Enable" : "/Disable";
                yield return (taskSetting.TaskPath, action, settingDef.Description);
            }
        }
    }

    /// <summary>New-catalog mirror of <see cref="CollectScheduledTasks"/> (Phase 6.8 Slice E1a). Yields the same
    /// (TaskPath, /Enable|/Disable, Description) tuples, sourcing the task paths from the catalog Setting's
    /// <see cref="TaskTarget"/>s (one per scheduled task) and the description from <see cref="Display.Description"/>
    /// instead of <see cref="SettingDefinition.ScheduledTaskSettings"/> / Description. A setting with no scheduled
    /// tasks has no TaskTargets, so this yields nothing. Proven command-equivalent by
    /// ScriptGenScheduledTaskEquivalenceTests.</summary>
    internal IEnumerable<(string TaskName, string Action, string Description)> CollectScheduledTasksFromCatalog(
        Winhance.Core.Features.Common.Catalog.Setting catalogSetting, ConfigurationItem configItem)
    {
        foreach (var taskTarget in catalogSetting.Targets.OfType<TaskTarget>())
        {
            var action = configItem.IsSelected == true ? "/Enable" : "/Disable";
            yield return (taskTarget.TaskPath, action, catalogSetting.Display.Description);
        }
    }

    internal void AppendScheduledTaskBatch(StringBuilder sb, List<(string TaskName, string Action, string Description)> tasks, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}$scheduledTasks = @(");

        for (int i = 0; i < tasks.Count; i++)
        {
            var (taskName, action, description) = tasks[i];
            var escapedTaskName = EscapePowerShellString(taskName);
            var escapedDescription = EscapePowerShellString(description);
            var comma = i < tasks.Count - 1 ? "," : "";

            sb.AppendLine($"{indent}    @{{ TN=\"{escapedTaskName}\"; Action=\"{action}\"; Desc=\"{escapedDescription}\" }}{comma}");
        }

        sb.AppendLine($"{indent})");
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Applying scheduled task settings...\" \"INFO\"");
        sb.AppendLine($"{indent}$processedCount = 0");
        sb.AppendLine($"{indent}foreach ($task in $scheduledTasks) {{");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        $result = & cmd.exe /c \"schtasks /Change /TN `\"$($task.TN)`\" $($task.Action)\" 2>&1");
        sb.AppendLine($"{indent}        if ($LASTEXITCODE -eq 0) {{");
        sb.AppendLine($"{indent}            Write-Log \"$($task.Desc)\" \"SUCCESS\"");
        sb.AppendLine($"{indent}            $processedCount++");
        sb.AppendLine($"{indent}        }} else {{");
        sb.AppendLine($"{indent}            Write-Log \"Task command failed for: $($task.Desc)\" \"WARNING\"");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to process task: $($task.Desc) - $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine($"{indent}Write-Log \"Processed $processedCount scheduled task settings\" \"SUCCESS\"");
        sb.AppendLine();
    }

    private void AppendWallpaperSetting(StringBuilder sb, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Setting wallpaper based on Windows version and theme...\" \"INFO\"");
        sb.AppendLine($"{indent}$buildNumber = [System.Environment]::OSVersion.Version.Build");
        sb.AppendLine($"{indent}$wallpaperPath = $null");
        sb.AppendLine();
        sb.AppendLine($"{indent}if ($buildNumber -ge 22000) {{");
        sb.AppendLine($"{indent}    $themeKey = 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize'");
        sb.AppendLine($"{indent}    $lightTheme = $false");
        sb.AppendLine();
        sb.AppendLine($"{indent}    if (Test-Path $themeKey) {{");
        sb.AppendLine($"{indent}        $value = Get-ItemProperty -Path $themeKey -Name 'SystemUsesLightTheme' -ErrorAction SilentlyContinue");
        sb.AppendLine($"{indent}        if ($value.SystemUsesLightTheme -eq 1) {{");
        sb.AppendLine($"{indent}            $lightTheme = $true");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    if ($lightTheme) {{");
        sb.AppendLine($"{indent}        $wallpaperPath = 'C:\\Windows\\Web\\Wallpaper\\Windows\\img0.jpg'");
        sb.AppendLine($"{indent}    }} else {{");
        sb.AppendLine($"{indent}        $wallpaperPath = 'C:\\Windows\\Web\\Wallpaper\\Windows\\img19.jpg'");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}} else {{");
        sb.AppendLine($"{indent}    $wallpaperPath = 'C:\\Windows\\Web\\4K\\Wallpaper\\Windows\\img0_3840x2160.jpg'");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent}if (-not (Test-Path $wallpaperPath)) {{");
        sb.AppendLine($"{indent}    Write-Log \"Wallpaper file not found: $wallpaperPath\" \"WARNING\"");
        sb.AppendLine($"{indent}}} else {{");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        $desktopKey = 'HKCU:\\Control Panel\\Desktop'");
        sb.AppendLine($"{indent}        Set-ItemProperty -Path $desktopKey -Name Wallpaper -Value $wallpaperPath -Type String -Force");
        sb.AppendLine($"{indent}        Set-ItemProperty -Path $desktopKey -Name WallpaperStyle -Value '10' -Type String -Force");
        sb.AppendLine($"{indent}        Set-ItemProperty -Path $desktopKey -Name TileWallpaper -Value '0' -Type String -Force");
        sb.AppendLine();
        sb.AppendLine($"{indent}        Remove-ItemProperty -Path $desktopKey -Name 'TranscodedImageCache' -ErrorAction SilentlyContinue");
        sb.AppendLine($"{indent}        Remove-ItemProperty -Path $desktopKey -Name 'TranscodedImageCache_000' -ErrorAction SilentlyContinue");
        sb.AppendLine();
        sb.AppendLine($"{indent}        Write-Log \"Wallpaper configured: $wallpaperPath\" \"SUCCESS\"");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to set wallpaper: $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private void AppendWindowsUpdateDisabledModeLogic(StringBuilder sb, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine($"{indent}# WINDOWS UPDATE DISABLED MODE - ADDITIONAL HARDENING - Based on work by Chris Titus: https://github.com/ChrisTitusTech/winutil/blob/main/functions/public/Invoke-WPFUpdatesdisable.ps1");
        sb.AppendLine($"{indent}# ============================================================================");
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Applying Windows Update Disabled mode hardening...\" \"INFO\"");
        sb.AppendLine();

        sb.AppendLine($"{indent}# Disable Windows Update services");
        sb.AppendLine($"{indent}$updateServices = @('wuauserv', 'UsoSvc', 'WaaSMedicSvc')");
        sb.AppendLine($"{indent}foreach ($service in $updateServices) {{");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        Write-Log \"Disabling service: $service\" \"INFO\"");
        sb.AppendLine($"{indent}        net stop $service 2>$null");
        sb.AppendLine($"{indent}        sc.exe config $service start= disabled 2>$null");
        sb.AppendLine($"{indent}        sc.exe failure $service reset= 0 actions= \"\" 2>$null");
        sb.AppendLine($"{indent}        Write-Log \"Disabled service: $service\" \"SUCCESS\"");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to disable $service : $($_.Exception.Message)\" \"WARNING\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();

        sb.AppendLine($"{indent}# Disable Windows Update scheduled tasks");
        sb.AppendLine($"{indent}$taskPaths = @(");
        sb.AppendLine($"{indent}    '\\Microsoft\\Windows\\InstallService\\*',");
        sb.AppendLine($"{indent}    '\\Microsoft\\Windows\\UpdateOrchestrator\\*',");
        sb.AppendLine($"{indent}    '\\Microsoft\\Windows\\UpdateAssistant\\*',");
        sb.AppendLine($"{indent}    '\\Microsoft\\Windows\\WaaSMedic\\*',");
        sb.AppendLine($"{indent}    '\\Microsoft\\Windows\\WindowsUpdate\\*'");
        sb.AppendLine($"{indent})");
        sb.AppendLine();
        sb.AppendLine($"{indent}foreach ($taskPath in $taskPaths) {{");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        $tasks = Get-ScheduledTask -TaskPath $taskPath -ErrorAction SilentlyContinue");
        sb.AppendLine($"{indent}        foreach ($task in $tasks) {{");
        sb.AppendLine($"{indent}            try {{");
        sb.AppendLine($"{indent}                Disable-ScheduledTask -TaskName $task.TaskName -TaskPath $task.TaskPath -ErrorAction Stop | Out-Null");
        sb.AppendLine($"{indent}                Write-Log \"Disabled task: $($task.TaskPath)$($task.TaskName)\" \"SUCCESS\"");
        sb.AppendLine($"{indent}            }} catch {{");
        sb.AppendLine($"{indent}                Write-Log \"Skipped task: $($task.TaskPath)$($task.TaskName)\" \"WARNING\"");
        sb.AppendLine($"{indent}            }}");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to process tasks in $taskPath : $($_.Exception.Message)\" \"WARNING\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();

        sb.AppendLine($"{indent}# Rename critical Windows Update DLLs");
        sb.AppendLine($"{indent}$updateDlls = @('WaaSMedicSvc.dll', 'wuaueng.dll')");
        sb.AppendLine($"{indent}foreach ($dll in $updateDlls) {{");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        $dllPath = \"C:\\Windows\\System32\\$dll\"");
        sb.AppendLine($"{indent}        $backupPath = \"C:\\Windows\\System32\\$($dll.Replace('.dll', '_BAK.dll'))\"");
        sb.AppendLine();
        sb.AppendLine($"{indent}        if ((Test-Path $dllPath) -and -not (Test-Path $backupPath)) {{");
        sb.AppendLine($"{indent}            Write-Log \"Renaming $dll to backup\" \"INFO\"");
        sb.AppendLine($"{indent}            takeown /f \"$dllPath\" 2>$null | Out-Null");
        sb.AppendLine($"{indent}            icacls \"$dllPath\" /grant *S-1-1-0:F 2>$null | Out-Null");
        sb.AppendLine($"{indent}            Move-Item -Path $dllPath -Destination $backupPath -Force -ErrorAction Stop");
        sb.AppendLine($"{indent}            Write-Log \"Renamed $dll to backup\" \"SUCCESS\"");
        sb.AppendLine($"{indent}        }} elseif (Test-Path $backupPath) {{");
        sb.AppendLine($"{indent}            Write-Log \"$dll already backed up\" \"INFO\"");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to rename $dll : $($_.Exception.Message)\" \"WARNING\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();

        sb.AppendLine($"{indent}# Cleanup SoftwareDistribution folder");
        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    $softwareDistPath = 'C:\\Windows\\SoftwareDistribution'");
        sb.AppendLine($"{indent}    if (Test-Path $softwareDistPath) {{");
        sb.AppendLine($"{indent}        Write-Log \"Cleaning SoftwareDistribution folder...\" \"INFO\"");
        sb.AppendLine($"{indent}        Remove-Item \"$softwareDistPath\\*\" -Recurse -Force -ErrorAction SilentlyContinue");
        sb.AppendLine($"{indent}        Write-Log \"SoftwareDistribution folder cleaned\" \"SUCCESS\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Failed to cleanup SoftwareDistribution: $($_.Exception.Message)\" \"WARNING\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();

        sb.AppendLine($"{indent}Write-Log \"Windows Update Disabled mode hardening completed\" \"SUCCESS\"");
        sb.AppendLine();
    }
}
