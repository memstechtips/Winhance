using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Throwaway migration check: for effects-based custom-detector settings with a WindowsDefault state (today the
/// system-restore toggle), compares the old live executor RESET's PowerShell-script intent against the new
/// ApplyPlanBuilder plan for the WindowsDefault state (reset:true). The funnel applies the WindowsDefault DIRECTION on a
/// reset, and these settings carry no registry targets, so the comparison is effects-only and reset:true is inert. Pure
/// - no registry I/O, so the result depends only on the catalog, not the machine. This locks the precondition for
/// removing the resolver's custom-detector reset guard. Run: dotnet test --filter CustomDetectorResetApplyEquivalence</summary>
public class CustomDetectorResetApplyEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public CustomDetectorResetApplyEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void CustomDetectorResetApplyEquivalence_OldAndNewResetAgree()
    {
        var rows = ApplyEquivalenceHarness.RunCustomDetectorReset(AllDefinitions());

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

        // At least the system-restore toggle must be covered - guards against a vacuous pass if a converter ever stops
        // emitting the WindowsDefault role.
        Assert.NotEmpty(rows);
        Assert.True(rows.All(r => r.Match), $"{mismatched} custom-detector reset plans differ - see output");
    }
}
