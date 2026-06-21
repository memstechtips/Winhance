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
    /// <summary>True when a definition is a pure registry toggle - the cleanest first slice to compare.
    /// Excludes anything that detects via a non-registry mechanism (powercfg, scheduled task, native
    /// power API, .reg blobs, PowerShell, or a custom DetectionType).</summary>
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

    /// <summary>True when a definition is a pure registry selection (ComboBox) - registry-backed, no
    /// non-registry mechanism. The selection analogue of <see cref="IsPureRegistryToggle"/>.</summary>
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

    /// <summary>The DisplayName of the option at <paramref name="index"/>, or "Custom" for the custom-state
    /// index / any out-of-range index - so OLD and NEW are compared on the same label vocabulary.</summary>
    private static string LabelForIndex(SettingDefinition def, int index)
    {
        var options = def.ComboBox?.Options;
        if (options is null || index < 0 || index >= options.Count)
            return "Custom";
        return options[index].DisplayName;
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
