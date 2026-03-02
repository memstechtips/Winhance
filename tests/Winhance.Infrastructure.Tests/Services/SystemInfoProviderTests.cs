using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SystemInfoProviderTests
{
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();

    #region Constructor

    [Fact]
    public void Constructor_NullInteractiveUserService_ThrowsArgumentNullException()
    {
        var act = () => new SystemInfoProvider(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("interactiveUserService");
    }

    [Fact]
    public void Constructor_ValidService_CreatesInstance()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        provider.Should().NotBeNull();
    }

    #endregion

    #region Collect — resilience

    [Fact]
    public void Collect_DoesNotThrow()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        var act = () => provider.Collect();

        act.Should().NotThrow();
    }

    [Fact]
    public void Collect_ReturnsNonNullSystemInfo()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        var info = provider.Collect();

        info.Should().NotBeNull();
    }

    #endregion

    #region Field-specific assertions

    [Fact]
    public void Collect_Architecture_IsRecognizedValue()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        var info = provider.Collect();

        info.Architecture.Should().BeOneOf("x64", "x86", "arm64", "arm");
    }

    [Fact]
    public void Collect_OperatingSystem_ContainsWindows()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        var info = provider.Collect();

        info.OperatingSystem.Should().Contain("Windows");
    }

    [Fact]
    public void Collect_Cpu_ContainsCores()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        var info = provider.Collect();

        info.Cpu.Should().Contain("cores");
    }

    [Fact]
    public void Collect_Ram_ContainsGB()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        var info = provider.Collect();

        info.Ram.Should().Contain("GB");
    }

    [Fact]
    public void Collect_DeviceType_IsRecognizedValue()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        var info = provider.Collect();

        info.DeviceType.Should().MatchRegex("Desktop|Laptop|Workstation|Slate|Server|Other");
    }

    [Fact]
    public void Collect_Elevation_IsRecognizedValue()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        var info = provider.Collect();

        info.Elevation.Should().BeOneOf("Admin", "Admin (OTS)", "Standard");
    }

    [Fact]
    public void Collect_DotNetRuntime_ContainsDotNet()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        var info = provider.Collect();

        info.DotNetRuntime.Should().Contain(".NET");
    }

    [Fact]
    public void Collect_Gpu_IsNotEmpty()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        var info = provider.Collect();

        info.Gpu.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Collect_FirmwareType_IsRecognizedValue()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        var info = provider.Collect();

        info.FirmwareType.Should().BeOneOf("UEFI", "Legacy BIOS", "Unknown");
    }

    [Fact]
    public void Collect_SecureBoot_IsRecognizedValue()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object);

        var info = provider.Collect();

        info.SecureBoot.Should().BeOneOf("Enabled", "Disabled", "Not Supported", "Unknown");
    }

    #endregion
}
