using FluentAssertions;
using Winhance.UI.Features.Common.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class ScrollRestoreTests
{
    [Fact]
    public void EndsOnceTheExtentReachesTheTarget()
    {
        var restore = new ScrollRestore(target: 500, startOffset: 0);

        restore.NextOffset(200).Should().Be(200);
        restore.IsDone.Should().BeFalse();

        restore.NextOffset(900).Should().Be(500);
        restore.IsDone.Should().BeTrue();
    }

    [Fact]
    public void AnUnreachableTarget_EndsAtTheReachableEnd_OnceTheExtentHoldsStill()
    {
        var restore = new ScrollRestore(target: 2000, startOffset: 0);

        for (var frame = 0; frame < ScrollRestore.SettleFrames; frame++)
        {
            restore.NextOffset(700).Should().Be(700);
            restore.IsDone.Should().BeFalse();
        }

        restore.NextOffset(700).Should().Be(700);
        restore.IsDone.Should().BeTrue();
    }

    [Fact]
    public void AGrowingExtent_KeepsTheRestoreGoing()
    {
        var restore = new ScrollRestore(target: 2000, startOffset: 0);

        for (var frame = 1; frame <= ScrollRestore.SettleFrames * 2; frame++)
        {
            restore.NextOffset(frame * 100);
            restore.IsDone.Should().BeFalse();
        }
    }

    [Fact]
    public void ContentThatHasNotArrived_WaitsUpToTheFrameCap()
    {
        var restore = new ScrollRestore(target: 500, startOffset: 0);

        for (var frame = 1; frame < ScrollRestore.MaxFrames; frame++)
        {
            restore.NextOffset(0).Should().Be(0);
            restore.IsDone.Should().BeFalse();
        }

        restore.NextOffset(0);
        restore.IsDone.Should().BeTrue();
    }

    [Fact]
    public void AnExtentThatKeepsChanging_StillEndsAtTheFrameCap()
    {
        var restore = new ScrollRestore(target: 100_000, startOffset: 0);

        for (var frame = 1; frame <= ScrollRestore.MaxFrames; frame++)
            restore.NextOffset(frame % 2 == 0 ? 300 : 400);

        restore.IsDone.Should().BeTrue();
    }

    [Fact]
    public void KnowsWhichOffsetsItCaused()
    {
        var restore = new ScrollRestore(target: 500, startOffset: 40);
        restore.NextOffset(300);

        restore.CausedOffset(40).Should().BeTrue();
        restore.CausedOffset(300.4).Should().BeTrue();
        restore.CausedOffset(180).Should().BeFalse();
    }
}
