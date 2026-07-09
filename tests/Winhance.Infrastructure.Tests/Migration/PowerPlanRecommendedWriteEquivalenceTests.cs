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

/// <summary>Slice 5: proves the catalog-Setting ComputePlanRecommendedWrite reproduces the SettingDefinition
/// version EXACTLY over the whole population. The def form is the verbatim extraction of the old
/// PowerPlanActivationService.ApplyRecommendedSettingsToPlanAsync inline branches (the recommended AC/DC SYSTEM
/// values written to a freshly-created power plan via PowerProf.PowerWriteAC/DCValueIndex); the service uses the
/// catalog form. Machine-independent (catalog + old defs only, no I/O). Confirms the two migration concerns are
/// non-issues in the real Power data: the one non-1:1 numeric (power-harddisk-timeout, a Minutes slider over a
/// Seconds powercfg value) round-trips losslessly (values are multiples of 60), and every powercfg selection
/// carries a RecommendedValueAC (so the def's
/// branch 2 - the Recommendation.RecommendedOptionAC label path - is unreachable, and its absence from the catalog
/// form is faithful). Survives the converter teardown (reads SettingCatalog.All + the old defs).</summary>
public class PowerPlanRecommendedWriteEquivalenceTests
{
    private readonly ITestOutputHelper _output;
    public PowerPlanRecommendedWriteEquivalenceTests(ITestOutputHelper output) => _output = output;

    private static IEnumerable<SettingDefinition> AllDefinitions() => new[]
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
    }.SelectMany(g => g);

    [Fact]
    public void ComputePlanRecommendedWrite_MatchesDefVersion_OverThePopulation()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var nonNullResults = 0;

        foreach (var def in AllDefinitions())
        {
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var setting))
                continue;
            compared++;

            var defW = RecommendedSettingsResolver.ComputePlanRecommendedWrite(def);
            var catW = RecommendedSettingsResolver.ComputePlanRecommendedWrite(setting);
            if (defW.HasValue) nonNullResults++;
            if (defW != catW)
                mismatches.Add($"{def.Id}: def {Describe(defW)} != catalog {Describe(catW)}");
        }

        _output.WriteLine($"{compared} settings compared, {nonNullResults} non-null (powercfg-recommended) writes, {mismatches.Count} mismatches");
        foreach (var m in mismatches) _output.WriteLine("  " + m);

        Assert.True(compared >= 300, $"only {compared} settings paired - population scoping bug (expected 400+)");
        Assert.True(nonNullResults > 30, $"only {nonNullResults} non-null writes - equivalence is vacuous (expected ~40 powercfg settings)");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} ComputePlanRecommendedWrite catalog-vs-def mismatches:\n" + string.Join("\n", mismatches));
    }

    private static string Describe((string SubgroupGuid, string SettingGuid, int Ac, int Dc)? w) =>
        w is { } v ? $"(sub={v.SubgroupGuid}, set={v.SettingGuid}, AC={v.Ac}, DC={v.Dc})" : "null";
}
