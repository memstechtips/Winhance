using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice L2: pins <see cref="Display.CrossGroupChildSettings"/> (child setting id -> localization key,
/// the source the UI's cross-group info banner is built from) against the old defs. Exactly ONE def authors the
/// field (privacy-ads-promotional-master, 8 entries); every other setting must carry null (the converter maps a
/// null def field to null, never an empty dictionary).
///
/// TEARDOWN NOTE: CrossGroupChildSettings_CatalogMatchesEveryDef is ORACLE-SCOPED -- it enumerates the old
/// SettingDefinition population and dies at the Plan-4 teardown. CrossGroupChildSettings_CatalogPinsTheOneCrossGroupMap
/// is def-independent (catalog side only: the single carrier, its entry count, known pairs) and SURVIVES teardown.
/// Machine-independent: compiled objects only, no I/O. Run: dotnet test --filter CrossGroupChildSettingsConformance</summary>
public class CrossGroupChildSettingsConformanceTests
{
    private const string MasterId = "privacy-ads-promotional-master";

    /// <summary>Every old SettingDefinition the app ships, pulled straight from the static feature providers -
    /// the same raw population the sibling Migration equivalence tests use.</summary>
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
    public void CrossGroupChildSettings_CatalogMatchesEveryDef()
    {
        var defs = AllDefinitions().ToList();
        Assert.True(defs.Count > 400, $"vacuity: only {defs.Count} defs enumerated");

        // Vacuity guards on the def side: exactly one authored map, on the expected setting, with the real entry
        // count and a literally-pinned pair - a silently-emptied def cannot green-wash the sweep below.
        var authored = defs.Where(d => d.CrossGroupChildSettings is not null).ToList();
        var master = Assert.Single(authored);
        Assert.Equal(MasterId, master.Id);
        Assert.Equal(8, master.CrossGroupChildSettings!.Count);
        Assert.Equal("Setting_privacy-ads-promotional-master_Child_Spotlight",
            master.CrossGroupChildSettings["privacy-rotating-lock-screen"]);

        var mismatches = new List<string>();
        foreach (var def in defs)
        {
            var catalog = SettingCatalog.Find(def.Id);   // normalizes the retired -win10 alias ids
            if (catalog is null)
            {
                mismatches.Add($"{def.Id}: no catalog peer");
                continue;
            }

            var defMap = def.CrossGroupChildSettings;
            var catMap = catalog.Display.CrossGroupChildSettings;
            if ((defMap is null) != (catMap is null))
            {
                mismatches.Add($"{def.Id}: nullness differs (def {(defMap is null ? "null" : "non-null")}, catalog {(catMap is null ? "null" : "non-null")})");
                continue;
            }
            if (defMap is null) continue;

            if (catMap!.Count != defMap.Count)
            {
                mismatches.Add($"{def.Id}: catalog count {catMap.Count} != def count {defMap.Count}");
                continue;
            }
            foreach (var (childId, locKey) in defMap)
            {
                if (!catMap.TryGetValue(childId, out var catKey))
                    mismatches.Add($"{def.Id}[{childId}]: missing from catalog map");
                else if (catKey != locKey)
                    mismatches.Add($"{def.Id}[{childId}]: catalog '{catKey}' != def '{locKey}'");
            }
        }

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatch(es):\n{string.Join("\n", mismatches)}");
    }

    /// <summary>The teardown survivor: pins the catalog side alone, no old-model reference. Exactly one catalog
    /// Setting carries a cross-group child map; it is the ads/promotional master, with 8 entries and the known
    /// first/last pairs.</summary>
    [Fact]
    public void CrossGroupChildSettings_CatalogPinsTheOneCrossGroupMap()
    {
        var carriers = SettingCatalog.All.Where(s => s.Display.CrossGroupChildSettings is not null).ToList();
        var only = Assert.Single(carriers);
        Assert.Equal(MasterId, only.Id);

        var map = only.Display.CrossGroupChildSettings!;
        Assert.Equal(8, map.Count);
        Assert.Equal("Setting_privacy-ads-promotional-master_Child_Spotlight", map["privacy-rotating-lock-screen"]);
        Assert.Equal("Setting_privacy-ads-promotional-master_Child_StartSuggestions", map["start-show-suggestions"]);
    }
}
