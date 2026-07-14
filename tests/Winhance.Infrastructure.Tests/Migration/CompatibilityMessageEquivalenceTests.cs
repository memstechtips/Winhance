using System.Collections.Generic;
using System.Linq;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>EQUIVALENCE ORACLE -- references the old model; deleted at Plan-4 teardown.
/// Proves the pure AvailabilityCompatibility.DeriveCompatibilityMessage reproduces the old
/// WindowsCompatibilityFilter decorate mode (FilterSettingsByWindowsVersion applyFilter: false) byte-for-byte
/// for EVERY shipped def across a build/revision environment matrix. The ONLY pinned divergence is the merged
/// "This PC" alias PAIRS (each canonical Windows-11 def plus its -win10 variant, enumerated via
/// SettingIdAliases): their catalog peer is Availability.Everywhere, so the new side is null wherever the old
/// side decorated. Machine-independent: the real old filter runs with a stubbed IWindowsVersionService.</summary>
public class CompatibilityMessageEquivalenceTests
{
    private static IReadOnlyList<SettingDefinition> AllOldDefs() =>
        ExplorerCustomizations.GetExplorerCustomizations().Settings
        .Concat(StartMenuCustomizations.GetStartMenuCustomizations().Settings)
        .Concat(TaskbarCustomizations.GetTaskbarCustomizations().Settings)
        .Concat(WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings)
        .Concat(PowerOptimizations.GetPowerOptimizations().Settings)
        .Concat(GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings)
        .Concat(NotificationOptimizations.GetNotificationOptimizations().Settings)
        .Concat(PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings)
        .Concat(SoundOptimizations.GetSoundOptimizations().Settings)
        .Concat(UpdateOptimizations.GetUpdateOptimizations().Settings)
        .ToList();

    [Fact]
    public void Derived_compatibility_messages_match_old_decorator_across_environments()
    {
        var oldDefs = AllOldDefs();
        Assert.True(oldDefs.Count > 400, $"vacuity: only {oldDefs.Count} defs enumerated");

        // The merged "This PC" pairs, enumerated via SettingIdAliases: each def whose id normalizes away is an
        // alias variant; the pair is (variant id, canonical id).
        var expectedAliasPairIds = new HashSet<string>();
        foreach (var d in oldDefs)
        {
            var canonical = SettingIdAliases.Normalize(d.Id);
            if (canonical != d.Id)
            {
                expectedAliasPairIds.Add(d.Id);
                expectedAliasPairIds.Add(canonical);
            }
        }
        Assert.True(expectedAliasPairIds.Count > 0, "vacuity: no merged alias pairs enumerated");

        var envs = new (string Name, int Build, int Rev)[]
        {
            ("Win10 19045.0", 19045, 0),
            ("Win11 22000.0", 22000, 0),
            ("Win11 22621.3000", 22621, 3000),
            ("Win11 26100.3000", 26100, 3000),
            ("Win11 26100.5000", 26100, 5000),
            ("Win11 26200.8000", 26200, 8000),
        };

        var problems = new List<string>();
        var divergentIds = new HashSet<string>();
        var unpaired = new HashSet<string>();
        var sawMinBuildWithRevision = false;
        var sawMaxBuild = false;
        var sawBuildRange = false;
        var win11OnlyCountOnWin10Env = 0;
        var win10OnlyCountOn22621Env = 0;

        foreach (var env in envs)
        {
            var ver = new Mock<IWindowsVersionService>();
            ver.Setup(v => v.GetWindowsBuildNumber()).Returns(env.Build);
            ver.Setup(v => v.GetWindowsBuildRevision()).Returns(env.Rev);
            ver.Setup(v => v.IsWindows11()).Returns(env.Build >= 22000);
            var log = new Mock<ILogService>().Object;

            var oldFilter = new WindowsCompatibilityFilter(ver.Object, log);
            var decorated = oldFilter.FilterSettingsByWindowsVersion(oldDefs, applyFilter: false).ToList();
            Assert.Equal(oldDefs.Count, decorated.Count);

            var build = new WinBuild(env.Build, env.Rev);
            foreach (var def in decorated)
            {
                var paired = SettingCatalog.Find(def.Id);
                if (paired is null)
                {
                    unpaired.Add(def.Id);
                    continue;
                }

                var oldMsg = def.VersionCompatibilityMessage;
                var newMsg = AvailabilityCompatibility.DeriveCompatibilityMessage(paired.Availability, build);

                if (oldMsg is not null)
                {
                    if (oldMsg == "Compatibility_Windows11Only" && env.Build == 19045) win11OnlyCountOnWin10Env++;
                    if (oldMsg == "Compatibility_Windows10Only" && env.Build == 22621) win10OnlyCountOn22621Env++;
                    if (oldMsg.StartsWith("Compatibility_MinBuild|") && oldMsg.Contains('.')) sawMinBuildWithRevision = true;
                    if (oldMsg.StartsWith("Compatibility_MaxBuild|")) sawMaxBuild = true;
                    if (oldMsg.StartsWith("Compatibility_BuildRange|")) sawBuildRange = true;
                }

                if (oldMsg == newMsg) continue;

                // The pinned model-collapse divergence: the merged catalog peer is Everywhere, so ONLY
                // old-decorated -> new-null on an alias-pair id is expected. Any other divergence -- including
                // the other direction (old null, new non-null) -- is a failure.
                if (newMsg is null && oldMsg is not null && expectedAliasPairIds.Contains(def.Id))
                {
                    divergentIds.Add(def.Id);
                    continue;
                }

                problems.Add($"{def.Id} [{env.Name}]: old='{oldMsg ?? "<null>"}' new='{newMsg ?? "<null>"}'");
            }
        }

        if (unpaired.Count > 0)
            problems.Insert(0, $"UNPAIRED defs ({unpaired.Count}): {string.Join(", ", unpaired.OrderBy(x => x))}");
        Assert.True(problems.Count == 0, string.Join("\n", problems));

        Assert.True(divergentIds.SetEquals(expectedAliasPairIds),
            "pinned divergence set mismatch. Diverged-but-not-expected: "
            + string.Join(", ", divergentIds.Except(expectedAliasPairIds).OrderBy(x => x))
            + " | Expected-but-did-not-diverge: "
            + string.Join(", ", expectedAliasPairIds.Except(divergentIds).OrderBy(x => x)));

        Assert.True(win11OnlyCountOnWin10Env >= 65,
            $"vacuity: only {win11OnlyCountOnWin10Env} Windows11Only messages on the Win10 env (expected >= 65)");
        Assert.True(win10OnlyCountOn22621Env >= 19,
            $"vacuity: only {win10OnlyCountOn22621Env} Windows10Only messages on the 22621 env (expected >= 19)");
        Assert.True(sawMinBuildWithRevision, "vacuity: no MinBuild-with-revision message exercised");
        Assert.True(sawMaxBuild, "vacuity: no MaxBuild message exercised");
        Assert.True(sawBuildRange, "vacuity: no BuildRange message exercised");
    }
}
