using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Throwaway migration check: for every PLAIN powercfg setting, compares the old live apply's value-index
/// write intent (PowerCfgApplier writing AC+DC) against the new ApplyPlanBuilder plan. Selections apply the chosen
/// option's value to both contexts; numerics apply the per-context Recommended/WindowsDefault value (display-unit
/// values round-tripped back to system units). Pure - no power I/O, so the result depends only on the catalog.
/// Run: dotnet test --filter PowerCfgApplyEquivalence</summary>
public class PowerCfgApplyEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public PowerCfgApplyEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void PowerCfgSelectionApplyEquivalence_OldAndNewApplyAgree()
    {
        var rows = ApplyEquivalenceHarness.RunPowerCfgSelectionApply(AllDefinitions());

        foreach (var row in rows)
        {
            var tag = row.Match ? "[MATCH]" : "[DIFF]";
            _output.WriteLine($"{tag} {row.Id}");
            if (!row.Match)
            {
                _output.WriteLine($"    old: {row.OldState}");
                _output.WriteLine($"    new: {row.NewState}");
            }
        }

        var matched = rows.Count(r => r.Match);
        var mismatched = rows.Count - matched;
        _output.WriteLine($"{matched}/{rows.Count} match, {mismatched} differ");

        Assert.True(rows.All(r => r.Match), $"{mismatched} powercfg selection apply plans differ - see output");
    }

    [Fact]
    public void PowerCfgNumericApplyEquivalence_OldAndNewApplyAgree()
    {
        var rows = ApplyEquivalenceHarness.RunPowerCfgNumericApply(AllDefinitions());

        foreach (var row in rows)
        {
            var tag = row.Match ? "[MATCH]" : "[DIFF]";
            _output.WriteLine($"{tag} {row.Id}");
            if (!row.Match)
            {
                _output.WriteLine($"    old: {row.OldState}");
                _output.WriteLine($"    new: {row.NewState}");
            }
        }

        var matched = rows.Count(r => r.Match);
        var mismatched = rows.Count - matched;
        _output.WriteLine($"{matched}/{rows.Count} match, {mismatched} differ");

        Assert.True(rows.All(r => r.Match), $"{mismatched} powercfg numeric apply plans differ - see output");
    }
}
