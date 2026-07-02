using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Throwaway migration check: for every pure powercfg SELECTION, compares the old live ASYMMETRIC AC/DC apply
/// intent (PowerCfgApplier: option[acIndex].PowerCfgValue -> AC, option[dcIndex].PowerCfgValue -> DC) against the new
/// ApplyPlanBuilder.BuildPowerCfgSelectionAcDc plan, over EVERY (acIndex, dcIndex) pair (so ac != dc is exercised).
/// Pure - no powercfg I/O, so the result depends only on the catalog, not the machine. Locks the precondition for the
/// resolver routing the AC/DC tuple/dict through the new engine. Run: dotnet test --filter PowerCfgSelectionAcDcApplyEquivalence</summary>
public class PowerCfgSelectionAcDcApplyEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public PowerCfgSelectionAcDcApplyEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void PowerCfgSelectionAcDcApplyEquivalence_OldAndNewApplyAgree()
    {
        var rows = ApplyEquivalenceHarness.RunPowerCfgSelectionAcDcApply(AllDefinitions());

        foreach (var row in rows.Where(r => !r.Match))
        {
            _output.WriteLine($"[DIFF] {row.Id}");
            _output.WriteLine($"    old: {row.OldState}");
            _output.WriteLine($"    new: {row.NewState}");
        }

        var matched = rows.Count(r => r.Match);
        var mismatched = rows.Count - matched;
        _output.WriteLine($"{matched}/{rows.Count} match, {mismatched} differ");

        Assert.NotEmpty(rows);
        Assert.True(rows.All(r => r.Match), $"{mismatched} powercfg AC/DC selection apply plans differ - see output");
    }
}
