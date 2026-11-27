using System.Text.RegularExpressions;
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
        Assert.Equal("20", Theme.Styles["BadgePillBase"].Setters["MinHeight"]);
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

    // The docs draw each pill's icon by this Fluent name.
    [Fact]
    public void Styles_PillIcons_CarryTheirFluentIconName()
    {
        Assert.Equal("Star", Theme.Styles["BadgeRecommendedIcon"].Setters["Icon"]);
        Assert.Equal("Apps", Theme.Styles["BadgeDefaultIcon"].Setters["Icon"]);
        Assert.Equal("Person", Theme.Styles["BadgePreferenceIcon"].Setters["Icon"]);
    }
    // A setter's {StaticResource} to an icon size must resolve on the website, which only has the export.
    [Fact]
    public void Sizes_EveryIconSizeAStyleReferences_IsExported()
    {
        var referenced = Theme.Styles.Values
            .SelectMany(s => s.Setters.Values)
            .Select(v => Regex.Match(v, @"^\{StaticResource\s+(\w+IconFontSize)\}$"))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(referenced);
        Assert.All(referenced, key => Assert.True(Theme.Sizes.ContainsKey(key), key));
        Assert.Equal("12", Theme.Sizes["CaptionIconFontSize"]);
    }
}
