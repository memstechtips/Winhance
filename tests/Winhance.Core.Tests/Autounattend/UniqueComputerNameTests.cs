using FluentAssertions;
using Winhance.Core.Features.Autounattend;
using Xunit;

namespace Winhance.Core.Tests.Autounattend;

public class UniqueComputerNameTests
{
    [Theory]
    [InlineData("OFFICE", "OFFICE")]
    [InlineData("office-pc", "office-pc")]
    [InlineData(null, "WINHANCE")]
    [InlineData("", "WINHANCE")]
    [InlineData("--", "WINHANCE")]
    [InlineData("My PC!", "MyPC")]
    [InlineData("-LAB", "LAB")]
    [InlineData("WORKSTATION-01", "WORKSTATIO")]
    [InlineData("ABCDEFGHI-XYZ", "ABCDEFGHI")]
    [InlineData("ABCDEFGHIJKLMNO", "ABCDEFGHIJ")]
    public void Prefix_WithTypedName_KeepsNameCharactersAndLeavesRoomForTheEnding(string? typed, string expected) =>
        UniqueComputerName.Prefix(typed).Should().Be(expected);

    [Theory]
    [InlineData("ABCDEFGHIJKLMNO")]
    [InlineData("A-B-C-D-E-F-G-H")]
    [InlineData(null)]
    public void Name_WithAnyPrefix_FitsTheNetbiosLimitAndNeverEndsThePrefixOnADash(string? typed)
    {
        var name = UniqueComputerName.Name(typed, "7F3A");

        name.Length.Should().BeLessThanOrEqualTo(15);
        name.Should().EndWith("-7F3A").And.NotContain("--");
    }

    [Theory]
    [InlineData("PF3ABCDE", null, "BCDE")]
    [InlineData("5cg-12 34x", null, "234X")]
    [InlineData("To Be Filled By O.E.M.", "00:15:5D:01:7F:3A", "7F3A")]
    [InlineData("Default string", "00-15-5D-01-7F-3A", "7F3A")]
    [InlineData("0", "00155D017F3A", "7F3A")]
    [InlineData("00000000", "00155D017F3A", "7F3A")]
    [InlineData("123456789", "00155D017F3A", "7F3A")]
    [InlineData(null, "00:15:5d:01:7f:3a", "7F3A")]
    public void Ending_WithSerialAndNetworkCard_ComesFromTheSerialThenTheNetworkCard(string? serial, string? mac, string expected) =>
        UniqueComputerName.Ending(serial, mac).Should().Be(expected);

    [Fact]
    public void Ending_WithNoUsableSerialOrNetworkCard_LeavesItToRandomHex()
    {
        UniqueComputerName.Ending("NONE", null).Should().BeNull();
        UniqueComputerName.RandomEnding().Should().MatchRegex("^[0-9A-F]{4}$");
    }
}
