using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice C/D foundation (additive, wired to nothing yet): proves the new catalog-Setting overloads of
/// RecommendedSettingsResolver's powercfg helpers reproduce the SettingDefinition versions EXACTLY over the whole
/// powercfg population, machine-independently (catalog + old defs only, no I/O). These helpers are the E2/SAS
/// "partial-block" - the SAS change-history rendering + the config bridge read them off a def today; once proven
/// equivalent, the apply-cluster port can repoint those consumers onto the Setting overloads without a def in hand.
/// GetPowerCfgDisplayUnits: catalog Numeric.Units (== def.NumericRange?.Units ?? pcs.Units, set by the converter)
/// with the PowerCfgTarget.Units fallback for selections == def.NumericRange?.Units ?? def.PowerCfgSettings[0].Units.
/// FindOptionIndexForPowerCfgValue: the converter builds one State per option (Set["Power"] = Of(PowerCfgValue)),
/// so the first matching State index == the def's first matching Options index. Survives the converter teardown
/// (reads SettingCatalog.All + the old defs). Run: dotnet test --filter PowerCfgHelperCatalog</summary>
public class PowerCfgHelperCatalogEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public PowerCfgHelperCatalogEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void CatalogPowerCfgHelpers_MatchDefVersions_OverThePowerCfgPopulation()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var comparedSettings = 0;
        var nonEmptyUnits = 0;
        var indexProbes = 0;
        var nonNullMatches = 0;

        foreach (var def in AllDefinitions())
        {
            if (def.PowerCfgSettings is not { Count: > 0 })
                continue;
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var setting))
                continue;
            comparedSettings++;

            // (1) GetPowerCfgDisplayUnits: def vs catalog.
            var defUnits = RecommendedSettingsResolver.GetPowerCfgDisplayUnits(def);
            var catUnits = RecommendedSettingsResolver.GetPowerCfgDisplayUnits(setting);
            if (!string.IsNullOrEmpty(defUnits))
                nonEmptyUnits++;
            if (defUnits != catUnits)
                mismatches.Add($"{def.Id}: GetPowerCfgDisplayUnits def '{defUnits}' != catalog '{catUnits}'");

            // (2) FindOptionIndexForPowerCfgValue: probe every option's PowerCfgValue (must map to its own index on
            //     both), the recommended/default AC/DC values (what BuildPowerCfgApplyValue passes), a guaranteed
            //     miss (both null), and null (both null).
            var probes = new List<int?>();
            var opts = def.ComboBox?.Options;
            if (opts != null)
                foreach (var o in opts)
                    if (o.ValueMappings is { } vm && vm.TryGetValue("PowerCfgValue", out var v) && v != null)
                        probes.Add(Convert.ToInt32(v));
            var pcs = def.PowerCfgSettings[0];
            foreach (var v in new int?[] { pcs.RecommendedValueAC, pcs.RecommendedValueDC, pcs.DefaultValueAC, pcs.DefaultValueDC })
                if (v.HasValue) probes.Add(v);
            probes.Add(int.MinValue); // guaranteed miss
            probes.Add(null);         // null -> null

            foreach (var probe in probes)
            {
                var defIdx = RecommendedSettingsResolver.FindOptionIndexForPowerCfgValue(def, probe);
                var catIdx = RecommendedSettingsResolver.FindOptionIndexForPowerCfgValue(setting, probe);
                indexProbes++;
                if (defIdx != null)
                    nonNullMatches++;
                if (defIdx != catIdx)
                    mismatches.Add($"{def.Id}: FindOptionIndexForPowerCfgValue({Fmt(probe)}) def {Fmt(defIdx)} != catalog {Fmt(catIdx)}");
            }
        }

        _output.WriteLine($"{comparedSettings} powercfg settings, {nonEmptyUnits} non-empty units, {indexProbes} index probes ({nonNullMatches} non-null), {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine("  " + m);

        Assert.True(comparedSettings >= 30, $"only {comparedSettings} powercfg settings paired - population scoping bug (expected ~40+)");
        Assert.True(nonEmptyUnits > 0, $"no non-empty units - GetPowerCfgDisplayUnits comparison is vacuous");
        Assert.True(nonNullMatches > 20, $"only {nonNullMatches} non-null index matches - FindOptionIndex equivalence is vacuous (all null)");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} powercfg-helper catalog-vs-def mismatches:\n" + string.Join("\n", mismatches));
    }

    private static string Fmt(int? v) => v is null ? "null" : v.Value.ToString();
}
