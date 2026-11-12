using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Xunit;

namespace Winhance.Core.Tests.Utilities;

public class PackageVersionTests
{
    [Theory]
    [InlineData("25.01", "24.09", 1)]
    [InlineData("24.09", "24.9", 0)]
    [InlineData("1.2", "1.2.0.0", 0)]
    [InlineData("1.10", "1.9", 1)]
    [InlineData("10.0.403", "10.4.226.50611", -1)]
    [InlineData("1.2.3.4.5", "1.2.3.4", 1)]
    [InlineData("v2.0", "1.9.9", 1)]
    [InlineData("V1.0", "1.0", 0)]
    [InlineData("1.0.0+build.7", "1.0.0", 0)]
    [InlineData("1.0.0-beta", "1.0.0", -1)]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.10", -1)]
    [InlineData("1.0.0-beta", "1.0.0-alpha", 1)]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha", 1)]
    [InlineData("1.0.0-rc.1", "1.0.0-rc.beta", -1)]
    [InlineData("2026.11.20261101", "2026.11.9", 1)]
    [InlineData("99999999999999999999.1", "99999999999999999998.9", 1)]
    public void Compare_RealWorldVersions_OrdersThem(string left, string right, int expected) =>
        PackageVersion.Compare(left, right).Should().Be(expected);

    [Theory]
    [InlineData("Unknown", "1.0")]
    [InlineData("1.0", "")]
    [InlineData("1.0", null)]
    [InlineData("latest", "beta")]
    public void Compare_WhenASideHasNoVersionNumber_ReturnsNull(string? left, string? right) =>
        PackageVersion.Compare(left, right).Should().BeNull();

    [Theory]
    [InlineData("25.01", "24.09", true)]
    [InlineData("24.09", "24.09", false)]
    [InlineData("24.09", "25.01", false)]
    [InlineData("1.0.0", "1.0.0-rc.1", true)]
    [InlineData("1.0.0-rc.1", "1.0.0", false)]
    public void IsUpdate_Always_ReportsOnlyAStrictlyNewerVersion(string available, string installed, bool expected) =>
        PackageVersion.IsUpdate(available, installed).Should().Be(expected);

    [Theory]
    [InlineData("2.0", "Unknown", true)]
    [InlineData("2.0", null, true)]
    [InlineData("Latest", "latest", false)]
    [InlineData("", "1.0", false)]
    [InlineData(null, "1.0", false)]
    public void IsUpdate_WhenASideIsUnreadable_KeepsThePackageManagersVerdictUnlessNothingChanged(string? available, string? installed, bool expected) =>
        PackageVersion.IsUpdate(available, installed).Should().Be(expected);
}
