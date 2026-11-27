using Microsoft.Win32;
using Winhance.Core.Features.Autounattend.Catalogs;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Customize.Catalogs;
using Winhance.Core.Features.Optimize.Catalogs;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class SettingCatalogTests
{
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

    [Theory]
    [InlineData(22631)]
    [InlineData(26300)]
    public void ClassicContextMenu_OnAndOff_ApplyBothShellOverrides(int build)
    {
        var setting = Assert.Single(ContextMenuCustomizationsCatalog.All, s => s.Id == "context-menu-classic");
        var version = new WinBuild(build);
        string[] paths =
        [
            @"HKEY_CURRENT_USER\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
            @"HKEY_CURRENT_USER\Software\Classes\CLSID\{51668040-5e31-4023-bbe3-149163bd80f1}\InprocServer32",
        ];

        var writes = ApplyPlanBuilder.Build(setting, LocKey.Common.Enabled, version).OfType<RegistryWriteOp>().ToArray();
        Assert.Equal(paths, writes.Select(op => op.Path).ToArray());
        Assert.All(writes, op =>
        {
            Assert.Equal("", op.Target.ValueName);
            Assert.Equal(RegistryValueKind.String, op.Target.Type);
            Assert.Equal("", op.Value);
        });
        var deletes = ApplyPlanBuilder.Build(setting, LocKey.Common.Disabled, version).OfType<RegistryDeleteOp>();
        Assert.Equal(paths, deletes.Select(op => op.Path).ToArray());
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
}
