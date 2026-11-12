using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Docs;

public class XamlTokensTests
{
    private static readonly ThemeExport Theme = XamlTokens.Extract(RepoPaths.SolutionDir());

    // The pills borrow Windows' own status colours, so each theme resolves them through WinUI's tokens.
    [Fact]
    public void Aliases_BothThemes_CarryBadgePalettes()
    {
        foreach (var theme in new[] { "light", "dark" })
        {
            var aliases = Theme.Aliases[theme];
            foreach (var role in new[] { "Recommended", "Default", "Preference" })
            {
                Assert.StartsWith("SystemFillColor", aliases[$"Badge{role}Background"]);
                Assert.Equal("ControlStrokeColorDefaultBrush", aliases[$"Badge{role}Border"]);
                Assert.StartsWith("SystemFillColor", aliases[$"Badge{role}Foreground"]);
            }
        }
    }

    [Fact]
    public void Aliases_TableStroke_ResolvesToDifferentWinUiTokenPerTheme()
    {
        Assert.Equal("CardStrokeColorDefaultBrush", Theme.Aliases["light"]["TechDetail.Table.StrokeBrush"]);
        Assert.Equal("ControlStrokeColorDefaultBrush", Theme.Aliases["dark"]["TechDetail.Table.StrokeBrush"]);
    }

    [Fact]
    public void Styles_TableStyles_CarryMetricsTheWebTableNeeds()
    {
        Assert.Equal("32", Theme.Styles["TechDetail.Table.HeaderBand"].Setters["MinHeight"]);
        Assert.Equal("12,4", Theme.Styles["TechDetail.Table.HeaderBand"].Setters["Padding"]);
        Assert.Equal("40", Theme.Styles["TechDetail.Table.Cell"].Setters["MinHeight"]);
        Assert.Equal("12,0", Theme.Styles["TechDetail.Table.Cell"].Setters["Padding"]);
        Assert.Equal("20", Theme.Styles["BadgePillBase"].Setters["Height"]);
        Assert.Equal("10", Theme.Styles["BadgePillBase"].Setters["CornerRadius"]);
        Assert.Equal("BadgePillBase", Theme.Styles["BadgeRecommendedStyle"].BasedOn);
        Assert.Equal("#FF0D1117", Theme.Styles["TechDetail.CodeBlock.PowerShell"].Setters["Background"]);
        Assert.Equal("Consolas,Cascadia Code,Courier New", Theme.Styles["TechDetail.CodeText"].Setters["FontFamily"]);
    }

    [Fact]
    public void Styles_ThemeResourceReferences_AreKeptAsReferences()
    {
        Assert.Equal("{ThemeResource SubtleFillColorSecondaryBrush}", Theme.Styles["TechDetail.Table.HeaderBand"].Setters["Background"]);
        Assert.Equal("{ThemeResource TechDetail.Table.StrokeBrush}", Theme.Styles["TechDetail.Table.Cell"].Setters["BorderBrush"]);
    }

    [Fact]
    public void Geometries_ThreePillGeometries_AreExportedWithViewbox()
    {
        foreach (var key in new[] { "BadgeRecommendedIconPath", "BadgeDefaultIconPath", "BadgePreferenceIconPath" })
        {
            Assert.StartsWith("M", Theme.Geometries[key].Data);
            Assert.InRange(Theme.Geometries[key].ViewBox, 8, 32);
        }

        Assert.Equal(3, Theme.Geometries.Count);
    }
}
