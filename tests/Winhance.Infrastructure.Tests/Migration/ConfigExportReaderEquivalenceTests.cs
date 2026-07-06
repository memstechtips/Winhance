using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice E4 precondition: the config EXPORTERS (ConfigExportService + AutounattendXmlGeneratorService, which
/// share read shapes) are moving their per-setting export decisions off the old SettingDefinition onto the catalog
/// Setting (paired via SettingCatalog.Find). This proves the three NON-InputType reads are old-vs-new IDENTICAL over
/// the whole shipped population, machine-independently (catalog + old defs only, no I/O). The InputType dispatch
/// (Toggle/Selection/NumericRange -> Control, Selection incl. power-plan) is already proven by
/// ConfigBridgeReaderEquivalenceTests + ControlDerivationConformanceTests and is not re-proven here.
///
///   1. PowerModeSupport (the AC/DC-Separate export gate): def.PowerCfgSettings[0].PowerModeSupport moves to the
///      catalog PowerCfgTarget.Mode. The converter copies PowerModeSupport verbatim onto Mode, so they must be equal.
///
///   2. Custom-state registry KEYS (GetSelectionStateFromState's `foreach (rs in setting.RegistrySettings)` keyed by
///      ValueName ?? "KeyExists"): the KEY SET moves to the catalog RegTargets' keys (ValueName ?? "KeyExists"). The
///      converter groups mirror RegistrySettings by ValueName into one RegTarget, so the DISTINCT key set is identical
///      (the old loop over-writes the same key for a mirror; deduping to the RegTarget key set yields the same dict).
///
///   3. ResolveValueToIndex (powercfg AC/DC value -> option index): the old code scans ComboBox options for the one
///      whose ValueMappings["PowerCfgValue"] equals the value. ConvertPowerCfg builds one State per option with
///      Set["Power"] = StateValue.Of(that PowerCfgValue), index-aligned, so States[i].Set["Power"].WritePayload ==
///      options[i].ValueMappings["PowerCfgValue"] per index - the catalog reproduces the same value->index scan.
///
/// Green means the E4 reader swaps are provably behaviour-preserving. Pure - depends only on the catalog.
/// Run: dotnet test --filter ConfigExportReaderEquivalence</summary>
public class ConfigExportReaderEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ConfigExportReaderEquivalenceTests(ITestOutputHelper output) => _output = output;

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

    /// <summary>PowerModeSupport swap: for every paired powercfg setting, the old def.PowerCfgSettings[0].PowerModeSupport
    /// equals the catalog's single PowerCfgTarget.Mode.</summary>
    [Fact]
    public void PowerModeSupport_CatalogPowerCfgTargetMode_MatchesOldFlag()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;

        foreach (var def in AllDefinitions())
        {
            if (def.PowerCfgSettings == null || def.PowerCfgSettings.Count == 0)
                continue; // the exporter only reads PowerModeSupport for powercfg settings
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
                continue;

            var pcfgTargets = s.Targets.OfType<PowerCfgTarget>().ToList();
            if (pcfgTargets.Count != 1)
            {
                mismatches.Add($"{def.Id}: expected exactly one PowerCfgTarget, found {pcfgTargets.Count}");
                continue;
            }
            compared++;

            var oldMode = def.PowerCfgSettings[0].PowerModeSupport;
            var newMode = pcfgTargets[0].Mode;
            if (oldMode != newMode)
                mismatches.Add($"{def.Id}: old PowerModeSupport={oldMode} != catalog PowerCfgTarget.Mode={newMode}");
        }

        _output.WriteLine($"{compared} powercfg settings compared, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.True(compared > 20, $"only compared {compared} powercfg settings - population scoping bug");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} PowerModeSupport mismatches:\n" + string.Join("\n", mismatches));
    }

    /// <summary>Custom-state registry KEYS swap: for every paired Selection setting, the old
    /// `{rs.ValueName ?? "KeyExists"}` set (over def.RegistrySettings) equals the catalog RegTargets' key set.</summary>
    [Fact]
    public void CustomStateKeys_CatalogRegTargets_MatchOldRegistrySettings()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var withKeys = 0;

        foreach (var def in AllDefinitions())
        {
            if (def.InputType != InputType.Selection)
                continue; // GetSelectionStateFromState's custom-state loop runs only for Selection settings
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
                continue;
            compared++;

            var oldKeys = new HashSet<string>(
                (def.RegistrySettings ?? new List<RegistrySetting>())
                    .Select(rs => rs.ValueName ?? "KeyExists"));
            var newKeys = new HashSet<string>(
                s.Targets.OfType<RegTarget>().Select(rt => rt.ValueName ?? "KeyExists"));

            if (oldKeys.Count > 0)
                withKeys++;

            if (!oldKeys.SetEquals(newKeys))
                mismatches.Add($"{def.Id}: custom-state key set differs old=[{string.Join(",", oldKeys.OrderBy(x => x))}] new=[{string.Join(",", newKeys.OrderBy(x => x))}]");
        }

        _output.WriteLine($"{compared} selection settings compared, {withKeys} with registry keys, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.True(compared > 50, $"only compared {compared} selection settings - population scoping bug");
        Assert.True(withKeys > 0, "no selection setting had registry keys - the comparison would be vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} custom-state key mismatches (catalog RegTargets vs old RegistrySettings):\n" + string.Join("\n", mismatches));
    }

    /// <summary>ResolveValueToIndex swap: for every paired powercfg-Separate Selection, the per-option
    /// ValueMappings["PowerCfgValue"] equals the catalog State's Set["Power"].WritePayload at the SAME index, so the
    /// old value->index scan and the catalog value->index scan return the same index for any value.</summary>
    [Fact]
    public void ResolveValueToIndex_CatalogStatePowerValue_MatchesOldComboBoxMapping()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var indices = 0;

        foreach (var def in AllDefinitions())
        {
            if (def.InputType != InputType.Selection)
                continue;
            if (def.PowerCfgSettings == null || def.PowerCfgSettings.Count == 0
                || def.PowerCfgSettings[0].PowerModeSupport != PowerModeSupport.Separate)
                continue; // ResolveValueToIndex is only exercised for powercfg-Separate selections (AC/DC export)
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
                continue;

            var options = def.ComboBox?.Options;
            if (options == null)
            {
                mismatches.Add($"{def.Id}: powercfg-Separate selection has null ComboBox.Options");
                continue;
            }
            if (options.Count != s.States.Count)
            {
                mismatches.Add($"{def.Id}: ComboBox has {options.Count} options but catalog has {s.States.Count} States");
                continue;
            }
            compared++;

            for (int i = 0; i < options.Count; i++)
            {
                var mapping = options[i].ValueMappings;
                if (mapping == null || !mapping.TryGetValue("PowerCfgValue", out var expected) || expected == null)
                    continue; // old ResolveValueToIndex skips options without a PowerCfgValue mapping
                indices++;

                int oldValue = Convert.ToInt32(expected);

                if (!s.States[i].Set.TryGetValue("Power", out var sv) || sv.WritePayload == null)
                {
                    mismatches.Add($"{def.Id}[{i}]: catalog State has no Set[\"Power\"] write payload (old PowerCfgValue={oldValue})");
                    continue;
                }
                int newValue = Convert.ToInt32(sv.WritePayload);
                if (oldValue != newValue)
                    mismatches.Add($"{def.Id}[{i}]: old PowerCfgValue={oldValue} != catalog Set[\"Power\"]={newValue}");
            }
        }

        _output.WriteLine($"{compared} powercfg-Separate selections compared, {indices} option indices, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.True(compared > 0, "no powercfg-Separate selection compared - population scoping bug");
        Assert.True(indices > 0, "no option indices compared - the comparison would be vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} ResolveValueToIndex mismatches (catalog Set[\"Power\"] vs old ComboBox PowerCfgValue):\n" + string.Join("\n", mismatches));
    }
}
