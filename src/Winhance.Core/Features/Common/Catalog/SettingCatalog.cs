using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>The full live Setting catalog: every authored area's settings concatenated into one list. The single
/// source of truth the new detection and apply engines enumerate.</summary>
public static class SettingCatalog
{
    public static IReadOnlyList<Setting> All { get; } = new[]
    {
        WindowsThemeCustomizationsCatalog.All,
        ExplorerCustomizationsCatalog.All,
        TaskbarCustomizationsCatalog.All,
        StartMenuCustomizationsCatalog.All,
        SoundOptimizationsCatalog.All,
        UpdateOptimizationsCatalog.All,
        NotificationOptimizationsCatalog.All,
        GamingAndPerformanceOptimizationsCatalog.All,
        PrivacyOptimizationsCatalog.All,
        PowerOptimizationsCatalog.All,
    }.SelectMany(x => x).ToArray();

    /// <summary>The catalog partitioned by feature module (Explorer/Power/...), mirroring the old
    /// CompatibleSettingsRegistry.GetKnownFeatureProviders partition. Each *Catalog.cs declares its own
    /// FeatureId const; this is the catalog-sourced replacement for the old per-feature SettingDefinition groups.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<Setting>> ByFeature { get; } =
        new Dictionary<string, IReadOnlyList<Setting>>
        {
            [WindowsThemeCustomizationsCatalog.FeatureId] = WindowsThemeCustomizationsCatalog.All,
            [ExplorerCustomizationsCatalog.FeatureId] = ExplorerCustomizationsCatalog.All,
            [TaskbarCustomizationsCatalog.FeatureId] = TaskbarCustomizationsCatalog.All,
            [StartMenuCustomizationsCatalog.FeatureId] = StartMenuCustomizationsCatalog.All,
            [SoundOptimizationsCatalog.FeatureId] = SoundOptimizationsCatalog.All,
            [UpdateOptimizationsCatalog.FeatureId] = UpdateOptimizationsCatalog.All,
            [NotificationOptimizationsCatalog.FeatureId] = NotificationOptimizationsCatalog.All,
            [GamingAndPerformanceOptimizationsCatalog.FeatureId] = GamingAndPerformanceOptimizationsCatalog.All,
            [PrivacyOptimizationsCatalog.FeatureId] = PrivacyOptimizationsCatalog.All,
            [PowerOptimizationsCatalog.FeatureId] = PowerOptimizationsCatalog.All,
        };

    /// <summary>Every setting by its (canonical) Id - the O(1) pairing index.</summary>
    public static IReadOnlyDictionary<string, Setting> ById { get; } = All.ToDictionary(s => s.Id);

    /// <summary>The pairing primitive for the SettingDefinition retirement: given a setting id (canonical OR a
    /// retired -win10 alias), return its catalog Setting, or null if unpaired. Mirrors the live UI pairing
    /// (SettingsLoadingService uses SettingIdAliases.Normalize then looks up SettingCatalog.All by Id).</summary>
    public static Setting? Find(string settingId) =>
        ById.TryGetValue(SettingIdAliases.Normalize(settingId), out var s) ? s : null;
}
