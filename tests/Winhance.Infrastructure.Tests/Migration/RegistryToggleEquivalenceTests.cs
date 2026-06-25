using System.Collections.Generic;
using System.Linq;
using Moq;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Throwaway migration check, run on Windows: for every pure registry toggle in the catalog,
/// compares the existing app's detection (IsSettingApplied) against the new engine's (CatalogDiscovery)
/// reading the live registry. Green when every toggle agrees; prints exactly which ones differ otherwise.
/// Run: dotnet test --filter RegistryToggleEquivalence</summary>
public class RegistryToggleEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public RegistryToggleEquivalenceTests(ITestOutputHelper output) => _output = output;

    /// <summary>Every SettingDefinition the app ships, pulled straight from the static feature providers
    /// (no DI, no Windows-version filtering — so the comparison population is the full raw catalog).</summary>
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
    public void RegistryToggleEquivalence_OldAndNewDetectionAgree()
    {
        // Real registry service, reading the live machine. Its two ctor deps are not exercised by
        // the read path used here (GetValue / GetSubKeyNames / IsSettingApplied), so no-op mocks suffice.
        var log = new Mock<ILogService>();
        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        var reg = new WindowsRegistryService(log.Object, interactiveUser.Object);

        var toggleDefs = AllDefinitions()
            .Where(RegistryToggleEquivalenceHarness.IsPureRegistryToggle)
            .ToList();

        var rows = RegistryToggleEquivalenceHarness.Run(reg, toggleDefs);

        foreach (var row in rows)
        {
            var tag = row.Match ? "[MATCH]" : "[DIFF]";
            _output.WriteLine($"{tag} {row.Id}: old={row.OldState} new={row.NewState}");
        }

        var matched = rows.Count(r => r.Match);
        var mismatched = rows.Count - matched;
        _output.WriteLine($"{matched}/{rows.Count} match, {mismatched} differ");

        if (mismatched > 0)
        {
            _output.WriteLine("Mismatches:");
            foreach (var row in rows.Where(r => !r.Match))
                _output.WriteLine($"  {row.Id}: old={row.OldState} new={row.NewState}");
        }

        Assert.True(rows.All(r => r.Match), $"{mismatched} toggle settings differ — see output");
    }
}
