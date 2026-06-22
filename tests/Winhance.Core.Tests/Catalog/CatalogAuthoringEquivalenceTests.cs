using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

/// <summary>The Phase B mechanical gate: every hand-authored catalog Setting must structurally equal the
/// converter's output for the same old definition (the converter is proven equivalent to the live app by the
/// Windows harness, so matching it proves the authored Setting is equivalent too). One test per migrated file.</summary>
public class CatalogAuthoringEquivalenceTests
{
    [Fact]
    public void WindowsTheme()
        => AssertCatalogMatches(
            WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings,
            WindowsThemeCustomizationsCatalog.All);

    [Fact]
    public void Sound()
        => AssertCatalogMatches(
            SoundOptimizations.GetSoundOptimizations().Settings,
            SoundOptimizationsCatalog.All);

    [Fact]
    public void Update()
        => AssertCatalogMatches(
            UpdateOptimizations.GetUpdateOptimizations().Settings,
            UpdateOptimizationsCatalog.All);

    [Fact]
    public void Notifications()
        => AssertCatalogMatches(
            NotificationOptimizations.GetNotificationOptimizations().Settings,
            NotificationOptimizationsCatalog.All);

    [Fact]
    public void GamingAndPerformance()
        => AssertCatalogMatches(
            GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings,
            GamingAndPerformanceOptimizationsCatalog.All);

    private static void AssertCatalogMatches(IReadOnlyList<SettingDefinition> old, IReadOnlyList<Setting> authored)
    {
        var byId = authored.ToDictionary(s => s.Id);
        Assert.Equal(old.Count, authored.Count); // nothing dropped, nothing added
        foreach (var def in old)
        {
            Assert.True(byId.ContainsKey(def.Id), $"authored catalog is missing setting '{def.Id}'");
            var diff = SettingStructuralComparer.Diff(byId[def.Id], Convert(def));
            Assert.True(diff.Count == 0, $"'{def.Id}' differs from converter output: {string.Join(" | ", diff)}");
        }
    }

    /// <summary>Routes an old definition to the converter for its mechanism (detectors before scheduled-task
    /// before the registry selection/toggle split), mirroring how the equivalence harness routes settings.</summary>
    private static Setting Convert(SettingDefinition def)
    {
        if (def.DetectionType == DetectionType.SystemTrayIcons) return SettingDefinitionConverter.ConvertSystemTray(def);
        if (def.DetectionType == DetectionType.SystemRestore) return SettingDefinitionConverter.ConvertSystemRestore(def);
        if (def.DetectionType == DetectionType.DnsServer) return SettingDefinitionConverter.ConvertDnsServer(def);
        if (def.ScheduledTaskSettings.Count > 0) return SettingDefinitionConverter.ConvertScheduledTaskToggle(def);
        if (def.InputType == InputType.Selection) return SettingDefinitionConverter.ConvertSelection(def);
        return SettingDefinitionConverter.ConvertToggle(def);
    }
}
