using FluentAssertions;
using Winhance.UI.Features.Common.Dialogs;
using Xunit;

namespace Winhance.UI.Tests.Utilities;

public class DialogListLayoutTests
{
    [Fact]
    public void ConfirmationList_CapsLongListsEvenOnTallWindows()
    {
        DialogListLayout.ListHeight(DialogListLayout.BodyHeight(1000), 200, DialogListLayout.ConfirmationListLimit)
            .Should().Be(250);
    }

    [Fact]
    public void ConfirmationList_LeavesSpaceForBothLegendsAndWrappedCheckboxDescription()
    {
        var body = DialogListLayout.BodyHeight(722);
        const double otherContentHeight = 330;
        var list = DialogListLayout.ListHeight(body, otherContentHeight, DialogListLayout.ConfirmationListLimit);

        list.Should().BeLessThan(DialogListLayout.ConfirmationListLimit);
        (list + otherContentHeight).Should().Be(body);
    }

    [Fact]
    public void SmallWindow_LeavesAScrollableListWhenFixedContentExceedsViewport()
    {
        var body = DialogListLayout.BodyHeight(480);
        DialogListLayout.ListHeight(body, 400, DialogListLayout.ConfirmationListLimit).Should().Be(64);
    }

    [Fact]
    public void SummaryList_UsesActualWrappedContentInsteadOfEstimatedLegendHeights()
    {
        var body = DialogListLayout.BodyHeight(650);
        DialogListLayout.ListHeight(body, 240, DialogListLayout.SummaryListLimit).Should().Be(230);
    }
}
