using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Edge-2 arbiter: the settings whose reset ApplyRequestResolver now FALLS THROUGH to the normal apply
/// resolution (no WindowsDefault-roled catalog state, not an Action) each reproduce the old executor's reset
/// faithfully - proven by RESET-INERTNESS (old reset writes == old apply writes) + COVERAGE (the peer's normal apply
/// is proven == old apply by an existing apply-equivalence harness). Pure - no registry I/O. Run: dotnet test
/// --filter NoWindowsDefaultResetInert</summary>
public class NoWindowsDefaultResetInertTests
{
    private readonly ITestOutputHelper _output;
    public NoWindowsDefaultResetInertTests(ITestOutputHelper output) => _output = output;

    private static IEnumerable<SettingDefinition> AllDefs() => new[]
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
    public void NoWindowsDefaultResetInert_FallThroughReproducesOldReset()
    {
        var rows = ApplyEquivalenceHarness.RunNoWindowsDefaultResetInert(AllDefs(), SettingCatalog.All);

        foreach (var row in rows)
        {
            var tag = row.Match ? "[MATCH]" : "[DIFF]";
            _output.WriteLine($"{tag} {row.Id} | {row.OldState} | {row.NewState}");
        }

        Assert.NotEmpty(rows); // guard against a scoping bug that would make the test vacuous
        var matched = rows.Count(r => r.Match);
        _output.WriteLine($"{matched}/{rows.Count} no-WindowsDefault non-Action settings are reset-inert AND covered");
        Assert.True(rows.All(r => r.Match), $"{rows.Count - matched} settings are not reset-inert or not apply-covered - see output");
    }
}
