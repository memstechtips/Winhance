using Winhance.Core.Features.Autounattend.Catalogs;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Customize.Catalogs;
using Winhance.Core.Features.Optimize.Catalogs;
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
            + TimeRegionLanguageCatalog.All.Count
            + SoundOptimizationsCatalog.All.Count
            + UpdateOptimizationsCatalog.All.Count
            + NotificationOptimizationsCatalog.All.Count
            + GamingAndPerformanceOptimizationsCatalog.All.Count
            + PrivacyOptimizationsCatalog.All.Count
            + PowerOptimizationsCatalog.All.Count
            + AutounattendCatalog.All.Count;

        Assert.Equal(sum, SettingCatalog.All.Count);
    }

    [Fact]
    public void All_is_non_empty()
    {
        Assert.NotEmpty(SettingCatalog.All);
    }

    [Fact]
    public void A_card_covers_itself_and_the_children_drawn_under_it()
    {
        Assert.True(SettingCatalog.IsOrChildOf("theme-wallpaper", "theme-wallpaper"));
        Assert.True(SettingCatalog.IsOrChildOf("theme-wallpaper-picture", "theme-wallpaper"));
    }

    [Fact]
    public void A_card_covers_neither_an_unrelated_setting_nor_an_id_the_catalog_lacks()
    {
        Assert.False(SettingCatalog.IsOrChildOf("theme-mode-windows", "theme-wallpaper"));
        Assert.False(SettingCatalog.IsOrChildOf("no-such-setting", "theme-wallpaper"));
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
