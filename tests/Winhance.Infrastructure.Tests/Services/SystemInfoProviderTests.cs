using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SystemInfoProviderTests
{
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();
    private readonly FakeWmiApi _wmiApi = new();

    // The facts built on RealWmiApi read whatever machine runs the suite, which is the point for the
    // fields that have no parsing to check. Asserting a parsed field that way fails outright wherever
    // WMI is degraded and proves almost nothing when it passes, so those facts register constructed
    // instances on the fake instead.
    private static readonly IWmiApi RealWmiApi = new WmiManagementApi();

    private SystemInfoProvider CreateWithFakeWmi() =>
        new(_mockInteractiveUserService.Object, _wmiApi);

    [Fact]
    public void Constructor_ValidService_CreatesInstance()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object, RealWmiApi);

        provider.Should().NotBeNull();
    }

    [Fact]
    public void Collect_DoesNotThrow()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object, RealWmiApi);

        var act = () => provider.Collect();

        act.Should().NotThrow();
    }

    [Fact]
    public void Collect_ReturnsNonNullSystemInfo()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object, RealWmiApi);

        var info = provider.Collect();

        info.Should().NotBeNull();
    }

    [Fact]
    public void Collect_WhenWmiQueryThrows_FieldsFallBackWithoutThrowing()
    {
        // The worker's real state: WMI answers nothing. Collect must degrade, not throw.
        var wmiApi = new Mock<IWmiApi>();
        wmiApi
            .Setup(api => api.Query(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Throws(new InvalidOperationException("WMI is down"));
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object, wmiApi.Object);

        var act = () => provider.Collect();

        act.Should().NotThrow();

        var info = provider.Collect();
        info.DeviceType.Should().Be("Unknown");
        info.Ram.Should().Be("Unknown");
        info.Cpu.Should().Be("Unknown");
        info.Gpu.Should().Be("Unknown");
        info.DomainJoined.Should().Be("Unknown");
        info.Tpm.Should().Be("Not Detected");
    }

    [Fact]
    public void Collect_Architecture_IsRecognizedValue()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object, RealWmiApi);

        var info = provider.Collect();

        info.Architecture.Should().BeOneOf("x64", "x86", "arm64", "arm");
    }

    [Fact]
    public void Collect_OperatingSystem_ContainsWindows()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object, RealWmiApi);

        var info = provider.Collect();

        info.OperatingSystem.Should().Contain("Windows");
    }

    [Fact]
    public void Collect_Cpu_FormatsNameWithLogicalProcessorCount()
    {
        _wmiApi.For("Win32_Processor").Add(new FakeWmiInstance
        {
            ["Name"] = "Intel Core i7-9700K",
            ["NumberOfLogicalProcessors"] = 8,
        });
        var provider = CreateWithFakeWmi();

        var info = provider.Collect();

        info.Cpu.Should().Be("Intel Core i7-9700K (8 cores)");
    }

    [Fact]
    public void Collect_Cpu_WithoutLogicalProcessorCount_ReturnsNameOnly()
    {
        _wmiApi.For("Win32_Processor").Add(new FakeWmiInstance
        {
            ["Name"] = "Intel Core i7-9700K",
            ["NumberOfLogicalProcessors"] = 0,
        });
        var provider = CreateWithFakeWmi();

        var info = provider.Collect();

        info.Cpu.Should().Be("Intel Core i7-9700K");
    }

    [Fact]
    public void Collect_Ram_RoundsTotalPhysicalMemoryToWholeGb()
    {
        // 17179869184 bytes is exactly 16 GiB; the provider rounds to whole GB and appends the
        // unit, so a bytes-to-GB regression changes this string.
        _wmiApi.For("Win32_ComputerSystem").Add(new FakeWmiInstance
        {
            ["TotalPhysicalMemory"] = 17179869184L,
        });
        var provider = CreateWithFakeWmi();

        var info = provider.Collect();

        info.Ram.Should().Be("16 GB");
    }

    [Fact]
    public void Collect_DeviceType_MapsPcSystemTypeToLabel()
    {
        _wmiApi.For("Win32_ComputerSystem").Add(new FakeWmiInstance
        {
            ["PCSystemType"] = 2,
            ["Model"] = "Latitude 7440",
            ["Manufacturer"] = "Dell Inc.",
        });
        var provider = CreateWithFakeWmi();

        var info = provider.Collect();

        info.DeviceType.Should().Be("Laptop");
    }

    [Fact]
    public void Collect_DeviceType_UnmappedPcSystemType_FallsBackToChassisTypes()
    {
        // 6 is not in the PCSystemType map, so the provider falls through to Win32_SystemEnclosure.
        // Chassis type 6 (Mini Tower) is not one of the laptop chassis, so this reads as Desktop.
        _wmiApi.For("Win32_ComputerSystem").Add(new FakeWmiInstance
        {
            ["PCSystemType"] = 6,
        });
        _wmiApi.For("Win32_SystemEnclosure").Add(new FakeWmiInstance
        {
            ["ChassisTypes"] = new ushort[] { 6 },
        });
        var provider = CreateWithFakeWmi();

        var info = provider.Collect();

        info.DeviceType.Should().Be("Desktop");
    }

    [Fact]
    public void Collect_OnThisMachine_ReadsEveryWmiBackedField()
    {
        // Each of these falls back to Unknown when WMI refuses the caller, which is what the gate used to
        // see; a real answer is the point of running against the machine.
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object, RealWmiApi);

        var info = provider.Collect();

        info.Ram.Should().EndWith(" GB");
        info.Cpu.Should().NotBe("Unknown");
        info.Gpu.Should().NotBe("Unknown");
        info.DeviceType.Should().NotBe("Unknown");
        info.DomainJoined.Should().MatchRegex(@"^(Yes \(.+\)|No)$");
    }

    [Fact]
    public void Collect_Elevation_IsRecognizedValue()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object, RealWmiApi);

        var info = provider.Collect();

        info.Elevation.Should().BeOneOf("Admin", "Admin (OTS)", "Standard");
    }

    [Fact]
    public void Collect_DotNetRuntime_ContainsDotNet()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object, RealWmiApi);

        var info = provider.Collect();

        info.DotNetRuntime.Should().Contain(".NET");
    }

    [Fact]
    public void Collect_Gpu_FormatsNameWithClassification()
    {
        _wmiApi.For("Win32_VideoController").Add(new FakeWmiInstance
        {
            ["Name"] = "NVIDIA GeForce RTX 4070",
            ["AdapterDACType"] = "Integrated RAMDAC",
        });
        var provider = CreateWithFakeWmi();

        var info = provider.Collect();

        info.Gpu.Should().Be("NVIDIA GeForce RTX 4070 (Dedicated)");
    }

    [Fact]
    public void Collect_Tpm_ReadsSpecVersionFromTheTpmNamespace()
    {
        // Win32_Tpm is the one class the provider reads outside root\cimv2, and SpecVersion arrives
        // as "2.0, 0, 1.59". Only the TPM namespace is set up, so querying the wrong one falls back
        // to the empty default and fails this fact instead of reading the gate machine's own TPM.
        IReadOnlyList<IWmiInstance> noInstances = [];
        IReadOnlyList<IWmiInstance> tpmInstances =
            [new FakeWmiInstance { ["SpecVersion"] = "2.0, 0, 1.59" }];
        var wmiApi = new Mock<IWmiApi>();
        wmiApi
            .Setup(api => api.Query(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(noInstances);
        wmiApi
            .Setup(api => api.Query(@"root\cimv2\Security\MicrosoftTpm", "Win32_Tpm", null))
            .Returns(tpmInstances);
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object, wmiApi.Object);

        var info = provider.Collect();

        info.Tpm.Should().Be("2.0");
    }

    [Fact]
    public void Collect_FirmwareType_IsRecognizedValue()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object, RealWmiApi);

        var info = provider.Collect();

        info.FirmwareType.Should().BeOneOf("UEFI", "Legacy BIOS", "Unknown");
    }

    [Fact]
    public void Collect_SecureBoot_IsRecognizedValue()
    {
        var provider = new SystemInfoProvider(_mockInteractiveUserService.Object, RealWmiApi);

        var info = provider.Collect();

        info.SecureBoot.Should().BeOneOf("Enabled", "Disabled", "Not Supported", "Unknown");
    }
}
