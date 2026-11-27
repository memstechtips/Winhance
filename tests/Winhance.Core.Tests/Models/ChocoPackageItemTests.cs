using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class ChocoPackageItemTests
{
    [Fact]
    public void Create_CarriesThePackageIdInTheIdAndAsTheChocolateyId()
    {
        var item = ChocoPackageItem.Create("vlc");

        item.Id.Should().Be("choco-vlc");
        item.ChocoPackageId.Should().Be("vlc");
        item.WinGetPackageId.Should().BeNull();
        item.GroupName.Should().Be(ChocoPackageItem.GroupName);
    }

    [Fact]
    public void FromId_RebuildsAPickSavedByItsId() =>
        ChocoPackageItem.FromId("choco-notepadplusplus")!.ChocoPackageId.Should().Be("notepadplusplus");

    [Theory]
    [InlineData("choco-")]
    [InlineData("winget-VideoLAN.VLC")]
    [InlineData("external-app-vlc")]
    public void FromId_IgnoresIdsThatAreNotChocolateyPicks(string id) => ChocoPackageItem.FromId(id).Should().BeNull();

    [Theory]
    [InlineData("winget-VideoLAN.VLC", "VideoLAN.VLC", null, null)]
    [InlineData("choco-vlc", null, "vlc", null)]
    [InlineData("msstore-9PMMSR1CGPWG", null, null, "9PMMSR1CGPWG")]
    public void PackageSearchItem_RebuildsEveryKindOfPick(string id, string? winGetId, string? chocoId, string? storeId)
    {
        var item = PackageSearchItem.FromId(id)!;

        item.WinGetPackageId?.Single().Should().Be(winGetId);
        item.ChocoPackageId.Should().Be(chocoId);
        item.MsStoreId.Should().Be(storeId);
        PackageSearchItem.Is(id).Should().BeTrue();
    }

    [Theory]
    [InlineData("winget-")]
    [InlineData("choco-")]
    [InlineData("msstore-")]
    public void PackageSearchItem_BuildsNoItemFromAnIdThatIsNotAPackageId(string prefix)
    {
        string[] packageIds =
        [
            "a\\..\\..\\Users\\Public",
            "..\\x",
            "vlc --source=https://example.test/feed",
            "vlc\"x",
            "vlc'x",
            "-vlc",
            "--config",
            "a/b",
            "a:b",
            "vlc;x",
            ".vlc",
        ];

        foreach (var packageId in packageIds)
            PackageSearchItem.FromId(prefix + packageId).Should().BeNull($"'{packageId}' is not a package id");
    }

    [Theory]
    [InlineData("winget-Notepad++.Notepad++")]
    [InlineData("winget-7zip.7zip")]
    [InlineData("winget-Microsoft.VCRedist.2015+.x64")]
    [InlineData("choco-notepadplusplus.install")]
    [InlineData("choco-python3_x86")]
    [InlineData("msstore-9PMMSR1CGPWG")]
    [InlineData("msstore-XP89DCGQ3K6VLD")]
    public void PackageSearchItem_StillBuildsAnItemFromARealPackageId(string id) =>
        PackageSearchItem.FromId(id).Should().NotBeNull();
}
