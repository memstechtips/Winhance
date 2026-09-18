using Winhance.Core.Features.Autounattend.Catalogs;
using Winhance.Core.Features.Customize.Catalogs;
using Winhance.Core.Features.Optimize.Catalogs;

namespace Winhance.Core.Features.Common.Catalog;

public static class SettingCatalog
{
    public static IReadOnlyList<Setting> All { get; } = new[]
    {
        WindowsThemeCustomizationsCatalog.All,
        ExplorerCustomizationsCatalog.All,
        TaskbarCustomizationsCatalog.All,
        StartMenuCustomizationsCatalog.All,
        TimeRegionLanguageCatalog.All,
        SoundOptimizationsCatalog.All,
        UpdateOptimizationsCatalog.All,
        NotificationOptimizationsCatalog.All,
        GamingAndPerformanceOptimizationsCatalog.All,
        PrivacyOptimizationsCatalog.All,
        PowerOptimizationsCatalog.All,
        AutounattendCatalog.All,
    }.SelectMany(x => x).ToArray();

    public static IReadOnlyDictionary<string, IReadOnlyList<Setting>> ByFeature { get; } =
        new Dictionary<string, IReadOnlyList<Setting>>
        {
            [WindowsThemeCustomizationsCatalog.FeatureId] = WindowsThemeCustomizationsCatalog.All,
            [ExplorerCustomizationsCatalog.FeatureId] = ExplorerCustomizationsCatalog.All,
            [TaskbarCustomizationsCatalog.FeatureId] = TaskbarCustomizationsCatalog.All,
            [StartMenuCustomizationsCatalog.FeatureId] = StartMenuCustomizationsCatalog.All,
            [TimeRegionLanguageCatalog.FeatureId] = TimeRegionLanguageCatalog.All,
            [SoundOptimizationsCatalog.FeatureId] = SoundOptimizationsCatalog.All,
            [UpdateOptimizationsCatalog.FeatureId] = UpdateOptimizationsCatalog.All,
            [NotificationOptimizationsCatalog.FeatureId] = NotificationOptimizationsCatalog.All,
            [GamingAndPerformanceOptimizationsCatalog.FeatureId] = GamingAndPerformanceOptimizationsCatalog.All,
            [PrivacyOptimizationsCatalog.FeatureId] = PrivacyOptimizationsCatalog.All,
            [PowerOptimizationsCatalog.FeatureId] = PowerOptimizationsCatalog.All,
            [AutounattendCatalog.FeatureId] = AutounattendCatalog.All,
        };

    public static IReadOnlyDictionary<string, Setting> ById { get; } = All.ToDictionary(s => s.Id);

    // Accepts a canonical id OR a retired -win10 alias (SettingIdAliases.Normalize).
    public static Setting? Find(string settingId) =>
        ById.TryGetValue(SettingIdAliases.Normalize(settingId), out var s) ? s : null;

    public static bool IsOrChildOf(string settingId, string parentId) =>
        settingId == parentId
        || (ById.TryGetValue(settingId, out var setting) && setting.UiParentId == parentId);
}
