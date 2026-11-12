using Winhance.Core.Features.Autounattend.Catalogs;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Customize.Catalogs;
using Winhance.Core.Features.Optimize.Catalogs;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class SettingCatalogTests
{
    private static readonly string[] OutcomeNoteSettings = ["start-recommended-section", "theme-wallpaper"];

    [Fact]
    public void All_Always_IsTheSumOfEveryCatalogWithNoDrops()
    {
        int sum =
            WindowsThemeCustomizationsCatalog.All.Count
            + ExplorerCustomizationsCatalog.All.Count
            + ContextMenuCustomizationsCatalog.All.Count
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
    public void ContextMenuCustomizationsCatalog_Always_PutsEverySettingOnTheContextMenuPage()
    {
        Assert.Equal(FeatureIds.ContextMenu, ContextMenuCustomizationsCatalog.FeatureId);
        Assert.All(ContextMenuCustomizationsCatalog.All, s =>
        {
            Assert.StartsWith("context-menu-", s.Id);
            Assert.Equal(FeatureIds.ContextMenu, SettingCatalog.FeatureById[s.Id]);
        });
        Assert.DoesNotContain(ExplorerCustomizationsCatalog.All, s => s.Id.Contains("context-menu"));
    }

    [Fact]
    public void All_Always_IsNotEmpty()
    {
        Assert.NotEmpty(SettingCatalog.All);
    }

    [Fact]
    public void All_Always_HasUniqueSettingIds()
    {
        var duplicates = SettingCatalog.All
            .GroupBy(s => s.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    // A Warning shows only once its option is chosen, so it is for outcome notes. Anything that warns of harm
    // is a RiskWarning and shows before the choice.
    [Fact]
    public void All_Always_UsesWarningOnlyOnTheTwoOutcomeNotes()
    {
        var outcomeNotes = SettingCatalog.All
            .Where(s => s.States.Any(st => st.Warning is not null))
            .Select(s => s.Id)
            .Order()
            .ToList();

        Assert.Equal(OutcomeNoteSettings, outcomeNotes);
    }

    [Fact]
    public void All_Always_NeverCarriesBothAWarningAndARiskWarningOnOneState()
    {
        var both = SettingCatalog.All
            .Where(s => s.States.Any(st => st.Warning is not null && st.RiskWarning is not null))
            .Select(s => s.Id)
            .ToList();

        Assert.Empty(both);
    }
}
