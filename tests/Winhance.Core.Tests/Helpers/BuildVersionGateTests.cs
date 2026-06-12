using FluentAssertions;
using Winhance.Core.Features.Common.Helpers;
using Xunit;

namespace Winhance.Core.Tests.Helpers;

public class BuildVersionGateTests
{
    [Fact]
    public void NoConstraints_IsCompatible()
    {
        BuildVersionGate.IsCompatible(26100, 7171, null, null, null, null).Should().BeTrue();
    }

    [Theory]
    [InlineData(22000, 100, 26100, null, true)]  // below min build, no revision -> incompatible
    [InlineData(26099, 9999, 26100, null, true)] // one short of min -> incompatible
    [InlineData(26100, 0, 26100, null, false)]   // exactly min build, no revision constraint -> ok
    [InlineData(26101, 0, 26100, null, false)]   // above min -> ok
    public void MinBuild_OnlyMajor(int currentBuild, int currentRevision, int min, int? _, bool expectIncompatible)
    {
        BuildVersionGate
            .IsCompatible(currentBuild, currentRevision, min, null, null, null)
            .Should().Be(!expectIncompatible);
    }

    [Theory]
    [InlineData(26099, 9999, 7171, true)]  // major below min -> incompatible regardless of revision
    [InlineData(26100, 7170, 7171, true)]  // at boundary major, revision short -> incompatible
    [InlineData(26100, 7171, 7171, false)] // at boundary major, revision exact -> ok
    [InlineData(26100, 7172, 7171, false)] // at boundary major, revision above -> ok
    [InlineData(26101, 0, 7171, false)]    // above boundary major, revision ignored -> ok
    public void MinBuild_WithRevision(int currentBuild, int currentRevision, int minRevision, bool expectIncompatible)
    {
        BuildVersionGate
            .IsCompatible(currentBuild, currentRevision, 26100, minRevision, null, null)
            .Should().Be(!expectIncompatible);
    }

    [Theory]
    [InlineData(26119, 0, 26120, false)]   // below max -> ok
    [InlineData(26120, 0, 26120, false)]   // exactly max, no revision constraint -> ok
    [InlineData(26121, 0, 26120, true)]    // above max -> incompatible
    public void MaxBuild_OnlyMajor(int currentBuild, int currentRevision, int max, bool expectIncompatible)
    {
        BuildVersionGate
            .IsCompatible(currentBuild, currentRevision, null, null, max, null)
            .Should().Be(!expectIncompatible);
    }

    [Theory]
    [InlineData(26120, 4483, 4484, false)] // at max major, revision below -> ok
    [InlineData(26120, 4484, 4484, false)] // at max major, revision exact -> ok
    [InlineData(26120, 4485, 4484, true)]  // at max major, revision above -> incompatible
    [InlineData(26121, 0, 4484, true)]     // above max major -> incompatible regardless of revision
    public void MaxBuild_WithRevision(int currentBuild, int currentRevision, int maxRevision, bool expectIncompatible)
    {
        BuildVersionGate
            .IsCompatible(currentBuild, currentRevision, null, null, 26120, maxRevision)
            .Should().Be(!expectIncompatible);
    }

    [Theory]
    // Bounded on both sides: 26100.7171 to 26999 (open revision at top)
    [InlineData(26099, 9999, true)]  // below min -> incompatible
    [InlineData(26100, 7170, true)]  // at min major, revision short -> incompatible
    [InlineData(26100, 7171, false)] // at min major, revision exact -> ok
    [InlineData(26500, 0, false)]    // inside range -> ok
    [InlineData(27000, 0, true)]     // above max -> incompatible
    public void BothBounds(int currentBuild, int currentRevision, bool expectIncompatible)
    {
        BuildVersionGate
            .IsCompatible(currentBuild, currentRevision, 26100, 7171, 26999, null)
            .Should().Be(!expectIncompatible);
    }
}
