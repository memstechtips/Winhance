using System.Collections.Generic;
using System.Linq;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Proves the catalog load order (SettingCatalog.ByFeature filtered by CatalogMembershipFilter)
/// reproduces the old CompatibleSettingsRegistry per-feature SEQUENCE in the default filter-ON scope. The
/// settings page renders the load list verbatim (BaseSettingsFeatureViewModel groups in first-encounter order),
/// so load order IS page order; the existing partition equivalence proves per-feature id SET equality only.
/// Machine-independent: the real old registry runs with stubbed probe services and an identity
/// powercfg-existence stub, so the mutating existence filter is neutralized on both sides (the new side never
/// applies it).</summary>
public class SettingsPageOrderEquivalenceTests
{
    // The same feature set the old registry serves (CompatibleSettingsRegistry.GetKnownFeatureProviders).
    private static readonly string[] KnownFeatureIds =
    {
        FeatureIds.ExplorerCustomization,
        FeatureIds.StartMenu,
        FeatureIds.Taskbar,
        FeatureIds.WindowsTheme,
        FeatureIds.Power,
        FeatureIds.GamingPerformance,
        FeatureIds.Notifications,
        FeatureIds.Privacy,
        FeatureIds.Sound,
        FeatureIds.Update,
    };

    [Fact]
    public void Catalog_load_order_matches_old_registry_sequence_across_environments()
    {
        var problems = new List<string>();
        var envs = new (string Name, int Build, int Rev, HardwareCaps Caps)[]
        {
            ("Win11 desktop", 22631, 0, new HardwareCaps(false, false, true, true)),
            ("Win11 laptop", 22631, 0, new HardwareCaps(true, true, true, true)),
            ("Win10 desktop", 19045, 0, new HardwareCaps(false, false, true, true)),
            ("Win10 laptop", 19045, 0, new HardwareCaps(true, true, true, true)),
            // Review finding: a >=26100 env so the three 26100-gated settings (compress-to,
            // taskbar-button-size 26100.4484, start-all-apps-view 26100.7171) are sequence-compared
            // too (start-menu-layout, max 26120, stays covered as well).
            ("Win11 26100.8000 desktop", 26100, 8000, new HardwareCaps(false, false, true, true)),
            ("Win11 26100.8000 laptop", 26100, 8000, new HardwareCaps(true, true, true, true)),
        };

        foreach (var env in envs)
        {
            var ver = new Mock<IWindowsVersionService>();
            ver.Setup(v => v.GetWindowsBuildNumber()).Returns(env.Build);
            ver.Setup(v => v.GetWindowsBuildRevision()).Returns(env.Rev);
            ver.Setup(v => v.IsWindows11()).Returns(env.Build >= 22000);
            var hw = new Mock<IHardwareDetectionService>();
            hw.Setup(h => h.HasBatteryAsync()).ReturnsAsync(env.Caps.HasBattery);
            hw.Setup(h => h.HasLidAsync()).ReturnsAsync(env.Caps.HasLid);
            hw.Setup(h => h.SupportsBrightnessControlAsync()).ReturnsAsync(env.Caps.SupportsBrightness);
            hw.Setup(h => h.SupportsHybridSleepAsync()).ReturnsAsync(env.Caps.SupportsHybridSleep);

            // Identity existence stub: NEITHER side runs the mutating powercfg-existence filter.
            var existence = new Mock<IPowerSettingsValidationService>();
            existence.Setup(p => p.FilterSettingsByExistenceAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
                .ReturnsAsync((IEnumerable<SettingDefinition> s) => s.ToList());
            var log = new Mock<ILogService>().Object;

            var registry = new CompatibleSettingsRegistry(
                new WindowsCompatibilityFilter(ver.Object, log),
                new HardwareCompatibilityFilter(hw.Object, log),
                existence.Object,
                log);
            registry.InitializeAsync().GetAwaiter().GetResult();

            var build = new WinBuild(env.Build, env.Rev);
            var comparedSettings = 0;
            var comparedFeatures = 0;

            foreach (var featureId in KnownFeatureIds)
            {
                comparedFeatures++;
                var oldSeq = registry.GetFilteredSettings(featureId).Select(d => d.Id).ToList();
                if (!SettingCatalog.ByFeature.TryGetValue(featureId, out var catalogSettings))
                {
                    problems.Add($"[{env.Name}] {featureId}: absent from SettingCatalog.ByFeature");
                    continue;
                }
                var newSeq = catalogSettings
                    .Where(s => CatalogMembershipFilter.IsAvailable(s, build, env.Caps))
                    .Select(s => s.Id)
                    .ToList();
                comparedSettings += oldSeq.Count;

                if (oldSeq.SequenceEqual(newSeq)) continue;

                var i = 0;
                while (i < oldSeq.Count && i < newSeq.Count && oldSeq[i] == newSeq[i]) i++;
                var oldAt = i < oldSeq.Count ? oldSeq[i] : "<end>";
                var newAt = i < newSeq.Count ? newSeq[i] : "<end>";
                problems.Add(
                    $"[{env.Name}] {featureId}: sequence diverges at index {i}: old='{oldAt}' new='{newAt}' "
                    + $"(old count {oldSeq.Count}, new count {newSeq.Count}); "
                    + $"old from {i}: {string.Join(", ", oldSeq.Skip(i).Take(8))} | "
                    + $"new from {i}: {string.Join(", ", newSeq.Skip(i).Take(8))}");
            }

            if (comparedFeatures != KnownFeatureIds.Length)
                problems.Add($"[{env.Name}] vacuity: compared {comparedFeatures} of {KnownFeatureIds.Length} features");
            if (comparedSettings <= 300)
                problems.Add($"[{env.Name}] vacuity: only {comparedSettings} settings compared");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
