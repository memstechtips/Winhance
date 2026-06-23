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

    [Fact]
    public void Privacy()
        => AssertCatalogMatches(
            PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings,
            PrivacyOptimizationsCatalog.All);

    // The 6 ThisPC-folder settings each merge TWO old defs (a Win11 value toggle + a Win10 key-existence toggle
    // sharing display and loc) into ONE Setting with build-gated targets, so the 1:1 gate cannot apply to them.
    private static readonly string[] ThisPcMergedIds =
    {
        "explorer-customization-thispc-folder-desktop",
        "explorer-customization-thispc-folder-documents",
        "explorer-customization-thispc-folder-downloads",
        "explorer-customization-thispc-folder-music",
        "explorer-customization-thispc-folder-pictures",
        "explorer-customization-thispc-folder-videos",
    };

    [Fact]
    public void Explorer()
    {
        var old = ExplorerCustomizations.GetExplorerCustomizations().Settings;
        var authored = ExplorerCustomizationsCatalog.All;
        var merged = new HashSet<string>(ThisPcMergedIds);
        var win10 = new HashSet<string>(ThisPcMergedIds.Select(id => id + "-win10"));

        // 1:1 equivalence for every NON-merged setting (the 12 ThisPC old defs are verified build-aware below).
        AssertCatalogMatches(
            old.Where(d => !merged.Contains(d.Id) && !win10.Contains(d.Id)).ToList(),
            authored.Where(s => !merged.Contains(s.Id)).ToList());

        // Build-aware equivalence for the 6 merges: the setting PROJECTED at a Win11 build must equal the converter's
        // output for the Win11 def, and projected at a Win10 build must equal the converter's output for the -win10 def.
        var byId = authored.ToDictionary(s => s.Id);
        foreach (var id in ThisPcMergedIds)
        {
            AssertMergedProjectionMatches(byId[id], old.Single(d => d.Id == id), new WinBuild(22631));
            AssertMergedProjectionMatches(byId[id], old.Single(d => d.Id == id + "-win10"), new WinBuild(19045));
        }
    }

    private static void AssertMergedProjectionMatches(Setting merged, SettingDefinition osDef, WinBuild build)
    {
        var diff = SettingStructuralComparer.Diff(
            Normalize(ProjectAtBuild(merged, build) with { Id = osDef.Id }),
            Normalize(Convert(osDef)));
        Assert.True(diff.Count == 0,
            $"merged '{merged.Id}' projected at build {build.Build} differs from converter('{osDef.Id}'): {string.Join(" | ", diff)}");
    }

    /// <summary>Projects a build-adaptive setting onto a single build: keep only targets whose AppliesTo admits the
    /// build (and clear AppliesTo, since the single-OS converter target carries none), and restrict each state's Set
    /// to those active targets' keys. Reduces the merge to the one OS's live mechanism for comparison.</summary>
    private static Setting ProjectAtBuild(Setting s, WinBuild build)
    {
        var active = s.Targets.Where(t => t.AppliesTo.Count == 0 || t.AppliesTo.Any(r => r.Contains(build))).ToList();
        var keys = new HashSet<string>(active.Select(t => t.Key));
        return s with
        {
            Targets = active.Select(t => t with { AppliesTo = System.Array.Empty<BuildRange>() }).ToList(),
            States = s.States.Select(st => st with
            {
                Set = st.Set.Where(kv => keys.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value),
            }).ToList(),
        };
    }

    /// <summary>Clears the fields a merge deliberately diverges on between OSes so a per-build projection can be
    /// compared to the single-OS converter output: Roles (the OS-default badge is dropped in the merge) and
    /// Availability (the merge has no setting-level gate - the gate moved onto Target.AppliesTo).</summary>
    private static Setting Normalize(Setting s) => s with
    {
        Availability = Availability.Everywhere,
        States = s.States.Select(st => st with { Roles = System.Array.Empty<StateRole>() }).ToList(),
    };

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
        if (def.InputType == InputType.Action) return SettingDefinitionConverter.ConvertAction(def);
        if (def.DetectionType == DetectionType.SystemTrayIcons) return SettingDefinitionConverter.ConvertSystemTray(def);
        if (def.DetectionType == DetectionType.SystemRestore) return SettingDefinitionConverter.ConvertSystemRestore(def);
        if (def.DetectionType == DetectionType.DnsServer) return SettingDefinitionConverter.ConvertDnsServer(def);
        if (def.ScheduledTaskSettings.Count > 0) return SettingDefinitionConverter.ConvertScheduledTaskToggle(def);
        if (def.InputType == InputType.Selection) return SettingDefinitionConverter.ConvertSelection(def);
        return SettingDefinitionConverter.ConvertToggle(def);
    }
}
