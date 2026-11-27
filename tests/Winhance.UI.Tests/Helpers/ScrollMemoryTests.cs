using FluentAssertions;
using Winhance.UI.Features.Common.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class ScrollMemoryTests
{
    // The grid hands its rows back in reuse order, not top to bottom.
    [Fact]
    public void GridAnchor_KeepsHowFarTheViewSitsIntoAMostlyHiddenRow()
    {
        var anchor = ScrollMemory.GridAnchor([(7, 69, 48), (5, -27, 48), (6, 21, 48), (4, -75, 48)]);

        anchor.Should().Be((5, 27.0));
    }

    [Fact]
    public void GridAnchor_FlushRow_SitsNoWayIntoIt()
    {
        ScrollMemory.GridAnchor([(6, 48, 48), (5, 0, 48)]).Should().Be((5, 0.0));
    }

    [Fact]
    public void GridAnchor_NoRowInView_IsNone()
    {
        ScrollMemory.GridAnchor([(3, -48, 48)]).Should().BeNull();
    }
}
