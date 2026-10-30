using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class WinGetPackageItemTests
{
    [Fact]
    public void Create_CarriesThePackageIdInTheIdAndAsTheWinGetId()
    {
        var item = WinGetPackageItem.Create("VideoLAN.VLC", "VLC media player");

        item.Id.Should().Be("winget-VideoLAN.VLC");
        item.Name.Should().Be("VLC media player");
        item.WinGetPackageId.Should().Equal("VideoLAN.VLC");
        item.GroupName.Should().Be(WinGetPackageItem.GroupName);
    }

    [Fact]
    public void FromId_RebuildsTheItemASavedIdNamed()
    {
        var item = WinGetPackageItem.FromId("winget-Notepad++.Notepad++");

        item.Should().NotBeNull();
        item!.WinGetPackageId.Should().Equal("Notepad++.Notepad++");
    }

    [Theory]
    [InlineData("external-app-vlc")]
    [InlineData("winget-")]
    [InlineData("")]
    public void FromId_ReturnsNullForAnIdThatNamesNoPackage(string id)
    {
        WinGetPackageItem.FromId(id).Should().BeNull();
    }
}
