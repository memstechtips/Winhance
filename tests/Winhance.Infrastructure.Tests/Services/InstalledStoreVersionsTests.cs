using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class InstalledStoreVersionsTests
{
    private static ulong Packed(string dotted)
    {
        PackedVersion.TryFromDotted(dotted, out var packed);
        return packed;
    }

    [Fact]
    public void BundleKey_GivesTheFamilyAndBundleVersion()
    {
        InstalledStoreVersions.TryParseKeyName("Microsoft.WindowsCalculator_2021.2607.0.0_neutral_~_8wekyb3d8bbwe", out var family, out var version)
            .Should().BeTrue();

        family.Should().Be("Microsoft.WindowsCalculator_8wekyb3d8bbwe");
        version.Should().Be(Packed("2021.2607.0.0"));
    }

    [Fact]
    public void SinglePackageKey_GivesTheFamilyAndPackageVersion()
    {
        InstalledStoreVersions.TryParseKeyName("Microsoft.OutlookForWindows_1.2026.1001.200_x64__8wekyb3d8bbwe", out var family, out var version)
            .Should().BeTrue();

        family.Should().Be("Microsoft.OutlookForWindows_8wekyb3d8bbwe");
        version.Should().Be(Packed("1.2026.1001.200"));
    }

    [Theory]
    [InlineData("Microsoft.WindowsCalculator_2021.2607.0.0_neutral_split.scale-100_8wekyb3d8bbwe")]
    [InlineData("Microsoft.WindowsCalculator_notaversion_neutral_~_8wekyb3d8bbwe")]
    [InlineData("Microsoft.WindowsCalculator_2021.2607.0.0_neutral_~_")]
    [InlineData("NoUnderscores")]
    public void KeysThatAreNotABundleOrSinglePackage_AreIgnored(string key) =>
        InstalledStoreVersions.TryParseKeyName(key, out _, out _).Should().BeFalse();

    [Fact]
    public void Read_SeveralVersionsOfOneFamily_TakesTheHighest()
    {
        var registry = new Mock<IWindowsRegistryService>();
        registry.Setup(r => r.GetSubKeyNames(InstalledStoreVersions.Applications)).Returns(
        [
            "Microsoft.WindowsCalculator_2021.2502.2.0_neutral_~_8wekyb3d8bbwe",
            "Microsoft.WindowsCalculator_2021.2607.0.0_neutral_~_8wekyb3d8bbwe",
            "Microsoft.WindowsCalculator_2021.2507.0.0_neutral_~_8wekyb3d8bbwe",
            "Microsoft.WindowsNotepad_11.2607.14.0_neutral_~_8wekyb3d8bbwe",
        ]);

        var versions = InstalledStoreVersions.Read(registry.Object);

        versions["microsoft.windowscalculator_8wekyb3d8bbwe"].Should().Be(Packed("2021.2607.0.0"));
        versions.Should().HaveCount(2);
    }
}
