using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

// NavButton parses IconSymbol at runtime and renders nothing on a miss, so the names are pinned here.
public class SidebarIconNamesTests
{
    private static readonly string UiDir = Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI");

    [Fact]
    public void Every_sidebar_icon_name_exists_in_the_fluent_set()
    {
        var xaml = File.ReadAllText(Path.Combine(UiDir, "Features", "Common", "Controls", "NavSidebar.xaml"));
        var resources = FeatureIconsXaml.Read();
        var iconNames = Regex.Matches(xaml, "IconSymbol=\"([^\"]+)\"")
            .Select(m => Resolve(m.Groups[1].Value, resources))
            .ToList();

        iconNames.Count.Should().BeGreaterThanOrEqualTo(7);
        foreach (var name in iconNames)
            Enum.TryParse<FluentIcons.Common.Icon>(name, ignoreCase: true, out _).Should().BeTrue(name);
    }

    // SectionPageShell.PageIcon is an enum attribute, so a page header cannot read the dictionary the sidebar reads.
    [Theory]
    [InlineData("Optimize", "OptimizeIconSymbol")]
    [InlineData("Customize", "CustomizeIconSymbol")]
    public void A_page_header_draws_the_sidebar_icon_of_its_destination(string page, string resourceKey)
    {
        var xaml = File.ReadAllText(Path.Combine(UiDir, "Features", page, page + "Page.xaml"));

        Regex.Match(xaml, "PageIcon=\"([^\"]+)\"").Groups[1].Value.Should().Be(FeatureIconsXaml.Read()[resourceKey]);
    }

    private static string Resolve(string attribute, Dictionary<string, string> resources)
    {
        var resource = Regex.Match(attribute, @"^\{StaticResource\s+(\w+)\}$");
        return resource.Success ? resources[resource.Groups[1].Value] : attribute;
    }
}
