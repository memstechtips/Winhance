using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Xunit;

namespace Winhance.Core.Tests.Utilities;

public class PackedVersionTests
{
    [Theory]
    [InlineData("2814751014977536", "10.0.19041.0")]
    [InlineData("562949953421312", "2.0.0.0")]
    [InlineData("0", "0.0.0.0")]
    public void ToDotted_UnpacksMajorMinorBuildRevision(string packed, string dotted)
    {
        PackedVersion.TryParse(packed, out var value).Should().BeTrue();

        PackedVersion.ToDotted(value).Should().Be(dotted);
    }

    [Theory]
    [InlineData("2021.2607.0.0")]
    [InlineData("12.130.2510.6003")]
    [InlineData("65535.65535.65535.65535")]
    public void TryFromDotted_RoundTripsThroughToDotted(string dotted)
    {
        PackedVersion.TryFromDotted(dotted, out var packed).Should().BeTrue();

        PackedVersion.ToDotted(packed).Should().Be(dotted);
    }

    [Fact]
    public void TryFromDotted_FewerThanFourParts_PadsWithZeros()
    {
        PackedVersion.TryFromDotted("1.2", out var packed).Should().BeTrue();

        PackedVersion.ToDotted(packed).Should().Be("1.2.0.0");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1.x.3.4")]
    [InlineData("1.70000.3.4")]
    [InlineData("-1.0.0.0")]
    public void TryFromDotted_NotAVersion_IsFalse(string text) =>
        PackedVersion.TryFromDotted(text, out _).Should().BeFalse();

    [Fact]
    public void TryParse_NotADecimalNumber_IsFalse()
    {
        PackedVersion.TryParse("1.2.3.4", out _).Should().BeFalse();
        PackedVersion.TryParse(null, out _).Should().BeFalse();
    }

    [Fact]
    public void FromVersion_MatchesTheDottedForm()
    {
        PackedVersion.TryFromDotted("10.0.26300.0", out var expected);

        PackedVersion.FromVersion(new Version(10, 0, 26300)).Should().Be(expected);
        PackedVersion.FromVersion(new Version(10, 0, 26300, 9457)).Should().BeGreaterThan(expected);
    }
}
