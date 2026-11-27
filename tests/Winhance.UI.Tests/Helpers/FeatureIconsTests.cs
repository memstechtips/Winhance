using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Winhance.Core.Features.Common.Constants;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

// IconConverter draws nothing for a missing key or a Symbol it cannot parse, so both are pinned here.
public class FeatureIconsTests
{
    // A path geometry (a hand-drawn logo or mark) fails here: Marco's call (2026-11-16) is Fluent icons and cached
    // app logos only.
    [Fact]
    public void Keys_AreAllFluentSymbols()
    {
        FeatureIconsXaml.Read().Keys.Should().OnlyContain(k => k.EndsWith("Symbol", StringComparison.Ordinal),
            "every icon is a FluentIcon named by a ...Symbol key");
    }

    // A Setter value is parsed at runtime, so a misspelt Icon name compiles and then fails to load the dictionary.
    [Fact]
    public void BadgeIconStyles_NameIconsInFluentSet()
    {
        XNamespace ui = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var path = Path.Combine(
            RepoPaths.SolutionDir(), "src", "Winhance.UI", "Features", "Common", "Resources", "BadgeStyles.xaml");
        var icons = XDocument.Load(path).Root!.Elements(ui + "Style")
            .Where(s => ((string?)s.Attribute(x + "Key"))?.StartsWith("Badge", StringComparison.Ordinal) == true)
            .SelectMany(s => s.Elements(ui + "Setter").Where(e => (string?)e.Attribute("Property") == "Icon")
                .Select(e => (Key: (string)s.Attribute(x + "Key")!, Icon: (string)e.Attribute("Value")!)))
            .ToList();

        icons.Select(i => i.Key).Should().BeEquivalentTo("BadgeRecommendedIcon", "BadgeDefaultIcon", "BadgePreferenceIcon");
        foreach (var (key, icon) in icons)
            Enum.TryParse<FluentIcons.Common.Icon>(icon, out _).Should().BeTrue(key);
    }

    // FluentIcon.Icon is an enum attribute, so the tab cannot read the dictionary the section cards read.
    [Fact]
    public void ExternalAppsTabIcon_MatchesTheExternalAppsSectionIcon()
    {
        var xaml = File.ReadAllText(Path.Combine(
            RepoPaths.SolutionDir(), "src", "Winhance.UI", "Features", "SoftwareApps", "SoftwareAppsPage.xaml"));

        Regex.Match(xaml, @"x:Name=""ExternalAppsTabIcon""[^>]*\bIcon=""(\w+)""").Groups[1].Value
            .Should().Be(FeatureIconsXaml.Read().GetValueOrDefault("ExternalAppsIconSymbol"));
    }

    [Fact]
    public void IconKeys_EveryFeatureAndSidebarDestination_HaveIconInDictionary()
    {
        var resources = FeatureIconsXaml.Read();
        var keys = FeatureDefinitions.All.Select(f => f.IconKey)
            .Concat(FeatureDefinitions.All.Select(f => f.Category).Distinct().Select(FeatureDefinitions.CategoryIconKey))
            .ToList();

        keys.Should().HaveCount(19);
        foreach (var key in keys)
        {
            resources.Should().ContainKey(key);
            resources[key].Should().NotBeNullOrWhiteSpace(key);
        }
    }

    [Fact]
    public void SymbolKeys_Always_NameIconInFluentSet()
    {
        var symbols = FeatureIconsXaml.Read().Where(r => r.Key.EndsWith("Symbol", StringComparison.Ordinal)).ToList();

        symbols.Should().NotBeEmpty();
        foreach (var (key, value) in symbols)
        {
            Enum.TryParse<FluentIcons.Common.Icon>(value, ignoreCase: true, out _).Should().BeTrue(key);
        }
    }

    [Fact]
    public void CategoryIconKey_UnmappedCategory_Throws()
    {
        var act = () => FeatureDefinitions.CategoryIconKey("AdvancedTools");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

internal static class FeatureIconsXaml
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static Dictionary<string, string> Read()
    {
        var path = Path.Combine(
            RepoPaths.SolutionDir(), "src", "Winhance.UI", "Features", "Common", "Resources", "FeatureIcons.xaml");

        return XDocument.Load(path).Root!
            .Elements(Xaml + "String")
            .ToDictionary(el => (string)el.Attribute(Xaml + "Key")!, el => el.Value, StringComparer.Ordinal);
    }
}
