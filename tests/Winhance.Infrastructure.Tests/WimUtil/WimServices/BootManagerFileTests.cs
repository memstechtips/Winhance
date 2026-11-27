using FluentAssertions;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class BootManagerFileTests
{
    [Fact]
    public void Read_SignedByTheWindowsUefiCa2023_SaysSoAndReadsTheSecurityVersion()
    {
        var info = BootManagerFile.Read(FakeBootManager.Build("Windows UEFI CA 2023", 0x70000));

        info.SignedByCa2023.Should().BeTrue();
        info.SecurityVersion.Should().Be(0x70000u);
    }

    [Fact]
    public void Read_SignedByPca2011_IsNotA2023BootManager()
    {
        var info = BootManagerFile.Read(FakeBootManager.Build("Microsoft Windows Production PCA 2011", 0x50000));

        info.SignedByCa2023.Should().BeFalse();
        info.SecurityVersion.Should().Be(0x50000u);
    }

    [Fact]
    public void Read_NoSecurityVersionResource_ReturnsNone()
    {
        BootManagerFile.Read(FakeBootManager.Build("Microsoft Windows Production PCA 2011", null))
            .SecurityVersion.Should().BeNull();
    }
}
