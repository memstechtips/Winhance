using FluentAssertions;
using Winhance.Core.Features.Common.Helpers;
using Xunit;

namespace Winhance.Core.Tests.Helpers;

public class ZoomLevelsTests
{
    [Fact]
    public void Next_From100_Returns110()
        => ZoomLevels.Next(1.0).Should().BeApproximately(1.1, 0.0001);

    [Fact]
    public void Next_AtMax_StaysAtMax()
        => ZoomLevels.Next(1.75).Should().BeApproximately(1.75, 0.0001);

    [Fact]
    public void Previous_From100_StaysAt100()
        => ZoomLevels.Previous(1.0).Should().BeApproximately(1.0, 0.0001);

    [Fact]
    public void Previous_From150_Returns140()
        => ZoomLevels.Previous(1.5).Should().BeApproximately(1.4, 0.0001);

    [Theory]
    [InlineData(0.5, 1.0)]
    [InlineData(2.5, 1.75)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.75, 1.75)]
    public void Clamp_BoundsInput(double input, double expected)
        => ZoomLevels.Clamp(input).Should().BeApproximately(expected, 0.0001);

    [Theory]
    [InlineData(1.13, 1.1)]
    [InlineData(1.16, 1.2)]
    [InlineData(1.04, 1.0)]
    public void SnapToStep_RoundsToGrid(double input, double expected)
        => ZoomLevels.SnapToStep(input).Should().BeApproximately(expected, 0.0001);

    [Fact]
    public void SnapToStep_NaN_ReturnsMin()
        => ZoomLevels.SnapToStep(double.NaN).Should().BeApproximately(1.0, 0.0001);
}
