using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Throwaway migration check, run on Windows: runs the HAND-AUTHORED catalog (SettingCatalog.All, looked
/// up by Id) through the new detection engine against the live registry and compares to the app's real detection.
/// This is the validator for the precedence model: unlike the converter-based RegistryToggle/RegistrySelection
/// tests, this exercises detection-only tags (RegTarget.ApplyOnly) that the throwaway converter cannot emit, so a
/// mirror-key fix made in the catalog shows up here. With no ApplyOnly tags it reproduces the same diffs as the
/// converter tests; tagging genuine mirrors clears them. Run: dotnet test --filter CatalogDetectionEquivalence</summary>
public class CatalogDetectionEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public CatalogDetectionEquivalenceTests(ITestOutputHelper output) => _output = output;

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

    private static IReadOnlyDictionary<string, Setting> CatalogById() =>
        SettingCatalog.All.ToDictionary(s => s.Id);

    [Fact]
    public void CatalogDetectionEquivalence_Toggles_OldAndNewAgree()
    {
        // Real registry service, reading the live machine. Its two ctor deps are not exercised by the read path.
        var log = new Mock<ILogService>();
        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        var reg = new WindowsRegistryService(log.Object, interactiveUser.Object);

        var toggleDefs = AllDefinitions()
            .Where(RegistryToggleEquivalenceHarness.IsPureRegistryToggle)
            .ToList();

        var rows = CatalogDetectionEquivalenceHarness.RunToggles(reg, CatalogById(), toggleDefs);

        ReportAndAssert(rows, "toggle");
    }

    [Fact]
    public async Task CatalogDetectionEquivalence_Selections_OldAndNewAgree()
    {
        // Real registry service reading the live machine; its two ctor deps are not exercised by the read path.
        var log = new Mock<ILogService>();
        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        var reg = new WindowsRegistryService(log.Object, interactiveUser.Object);

        // Real discovery + resolver = the app's real selection detection. Pure registry selections only read the
        // registry, so the four non-registry discovery sources are no-op mocks.
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

        var rows = await CatalogDetectionEquivalenceHarness.RunSelections(reg, resolver, CatalogById(), selectionDefs);

        ReportAndAssert(rows, "selection");
    }

    /// <summary>Prints MATCH/DIFF/UNPAIRED per row + a summary, then asserts every PAIRED row matches. Unpaired
    /// settings (no catalog peer - e.g. the merged ThisPC -win10 variants) are reported but not failed.</summary>
    private void ReportAndAssert(IReadOnlyList<CatalogEquivalenceRow> rows, string kind)
    {
        foreach (var row in rows)
        {
            var tag = !row.Paired ? "[UNPAIRED]" : row.Match ? "[MATCH]" : "[DIFF]";
            _output.WriteLine($"{tag} {row.Id}: old={row.OldState} new={row.NewState}");
        }

        var paired = rows.Where(r => r.Paired).ToList();
        var matched = paired.Count(r => r.Match);
        var mismatched = paired.Count - matched;
        var unpaired = rows.Count - paired.Count;
        _output.WriteLine($"{matched}/{paired.Count} match, {mismatched} differ, {unpaired} unpaired ({kind})");

        if (mismatched > 0)
        {
            _output.WriteLine("Mismatches:");
            foreach (var row in paired.Where(r => !r.Match))
                _output.WriteLine($"  {row.Id}: old={row.OldState} new={row.NewState}");
        }

        Assert.True(paired.All(r => r.Match), $"{mismatched} {kind} settings differ - see output");
    }
}
