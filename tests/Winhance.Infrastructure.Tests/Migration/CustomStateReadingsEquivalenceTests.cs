using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Winhance.Core.Features.Common.Catalog;
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

/// <summary>Phase 6.8 D4 equivalence gate, run on Windows against the live registry. Proves the new engine's
/// <see cref="CatalogDetectionResult.Readings"/> reproduces the OLD discovery's RawValues for every per-registry-target
/// key the config-export custom-state path reads (<c>ValueName ?? "KeyExists"</c>) - so moving custom-state off
/// RawValues onto the new engine is value-preserving. It compares the REAL old discovery
/// (<see cref="SystemSettingsDiscoveryService.GetRawSettingsValuesAsync"/>) against the REAL new engine
/// (<see cref="CatalogDetectionService.DetectAsync"/>) reading the same live machine - NOT a reimplementation.
///
/// Population: pure-registry, catalog-paired settings (no powercfg / scheduled-task / DNS / system-restore branch, so
/// only the old discovery's registry branch runs and the mocked non-registry services are never touched). OS-merged
/// settings (any RegTarget carries an AppliesTo build gate) are excluded: their single-OS old def legitimately differs
/// from the multi-OS catalog setting, and they are toggles outside the custom-state selection path.
///
/// Run: dotnet test --filter CustomStateReadingsEquivalence</summary>
public class CustomStateReadingsEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public CustomStateReadingsEquivalenceTests(ITestOutputHelper output) => _output = output;

    /// <summary>Every SettingDefinition the app ships, straight from the static feature providers (no DI, no
    /// Windows-version filtering) - the same population the other migration equivalence tests use.</summary>
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
    public async Task Readings_reproduce_old_discovery_RawValues_for_registry_settings()
    {
        // Real registry service reading the live machine; its two ctor deps are not exercised by the read path.
        var log = new Mock<ILogService>();
        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        var reg = new WindowsRegistryService(log.Object, interactiveUser.Object);

        // OLD: the app's real discovery. Pure-registry settings only run the registry branch, so the four
        // non-registry sources are never invoked and stay no-op mocks.
        var discovery = new SystemSettingsDiscoveryService(
            reg,
            log.Object,
            new Mock<IPowerSettingsQueryService>().Object,
            new Mock<ISpecialDiscoveryRegistry>().Object,
            new Mock<IScheduledTaskService>().Object,
            new Mock<ISystemRestoreService>().Object);

        // NEW: the real catalog detection engine over the live system context (same registry instance).
        var factory = new SystemDetectionContextFactory(
            reg,
            new Mock<ISystemRestoreService>().Object,
            new Mock<IScheduledTaskService>().Object,
            new Mock<IPowerSettingsQueryService>().Object,
            log.Object);
        var detection = new CatalogDetectionService(factory, log.Object);

        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);

        var registryDefs = AllDefinitions()
            .Where(d => d.RegistrySettings is { Count: > 0 })
            .Where(d => (d.PowerCfgSettings?.Count ?? 0) == 0)
            .Where(d => (d.ScheduledTaskSettings?.Count ?? 0) == 0)
            .Where(d => d.DetectionType != DetectionType.DnsServer && d.DetectionType != DetectionType.SystemRestore)
            // Action settings (e.g. start-menu-clean-11) are modeled in the new catalog as apply-only Effects (a
            // RegistryWriteEffect, NOT a detectable RegTarget), so the new engine produces no readings for them. The
            // old discovery incidentally read their RegistrySettings, but the custom-state consumer is Selection-only
            // and never reads an Action's readings - so Actions are outside D4's scope.
            .Where(d => d.InputType != InputType.Action)
            .Where(d => catalogById.ContainsKey(d.Id))
            // Exclude OS-merged settings (any RegTarget gated by build) - single-OS old def vs multi-OS catalog setting.
            .Where(d => catalogById[d.Id].Targets.OfType<RegTarget>().All(rt => rt.AppliesTo.Count == 0))
            .ToList();

        var oldRaw = await discovery.GetRawSettingsValuesAsync(registryDefs);

        var pairedSettings = registryDefs.Select(d => catalogById[d.Id]).ToList();
        var newResults = await detection.DetectAsync(pairedSettings);

        int compared = 0;
        var mismatches = new List<string>();

        foreach (var def in registryDefs)
        {
            oldRaw.TryGetValue(def.Id, out var oldValues);
            var readings = newResults.TryGetValue(def.Id, out var r) ? r.Readings : null;

            // The custom-state consumer loops setting.RegistrySettings, keying by ValueName ?? "KeyExists" - mirror it.
            foreach (var key in def.RegistrySettings.Select(rs => rs.ValueName ?? "KeyExists").Distinct())
            {
                object? oldVal = oldValues != null && oldValues.TryGetValue(key, out var ov) ? ov : null;
                object? newVal = readings != null && readings.TryGetValue(key, out var nv) ? nv : null;

                compared++;
                if (!ValueEquals(oldVal, newVal))
                    mismatches.Add($"{def.Id}[{key}]: old={Fmt(oldVal)} new={Fmt(newVal)}");
            }
        }

        _output.WriteLine($"{compared - mismatches.Count}/{compared} reading keys match across {registryDefs.Count} registry settings");
        if (mismatches.Count > 0)
        {
            _output.WriteLine("Mismatches:");
            foreach (var m in mismatches)
                _output.WriteLine($"  {m}");
        }

        Assert.True(compared > 0, "no registry reading keys were compared - the test is vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} reading key(s) differ between old RawValues and new Readings - see output");
    }

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
