using System.Text;
using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class StoreCatalogPackagesTests
{
    private const string Family = "Microsoft.WindowsCalculator_8wekyb3d8bbwe";

    private static readonly ulong Windows11 = Packed("10.0.26300.0");

    private static ulong Packed(string dotted)
    {
        PackedVersion.TryFromDotted(dotted, out var packed);
        return packed;
    }

    private static string Package(
        string version, int rank, string family = Family, string format = "AppxBundle",
        string architectures = "\"neutral\"", string platform = "Windows.Universal", string minVersion = "10.0.19041.0") =>
        $$"""
        {"PackageFamilyName":"{{family}}","Version":"{{Packed(version)}}","PackageRank":{{rank}},"PackageFormat":"{{format}}",
         "Architectures":[{{architectures}}],"PlatformDependencies":[{"PlatformName":"{{platform}}","MinVersion":"{{Packed(minVersion)}}"}]}
        """;

    private static Dictionary<string, ulong> Pick(string osArchitecture, params string[] packages)
    {
        var json = """{"Products":[{"DisplaySkuAvailabilities":[{"Sku":{"Properties":{"Packages":[@PACKAGES@]}}}]}]}"""
            .Replace("@PACKAGES@", string.Join(',', packages));
        return StoreCatalogPackages.PickVersionByFamily(new MemoryStream(Encoding.UTF8.GetBytes(json)), osArchitecture, Windows11);
    }

    [Fact]
    public void HigherRank_BeatsHigherVersion()
    {
        var picks = Pick("x64", Package("2017.523.613.1000", 20000), Package("2021.2607.0.0", 30000));

        picks[Family].Should().Be(Packed("2021.2607.0.0"));
    }

    [Fact]
    public void EqualRank_TakesTheHigherVersion()
    {
        var picks = Pick("x64", Package("2021.2502.2.0", 30000), Package("2021.2607.0.0", 30000), Package("2021.2507.0.0", 30000));

        picks[Family].Should().Be(Packed("2021.2607.0.0"));
    }

    [Fact]
    public void ThePackageIsListedTwiceForTwoSkus_StillOnePick()
    {
        var picks = Pick("x64", Package("2021.2607.0.0", 30000), Package("2021.2607.0.0", 30000));

        picks.Should().ContainSingle();
    }

    [Theory]
    [InlineData("x64", "\"x64\"", true)]
    [InlineData("x64", "\"X64\"", true)]
    [InlineData("x64", "\"arm64\"", false)]
    [InlineData("arm64", "\"x64\",\"x86\"", false)]
    [InlineData("arm64", "\"arm64\"", true)]
    [InlineData("x64", "\"neutral\"", true)]
    public void ArchitectureMustMatchTheOsOrBeNeutral(string os, string architectures, bool expected)
    {
        var picks = Pick(os, Package("2021.2607.0.0", 30000, architectures: architectures));

        picks.ContainsKey(Family).Should().Be(expected);
    }

    [Theory]
    [InlineData("AppxBundle", true)]
    [InlineData("Appx", true)]
    [InlineData("Msix", true)]
    [InlineData("MsixBundle", true)]
    [InlineData("EAppxBundle", false)]
    [InlineData("EAppx", false)]
    public void OnlyInstallableFormatsAreCandidates(string format, bool expected)
    {
        var picks = Pick("x64", Package("2021.2607.0.0", 30000, format: format));

        picks.ContainsKey(Family).Should().Be(expected);
    }

    [Theory]
    [InlineData("Windows.Universal", "10.0.19041.0", true)]
    [InlineData("Windows.Desktop", "10.0.26300.0", true)]
    [InlineData("Windows.Desktop", "10.0.26301.0", false)]
    [InlineData("Windows.Universal", "11.0.0.0", false)]
    [InlineData("Windows.Xbox", "10.0.19041.0", false)]
    public void ThePlatformMustBeDesktopAndNotNewerThanTheOs(string platform, string minVersion, bool expected)
    {
        var picks = Pick("x64", Package("2021.2607.0.0", 30000, platform: platform, minVersion: minVersion));

        picks.ContainsKey(Family).Should().Be(expected);
    }

    [Fact]
    public void ANewerPackageTheOsCannotRun_IsSkippedForTheOlderOneItCan()
    {
        var picks = Pick("x64",
            Package("2021.2607.0.0", 40000, minVersion: "10.0.30000.0"),
            Package("2021.2502.2.0", 30000));

        picks[Family].Should().Be(Packed("2021.2502.2.0"));
    }

    [Fact]
    public void TwoFamilies_EachGetTheirOwnPick()
    {
        var picks = Pick("x64", Package("1.0.0.0", 1), Package("2.0.0.0", 1, family: "Other_8wekyb3d8bbwe"));

        picks.Should().HaveCount(2);
        picks["other_8wekyb3d8bbwe"].Should().Be(Packed("2.0.0.0"));
    }

    [Fact]
    public void VersionAndMinVersionAsJsonNumbers_AreRead()
    {
        var json = """
            {"Products":[{"DisplaySkuAvailabilities":[{"Sku":{"Properties":{"Packages":[
            {"PackageFamilyName":"A_x","Version":@VERSION@,"PackageRank":5,"PackageFormat":"Msix","Architectures":["x64"],
             "PlatformDependencies":[{"PlatformName":"Windows.Desktop","MinVersion":@MIN@}]}]}}}]}]}
            """.Replace("@VERSION@", Packed("1.2.3.4").ToString()).Replace("@MIN@", Packed("10.0.17763.0").ToString());

        var picks = StoreCatalogPackages.PickVersionByFamily(new MemoryStream(Encoding.UTF8.GetBytes(json)), "x64", Windows11);

        picks["A_x"].Should().Be(Packed("1.2.3.4"));
    }

    [Fact]
    public void NoProducts_GivesNoPicks()
    {
        var picks = StoreCatalogPackages.PickVersionByFamily(new MemoryStream("""{"Products":[]}"""u8.ToArray()), "x64", Windows11);

        picks.Should().BeEmpty();
    }
}
