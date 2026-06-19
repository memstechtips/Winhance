using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
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
            string newState = CatalogDiscovery.DetectState(
                setting,
                (p, v) => reg.GetValue(p, v ?? ""),
                context) ?? "Custom";

            rows.Add(new EquivalenceRow(def.Id, oldState, newState, oldState == newState));
        }

        return rows;
    }
}
