using System.Collections.Generic;
using System.Linq;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Migration precondition for the autounattend script-gen swap: the NEW SettingCatalog's per-state Set
/// must reproduce the OLD IComboBoxResolver.ResolveIndexToRawValues output EXACTLY for every selection the script
/// emitter resolves through ComboBox value-mappings. The emitter (AppendSelectionCommandsFiltered) early-returns on
/// power-plan-selection, so that one setting is excluded. For each remaining selection's mapped option index the OLD
/// dict comes from the REAL ComboBoxResolver (never reimplemented); the NEW dict is rebuilt from the catalog's
/// Setting.States[idx].Set, keyed by the matching Target (RegTarget.ValueName / "KeyExists", PowerCfgTarget ->
/// "PowerCfgValue"). Green means swapping the emitter onto the new model is provably faithful. Pure - depends only on
/// the catalog, not the machine. Run: dotnet test --filter ScriptGenSelectionResolveEquivalence</summary>
public class ScriptGenSelectionResolveEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ScriptGenSelectionResolveEquivalenceTests(ITestOutputHelper output) => _output = output;

    /// <summary>Every old SettingDefinition the app ships, pulled straight from the static feature providers -
    /// the same raw population the sibling Migration equivalence tests use.</summary>
    private static IEnumerable<SettingDefinition> AllDefinitions()
    {
        return new[]
        {
            ExplorerCustomizations.GetExplorerCustomizations().Settings,
            StartMenuCustomizations.GetStartMenuCustomizations().Settings,
            TaskbarCustomizations.GetTaskbarCustomizations().Settings,
            WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings,
            PowerOptimizations.GetPowerOptimizations().Settings,
            GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings,
            NotificationOptimizations.GetNotificationOptimizations().Settings,
            PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings,
            SoundOptimizations.GetSoundOptimizations().Settings,
            UpdateOptimizations.GetUpdateOptimizations().Settings,
        }.SelectMany(group => group);
    }

    [Fact]
    public void ScriptGenSelectionResolveEquivalence_NewCatalogReproducesOldResolveIndexToRawValues()
    {
        // The REAL resolver for the OLD side. ResolveIndexToRawValues only reads
        // setting.ComboBox.Options[idx].ValueMappings - a plain resolver (no dependencies) is enough.
        var resolver = new ComboBoxResolver();

        // Selections the script emitter resolves via ValueMappings, minus power-plan-selection,
        // on which AppendSelectionCommandsFiltered early-returns.
        var selectionDefs = AllDefinitions()
            .Where(d => d.InputType == InputType.Selection
                     && d.ComboBox?.Options?.Any(o => o.ValueMappings != null) == true
                     && d.Id != SettingIds.PowerPlanSelection)
            .ToList();

        var mismatches = new List<string>();
        var unpairedSkipped = new List<string>();
        var comparedSettingIds = new HashSet<string>();

        foreach (var def in selectionDefs)
        {
            var newSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == def.Id);
            if (newSetting == null)
            {
                // The F1 production swap has NO fallback to the old resolver, so an unpaired selection-with-
                // ValueMappings setting would silently emit nothing in the autounattend script - a regression,
                // asserted against below (not a benign skip).
                unpairedSkipped.Add(def.Id);
                continue;
            }

            var options = def.ComboBox!.Options;
            for (int idx = 0; idx < options.Count; idx++)
            {
                if (options[idx].ValueMappings == null)
                    continue;

                // OLD side - the REAL resolver, never reimplemented.
                var oldDict = resolver.ResolveIndexToRawValues(def, idx);

                // States order is ASSUMED 1:1 with ComboBox.Options order - but the test CHECKS it
                // rather than trusting it: an out-of-range index is recorded, never thrown.
                if (idx >= newSetting.States.Count)
                {
                    mismatches.Add(
                        $"{def.Id}[{idx}]: new catalog has only {newSetting.States.Count} States - no State at index {idx} (old={Fmt(oldDict)})");
                    comparedSettingIds.Add(def.Id);
                    continue;
                }

                // NEW side - rebuild the raw-values dict from the catalog state's Set.
                var state = newSetting.States[idx];
                var newDict = new Dictionary<string, object?>();
                bool buildFailed = false;
                foreach (var entry in state.Set)
                {
                    var target = newSetting.Targets.FirstOrDefault(t => t.Key == entry.Key);
                    if (target == null)
                    {
                        mismatches.Add($"{def.Id}[{idx}]: state.Set key '{entry.Key}' has no matching Target");
                        buildFailed = true;
                        break;
                    }

                    string? dictKey = target switch
                    {
                        RegTarget rt => rt.ValueName ?? "KeyExists",
                        PowerCfgTarget => "PowerCfgValue",
                        _ => null,
                    };

                    if (dictKey == null)
                    {
                        mismatches.Add(
                            $"{def.Id}[{idx}]: target '{entry.Key}' is {target.GetType().Name}, not Reg/PowerCfg");
                        buildFailed = true;
                        break;
                    }

                    newDict[dictKey] = entry.Value.WritePayload;
                }

                comparedSettingIds.Add(def.Id);

                if (buildFailed)
                    continue;

                // Compare OLD vs NEW: identical key sets, then identical value per key.
                var oldKeys = new HashSet<string>(oldDict.Keys);
                var newKeys = new HashSet<string>(newDict.Keys);
                if (!oldKeys.SetEquals(newKeys))
                {
                    mismatches.Add($"{def.Id}[{idx}]: key-set differs old={Fmt(oldDict)} new={Fmt(newDict)}");
                    continue;
                }

                foreach (var key in oldKeys)
                {
                    if (!ValuesEquivalent(oldDict[key], newDict[key]))
                    {
                        mismatches.Add(
                            $"{def.Id}[{idx}]: value differs for '{key}' old={Fmt(oldDict)} new={Fmt(newDict)}");
                        break;
                    }
                }
            }
        }

        foreach (var id in unpairedSkipped)
            _output.WriteLine($"[unpaired-skipped] {id}");
        _output.WriteLine(
            $"{comparedSettingIds.Count} settings compared, {unpairedSkipped.Count} unpaired-skipped, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        // A zero-coverage bug must not pass vacuously.
        Assert.NotEmpty(comparedSettingIds);

        // The F1 production swap (RegistryCommandEmitter.ResolveSelectionValuesFromCatalog) has NO fallback to the old
        // resolver: an unpaired selection-with-ValueMappings setting would silently emit NOTHING in the autounattend
        // script. So every such setting MUST be catalog-paired - an unpaired one is a regression, not a skip.
        Assert.True(
            unpairedSkipped.Count == 0,
            "selection settings with ValueMappings but NO catalog peer (would silently emit nothing post-F1): "
                + string.Join(", ", unpairedSkipped));

        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} selection resolve mismatches (new catalog vs old ResolveIndexToRawValues):\n"
                + string.Join("\n", mismatches));
    }

    private static string Fmt(IReadOnlyDictionary<string, object?> d) =>
        "{" + string.Join(", ", d.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={FmtVal(kv.Value)}")) + "}";

    private static string FmtVal(object? v) =>
        v switch
        {
            null => "null",
            byte[] bytes => "[" + string.Join(",", bytes) + "]",
            _ => v.ToString() ?? "null",
        };

    /// <summary>Boxing-tolerant value equality mirroring the production ValueComparer the resolver itself uses
    /// (int/long/byte/bool cross-boxing, structural byte[]). Deliberately NO ToString fallback, so a genuine
    /// type/value divergence between the old mapping value and the new WritePayload is caught, not masked.</summary>
    private static bool ValuesEquivalent(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a is byte[] ba && b is byte[] bb) return ba.SequenceEqual(bb);
        if (a.Equals(b)) return true;
        try
        {
            return System.Convert.ToInt64(a) == System.Convert.ToInt64(b);
        }
        catch
        {
            return false;
        }
    }
}
