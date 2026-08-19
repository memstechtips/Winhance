using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Docs;

public class XamlTokensTests
{
    private static readonly ThemeExport Theme = XamlTokens.Extract(RepoPaths.SolutionDir());

    [Fact]
    public void Both_themes_carry_the_badge_palettes()
    {
        foreach (var theme in new[] { "light", "dark" })
        {
            var colors = Theme.Themes[theme];
            foreach (var role in new[] { "Recommended", "Default", "Preference" })
            {
                Assert.Matches("^#[0-9A-Fa-f]{6,8}$", colors[$"Badge{role}Background"]);
                Assert.Matches("^#[0-9A-Fa-f]{6,8}$", colors[$"Badge{role}Border"]);
                Assert.Matches("^#[0-9A-Fa-f]{6,8}$", colors[$"Badge{role}Foreground"]);
            }
        }

        Assert.NotEqual(Theme.Themes["light"]["BadgeRecommendedForeground"], Theme.Themes["dark"]["BadgeRecommendedForeground"]);
    }

    [Fact]
    public void Table_stroke_resolves_to_a_different_winui_token_per_theme()
    {
        Assert.Equal("CardStrokeColorDefaultBrush", Theme.Aliases["light"]["TechDetail.Table.StrokeBrush"]);
        Assert.Equal("ControlStrokeColorDefaultBrush", Theme.Aliases["dark"]["TechDetail.Table.StrokeBrush"]);
    }

    [Fact]
    public void Styles_carry_the_metrics_the_web_table_needs()
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
    public void Theme_resource_references_are_kept_as_references()
    {
        Assert.Equal("{ThemeResource SubtleFillColorSecondaryBrush}", Theme.Styles["TechDetail.Table.HeaderBand"].Setters["Background"]);
        Assert.Equal("{ThemeResource TechDetail.Table.StrokeBrush}", Theme.Styles["TechDetail.Table.Cell"].Setters["BorderBrush"]);
    }

    [Fact]
    public void The_three_pill_geometries_are_exported_with_a_viewbox()
    {
        foreach (var key in new[] { "BadgeRecommendedIconPath", "BadgeDefaultIconPath", "BadgePreferenceIconPath" })
        {
            Assert.StartsWith("M", Theme.Geometries[key].Data);
            Assert.InRange(Theme.Geometries[key].ViewBox, 8, 32);
        }

        Assert.Equal(3, Theme.Geometries.Count);
    }
}
