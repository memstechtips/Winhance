using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

/// <summary>Throwaway migration check, run on Windows: for every pure powercfg setting in the catalog, compares
/// the app's real detection against the new engine's, reading the live power scheme. Selections compare the
/// resolved ComboBox option (ComboBoxResolver) against CatalogDiscovery.DetectState; numerics compare the raw AC
/// value index against CatalogDiscovery.DetectValue. The old app's canonical detected value is the AC value, so
/// both tracks run the engine in the AC context. Green when every setting agrees; prints which differ.
/// Run: dotnet test --filter PowerCfgEquivalence</summary>
public class PowerCfgEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public PowerCfgEquivalenceTests(ITestOutputHelper output) => _output = output;

    /// <summary>Every SettingDefinition the app ships, pulled straight from the static feature providers
    /// (no DI, no Windows-version filtering - so the comparison population is the full raw catalog).</summary>
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

    /// <summary>The app's real selection-detection stack for powercfg settings: a real PowerSettingsQueryService
    /// (reads the live power scheme), a real registry service, a real discovery service constructed WITH that
    /// power query (powercfg selections resolve through it), and a real ComboBoxResolver wrapping the discovery.
    /// The non-power discovery sources (special-discovery, scheduled-task, system-restore) are no-op mocks.</summary>
    private static (PowerSettingsQueryService PowerQuery, ComboBoxResolver Resolver) BuildRealServices()
    {
        var log = new Mock<ILogService>();
        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        var reg = new WindowsRegistryService(log.Object, interactiveUser.Object);

        // Real power query reading the live power scheme - must NOT be mocked.
        var powerQuery = new PowerSettingsQueryService(log.Object);

        var discovery = new SystemSettingsDiscoveryService(
            reg,
            log.Object,
            powerQuery,
            new Mock<ISpecialDiscoveryRegistry>().Object,
            new Mock<IScheduledTaskService>().Object,
            new Mock<ISystemRestoreService>().Object);
        var resolver = new ComboBoxResolver(discovery);

        return (powerQuery, resolver);
    }

    [Fact]
    public async Task PowerCfgSelectionEquivalence_OldAndNewDetectionAgree()
    {
        var (powerQuery, resolver) = BuildRealServices();

        var selectionDefs = AllDefinitions()
            .Where(RegistryToggleEquivalenceHarness.IsPurePowerCfgSelection)
            .ToList();

        var rows = await RegistryToggleEquivalenceHarness.RunPowerCfgSelections(powerQuery, resolver, selectionDefs);

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

        Assert.True(rows.All(r => r.Match), $"{mismatched} powercfg selection settings differ - see output");
    }

    [Fact]
    public async Task PowerCfgNumericEquivalence_OldAndNewDetectionAgree()
    {
        var (powerQuery, _) = BuildRealServices();

        var numericDefs = AllDefinitions()
            .Where(RegistryToggleEquivalenceHarness.IsPurePowerCfgNumeric)
            .ToList();

        var rows = await RegistryToggleEquivalenceHarness.RunPowerCfgNumerics(powerQuery, numericDefs);

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

        Assert.True(rows.All(r => r.Match), $"{mismatched} powercfg numeric settings differ - see output");
    }
}
