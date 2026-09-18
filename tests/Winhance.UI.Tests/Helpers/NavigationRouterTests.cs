using FluentAssertions;
using Winhance.UI.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class NavigationRouterTests
{
    // SetNavLock leaves a newly locked page only through the reverse map; a tag missing there strands the user.
    [Fact]
    public void Tag_and_page_maps_are_inverse()
    {
        NavigationRouter.PageTypeNameToTag.Should().HaveCount(NavigationRouter.TagToPageType.Count);
        foreach (var (tag, pageType) in NavigationRouter.TagToPageType)
            NavigationRouter.PageTypeNameToTag[pageType.Name].Should().Be(tag);
    }
}
