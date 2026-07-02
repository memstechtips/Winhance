using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Proves SettingCatalog.ByFeature reproduces the old CompatibleSettingsRegistry per-feature partition:
/// for each of the 10 feature modules, the catalog ids equal the old provider ids (each normalized via
/// SettingIdAliases, so the merged -win10 variants collapse to their canonical). Machine-independent; the
/// additive membership foundation for repointing the registry consumers off SettingDefinition.</summary>
public class CatalogFeaturePartitionEquivalenceTests
{
    private static readonly Dictionary<string, IReadOnlyList<SettingDefinition>> OldByFeature = new()
    {
        [FeatureIds.WindowsTheme] = WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings,
        [FeatureIds.ExplorerCustomization] = ExplorerCustomizations.GetExplorerCustomizations().Settings,
        [FeatureIds.Taskbar] = TaskbarCustomizations.GetTaskbarCustomizations().Settings,
        [FeatureIds.StartMenu] = StartMenuCustomizations.GetStartMenuCustomizations().Settings,
        [FeatureIds.Sound] = SoundOptimizations.GetSoundOptimizations().Settings,
        [FeatureIds.Update] = UpdateOptimizations.GetUpdateOptimizations().Settings,
        [FeatureIds.Notifications] = NotificationOptimizations.GetNotificationOptimizations().Settings,
        [FeatureIds.GamingPerformance] = GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings,
        [FeatureIds.Privacy] = PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings,
        [FeatureIds.Power] = PowerOptimizations.GetPowerOptimizations().Settings,
    };

    [Fact]
    public void Catalog_ByFeature_partition_matches_old_providers()
    {
        var problems = new List<string>();
        var comparedFeatures = 0;
        foreach (var (featureId, oldDefs) in OldByFeature)
        {
            comparedFeatures++;
            var expected = oldDefs.Select(d => SettingIdAliases.Normalize(d.Id)).ToHashSet();
            if (!SettingCatalog.ByFeature.TryGetValue(featureId, out var catalog))
            {
                problems.Add($"{featureId}: absent from SettingCatalog.ByFeature");
                continue;
            }
            var actual = catalog.Select(s => s.Id).ToHashSet();
            var missing = expected.Except(actual).OrderBy(x => x).ToList();
            var extra = actual.Except(expected).OrderBy(x => x).ToList();
            if (missing.Count > 0) problems.Add($"{featureId}: catalog MISSING {missing.Count}: {string.Join(", ", missing)}");
            if (extra.Count > 0) problems.Add($"{featureId}: catalog EXTRA {extra.Count}: {string.Join(", ", extra)}");
        }
        Assert.Equal(10, comparedFeatures);
        Assert.Equal(10, SettingCatalog.ByFeature.Count);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
