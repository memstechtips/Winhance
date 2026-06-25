using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>One comparison result for a single toggle setting: the existing app's detected state
/// vs the new catalog engine's, and whether they agree.</summary>
public sealed record EquivalenceRow(string Id, string OldState, string NewState, bool Match);

/// <summary>Throwaway migration tool: for each pure registry toggle, runs both the existing app's
/// detection (<see cref="IWindowsRegistryService.IsSettingApplied"/>) and the new engine's
/// (<see cref="CatalogDiscovery.DetectState"/>) against the live registry, so the two can be compared.
/// Deleted once the migration is complete.</summary>
public static class RegistryToggleEquivalenceHarness
{
    /// <summary>True when a toggle's DETECTION is registry-based. Apply-only effects (PowerShell scripts, .reg
    /// blobs, native-power API) are ALLOWED - per the design they ride along on apply and do not change
    /// detection, which always comes from the registry Set. Only non-registry DETECTION is excluded: a
    /// combobox, powercfg (deferred), a scheduled task, or a custom DetectionType.</summary>
    public static bool IsPureRegistryToggle(SettingDefinition def)
    {
        if (def.InputType != InputType.Toggle && def.InputType != InputType.CheckBox)
            return false;
        if (def.RegistrySettings.Count == 0)
            return false;
        if (def.ComboBox != null)
            return false;
        if (def.PowerCfgSettings is { Count: > 0 })
            return false;
        if (def.ScheduledTaskSettings.Count > 0)
            return false;
        if (def.DetectionType.HasValue)
            return false;
        return true;
    }

    /// <summary>True when a selection's (ComboBox) DETECTION is registry-based. Apply-only effects (PowerShell
    /// scripts, .reg blobs, native-power API) are ALLOWED - they do not change detection. The selection
    /// analogue of <see cref="IsPureRegistryToggle"/>.</summary>
    public static bool IsPureRegistrySelection(SettingDefinition def)
    {
        if (def.InputType != InputType.Selection)
            return false;
        if (def.ComboBox?.Options is not { Count: > 0 })
            return false;
        if (def.RegistrySettings.Count == 0)
            return false;
        if (def.PowerCfgSettings is { Count: > 0 })
            return false;
        if (def.ScheduledTaskSettings.Count > 0)
            return false;
        if (def.DetectionType.HasValue)
            return false;
        return true;
    }

    /// <summary>True when a selection's (ComboBox) DETECTION is powercfg-based: it carries powercfg settings
    /// AND a ComboBox (so it maps the AC value index to an option). Excludes the power-plan selection (which has
    /// no ComboBox) and anything with a registry/scheduled-task/custom detection mechanism. The powercfg
    /// analogue of <see cref="IsPureRegistrySelection"/>.</summary>
    public static bool IsPurePowerCfgSelection(SettingDefinition def)
    {
        if (def.InputType != InputType.Selection)
            return false;
        if (def.PowerCfgSettings is not { Count: > 0 })
            return false;
        if (def.ComboBox?.Options is not { Count: > 0 })
            return false; // excludes power-plan-selection (no ComboBox)
        if (def.RegistrySettings.Count > 0)
            return false;
        if (def.ScheduledTaskSettings.Count > 0)
            return false;
        if (def.DetectionType.HasValue)
            return false;
        return true;
    }

    /// <summary>True when a numeric (slider) setting's DETECTION is powercfg-based - it carries powercfg settings
    /// and no registry/scheduled-task/custom detection mechanism. A numeric has no ComboBox; its value IS the
    /// raw AC value index.</summary>
    public static bool IsPurePowerCfgNumeric(SettingDefinition def)
    {
        if (def.InputType != InputType.NumericRange)
            return false;
        if (def.PowerCfgSettings is not { Count: > 0 })
            return false;
        if (def.RegistrySettings.Count > 0)
            return false;
        if (def.ScheduledTaskSettings.Count > 0)
            return false;
        if (def.DetectionType.HasValue)
            return false;
        return true;
    }

    /// <summary>True when a definition is a pure scheduled-task toggle - a single ScheduledTaskSetting and no
    /// other mechanism (no registry, combobox, powercfg, script, .reg, native-power, custom detector).</summary>
    public static bool IsPureScheduledTaskToggle(SettingDefinition def)
    {
        if (def.InputType != InputType.Toggle && def.InputType != InputType.CheckBox)
            return false;
        if (def.ScheduledTaskSettings.Count == 0)
            return false;
        if (def.RegistrySettings.Count > 0)
            return false;
        if (def.ComboBox != null)
            return false;
        if (def.PowerCfgSettings is { Count: > 0 })
            return false;
        if (def.PowerShellScripts.Count > 0)
            return false;
        if (def.RegContents.Count > 0)
            return false;
        if (def.NativePowerApiSettings.Count > 0)
            return false;
        if (def.DetectionType.HasValue)
            return false;
        return true;
    }

    /// <summary>Builds one <see cref="EquivalenceRow"/> per supplied toggle definition. Callers should
    /// pre-filter with <see cref="IsPureRegistryToggle"/>; any non-toggle definitions passed in are
    /// skipped defensively.</summary>
    public static IReadOnlyList<EquivalenceRow> Run(
        IWindowsRegistryService reg,
        IEnumerable<SettingDefinition> toggleDefs)
    {
        var context = new WindowsDetectionContext(reg);
        var rows = new List<EquivalenceRow>();

        foreach (var def in toggleDefs)
        {
            if (!IsPureRegistryToggle(def))
                continue;

            // OLD: reproduce the existing app's real per-setting detection
            // (SystemSettingsDiscoveryService.DetermineIfSettingIsEnabled): per-NIC/per-monitor
            // settings expand sub-keys via IsSettingApplied; key-existence settings pass a bool
            // (does the key exist); everything else passes the raw value. A setting is enabled
            // when any of its registry settings is in the enabled state.
            bool oldEnabled = def.RegistrySettings.Any(rs =>
            {
                if (rs.ApplyPerNetworkInterface || rs.ApplyPerMonitor)
                    return reg.IsSettingApplied(rs);

                object? current = rs.ValueName == null
                    ? (object)reg.KeyExists(rs.KeyPath)              // key-existence toggles pass a bool
                    : reg.GetValue(rs.KeyPath, rs.ValueName);
                return reg.IsRegistryValueInEnabledState(rs, current, current != null);
            });
            string oldState = oldEnabled ? "Enabled" : "Disabled";

            // NEW: convert to the unified Setting model and run the new detection engine.
            var setting = SettingDefinitionConverter.ConvertToggle(def);
            string newState = CatalogDiscovery.DetectState(setting, context) ?? "Custom";

            rows.Add(new EquivalenceRow(def.Id, oldState, newState, oldState == newState));
        }

        return rows;
    }

    /// <summary>Builds one <see cref="EquivalenceRow"/> per supplied registry SELECTION definition. OLD is the
    /// app's real selection detection (<see cref="IComboBoxResolver.ResolveCurrentValueAsync"/> -> option
    /// index -> its DisplayName); NEW converts to the unified model and runs the engine. Callers should
    /// pre-filter with <see cref="IsPureRegistrySelection"/>; any other definitions are skipped defensively.</summary>
    public static async Task<IReadOnlyList<EquivalenceRow>> RunSelections(
        IWindowsRegistryService reg,
        IComboBoxResolver resolver,
        IEnumerable<SettingDefinition> selectionDefs)
    {
        var context = new WindowsDetectionContext(reg);
        var rows = new List<EquivalenceRow>();

        foreach (var def in selectionDefs)
        {
            if (!IsPureRegistrySelection(def))
                continue;

            // OLD: the app's real selection detection resolves the live registry to an option index.
            var resolved = await resolver.ResolveCurrentValueAsync(def).ConfigureAwait(false);
            int oldIndex = resolved is int idx ? idx : ComboBoxConstants.CustomStateIndex;
            string oldState = LabelForIndex(def, oldIndex);

            // NEW: convert to the unified Setting model and run the new detection engine.
            var setting = SettingDefinitionConverter.ConvertSelection(def);
            string newState = CatalogDiscovery.DetectState(setting, context) ?? "Custom";

            rows.Add(new EquivalenceRow(def.Id, oldState, newState, oldState == newState));
        }

        return rows;
    }

    /// <summary>Builds one <see cref="EquivalenceRow"/> per supplied powercfg SELECTION definition. OLD is the
    /// app's real selection detection (<see cref="IComboBoxResolver.ResolveCurrentValueAsync"/> resolves the live
    /// AC value index to an option index -> its DisplayName); NEW converts to the unified model and runs the
    /// engine over the same pre-fetched AC/DC value pair (canonical detection = the AC context). The harness reads
    /// the AC/DC pair async up front since the context API is synchronous. Callers should pre-filter with
    /// <see cref="IsPurePowerCfgSelection"/>.</summary>
    public static async Task<IReadOnlyList<EquivalenceRow>> RunPowerCfgSelections(
        IPowerSettingsQueryService powerQuery,
        IComboBoxResolver resolver,
        IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsPurePowerCfgSelection(def))
                continue;

            var (ac, dc) = await powerQuery.GetPowerSettingACDCValuesAsync(def.PowerCfgSettings![0]).ConfigureAwait(false);
            var ctx = new PowerCfgDetectionContext(ac, dc);

            // NEW: convert to the unified Setting model and run the new detection engine (AC = canonical).
            var setting = SettingDefinitionConverter.ConvertPowerCfg(def);
            string newState = CatalogDiscovery.DetectState(setting, ctx, PowerContext.AC) ?? "Custom";

            // OLD: the app's real selection detection resolves the live powercfg value to an option index.
            var resolved = await resolver.ResolveCurrentValueAsync(def).ConfigureAwait(false);
            int oldIndex = resolved is int idx ? idx : ComboBoxConstants.CustomStateIndex;
            string oldState = LabelForIndex(def, oldIndex);

            rows.Add(new EquivalenceRow(def.Id, oldState, newState, oldState == newState));
        }

        return rows;
    }

    /// <summary>Builds one <see cref="EquivalenceRow"/> per supplied powercfg NUMERIC (slider) definition. A
    /// numeric has no options, so it compares the raw AC value index (the old app's canonical detected value)
    /// against the engine's <see cref="CatalogDiscovery.DetectValue"/> over the same pre-fetched pair. A value
    /// that is not present reads as "absent" on both sides. Callers should pre-filter with
    /// <see cref="IsPurePowerCfgNumeric"/>.</summary>
    public static async Task<IReadOnlyList<EquivalenceRow>> RunPowerCfgNumerics(
        IPowerSettingsQueryService powerQuery,
        IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsPurePowerCfgNumeric(def))
                continue;

            var (ac, dc) = await powerQuery.GetPowerSettingACDCValuesAsync(def.PowerCfgSettings![0]).ConfigureAwait(false);
            var ctx = new PowerCfgDetectionContext(ac, dc);

            // NEW: convert to the unified Setting model and read the slider's value through the engine (AC).
            var setting = SettingDefinitionConverter.ConvertPowerCfg(def);
            int? newVal = CatalogDiscovery.DetectValue(setting, ctx, PowerContext.AC);

            // OLD: the old app's canonical detected value is the raw AC value index.
            string oldState = ac?.ToString() ?? "absent";
            string newState = newVal?.ToString() ?? "absent";

            rows.Add(new EquivalenceRow(def.Id, oldState, newState, oldState == newState));
        }

        return rows;
    }

    /// <summary>The DisplayName of the option at <paramref name="index"/>, or "Custom" for the custom-state
    /// index / any out-of-range index - so OLD and NEW are compared on the same label vocabulary.</summary>
    private static string LabelForIndex(SettingDefinition def, int index)
    {
        var options = def.ComboBox?.Options;
        if (options is null || index < 0 || index >= options.Count)
            return "Custom";
        return options[index].DisplayName;
    }

    /// <summary>Builds one <see cref="EquivalenceRow"/> per DNS-server selection (DetectionType.DnsServer).
    /// OLD is the app's real detection (DetectDnsServerIndex via <see cref="IComboBoxResolver.ResolveCurrentValueAsync"/>
    /// -> option index -> DisplayName); NEW runs the <see cref="DnsServerDetector"/> over the live adapter (the
    /// <see cref="DnsDetectionContext"/> reproduces the same active-adapter primary-DNS read). Other detection
    /// types are skipped defensively.</summary>
    public static async Task<IReadOnlyList<EquivalenceRow>> RunDns(
        IWindowsRegistryService reg,
        IComboBoxResolver resolver,
        IEnumerable<SettingDefinition> defs)
    {
        var context = new DnsDetectionContext(reg);
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (def.DetectionType != DetectionType.DnsServer)
                continue;

            // OLD: the app's real detection resolves the live adapter/registry to an option index.
            var resolved = await resolver.ResolveCurrentValueAsync(def).ConfigureAwait(false);
            int oldIndex = resolved is int idx ? idx : ComboBoxConstants.CustomStateIndex;
            string oldState = LabelForIndex(def, oldIndex);

            // NEW: convert to a detector-backed Setting and run the engine (which delegates to the detector).
            var setting = SettingDefinitionConverter.ConvertDnsServer(def);
            string newState = CatalogDiscovery.DetectState(setting, context) ?? "Custom";

            rows.Add(new EquivalenceRow(def.Id, oldState, newState, oldState == newState));
        }

        return rows;
    }

    /// <summary>Builds one <see cref="EquivalenceRow"/> per system-restore toggle
    /// (DetectionType.SystemRestore). OLD is the app's real detection (<see cref="ISystemRestoreService.IsEnabledForC"/>
    /// -> the toggle is Enabled iff System Restore is on for C:); NEW runs the <see cref="SystemRestoreDetector"/>
    /// over the same pre-fetched value. Definitions of other detection types are skipped defensively.</summary>
    public static IReadOnlyList<EquivalenceRow> RunSystemRestore(
        ISystemRestoreService restoreService,
        IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (def.DetectionType != DetectionType.SystemRestore)
                continue;

            bool enabled = restoreService.IsEnabledForC();
            string oldState = enabled ? "Enabled" : "Disabled";

            var setting = SettingDefinitionConverter.ConvertSystemRestore(def);
            string newState = CatalogDiscovery.DetectState(setting, new SystemRestoreDetectionContext(enabled)) ?? "Custom";

            rows.Add(new EquivalenceRow(def.Id, oldState, newState, oldState == newState));
        }

        return rows;
    }

    /// <summary>Builds one <see cref="EquivalenceRow"/> per system-tray-icons selection
    /// (DetectionType.SystemTrayIcons). OLD is the app's real detection (DetectSystemTrayIndex via
    /// <see cref="IComboBoxResolver.ResolveCurrentValueAsync"/> -> option index -> DisplayName); NEW runs the
    /// new <see cref="SystemTrayDetector"/> over the live registry. Definitions of other detection types are
    /// skipped defensively.</summary>
    public static async Task<IReadOnlyList<EquivalenceRow>> RunSystemTray(
        IWindowsRegistryService reg,
        IComboBoxResolver resolver,
        IEnumerable<SettingDefinition> defs)
    {
        var context = new WindowsDetectionContext(reg);
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (def.DetectionType != DetectionType.SystemTrayIcons)
                continue;

            // OLD: the app's real detection resolves the live registry to an option index.
            var resolved = await resolver.ResolveCurrentValueAsync(def).ConfigureAwait(false);
            int oldIndex = resolved is int idx ? idx : ComboBoxConstants.CustomStateIndex;
            string oldState = LabelForIndex(def, oldIndex);

            // NEW: convert to a detector-backed Setting and run the engine (which delegates to the detector).
            var setting = SettingDefinitionConverter.ConvertSystemTray(def);
            string newState = CatalogDiscovery.DetectState(setting, context) ?? "Custom";

            rows.Add(new EquivalenceRow(def.Id, oldState, newState, oldState == newState));
        }

        return rows;
    }

    /// <summary>Builds one <see cref="EquivalenceRow"/> per supplied scheduled-task toggle. OLD is the app's
    /// real detection (<see cref="IScheduledTaskService.IsTaskEnabledAsync"/> -> the toggle is on iff the task
    /// is enabled; an absent task makes the setting unavailable). NEW reads the same task state through the
    /// engine. Callers should pre-filter with <see cref="IsPureScheduledTaskToggle"/>.</summary>
    public static async Task<IReadOnlyList<EquivalenceRow>> RunScheduledTasks(
        IScheduledTaskService taskService,
        IEnumerable<SettingDefinition> taskDefs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in taskDefs)
        {
            if (!IsPureScheduledTaskToggle(def))
                continue;

            var taskPath = def.ScheduledTaskSettings[0].TaskPath;
            bool? enabled = await taskService.IsTaskEnabledAsync(taskPath).ConfigureAwait(false);

            string oldState;
            string newState;
            if (enabled is null)
            {
                // Old app marks a missing task's setting unavailable; the new engine has nothing to detect.
                oldState = newState = "Unavailable";
            }
            else
            {
                // OLD: DetermineIfSettingIsEnabled -> the toggle is Enabled iff the task is enabled.
                oldState = enabled.Value ? "Enabled" : "Disabled";
                var setting = SettingDefinitionConverter.ConvertScheduledTaskToggle(def);
                newState = CatalogDiscovery.DetectState(setting, new ScheduledTaskDetectionContext(enabled)) ?? "Custom";
            }

            rows.Add(new EquivalenceRow(def.Id, oldState, newState, oldState == newState));
        }

        return rows;
    }
}
