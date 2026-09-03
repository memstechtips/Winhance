using FluentAssertions;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

public class UsbWriteLayoutPlannerTests
{
    [Theory]
    [InlineData(3_000_000_000L, false)]
    [InlineData(4_294_967_295L, false)]
    [InlineData(4_294_967_296L, true)]
    [InlineData(7_578_075_168L, true)]
    public void Plan_LargestFileVaries_RequiresSplitOnlyPastTheFat32Ceiling(
        long largestFileBytes, bool expected)
    {
        var layout = UsbWriteLayoutPlanner.Plan(
            totalPayloadBytes: 8_465_957_864L, largestFileBytes: largestFileBytes);

        layout.RequiresSplit.Should().Be(expected);
    }

    [Fact]
    public void SplitSizeMb_IsTheDocumented3800Megabytes()
    {
        UsbWriteLayoutPlanner.SplitSizeMb.Should().Be(3800);
    }

    [Fact]
    public void Plan_PayloadOneBytePastTheCeiling_ExceedsFat32Ceiling()
    {
        var layout = UsbWriteLayoutPlanner.Plan(
            totalPayloadBytes: UsbWriteLayoutPlanner.Fat32MaxVolumeBytes + 1, largestFileBytes: 1_000L);

        layout.ExceedsFat32Ceiling.Should().BeTrue();
    }

    [Fact]
    public void Plan_PayloadExactlyAtTheCeiling_Fits()
    {
        var layout = UsbWriteLayoutPlanner.Plan(
            totalPayloadBytes: UsbWriteLayoutPlanner.Fat32MaxVolumeBytes, largestFileBytes: 1_000L);

        layout.ExceedsFat32Ceiling.Should().BeFalse();
    }

    [Fact]
    public void SplitTargetPath_MediaRoot_PointsAtSourcesInstallSwm()
    {
        UsbWriteLayoutPlanner.SplitTargetPath(@"E:\")
            .Should().Be(@"E:\sources\install.swm");
    }
}
