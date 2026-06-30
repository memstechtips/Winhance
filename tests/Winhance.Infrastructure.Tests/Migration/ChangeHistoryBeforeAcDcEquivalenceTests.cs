using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Phase 6.8 Slice B equivalence gate, run on Windows against the live power scheme. Proves the per-setting
/// AC/DC value that the change-history before-receipt reads (<see cref="SettingApplicationService"/>.FormatBeforeDisplay
/// at the <c>:161</c> read) is identical whether sourced from the OLD discovery's
/// <c>RawValues["ACValue"/"DCValue"]</c> (its SINGLE-setting / PER-TARGET powercfg branch) or the NEW engine's typed
/// <c>AcValue/DcValue</c> (the BATCHED <c>GetAllPowerSettingsACDCAsync</c> read). The before-receipt calls
/// <c>GetSettingStatesAsync(new[]{ setting })</c> with ONE setting, so the old discovery takes the
/// <c>powerCfgSettings.Count == 1</c> per-target branch - which D1/D2's exporter equivalence (multi-setting, batched)
/// did NOT cover. This test closes that gap so the FormatBeforeDisplay swap to the typed fields is provably
/// value-preserving. Uses the REAL PowerSettingsQueryService + the REAL old discovery + the REAL new engine reading
/// the same machine.
///
/// Run: dotnet test --filter ChangeHistoryBeforeAcDcEquivalence</summary>
public class ChangeHistoryBeforeAcDcEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ChangeHistoryBeforeAcDcEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public async Task BeforeReceipt_AcDc_OldPerTarget_matches_NewBatched()
    {
        var log = new Mock<ILogService>();
        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        var reg = new WindowsRegistryService(log.Object, interactiveUser.Object);

        // Real power query reading the live power scheme - must NOT be mocked. Shared by both engines so they read the
        // same machine state.
        var powerQuery = new PowerSettingsQueryService(log.Object);

        // OLD: the app's real discovery, exactly as SettingApplicationService :161 constructs the before-state.
        var discovery = new SystemSettingsDiscoveryService(
            reg,
            log.Object,
            powerQuery,
            new Mock<ISpecialDiscoveryRegistry>().Object,
            new Mock<IScheduledTaskService>().Object,
            new Mock<ISystemRestoreService>().Object);

        // NEW: the real catalog detection engine over the live system context (same registry + power query).
        var factory = new SystemDetectionContextFactory(
            reg,
            new Mock<ISystemRestoreService>().Object,
            new Mock<IScheduledTaskService>().Object,
            powerQuery,
            log.Object);
        var detection = new CatalogDetectionService(factory, log.Object);

        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);

        // FormatBeforeDisplay's two AC/DC branches fire for any powercfg setting whose RawValues carry ACValue+DCValue
        // (both InputType.NumericRange and InputType.Selection). The old discovery populates those for every powercfg
        // setting (Separate and Both alike), so the population is every paired pure-powercfg setting except the
        // power-plan (which has no PowerCfgSettings).
        var powerDefs = AllDefinitions()
            .Where(d => d.PowerCfgSettings is { Count: > 0 })
            .Where(d => d.Id != SettingIds.PowerPlanSelection)
            .Where(d => catalogById.ContainsKey(d.Id))
            .ToList();

        int compared = 0;
        var mismatches = new List<string>();

        foreach (var def in powerDefs)
        {
            // OLD: single-setting batch -> the per-target GetPowerSettingACDCValuesAsync branch (Count == 1).
            var oldStates = await discovery.GetSettingStatesAsync(new[] { def });
            oldStates.TryGetValue(def.Id, out var oldState);
            int? oldAc = AsInt(oldState?.RawValues != null && oldState.RawValues.TryGetValue("ACValue", out var oa) ? oa : null);
            int? oldDc = AsInt(oldState?.RawValues != null && oldState.RawValues.TryGetValue("DCValue", out var od) ? od : null);

            // NEW: the batched GetAllPowerSettingsACDCAsync read, surfaced as the typed AcValue/DcValue.
            var newResults = await detection.DetectAsync(new[] { catalogById[def.Id] });
            newResults.TryGetValue(def.Id, out var newResult);
            int? newAc = newResult?.AcValue;
            int? newDc = newResult?.DcValue;

            compared++;
            if (oldAc != newAc || oldDc != newDc)
                mismatches.Add($"{def.Id}: old(AC={Fmt(oldAc)},DC={Fmt(oldDc)}) new(AC={Fmt(newAc)},DC={Fmt(newDc)})");
        }

        _output.WriteLine($"{compared - mismatches.Count}/{compared} powercfg settings match (AC+DC)");
        if (mismatches.Count > 0)
        {
            _output.WriteLine("Mismatches:");
            foreach (var m in mismatches)
                _output.WriteLine($"  {m}");
        }

        Assert.True(compared > 0, "no powercfg settings were compared - the test is vacuous");
        Assert.True(mismatches.Count == 0,
            $"{mismatches.Count} powercfg setting(s) differ between old per-target AC/DC and new batched AC/DC - see output");
    }

    private static int? AsInt(object? v) => v switch
    {
        null => null,
        int i => i,
        _ => int.TryParse(v.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : null,
    };

    private static string Fmt(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? "<null>";
}
