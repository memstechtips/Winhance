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

/// <summary>Throwaway migration check, run on Windows: for every pure registry selection (ComboBox) in the
/// catalog, compares the app's real selection detection (ComboBoxResolver) against the new engine's
/// (CatalogDiscovery), reading the live registry. Green when every selection agrees; prints which differ.
/// Run: dotnet test --filter RegistrySelectionEquivalence</summary>
public class RegistrySelectionEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public RegistrySelectionEquivalenceTests(ITestOutputHelper output) => _output = output;

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

    [Fact]
    public async Task RegistrySelectionEquivalence_OldAndNewDetectionAgree()
    {
        // Real registry service reading the live machine; its two ctor deps are not exercised by the read path.
        var log = new Mock<ILogService>();
        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        var reg = new WindowsRegistryService(log.Object, interactiveUser.Object);

        // Real discovery + resolver = the app's real selection detection. Pure registry selections only read
        // the registry, so the four non-registry discovery sources are no-op mocks.
        var discovery = new SystemSettingsDiscoveryService(
            reg,
            log.Object,
            new Mock<IPowerSettingsQueryService>().Object,
            new Mock<ISpecialDiscoveryRegistry>().Object,
            new Mock<IScheduledTaskService>().Object,
            new Mock<ISystemRestoreService>().Object);
        var resolver = new ComboBoxResolver(discovery);

        var selectionDefs = AllDefinitions()
            .Where(RegistryToggleEquivalenceHarness.IsPureRegistrySelection)
            .ToList();

        var rows = await RegistryToggleEquivalenceHarness.RunSelections(reg, resolver, selectionDefs);

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

        Assert.True(rows.All(r => r.Match), $"{mismatched} selection settings differ - see output");
    }
}
