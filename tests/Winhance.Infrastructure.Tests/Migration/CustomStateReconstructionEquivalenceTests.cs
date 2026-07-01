using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Phase 6.9 precondition proof for the custom-state decoupling, run on Windows against the live machine.
/// Proves <see cref="CustomStateValueReconstructor.Build"/> (from the NEW provider's typed fields) reproduces the
/// custom-state bag the two consumers (SettingViewModelFactory.CapturedCustomStateValues + the AutounattendXml bulk
/// dump) captured off the OLD hybrid's <see cref="SettingStateResult.RawValues"/> - so migrating those consumers off
/// RawValues onto the reconstruction is value-identical, and SettingsLoadingService + Autounattend can then repoint to
/// the provider (dropping the overlay). The ORACLE is the REAL hybrid the consumers saw (old
/// <c>GetSettingStatesAsync</c> + <c>CatalogDetectionStateOverlay.Apply</c>), NOT raw old discovery, because the
/// overlay threaded the new AC/DC/Readings into what the consumers actually captured.
///
/// Population: catalog-paired SELECTIONS (both consumers only capture for a Selection), excluding the power-plan
/// selection (both consumers explicitly exclude it) and OS-merged settings (a build-gated RegTarget means the single-OS
/// old def differs from the multi-OS catalog setting). Covers registry (Readings), powercfg (ACValue/DCValue/
/// PowerCfgValue), and DNS/system-tray (DetectedIndex) selections.
///
/// Run: dotnet test --filter CustomStateReconstructionEquivalence</summary>
public class CustomStateReconstructionEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public CustomStateReconstructionEquivalenceTests(ITestOutputHelper output) => _output = output;

    private static IEnumerable<SettingDefinition> AllDefinitions() => new[]
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
    public async Task Reconstructor_reproduces_hybrid_RawValues_for_selections()
    {
        var log = new Mock<ILogService>();
        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        var reg = new WindowsRegistryService(log.Object, interactiveUser.Object);
        var powerQuery = new PowerSettingsQueryService(log.Object);

        var discovery = new SystemSettingsDiscoveryService(
            reg, log.Object, powerQuery,
            new Mock<ISpecialDiscoveryRegistry>().Object,
            new Mock<IScheduledTaskService>().Object,
            new Mock<ISystemRestoreService>().Object);

        var factory = new SystemDetectionContextFactory(
            reg,
            new Mock<ISystemRestoreService>().Object,
            new Mock<IScheduledTaskService>().Object,
            powerQuery,
            log.Object);
        var detection = new CatalogDetectionService(factory, log.Object);
        var provider = new CatalogSettingStateProvider(detection);

        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);

        var selectionDefs = AllDefinitions()
            .Where(d => d.InputType == InputType.Selection)
            .Where(d => d.Id != SettingIds.PowerPlanSelection)
            .Where(d => catalogById.ContainsKey(d.Id))
            .Where(d => catalogById[d.Id].Targets.OfType<RegTarget>().All(rt => rt.AppliesTo.Count == 0))
            .ToList();

        // ORACLE: the real hybrid the consumers saw (old discovery + overlay).
        var oldStates = await discovery.GetSettingStatesAsync(selectionDefs);
        var pairedSettings = selectionDefs.Select(d => catalogById[d.Id]).ToList();
        var newResults = await detection.DetectAsync(pairedSettings);
        var hybrid = new Dictionary<string, SettingStateResult>();
        foreach (var def in selectionDefs)
        {
            if (!oldStates.TryGetValue(def.Id, out var old))
                continue;
            newResults.TryGetValue(def.Id, out var nr);
            hybrid[def.Id] = CatalogDetectionStateOverlay.Apply(def, old, nr);
        }

        // NEW: the provider state the consumers WILL see after the repoint.
        var provided = await provider.GetStatesAsync(selectionDefs);

        int compared = 0;
        var mismatches = new List<string>();

        foreach (var def in selectionDefs)
        {
            if (!hybrid.TryGetValue(def.Id, out var h) || !provided.TryGetValue(def.Id, out var p))
                continue;

            // What the consumers captured: hybrid.RawValues filtered to non-null.
            var oldDict = (h.RawValues ?? new Dictionary<string, object?>())
                .Where(kv => kv.Value != null)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            // What they WILL capture: the reconstruction filtered to non-null.
            var newDict = CustomStateValueReconstructor.Build(catalogById[def.Id], p)
                .Where(kv => kv.Value != null)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            compared++;
            foreach (var key in oldDict.Keys.Union(newDict.Keys))
            {
                object? oldVal = oldDict.TryGetValue(key, out var ov) ? ov : null;
                object? newVal = newDict.TryGetValue(key, out var nv) ? nv : null;
                if (!ValueEquals(oldVal, newVal))
                    mismatches.Add($"{def.Id}[{key}]: hybrid={Fmt(oldVal)} reconstructed={Fmt(newVal)}");
            }
        }

        _output.WriteLine($"{compared - DistinctSettings(mismatches)}/{compared} selection custom-state bags match");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.True(compared > 0, "no selections were compared - the test is vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} custom-state value(s) differ between hybrid RawValues and the reconstruction - see output");
    }

    private static int DistinctSettings(IEnumerable<string> m) => m.Select(x => x.Split('[')[0]).Distinct().Count();

    private static bool ValueEquals(object? a, object? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        if (a is byte[] ba && b is byte[] bb)
            return ba.SequenceEqual(bb);
        return a.Equals(b);
    }

    private static string Fmt(object? v) => v switch
    {
        null => "<null>",
        byte[] bytes => "byte[" + string.Join(",", bytes) + "]",
        _ => $"{v} ({v.GetType().Name})",
    };
}
