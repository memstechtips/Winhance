using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>One comparison result for a single setting in the catalog-detection harness: the existing app's
/// detected state vs the HAND-AUTHORED catalog's, whether a catalog peer was found, and whether they agree.</summary>
public sealed record CatalogEquivalenceRow(string Id, string OldState, string NewState, bool Paired, bool Match);

/// <summary>Throwaway migration check, run on Windows: for every pure registry toggle / selection, compares the
/// existing app's real detection against the new engine reading the HAND-AUTHORED <see cref="SettingCatalog"/>
/// (looked up by Id), reading the live registry. Unlike <see cref="RegistryToggleEquivalenceHarness"/> - which
/// runs the throwaway converter and so cannot carry detection-only tags like <c>RegTarget.ApplyOnly</c> - this
/// runs the catalog the app will ship, so the precedence model (an ApplyOnly mirror key is written but not read;
/// the highest-precedence present key decides) is actually exercised. Settings with no catalog peer (e.g. the
/// merged ThisPC -win10 variants) are reported Unpaired and excluded from the pass/fail set. Deleted once the
/// migration is complete.</summary>
public static class CatalogDetectionEquivalenceHarness
{
    /// <summary>Builds one row per supplied registry TOGGLE definition. OLD reproduces the app's real per-setting
    /// detection (<see cref="RegistryToggleEquivalenceHarness.IsPureRegistryToggle"/> pre-filter assumed); NEW runs
    /// the hand-authored catalog Setting (by Id) through the engine reading the live registry.</summary>
    public static IReadOnlyList<CatalogEquivalenceRow> RunToggles(
        IWindowsRegistryService reg,
        IReadOnlyDictionary<string, Setting> catalogById,
        IEnumerable<SettingDefinition> toggleDefs)
    {
        var context = new WindowsDetectionContext(reg);
        var rows = new List<CatalogEquivalenceRow>();

        foreach (var def in toggleDefs)
        {
            if (!RegistryToggleEquivalenceHarness.IsPureRegistryToggle(def))
                continue;

            // OLD: reproduce the existing app's real per-setting detection
            // (SystemSettingsDiscoveryService.DetermineIfSettingIsEnabled): per-NIC/per-monitor settings expand
            // sub-keys via IsSettingApplied; key-existence settings pass a bool (does the key exist); everything
            // else passes the raw value. A setting is enabled when any of its registry settings is enabled.
            bool oldEnabled = def.RegistrySettings.Any(rs =>
            {
                if (rs.ApplyPerNetworkInterface || rs.ApplyPerMonitor)
                    return reg.IsSettingApplied(rs);

                object? current = rs.ValueName == null
                    ? (object)reg.KeyExists(rs.KeyPath)
                    : reg.GetValue(rs.KeyPath, rs.ValueName);
                return reg.IsRegistryValueInEnabledState(rs, current, current != null);
            });
            string oldState = oldEnabled ? "Enabled" : "Disabled";

            // NEW: run the HAND-AUTHORED catalog setting (carries any ApplyOnly tags), not the converter.
            if (!catalogById.TryGetValue(def.Id, out var setting))
            {
                rows.Add(new CatalogEquivalenceRow(def.Id, oldState, "(unpaired)", Paired: false, Match: false));
                continue;
            }

            string newState = CatalogDiscovery.DetectState(setting, context) ?? "Custom";
            rows.Add(new CatalogEquivalenceRow(def.Id, oldState, newState, Paired: true, Match: oldState == newState));
        }

        return rows;
    }

    /// <summary>Builds one row per supplied registry SELECTION definition. OLD is the app's real selection
    /// detection (<see cref="IComboBoxResolver.ResolveCurrentValueAsync"/> -> option index -> its DisplayName);
    /// NEW runs the hand-authored catalog Setting (by Id) through the engine. Callers should pre-filter with
    /// <see cref="RegistryToggleEquivalenceHarness.IsPureRegistrySelection"/>.</summary>
    public static async Task<IReadOnlyList<CatalogEquivalenceRow>> RunSelections(
        IWindowsRegistryService reg,
        IComboBoxResolver resolver,
        IReadOnlyDictionary<string, Setting> catalogById,
        IEnumerable<SettingDefinition> selectionDefs)
    {
        var context = new WindowsDetectionContext(reg);
        var rows = new List<CatalogEquivalenceRow>();

        foreach (var def in selectionDefs)
        {
            if (!RegistryToggleEquivalenceHarness.IsPureRegistrySelection(def))
                continue;

            // OLD: the app's real selection detection resolves the live registry to an option index.
            var resolved = await resolver.ResolveCurrentValueAsync(def).ConfigureAwait(false);
            int oldIndex = resolved is int idx ? idx : ComboBoxConstants.CustomStateIndex;
            string oldState = LabelForIndex(def, oldIndex);

            // NEW: run the HAND-AUTHORED catalog setting (carries any ApplyOnly tags), not the converter.
            if (!catalogById.TryGetValue(def.Id, out var setting))
            {
                rows.Add(new CatalogEquivalenceRow(def.Id, oldState, "(unpaired)", Paired: false, Match: false));
                continue;
            }

            string newState = CatalogDiscovery.DetectState(setting, context) ?? "Custom";
            rows.Add(new CatalogEquivalenceRow(def.Id, oldState, newState, Paired: true, Match: oldState == newState));
        }

        return rows;
    }

    /// <summary>The DisplayName of the option at <paramref name="index"/>, or "Custom" for the custom-state index /
    /// any out-of-range index - so OLD and NEW are compared on the same label vocabulary.</summary>
    private static string LabelForIndex(SettingDefinition def, int index)
    {
        var options = def.ComboBox?.Options;
        if (options is null || index < 0 || index >= options.Count)
            return "Custom";
        return options[index].DisplayName;
    }
}
