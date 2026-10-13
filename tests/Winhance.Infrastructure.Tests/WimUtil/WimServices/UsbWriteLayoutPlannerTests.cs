using FluentAssertions;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class UsbWriteLayoutPlannerTests
{
    [Theory]
    [InlineData("bootmgr")]
    [InlineData("bootmgr.efi")]
    [InlineData("bootmgfw.efi")]
    [InlineData("autounattend.xml")]
    [InlineData("sources")]
    [InlineData(@"sources\boot.wim")]
    [InlineData("boot")]
    [InlineData(@"boot\bcd")]
    [InlineData(@"efi\boot\bootx64.efi")]
    [InlineData(@"EFI\Microsoft\Boot\BCD")]
    public void IsOnBootPartition_WhatThePcStartsFrom_IsOnIt(string relativePath)
    {
        UsbWriteLayoutPlanner.IsOnBootPartition(relativePath).Should().BeTrue();
    }

    [Theory]
    [InlineData(@"sources\install.wim")]
    [InlineData(@"sources\install.esd")]
    [InlineData(@"sources\$OEM$")]
    [InlineData(@"sources\$OEM$\$1\Winhance\Setup.exe")]
    [InlineData("$WinpeDriver$")]
    [InlineData("support")]
    [InlineData("setup.exe")]
    [InlineData("bootsect.exe")]
    [InlineData("efisys")]
    public void IsOnBootPartition_TheRest_StaysOff(string relativePath)
    {
        UsbWriteLayoutPlanner.IsOnBootPartition(relativePath).Should().BeFalse();
    }

    [Theory]
    [InlineData(@"efi\boot", true)]
    [InlineData(@"efi\boot\bootx64.efi", true)]
    [InlineData(@"efi\microsoft\boot\bcd", false)]
    [InlineData(@"boot\bcd", false)]
    [InlineData(@"sources\install.wim", false)]
    [InlineData("autounattend.xml", false)]
    public void IsLeftOffInstallPartition_OnlyTheFirmwareFallbackPath(string relativePath, bool leftOff)
    {
        UsbWriteLayoutPlanner.IsLeftOffInstallPartition(relativePath).Should().Be(leftOff);
    }

    [Fact]
    public void Plan_CountsEachFileOnEveryPartitionItLandsOn()
    {
        var sizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            [@"sources\boot.wim"] = 600,
            [@"efi\boot\bootx64.efi"] = 3,
            ["autounattend.xml"] = 1,
            [@"sources\install.wim"] = 7_000,
            ["setup.exe"] = 100,
        };

        var layout = UsbWriteLayoutPlanner.Plan(sizes);

        layout.BootBytes.Should().Be(604);
        layout.InstallBytes.Should().Be(7_701);
    }
}
