using FluentAssertions;
using Winhance.UI.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class NavHistoryTests
{
    [Fact]
    public void BeginBack_AfterFirstPage_LeavesNothingToGoBackTo()
    {
        var history = new NavHistory();
        history.Arrived("SoftwareApps");

        history.CanGoBack.Should().BeFalse();
        history.BeginBack().Should().BeNull();
    }

    [Fact]
    public void BeginBack_Repeatedly_ReturnsToEachPageInTurn()
    {
        var history = new NavHistory();
        history.Arrived("SoftwareApps");
        history.Arrived("WimUtil");
        history.Arrived("Optimize");

        history.BeginBack().Should().Be("WimUtil");
        history.Arrived("WimUtil");
        history.EndBack();

        history.BeginBack().Should().Be("SoftwareApps");
        history.Arrived("SoftwareApps");
        history.EndBack();

        history.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public void BeginBack_AfterADeclinedBack_DropsThatPageSoTheNextBackGoesFurther()
    {
        var history = new NavHistory();
        history.Arrived("SoftwareApps");
        history.Arrived("WimUtil");
        history.Arrived("Optimize");

        history.BeginBack().Should().Be("WimUtil");
        history.EndBack();

        history.BeginBack().Should().Be("SoftwareApps");
    }

    [Fact]
    public void BeginBack_ToThePageAlreadyShowing_DropsThatEntryInsteadOfSticking()
    {
        var history = new NavHistory();
        history.Arrived("Optimize");
        history.Arrived("WimUtil");
        history.Arrived("Optimize");

        history.BeginBack().Should().Be("WimUtil");
        history.EndBack();
        history.BeginBack().Should().Be("Optimize");
        history.EndBack();

        history.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public void Arrived_AtTheDeclinedPageLater_StillRecordsThePageLeft()
    {
        var history = new NavHistory();
        history.Arrived("WimUtil");
        history.Arrived("Optimize");
        history.BeginBack();
        history.EndBack();

        history.Arrived("WimUtil");

        history.BeginBack().Should().Be("Optimize");
    }

    [Fact]
    public void Arrived_AtTheCurrentPageAgain_RecordsNothing()
    {
        var history = new NavHistory();
        history.Arrived("WimUtil");
        history.Arrived("WimUtil");

        history.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public void Arrived_BeyondTwentyPages_KeepsOnlyTheLastTwenty()
    {
        var history = new NavHistory();
        for (var i = 0; i < 30; i++)
            history.Arrived(i % 2 == 0 ? "Optimize" : "Customize");

        var steps = 0;
        while (history.BeginBack() is { } tag)
        {
            history.Arrived(tag);
            history.EndBack();
            steps++;
        }

        steps.Should().Be(20);
    }
}
