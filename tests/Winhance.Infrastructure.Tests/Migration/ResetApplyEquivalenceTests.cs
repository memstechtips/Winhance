using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Throwaway migration check: for every PLAIN registry toggle with a Windows-default direction, compares
/// the old live RESET-to-default apply's registry write intent (WindowsRegistryService.ApplySetting with the
/// useDefaultValue / default-direction routing, mirrored) against the new ApplyPlanBuilder plan built with
/// reset:true over the WindowsDefault state. Pure - no registry I/O, so the result depends only on the catalog,
/// not the machine. This is the gate proving the 18 [1,null] Explorer reset-DELETE divergences (and every other
/// toggle's reset) are reproduced by the new engine. Run: dotnet test --filter ResetApplyEquivalence</summary>
public class ResetApplyEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ResetApplyEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void ResetApplyEquivalence_OldAndNewApplyAgree()
    {
        var rows = ApplyEquivalenceHarness.RunResetApply(AllDefinitions());

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

        Assert.NotEmpty(rows);
        Assert.True(rows.All(r => r.Match), $"{mismatched} reset apply plans differ - see output");
    }
}
