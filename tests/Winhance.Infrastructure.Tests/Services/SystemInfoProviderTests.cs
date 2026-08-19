using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SystemInfoProviderTests
{
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();

    #region WMI test seam

    // The WMI-backed fields used to be asserted against whatever hardware ran the suite, so they
    // failed outright wherever WMI is degraded - and proved almost nothing when they passed.
    // These helpers push constructed rows through the provider's internal query seam instead, so
    // the assertions are about Winhance's parsing.

    private SystemInfoProvider Create(SystemInfoProvider.WmiQuery query) =>
        new(_mockInteractiveUserService.Object, query);

    /// <summary>One WMI row. Keys are matched case-insensitively, as WMI does.</summary>
    private static IReadOnlyDictionary<string, object?> Row(
        params (string Key, object? Value)[] fields)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in fields)
            row[key] = value;
        return row;
    }

    /// <summary>
    /// A fake query keyed by WMI class name. A query for a class with no entry returns no rows,
    /// which is what the real WMI does when nothing matches.
    /// </summary>
    private static SystemInfoProvider.WmiQuery Wmi(
        params (string ClassName, IReadOnlyDictionary<string, object?>[] Rows)[] table)
    {
        return (scope, wql) =>
        {
            foreach (var (className, rows) in table)
            {
                if (wql.Contains(className, StringComparison.OrdinalIgnoreCase))
                    return rows;
            }

            return Array.Empty<IReadOnlyDictionary<string, object?>>();
        };
    }

    #endregion

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

    [Fact]
    public void Collect_WhenWmiQueryThrows_FieldsFallBackWithoutThrowing()
    {
        // The worker's real state: WMI answers nothing. Collect must degrade, not throw.
        var provider = Create((scope, wql) => throw new InvalidOperationException("WMI is down"));

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
    public void Collect_Cpu_FormatsNameWithLogicalProcessorCount()
    {
        var provider = Create(Wmi(("Win32_Processor", new[]
        {
            Row(("Name", "Intel Core i7-9700K"), ("NumberOfLogicalProcessors", 8))
        })));

        var info = provider.Collect();

        info.Cpu.Should().Be("Intel Core i7-9700K (8 cores)");
    }

    [Fact]
    public void Collect_Cpu_WithoutLogicalProcessorCount_ReturnsNameOnly()
    {
        var provider = Create(Wmi(("Win32_Processor", new[]
        {
            Row(("Name", "Intel Core i7-9700K"), ("NumberOfLogicalProcessors", 0))
        })));

        var info = provider.Collect();

        info.Cpu.Should().Be("Intel Core i7-9700K");
    }

    [Fact]
    public void Collect_Ram_RoundsTotalPhysicalMemoryToWholeGb()
    {
        // 17179869184 bytes is exactly 16 GiB; the provider rounds to whole GB and appends the
        // unit, so a bytes-to-GB regression changes this string.
        var provider = Create(Wmi(("Win32_ComputerSystem", new[]
        {
            Row(("TotalPhysicalMemory", 17179869184L))
        })));

        var info = provider.Collect();

        info.Ram.Should().Be("16 GB");
    }

    [Fact]
    public void Collect_DeviceType_MapsPcSystemTypeToLabel()
    {
        var provider = Create(Wmi(("Win32_ComputerSystem", new[]
        {
            Row(("PCSystemType", 2), ("Model", "Latitude 7440"), ("Manufacturer", "Dell Inc."))
        })));

        var info = provider.Collect();

        info.DeviceType.Should().Be("Laptop");
    }

    [Fact]
    public void Collect_DeviceType_UnmappedPcSystemType_FallsBackToChassisTypes()
    {
        // 6 is not in the PCSystemType map, so the provider falls through to Win32_SystemEnclosure.
        // Chassis type 6 (Mini Tower) is not one of the laptop chassis, so this reads as Desktop.
        var provider = Create(Wmi(
            ("Win32_ComputerSystem", new[] { Row(("PCSystemType", 6)) }),
            ("Win32_SystemEnclosure", new[] { Row(("ChassisTypes", new ushort[] { 6 })) })));

        var info = provider.Collect();

        info.DeviceType.Should().Be("Desktop");
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
    public void Collect_Gpu_FormatsNameWithClassification()
    {
        var provider = Create(Wmi(("Win32_VideoController", new[]
        {
            Row(("Name", "NVIDIA GeForce RTX 4070"), ("AdapterDACType", "Integrated RAMDAC"))
        })));

        var info = provider.Collect();

        info.Gpu.Should().Be("NVIDIA GeForce RTX 4070 (Dedicated)");
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
