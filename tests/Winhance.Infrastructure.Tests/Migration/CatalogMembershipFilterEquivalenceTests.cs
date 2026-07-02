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

/// <summary>Proves the pure CatalogMembershipFilter (OS + hardware gating off Setting.Availability) reproduces the
/// old WindowsCompatibilityFilter + HardwareCompatibilityFilter decisions, across several constructed environments
/// (Win10/Win11 x desktop/laptop). Machine-INDEPENDENT: the real old filters run with stubbed probe services, so the
/// gate is not green-by-luck on one machine. Existence filtering (powercfg presence) is deferred and not exercised on
/// either side. The additive foundation for repointing the registry membership off SettingDefinition.</summary>
public class CatalogMembershipFilterEquivalenceTests
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
    public void Catalog_membership_filter_matches_old_filters_across_environments()
    {
        var oldDefs = AllOldDefs();
        var problems = new List<string>();
        var envs = new (string Name, int Build, int Rev, HardwareCaps Caps)[]
        {
            ("Win11 desktop", 26100, 0, new HardwareCaps(false, false, true, true)),
            ("Win11 laptop", 26100, 0, new HardwareCaps(true, true, true, true)),
            ("Win10 desktop", 19045, 0, new HardwareCaps(false, false, false, false)),
            ("Win10 laptop", 19045, 0, new HardwareCaps(true, true, true, false)),
            ("early build", 10240, 0, new HardwareCaps(false, false, false, false)),
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
            var log = new Mock<ILogService>().Object;

            var osFilter = new WindowsCompatibilityFilter(ver.Object, log);
            var hwFilter = new HardwareCompatibilityFilter(hw.Object, log);
            var osFiltered = osFilter.FilterSettingsByWindowsVersion(oldDefs);
            var oldFiltered = hwFilter.FilterSettingsByHardwareAsync(osFiltered).GetAwaiter().GetResult();
            var oldIds = oldFiltered.Select(d => SettingIdAliases.Normalize(d.Id)).ToHashSet();

            var build = new WinBuild(env.Build, env.Rev);
            var newIds = SettingCatalog.All
                .Where(s => CatalogMembershipFilter.IsAvailable(s, build, env.Caps))
                .Select(s => s.Id)
                .ToHashSet();

            var missing = oldIds.Except(newIds).OrderBy(x => x).ToList();
            var extra = newIds.Except(oldIds).OrderBy(x => x).ToList();
            if (missing.Count > 0) problems.Add($"[{env.Name}] catalog MISSING {missing.Count}: {string.Join(", ", missing)}");
            if (extra.Count > 0) problems.Add($"[{env.Name}] catalog EXTRA {extra.Count}: {string.Join(", ", extra)}");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
