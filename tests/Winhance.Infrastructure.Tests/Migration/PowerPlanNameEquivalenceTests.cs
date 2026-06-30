using System.Linq;
using System.Threading.Tasks;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Old-discovery-retirement check, run on Windows against the live power scheme. Proves the NEW engine's
/// <c>CatalogDetectionResult.DynamicSelectionName</c> (threaded from <c>SystemDetectionContext.ActivePowerPlanName</c>)
/// reproduces the OLD discovery's <c>RawValues["ActivePowerPlan"]</c> for the power-plan setting EXACTLY - so the
/// 3 power-plan-name consumers (ConfigExport / autounattend / ConfigReview) can be repointed off RawValues onto the
/// typed field, retiring one of the RawValues residuals that pin the old discovery. Both sides read the SAME source
/// (GetAvailablePowerPlansAsync's active plan) via the SAME real PowerSettingsQueryService.
///
/// Run: dotnet test --filter PowerPlanNameEquivalence</summary>
public class PowerPlanNameEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public PowerPlanNameEquivalenceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ActivePowerPlanName_OldRawValues_matches_NewDynamicSelectionName()
    {
        var log = new Mock<ILogService>();
        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        var reg = new WindowsRegistryService(log.Object, interactiveUser.Object);

        // Real power query reading the live power scheme - shared so both engines see the same machine.
        var powerQuery = new PowerSettingsQueryService(log.Object);

        // OLD: the app's real discovery (its power-plan branch produces RawValues["ActivePowerPlan"]).
        var discovery = new SystemSettingsDiscoveryService(
            reg,
            log.Object,
            powerQuery,
            new Mock<ISpecialDiscoveryRegistry>().Object,
            new Mock<IScheduledTaskService>().Object,
            new Mock<ISystemRestoreService>().Object);

        // NEW: the real catalog detection engine over the live system context (same power query).
        var factory = new SystemDetectionContextFactory(
            reg,
            new Mock<ISystemRestoreService>().Object,
            new Mock<IScheduledTaskService>().Object,
            powerQuery,
            log.Object);
        var detection = new CatalogDetectionService(factory, log.Object);

        var oldDef = PowerOptimizations.GetPowerOptimizations().Settings
            .First(d => d.Id == SettingIds.PowerPlanSelection);
        var newSetting = SettingCatalog.All.First(s => s.Id == SettingIds.PowerPlanSelection);

        var oldStates = await discovery.GetSettingStatesAsync(new[] { oldDef });
        oldStates.TryGetValue(SettingIds.PowerPlanSelection, out var oldState);
        string? oldName = oldState?.RawValues != null
            && oldState.RawValues.TryGetValue("ActivePowerPlan", out var raw) ? raw?.ToString() : null;

        var newResults = await detection.DetectAsync(new[] { newSetting });
        newResults.TryGetValue(SettingIds.PowerPlanSelection, out var newResult);
        string? newName = newResult?.DynamicSelectionName;

        _output.WriteLine($"old RawValues[\"ActivePowerPlan\"]={Fmt(oldName)}  new DynamicSelectionName={Fmt(newName)}");

        Assert.Equal(oldName, newName);
    }

    private static string Fmt(string? v) => v ?? "<null>";
}
