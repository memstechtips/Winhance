using System.Xml.Linq;
using FluentAssertions;
using Winhance.Core.Features.Common.Constants;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

// SectionIconConverter draws nothing for a missing key or a Symbol it cannot parse, so both are pinned here.
public class FeatureIconsTests
{
    [Fact]
    public void Every_feature_and_every_sidebar_destination_has_an_icon_in_the_dictionary()
    {
        var resources = FeatureIconsXaml.Read();
        var keys = FeatureDefinitions.All.Select(f => f.IconKey)
            .Concat(FeatureDefinitions.All.Select(f => f.Category).Distinct().Select(FeatureDefinitions.CategoryIconKey))
            .ToList();

        keys.Should().HaveCount(18);
        foreach (var key in keys)
        {
            resources.Should().ContainKey(key);
            resources[key].Should().NotBeNullOrWhiteSpace(key);
        }
    }

    [Fact]
    public void Every_symbol_key_names_an_icon_in_the_fluent_set()
    {
        var symbols = FeatureIconsXaml.Read().Where(r => r.Key.EndsWith("Symbol", StringComparison.Ordinal)).ToList();

        symbols.Should().NotBeEmpty();
        foreach (var (key, value) in symbols)
        {
            Enum.TryParse<FluentIcons.Common.Icon>(value, ignoreCase: true, out _).Should().BeTrue(key);
        }
    }

    [Fact]
    public void An_unmapped_category_is_refused_loudly()
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
