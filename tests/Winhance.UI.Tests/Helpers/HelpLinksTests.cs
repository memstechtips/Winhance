using FluentAssertions;
using Winhance.UI.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class HelpLinksTests
{
    [Fact]
    public void EveryPage_HasItsOwnDocsPage()
    {
        NavigationRouter.TagToPageType.Keys.Should().BeEquivalentTo(HelpLinks.DocsPageByTag.Keys,
            "a page added to the sidebar should open its own docs, not the docs home");
    }

    [Theory]
    [InlineData("Optimize", "https://winhance.net/docs/features/optimize.html")]
    [InlineData("WimUtil", "https://winhance.net/docs/guides/wimutil.html")]
    [InlineData(null, "https://winhance.net/docs/index.html")]
    [InlineData("More", "https://winhance.net/docs/index.html")]
    public void DocsFor_MapsThePageOrFallsBackToTheDocsHome(string? tag, string expected)
    {
        HelpLinks.DocsFor(tag).AbsoluteUri.Should().Be(expected);
    }

    [Fact]
    public void BugReport_OpensTheBugTemplate()
    {
        HelpLinks.BugReport.AbsoluteUri.Should()
            .Be("https://github.com/memstechtips/Winhance/issues/new?template=bug_report.yml");
    }
}
