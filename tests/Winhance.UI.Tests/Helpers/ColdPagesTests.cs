using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Winhance.TestSupport;
using Winhance.UI.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class ColdPagesTests
{
    [Theory]
    [InlineData("Settings")]
    [InlineData("Autounattend")]
    [InlineData("WimUtil")]
    public void IsCold_UncachedPage_StaysColdAfterEveryVisit(string tag)
    {
        var pages = new ColdPages();
        pages.Arrived(tag);
        pages.Arrived(tag);

        pages.IsCold(tag).Should().BeTrue();
    }

    [Theory]
    [InlineData("Optimize")]
    [InlineData("Customize")]
    [InlineData("SoftwareApps")]
    public void IsCold_CachedPage_IsColdOnlyUntilTheFrameBuildsIt(string tag)
    {
        var pages = new ColdPages();
        pages.IsCold(tag).Should().BeTrue();

        pages.Arrived(tag);

        pages.IsCold(tag).Should().BeFalse();
    }

    // A cached page missing from the set flashes the loader on every visit; an uncached one listed in it is shown
    // half built with no loader.
    [Fact]
    public void CachedTags_Always_MatchTheCacheModeEachPageSets()
    {
        var uiDir = Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI");
        foreach (var (tag, pageType) in NavigationRouter.TagToPageType)
        {
            var xaml = Directory.GetFiles(uiDir, pageType.Name + ".xaml", SearchOption.AllDirectories).Should().ContainSingle().Subject;
            var mode = (string?)XDocument.Load(xaml).Root!.Attribute("NavigationCacheMode")
                ?? (Regex.Match(File.ReadAllText(xaml + ".cs"), @"NavigationCacheMode\s*=\s*NavigationCacheMode\.(\w+)") is { Success: true } m
                    ? m.Groups[1].Value
                    : "Disabled");

            ColdPages.CachedTags.Contains(tag).Should().Be(mode != "Disabled", $"{pageType.Name} sets NavigationCacheMode {mode}");
        }
    }
}
