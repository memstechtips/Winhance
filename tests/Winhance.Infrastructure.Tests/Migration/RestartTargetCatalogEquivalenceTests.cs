using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice 3a foundation (additive): proves ProcessRestartManager.CollectRestartTargets over a catalog
/// Setting reproduces the SettingDefinition version EXACTLY over the whole population, machine-independently (no
/// real restarts - just the coalesced (process, service) set). The def carries RestartProcess / RestartService as
/// two separate strings; the catalog unifies them into a single ApplyBehavior.Restart RestartTarget - lossless
/// ONLY if no setting sets both, which this test PINS (a setting with both would give the def set a process AND a
/// service while the catalog could carry one -> mismatch -> red). The batch flush's Setting overload repoints the
/// apply-cluster onto this at the Slice-3 coordinated cutover. Survives the converter teardown (reads
/// SettingCatalog.All + the old defs).</summary>
public class RestartTargetCatalogEquivalenceTests
{
    private readonly ITestOutputHelper _output;
    public RestartTargetCatalogEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void CatalogRestartTargets_MatchDefVersion_OverThePopulation()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var withRestart = 0;

        foreach (var def in AllDefinitions())
        {
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var setting))
                continue;
            compared++;

            var (defProc, defSvc) = ProcessRestartManager.CollectRestartTargets(new[] { def });
            var (catProc, catSvc) = ProcessRestartManager.CollectRestartTargets(new[] { setting });
            if (defProc.Count > 0 || defSvc.Count > 0) withRestart++;

            if (!defProc.SetEquals(catProc))
                mismatches.Add($"{def.Id}: process def [{string.Join(",", defProc)}] != catalog [{string.Join(",", catProc)}]");
            if (!defSvc.SetEquals(catSvc))
                mismatches.Add($"{def.Id}: service def [{string.Join(",", defSvc)}] != catalog [{string.Join(",", catSvc)}]");
        }

        _output.WriteLine($"{compared} settings compared, {withRestart} with a restart, {mismatches.Count} mismatches");
        foreach (var m in mismatches) _output.WriteLine("  " + m);

        Assert.True(compared >= 300, $"only {compared} settings paired - population scoping bug (expected 400+)");
        Assert.True(withRestart > 30, $"only {withRestart} settings with a restart - comparison is vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} restart-target catalog-vs-def mismatches:\n" + string.Join("\n", mismatches));
    }
}
