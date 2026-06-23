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

/// <summary>Throwaway migration check, run on Windows: for the dynamic power-plan-selection setting, compares the
/// app's real active-plan read (PowerSettingsQueryService.GetActivePowerPlanAsync) against the new
/// PowerPlanDetector (fed the same GUID through PowerPlanDetectionContext). Both normalise the active scheme to a
/// lowercase GUID string ("none" when there is no active scheme), so a green run proves the detector returns the
/// same active plan the old stack does. Run: dotnet test --filter PowerPlanEquivalence</summary>
public class PowerPlanEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public PowerPlanEquivalenceTests(ITestOutputHelper output) => _output = output;

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

    /// <summary>Lowercases a scheme GUID string to the canonical comparison form; an empty/null GUID (no active
    /// scheme) collapses to "none".</summary>
    private static string Normalize(string? guid)
        => string.IsNullOrEmpty(guid) ? "none" : guid.ToLowerInvariant();

    [Fact]
    public async Task PowerPlanEquivalence_OldAndNewDetectionAgree()
    {
        var def = AllDefinitions().Single(d => d.Id == "power-plan-selection");

        // OLD baseline: the app's real active-plan read against the live power scheme.
        var log = new Mock<ILogService>();
        var powerQuery = new PowerSettingsQueryService(log.Object);
        var activePlan = await powerQuery.GetActivePowerPlanAsync();
        var old = Normalize(activePlan.Guid);

        // NEW: the converted Setting's PowerPlanDetector, fed the same active GUID through the harness context.
        var setting = SettingDefinitionConverter.ConvertPowerPlan(def);
        var ctx = new PowerPlanDetectionContext(activePlan.Guid);
        var newGuid = Normalize(CatalogDiscovery.DetectState(setting, ctx));

        var match = old == newGuid;
        _output.WriteLine($"{(match ? "[MATCH]" : "[DIFF]")} power-plan-selection: old={old} new={newGuid}");

        Assert.Equal(old, newGuid);
    }
}
