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
}
