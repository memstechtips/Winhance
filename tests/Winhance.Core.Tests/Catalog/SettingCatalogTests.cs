using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class SettingCatalogTests
{
    [Fact]
    public void All_is_the_sum_of_every_catalog_with_no_drops()
    {
        int sum =
            WindowsThemeCustomizationsCatalog.All.Count
            + ExplorerCustomizationsCatalog.All.Count
            + TaskbarCustomizationsCatalog.All.Count
            + StartMenuCustomizationsCatalog.All.Count
            + SoundOptimizationsCatalog.All.Count
            + UpdateOptimizationsCatalog.All.Count
            + NotificationOptimizationsCatalog.All.Count
            + GamingAndPerformanceOptimizationsCatalog.All.Count
            + PrivacyOptimizationsCatalog.All.Count
            + PowerOptimizationsCatalog.All.Count;

        Assert.Equal(sum, SettingCatalog.All.Count);
    }

    [Fact]
    public void All_is_non_empty()
    {
        Assert.NotEmpty(SettingCatalog.All);
    }

    [Fact]
    public void Every_setting_id_is_unique()
    {
        var duplicates = SettingCatalog.All
            .GroupBy(s => s.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }
}
